// ============================================================
// PlayerStatusUISetupMenu — Dist/MCP 상태창 Setup·Patch·Ensure (에이전트용)
// ============================================================

#if UNITY_EDITOR
using System.IO;
using System.Text;
using Garunnir.Runtime.Gameplay.Data;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

static class PlayerStatusUISetupMenu
{
    const string PrefabFolder = "Assets/Dist/Visual/Prefabs/UIComponents/PlayerStatus";
    const string WindowPrefabPath = PrefabFolder + "/Grp_PlayerStatusWindow.prefab";
    const string SummaryPrefabPath = PrefabFolder + "/Grp_PlayerStatusSummary.prefab";
    const string BodyChibiPrefabPath = PrefabFolder + "/Grp_PlayerStatusBodyChibi.prefab";
    const string BodyChibiSpriteFolder =
        "Assets/Dist/Visual/Sprites/UI/PlayerStatus/Body/ChibiBody";
    const string BandageSourcePath = BodyChibiSpriteFolder + "/bodyStatBandage.png";
    const string BandageSliceFolder = BodyChibiSpriteFolder + "/Bandage";
    const string FullBandageObjectName = "bandage";
    const float BandageMaskAlphaMin = 0.5f;
    const string MoodSpriteFolder = "Assets/Dist/Visual/Sprites/UI/PlayerStatus/Mood";
    /// <summary>Icons.png 8×8 시트 — ExtendedMoodIconIds[0..63] (GoodMood..Respect) SSOT.</summary>
    const string MoodIconsAtlasPath = MoodSpriteFolder + "/Icons.png";
    const int MoodIconsAtlasCount = 64;
    const int MoodIconsAtlasCropSize = 72;
    const string MoodCatalogAssetPath = PlayerStatusMoodIconCatalog.DefaultAssetPath;
    const string SoGameplayFolder = "Assets/Dist/SOData/Gameplay";
    const string SoPlayerStatusFolder = SoGameplayFolder + "/PlayerStatus";

    [MenuItem(DistMcpMenus.PlayerStatusEnsureMoodAssets)]
    static void EnsureMoodAssetsMenu()
    {
        EnsureMoodAssets();
        Debug.Log("[PlayerStatusUISetupMenu] Ensure Mood Assets complete.");
    }

    /// <summary>
    /// 구 핸들 자식 제거 후 UIWindowResizeHandles 부착 (레이아웃 유지).
    /// </summary>
    [MenuItem(DistMcpMenus.PlayerStatusPatchWindowResizeHandlers)]
    static void PatchWindowResizeHandlers()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WindowPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] Failed to load: {WindowPrefabPath}");
            return;
        }

        try
        {
            UICharacterWindow window = root.GetComponent<UICharacterWindow>();
            if (window == null)
            {
                Debug.LogError(
                    "[PlayerStatusUISetupMenu] UICharacterWindow missing; cannot patch.",
                    root);
                return;
            }

            UIWindowResizeHandlesPrefabPatch.Apply(
                root,
                PlayerStatusUIFactory.ResizeEdgeThickness,
                proximityReveal: false,
                new Vector2(PlayerStatusWindowLayout.MinWidth, PlayerStatusWindowLayout.MinHeight),
                PlayerStatusWindowLayout.GetMaxSize(null));

            PrefabUtility.SaveAsPrefabAsset(root, WindowPrefabPath);
            Debug.Log(
                $"[PlayerStatusUISetupMenu] Applied UIWindowResizeHandles on {WindowPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Area_BodyDiagram/BodyDiagramCanvas 안쪽만 치비로 교체.
    /// TabBar·GearPanel·Area_BodyDiagram·BodyDiagramCanvas Rect는 유지.
    /// </summary>
    [MenuItem(DistMcpMenus.PlayerStatusPatchWindowBodyDiagramChibi)]
    static void PatchWindowBodyDiagramChibi()
    {
        GameObject chibiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BodyChibiPrefabPath);
        if (chibiPrefab == null)
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] Missing: {BodyChibiPrefabPath}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(WindowPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] Failed to load: {WindowPrefabPath}");
            return;
        }

        try
        {
            Transform diagram = root.transform.Find("Area_BodyProfile/Area_BodyDiagram");
            if (diagram == null)
                diagram = FindDeep(root.transform, "Area_BodyDiagram");
            if (diagram == null)
            {
                Debug.LogError(
                    "[PlayerStatusUISetupMenu] Area_BodyDiagram missing; abort (no chrome rewrite).",
                    root);
                return;
            }

            Transform canvas = diagram.Find("BodyDiagramCanvas");
            if (canvas == null)
            {
                Debug.LogError(
                    "[PlayerStatusUISetupMenu] BodyDiagramCanvas missing; abort.",
                    diagram);
                return;
            }

            for (int i = canvas.childCount - 1; i >= 0; i--)
            {
                Transform child = canvas.GetChild(i);
                if (child.name == "Grp_PlayerStatusBodyChibi")
                    continue;

                UIPlayerStatusBodyPartGraphic graphic =
                    child.GetComponent<UIPlayerStatusBodyPartGraphic>();
                if (graphic != null)
                    Object.DestroyImmediate(graphic);
                child.gameObject.SetActive(false);
            }

            Transform existingChibi = canvas.Find("Grp_PlayerStatusBodyChibi");
            GameObject chibiGo;
            if (existingChibi != null)
            {
                chibiGo = existingChibi.gameObject;
            }
            else
            {
                chibiGo = (GameObject)PrefabUtility.InstantiatePrefab(chibiPrefab, canvas);
                if (chibiGo == null)
                {
                    Debug.LogError(
                        "[PlayerStatusUISetupMenu] InstantiatePrefab chibi failed.",
                        canvas);
                    return;
                }

                chibiGo.name = "Grp_PlayerStatusBodyChibi";
            }

            RectTransform chibiRt = chibiGo.GetComponent<RectTransform>();
            if (chibiRt != null)
            {
                chibiRt.anchorMin = Vector2.zero;
                chibiRt.anchorMax = Vector2.one;
                chibiRt.pivot = new Vector2(0.5f, 0.5f);
                chibiRt.anchoredPosition = Vector2.zero;
                chibiRt.sizeDelta = Vector2.zero;
                chibiRt.localScale = Vector3.one;
            }

            PrefabUtility.SaveAsPrefabAsset(root, WindowPrefabPath);
            Debug.Log(
                "[PlayerStatusUISetupMenu] Nested chibi under BodyDiagramCanvas; old Img_* hidden.",
                canvas);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// ColliderParts BoxCollider2D를 RectTransform 앵커로 구워 Graphic 히트로 배선.
    /// 변환은 에디트 타임만. Area_BodyDiagram / canvas Rect는 유지.
    /// </summary>
    [MenuItem(DistMcpMenus.PlayerStatusPatchWindowBodyChibiColliderHits)]
    static void PatchWindowBodyChibiColliderHits()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BodyChibiPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] Failed to load: {BodyChibiPrefabPath}");
            return;
        }

        try
        {
            Transform partsRoot = root.transform.Find("Parts");
            Transform colliderRoot = root.transform.Find("ColliderParts");
            if (partsRoot == null || colliderRoot == null)
            {
                Debug.LogError(
                    "[PlayerStatusUISetupMenu] Parts or ColliderParts missing; abort.",
                    root);
                return;
            }

            Image rootImage = root.GetComponent<Image>();
            if (rootImage != null)
                rootImage.raycastTarget = false;

            Vector2 parentSize = ResolveChibiColliderParentSize(root);

            int wired = 0;
            int baked = 0;
            for (int i = 0; i < colliderRoot.childCount; i++)
            {
                Transform hitT = colliderRoot.GetChild(i);
                Transform visualT = partsRoot.Find(hitT.name);
                if (visualT == null)
                {
                    Debug.LogError(
                        $"[PlayerStatusUISetupMenu] Parts/{hitT.name} missing.",
                        hitT);
                    continue;
                }

                Image visualImage = visualT.GetComponent<Image>();
                if (visualImage == null)
                {
                    Debug.LogError(
                        $"[PlayerStatusUISetupMenu] Parts/{hitT.name} has no Image.",
                        visualT);
                    continue;
                }

                visualImage.raycastTarget = false;

                string partId = null;
                UIPlayerStatusBodyPartGraphic visualGraphic =
                    visualT.GetComponent<UIPlayerStatusBodyPartGraphic>();
                if (visualGraphic != null)
                {
                    partId = visualGraphic.PartId;
                    Object.DestroyImmediate(visualGraphic);
                }

                if (string.IsNullOrEmpty(partId))
                    partId = PartIdFromGoName(hitT.name);

                BoxCollider2D box = hitT.GetComponent<BoxCollider2D>();
                if (box != null)
                {
                    ApplyBoxColliderToAnchors(
                        hitT as RectTransform,
                        box,
                        parentSize);
                    Object.DestroyImmediate(box);
                    baked++;
                }

                Image hitImage = EnsureInvisibleHitImage(hitT.gameObject);
                hitImage.raycastTarget = true;

                UIPlayerStatusBodyPartGraphic hitGraphic =
                    hitT.GetComponent<UIPlayerStatusBodyPartGraphic>();
                if (hitGraphic == null)
                    hitGraphic = hitT.gameObject.AddComponent<UIPlayerStatusBodyPartGraphic>();

                hitGraphic.Wire(visualImage, partId);
                wired++;
            }

            for (int i = 0; i < partsRoot.childCount; i++)
            {
                Image partImage = partsRoot.GetChild(i).GetComponent<Image>();
                if (partImage != null)
                    partImage.raycastTarget = false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, BodyChibiPrefabPath);
            Debug.Log(
                $"[PlayerStatusUISetupMenu] Wired {wired}/{colliderRoot.childCount} hits, baked {baked} boxes to anchors (parent {parentSize.x:0.##}x{parentSize.y:0.##}) on {BodyChibiPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// bodyStatBandage × 파츠 알파 슬라이스 베이크 후 부위 자식 Img_Bandage 배선.
    /// 통짜 bandage 오브젝트는 끈다. 레이아웃 유지.
    /// </summary>
    [MenuItem(DistMcpMenus.PlayerStatusPatchBodyBandageOverlays)]
    static void PatchBodyBandageOverlays()
    {
        Sprite bandageSource = AssetDatabase.LoadAssetAtPath<Sprite>(BandageSourcePath);
        if (bandageSource == null)
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] Missing bandage sprite: {BandageSourcePath}");
            return;
        }

        EnsureBandageSliceFolder();
        int summaryWired = PatchBandageOverlaysOnPrefab(
            SummaryPrefabPath,
            bandageSource,
            HideSummaryFullBandage);
        int chibiWired = PatchBandageOverlaysOnPrefab(
            BodyChibiPrefabPath,
            bandageSource,
            HideChibiFullBandage);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"[PlayerStatusUISetupMenu] Bandage overlays wired summary={summaryWired} chibi={chibiWired}.");
    }

    static int PatchBandageOverlaysOnPrefab(
        string prefabPath,
        Sprite bandageSource,
        System.Action<GameObject> hideFullBandage)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] Failed to load: {prefabPath}");
            return 0;
        }

        int wired = 0;
        try
        {
            hideFullBandage?.Invoke(root);
            UIPlayerStatusBodyPartGraphic[] graphics =
                root.GetComponentsInChildren<UIPlayerStatusBodyPartGraphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                UIPlayerStatusBodyPartGraphic graphic = graphics[i];
                Image visual = graphic.PartImage;
                if (visual == null || visual.sprite == null)
                {
                    Debug.LogError(
                        $"[PlayerStatusUISetupMenu] Bandage skip: no visual sprite on {graphic.name}.",
                        graphic);
                    continue;
                }

                Sprite slice = BakeBandageSlice(bandageSource, visual.sprite, visual.name);
                if (slice == null)
                    continue;

                Image bandageImage = EnsureBandageOverlay(visual.rectTransform, slice);
                graphic.Wire(visual, graphic.PartId, bandageImage);
                wired++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return wired;
    }

    static void HideSummaryFullBandage(GameObject root)
    {
        Transform body = root.transform.Find("Area_Status/Grp_Body");
        Transform found = body != null ? body.Find(FullBandageObjectName) : null;
        if (found != null)
            found.gameObject.SetActive(false);
    }

    static void HideChibiFullBandage(GameObject root)
    {
        Transform found = root.transform.Find(FullBandageObjectName);
        if (found != null)
            found.gameObject.SetActive(false);
    }

    static void EnsureBandageSliceFolder()
    {
        if (AssetDatabase.IsValidFolder(BandageSliceFolder))
            return;

        AssetDatabase.CreateFolder(BodyChibiSpriteFolder, "Bandage");
    }

    static Sprite BakeBandageSlice(Sprite bandage, Sprite partMask, string sliceName)
    {
        if (!TryReadSpritePixels(bandage, out Color[] bandagePixels, out int bandageW, out int bandageH))
            return null;
        if (!TryReadSpritePixels(partMask, out Color[] maskPixels, out int maskW, out int maskH))
            return null;

        var tex = new Texture2D(bandageW, bandageH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32 clear = new(0, 0, 0, 0);
        for (int y = 0; y < bandageH; y++)
        {
            for (int x = 0; x < bandageW; x++)
            {
                Color bandagePixel = bandagePixels[y * bandageW + x];
                Color maskPixel = SampleNearest(maskPixels, maskW, maskH, x, y, bandageW, bandageH);
                bool keep = bandagePixel.a >= BandageMaskAlphaMin &&
                            maskPixel.a >= BandageMaskAlphaMin;
                tex.SetPixel(x, y, keep ? bandagePixel : (Color)clear);
            }
        }

        tex.Apply();
        string assetPath = BandageSliceFolder + "/" + sliceName + ".png";
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        EnsureBandageSliceImportSettings(assetPath);
        Sprite slice = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (slice == null)
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] Bandage slice import failed: {assetPath}");
        }

        return slice;
    }

    static bool TryReadSpritePixels(Sprite sprite, out Color[] pixels, out int width, out int height)
    {
        pixels = null;
        width = 0;
        height = 0;
        if (sprite == null || sprite.texture == null)
            return false;

        Texture2D tex = sprite.texture;
        width = tex.width;
        height = tex.height;
        if (width <= 0 || height <= 0)
            return false;

        try
        {
            pixels = tex.GetPixels();
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[PlayerStatusUISetupMenu] GetPixels failed for {sprite.name}: {ex.Message}",
                sprite);
            return false;
        }

        return pixels != null && pixels.Length == width * height;
    }

    static Color SampleNearest(Color[] src, int srcW, int srcH, int x, int y, int dstW, int dstH)
    {
        int sx = srcW * x / dstW;
        int sy = srcH * y / dstH;
        if (sx < 0)
            sx = 0;
        else if (sx >= srcW)
            sx = srcW - 1;
        if (sy < 0)
            sy = 0;
        else if (sy >= srcH)
            sy = srcH - 1;
        return src[sy * srcW + sx];
    }

    static Image EnsureBandageOverlay(RectTransform visualRoot, Sprite slice)
    {
        Transform existing = visualRoot.Find(UIPlayerStatusBodyPartGraphic.BandageChildName);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(
                UIPlayerStatusBodyPartGraphic.BandageChildName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            go.layer = visualRoot.gameObject.layer;
            go.transform.SetParent(visualRoot, false);
        }

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        image.sprite = slice;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = false;
        image.enabled = false;
        go.SetActive(true);
        return image;
    }

    static void EnsureBandageSliceImportSettings(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// HUD Grp_Body/Parts Graphic 배선 + Grp_Switch 탭 펼침 스트립. 레이아웃 유지.
    /// </summary>
    [MenuItem(DistMcpMenus.PlayerStatusPatchSummaryBodyHits)]
    static void PatchSummaryBodyHits()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SummaryPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] Failed to load: {SummaryPrefabPath}");
            return;
        }

        try
        {
            Transform partsRoot = root.transform.Find("Area_Status/Grp_Body/Parts");
            if (partsRoot == null)
            {
                Debug.LogError(
                    "[PlayerStatusUISetupMenu] Area_Status/Grp_Body/Parts missing; abort.",
                    root);
                return;
            }

            int wired = 0;
            for (int i = 0; i < partsRoot.childCount; i++)
            {
                Transform partT = partsRoot.GetChild(i);
                Image partImage = partT.GetComponent<Image>();
                if (partImage == null)
                {
                    Debug.LogError(
                        $"[PlayerStatusUISetupMenu] Parts/{partT.name} has no Image.",
                        partT);
                    continue;
                }

                partImage.raycastTarget = false;

                UIPlayerStatusBodyPartGraphic graphic =
                    partT.GetComponent<UIPlayerStatusBodyPartGraphic>();
                if (graphic == null)
                    graphic = partT.gameObject.AddComponent<UIPlayerStatusBodyPartGraphic>();

                graphic.Wire(partImage, PartIdFromGoName(partT.name));
                wired++;
            }

            Transform switchT = root.transform.Find("Area_Status/Grp_Switch");
            if (switchT == null)
            {
                Debug.LogError(
                    "[PlayerStatusUISetupMenu] Area_Status/Grp_Switch missing; abort.",
                    root);
                return;
            }

            UIPlayerStatusBodyTabStrip tabStrip = PatchSummaryBodyTabStrip(switchT);
            if (tabStrip == null)
                return;

            UIPlayerStatusSummaryPanel panel = root.GetComponent<UIPlayerStatusSummaryPanel>();
            if (panel == null)
            {
                Debug.LogError(
                    "[PlayerStatusUISetupMenu] UIPlayerStatusSummaryPanel missing on summary root.",
                    root);
                return;
            }

            SerializedObject so = new(panel);
            so.FindProperty("_bodyPartsRoot").objectReferenceValue = partsRoot;
            so.FindProperty("_bodyTabStrip").objectReferenceValue = tabStrip;
            so.FindProperty("_consciousnessFill").objectReferenceValue =
                FindSummaryFill(root, UIPlayerStatusSummaryPanel.ConsciousnessFillPath);
            so.FindProperty("_bloodFill").objectReferenceValue =
                FindSummaryFill(root, UIPlayerStatusSummaryPanel.BloodFillPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, SummaryPrefabPath);
            Debug.Log(
                $"[PlayerStatusUISetupMenu] Wired {wired}/{partsRoot.childCount} HUD body parts + tab strip on {SummaryPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static Image FindSummaryFill(GameObject root, string path)
    {
        Transform found = root.transform.Find(path);
        if (found == null)
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] Fill missing at {path}.", root);
            return null;
        }

        Image image = found.GetComponent<Image>();
        if (image == null)
            Debug.LogError($"[PlayerStatusUISetupMenu] Image missing on {path}.", found);
        return image;
    }

    static UIPlayerStatusBodyTabStrip PatchSummaryBodyTabStrip(Transform switchT)
    {
        Button rootButton = switchT.GetComponent<Button>();
        if (rootButton != null)
            Object.DestroyImmediate(rootButton);

        ContentSizeFitter stripFitter = switchT.GetComponent<ContentSizeFitter>();
        if (stripFitter == null)
            stripFitter = switchT.gameObject.AddComponent<ContentSizeFitter>();
        stripFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        stripFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        Transform maskT = FindDeep(switchT, "Mask");
        if (maskT == null)
        {
            Debug.LogError(
                "[PlayerStatusUISetupMenu] Grp_Switch/Mask missing; abort.",
                switchT);
            return null;
        }

        EnsureTabIconSlot(maskT, "Icon (2)", CharacterWindowTab.Encumbrance);
        Transform icon3T = maskT.Find("Icon (3)");
        if (icon3T == null)
        {
            Transform icon2T = maskT.Find("Icon (2)");
            if (icon2T == null)
            {
                Debug.LogError(
                    "[PlayerStatusUISetupMenu] Grp_Switch Icon (2) missing; cannot add Icon (3).",
                    maskT);
                return null;
            }

            GameObject icon3Go = Object.Instantiate(icon2T.gameObject, maskT);
            icon3Go.name = "Icon (3)";
            icon3Go.SetActive(false);
            icon3T = icon3Go.transform;

            Transform backImgT = maskT.Find("BackImg");
            if (backImgT != null)
                icon3T.SetSiblingIndex(backImgT.GetSiblingIndex());
        }

        EnsureTabIconSlot(maskT, "Icon", CharacterWindowTab.Status);
        EnsureTabIconSlot(maskT, "Icon (1)", CharacterWindowTab.Equipment);
        EnsureTabIconSlot(maskT, "Icon (2)", CharacterWindowTab.Encumbrance);
        EnsureTabIconSlot(maskT, "Icon (3)", CharacterWindowTab.BodyTemp);

        maskT.Find("Icon (1)")?.gameObject.SetActive(false);
        maskT.Find("Icon (2)")?.gameObject.SetActive(false);
        maskT.Find("Icon (3)")?.gameObject.SetActive(false);

        UIPlayerStatusBodyTabStrip strip = switchT.GetComponent<UIPlayerStatusBodyTabStrip>();
        if (strip == null)
            strip = switchT.gameObject.AddComponent<UIPlayerStatusBodyTabStrip>();

        SerializedObject stripSo = new(strip);
        stripSo.FindProperty("_maskRoot").objectReferenceValue = maskT as RectTransform;

        SerializedProperty slotsProp = stripSo.FindProperty("_slots");
        slotsProp.arraySize = 4;
        WireTabStripSlot(slotsProp, 0, maskT.Find("Icon"), CharacterWindowTab.Status);
        WireTabStripSlot(slotsProp, 1, maskT.Find("Icon (1)"), CharacterWindowTab.Equipment);
        WireTabStripSlot(slotsProp, 2, maskT.Find("Icon (2)"), CharacterWindowTab.Encumbrance);
        WireTabStripSlot(slotsProp, 3, icon3T, CharacterWindowTab.BodyTemp);
        stripSo.ApplyModifiedPropertiesWithoutUndo();

        return strip;
    }

    static void WireTabStripSlot(
        SerializedProperty slotsProp,
        int index,
        Transform iconT,
        CharacterWindowTab tab)
    {
        if (iconT == null)
        {
            Debug.LogError(
                $"[PlayerStatusUISetupMenu] Tab icon missing for {tab}; abort slot {index}.",
                slotsProp.serializedObject.targetObject);
            return;
        }

        SerializedProperty slotProp = slotsProp.GetArrayElementAtIndex(index);
        slotProp.FindPropertyRelative("button").objectReferenceValue = iconT.GetComponent<Button>();
        slotProp.FindPropertyRelative("icon").objectReferenceValue = iconT.GetComponent<Image>();
        slotProp.FindPropertyRelative("tab").enumValueIndex = (int)tab;
    }

    static void EnsureTabIconSlot(Transform maskT, string iconName, CharacterWindowTab tab)
    {
        Transform iconT = maskT.Find(iconName);
        if (iconT == null)
        {
            Debug.LogError(
                $"[PlayerStatusUISetupMenu] Mask/{iconName} missing for tab {tab}.",
                maskT);
            return;
        }

        Image iconImage = iconT.GetComponent<Image>();
        if (iconImage == null)
        {
            Debug.LogError(
                $"[PlayerStatusUISetupMenu] Mask/{iconName} Image missing.",
                iconT);
            return;
        }

        iconImage.raycastTarget = true;

        Button button = iconT.GetComponent<Button>();
        if (button == null)
            button = iconT.gameObject.AddComponent<Button>();
        button.targetGraphic = iconImage;
    }

    static void ApplyBoxColliderToAnchors(
        RectTransform child,
        BoxCollider2D box,
        Vector2 parentSize)
    {
        if (child == null || box == null)
            return;
        if (parentSize.x <= 0f || parentSize.y <= 0f)
            return;

        Vector2 half = box.size * 0.5f;
        float xMin = 0.5f + (box.offset.x - half.x) / parentSize.x;
        float xMax = 0.5f + (box.offset.x + half.x) / parentSize.x;
        float yMin = 0.5f + (box.offset.y - half.y) / parentSize.y;
        float yMax = 0.5f + (box.offset.y + half.y) / parentSize.y;

        child.anchorMin = new Vector2(xMin, yMin);
        child.anchorMax = new Vector2(xMax, yMax);
        child.pivot = new Vector2(0.5f, 0.5f);
        child.anchoredPosition = Vector2.zero;
        child.sizeDelta = Vector2.zero;
    }

    static Vector2 ResolveChibiColliderParentSize(GameObject chibiRoot)
    {
        Vector2 size = PlayerStatusUIFactory.BodyDiagramSize;
        AspectRatioFitter fitter = chibiRoot.GetComponent<AspectRatioFitter>();
        if (fitter == null || fitter.aspectRatio <= 0f)
            return size;

        if (fitter.aspectMode == AspectRatioFitter.AspectMode.WidthControlsHeight)
            return new Vector2(size.x, size.x / fitter.aspectRatio);
        if (fitter.aspectMode == AspectRatioFitter.AspectMode.HeightControlsWidth)
            return new Vector2(size.y * fitter.aspectRatio, size.y);
        return size;
    }

    static Image EnsureInvisibleHitImage(GameObject go)
    {
        Image image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();

        Color hitColor = image.color;
        hitColor.a = 0f;
        image.color = hitColor;
        image.raycastTarget = true;
        return image;
    }

    static string PartIdFromGoName(string goName)
    {
        if (string.IsNullOrEmpty(goName))
            return goName;

        var sb = new StringBuilder(goName.Length + 4);
        for (int i = 0; i < goName.Length; i++)
        {
            char c = goName[i];
            if (char.IsUpper(c) && i > 0 && goName[i - 1] != '_')
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// TabBar + GearPanel chrome을 프리팹에 추가·배선 (손수 body diagram 유지, full bake 아님).
    /// </summary>
    [MenuItem(DistMcpMenus.PlayerStatusPatchCharacterTabsAndGearPanel)]
    static void PatchCharacterTabsAndGearPanel()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WindowPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] Failed to load: {WindowPrefabPath}");
            return;
        }

        try
        {
            UICharacterWindow window = root.GetComponent<UICharacterWindow>();
            if (window == null)
            {
                Debug.LogError(
                    "[PlayerStatusUISetupMenu] UICharacterWindow missing; cannot patch.",
                    root);
                return;
            }

            TMP_FontAsset font = ResolveWindowFont(root);
            Color headerColor = ResolveHeaderColor(root);
            Color panelColor = new Color(0.14f, 0.14f, 0.14f, 0.92f);
            Color tabColor = new Color(
                Mathf.Min(1f, headerColor.r + 0.06f),
                Mathf.Min(1f, headerColor.g + 0.06f),
                Mathf.Min(1f, headerColor.b + 0.06f),
                1f);

            RectTransform statusContent = FindChildRect(root.transform, "Area_Content");
            RectTransform bodyStatus = FindChildRect(
                root.transform.Find("Area_BodyProfile") != null
                    ? root.transform.Find("Area_BodyProfile")
                    : root.transform,
                "Area_BodyStatus");
            RectTransform tabBar = EnsureTabBar(root.transform, tabColor, font);
            RectTransform gearRoot = EnsureGearPanelRoot(root.transform, panelColor);
            UICharacterGearPanel gearPanel = EnsureGearPanelTree(gearRoot, font, tabColor);

            SerializedObject so = new(window);
            so.FindProperty("_statusContentRoot").objectReferenceValue = statusContent;
            SerializedProperty bodyStatusProp = so.FindProperty("_bodyStatusRoot");
            if (bodyStatusProp != null)
                bodyStatusProp.objectReferenceValue = bodyStatus;
            so.FindProperty("_tabBarRoot").objectReferenceValue = tabBar;
            so.FindProperty("_gearPanelRoot").objectReferenceValue = gearRoot;
            so.FindProperty("_gearPanel").objectReferenceValue = gearPanel;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, WindowPrefabPath);
            Debug.Log(
                $"[PlayerStatusUISetupMenu] Patched TabBar + GearPanel (parity) on {WindowPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static TMP_FontAsset ResolveWindowFont(GameObject root)
    {
        Transform title = root.transform.Find("Header/Title");
        if (title != null)
        {
            TMP_Text titleText = title.GetComponent<TMP_Text>();
            if (titleText != null && titleText.font != null)
                return titleText.font;
        }

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            InventoryUIHierarchyBuilder.DefaultUIFontPath);
    }

    static Color ResolveHeaderColor(GameObject root)
    {
        Transform header = root.transform.Find("Header");
        if (header != null)
        {
            Image img = header.GetComponent<Image>();
            if (img != null)
                return img.color;
        }

        return new Color(0.16f, 0.16f, 0.16f, 1f);
    }

    static RectTransform FindChildRect(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        return t as RectTransform;
    }

    static RectTransform EnsureTabBar(Transform windowRoot, Color tabColor, TMP_FontAsset font)
    {
        Transform existing = windowRoot.Find("TabBar");
        GameObject barGo;
        RectTransform barRt;
        if (existing != null)
        {
            barGo = existing.gameObject;
            barRt = existing as RectTransform;
        }
        else
        {
            barGo = new GameObject("TabBar", typeof(RectTransform));
            barGo.layer = LayerMask.NameToLayer("UI");
            barGo.transform.SetParent(windowRoot, false);
            barRt = barGo.GetComponent<RectTransform>();
            // Place just under Header; do not rebuild body diagram internals.
            barRt.SetSiblingIndex(1);
        }

        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.sizeDelta = new Vector2(-20f, 28f);
        barRt.anchoredPosition = new Vector2(0f, -40f);

        HorizontalLayoutGroup layout = barGo.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = barGo.AddComponent<HorizontalLayoutGroup>();
        layout.childForceExpandWidth = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlHeight = true;
        layout.spacing = 4f;
        layout.padding = new RectOffset(4, 4, 2, 2);

        EnsureTabButton(barRt, CharacterWindowTab.Status, CharacterGearLabels.TabStatus, tabColor, font);
        EnsureTabButton(barRt, CharacterWindowTab.Equipment, CharacterGearLabels.TabEquipment, tabColor, font);
        EnsureTabButton(barRt, CharacterWindowTab.Encumbrance, CharacterGearLabels.TabEncumbrance, tabColor, font);
        EnsureTabButton(barRt, CharacterWindowTab.BodyTemp, CharacterGearLabels.TabBodyTemp, tabColor, font);
        return barRt;
    }

    static void EnsureTabButton(
        RectTransform tabBar,
        CharacterWindowTab tab,
        string label,
        Color tabColor,
        TMP_FontAsset font)
    {
        string childName = "Tab_" + tab;
        Transform existing = tabBar.Find(childName);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(tabBar, false);
        }

        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null)
            le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minHeight = 24f;

        Image bg = go.GetComponent<Image>();
        if (bg == null)
            bg = go.AddComponent<Image>();
        bg.color = tabColor;
        bg.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        if (button == null)
            button = go.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.ColorTint;

        Transform labelTf = go.transform.Find("Label");
        GameObject labelGo;
        if (labelTf != null)
        {
            labelGo = labelTf.gameObject;
        }
        else
        {
            labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            labelGo.layer = LayerMask.NameToLayer("UI");
            labelGo.transform.SetParent(go.transform, false);
        }

        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelGo.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 15f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null)
            tmp.font = font;
    }

    static RectTransform EnsureGearPanelRoot(Transform windowRoot, Color panelColor)
    {
        Transform existing = windowRoot.Find("GearPanelRoot");
        GameObject rootGo;
        RectTransform rt;
        if (existing != null)
        {
            rootGo = existing.gameObject;
            rt = existing as RectTransform;
        }
        else
        {
            rootGo = new GameObject(
                "GearPanelRoot",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            rootGo.layer = LayerMask.NameToLayer("UI");
            rootGo.transform.SetParent(windowRoot, false);
            rt = rootGo.GetComponent<RectTransform>();
        }

        rt.anchorMin = new Vector2(0.48f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.offsetMin = new Vector2(4f, 8f);
        rt.offsetMax = new Vector2(-8f, -68f);

        Image bg = rootGo.GetComponent<Image>();
        if (bg == null)
            bg = rootGo.AddComponent<Image>();
        bg.color = panelColor;
        bg.raycastTarget = false;

        // Prefab default: hidden until Equipment/Encumbrance/BodyTemp.
        rootGo.SetActive(false);
        return rt;
    }

    static UICharacterGearPanel EnsureGearPanelTree(
        RectTransform gearRoot,
        TMP_FontAsset font,
        Color chromeColor)
    {
        UICharacterGearPanel panel = gearRoot.GetComponent<UICharacterGearPanel>();
        if (panel == null)
            panel = gearRoot.gameObject.AddComponent<UICharacterGearPanel>();

        VerticalLayoutGroup rootLayout = gearRoot.GetComponent<VerticalLayoutGroup>();
        if (rootLayout == null)
            rootLayout = gearRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(8, 8, 8, 8);
        rootLayout.spacing = 6f;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = false;

        RectTransform wieldRoot = EnsureWieldRootHorizontal(gearRoot, preferredHeight: GearConstants.WieldSlotHeight + 12f);
        EnsureWieldSlot(wieldRoot, "Wield_L", WieldSlotId.Left, font, chromeColor);
        EnsureWieldSlot(wieldRoot, "Wield_R", WieldSlotId.Right, font, chromeColor);

        RectTransform wornRoot = EnsureNamedVertical(gearRoot, "WornRoot", preferredHeight: 120f);
        TMP_Text filterLabel = EnsureTmpChild(wornRoot, "FilterLabel", CharacterGearLabels.WornFilterAll, 16f, font);
        LayoutElement filterLe = filterLabel.GetComponent<LayoutElement>();
        if (filterLe == null)
            filterLe = filterLabel.gameObject.AddComponent<LayoutElement>();
        filterLe.minHeight = 22f;
        filterLe.preferredHeight = 22f;

        TMP_Text encTotals = EnsureTmpChild(gearRoot, "EncTotals", string.Empty, 14f, font);
        encTotals.gameObject.SetActive(false);

        // Plan parity: no panel HoverDetail / Progress — DetailPanel + name-overlay bars.
        Transform legacyHover = gearRoot.Find("HoverDetail");
        if (legacyHover != null)
            Object.DestroyImmediate(legacyHover.gameObject);
        Transform legacyProgress = gearRoot.Find("Progress");
        if (legacyProgress != null)
            Object.DestroyImmediate(legacyProgress.gameObject);

        SerializedObject panelSo = new(panel);
        panelSo.FindProperty("_wieldRoot").objectReferenceValue = wieldRoot;
        panelSo.FindProperty("_wornRoot").objectReferenceValue = wornRoot;
        SerializedProperty hoverProp = panelSo.FindProperty("_hoverText");
        if (hoverProp != null)
            hoverProp.objectReferenceValue = null;
        SerializedProperty progressProp = panelSo.FindProperty("_progressBar");
        if (progressProp != null)
            progressProp.objectReferenceValue = null;
        panelSo.FindProperty("_filterLabel").objectReferenceValue = filterLabel;
        panelSo.FindProperty("_encTotalsText").objectReferenceValue = encTotals;
        panelSo.ApplyModifiedPropertiesWithoutUndo();
        return panel;
    }

    static RectTransform EnsureWieldRootHorizontal(Transform parent, float preferredHeight)
    {
        Transform existing = parent.Find("WieldRoot");
        GameObject go;
        RectTransform rt;
        if (existing != null)
        {
            go = existing.gameObject;
            rt = existing as RectTransform;
        }
        else
        {
            go = new GameObject("WieldRoot", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            rt = go.GetComponent<RectTransform>();
        }

        VerticalLayoutGroup vertical = go.GetComponent<VerticalLayoutGroup>();
        if (vertical != null)
            Object.DestroyImmediate(vertical);

        HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.spacing = 8f;

        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null)
            le = go.AddComponent<LayoutElement>();
        le.minHeight = preferredHeight * 0.5f;
        le.preferredHeight = preferredHeight;
        le.flexibleHeight = 0f;
        return rt;
    }

    static RectTransform EnsureNamedVertical(Transform parent, string name, float preferredHeight)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        RectTransform rt;
        if (existing != null)
        {
            go = existing.gameObject;
            rt = existing as RectTransform;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            rt = go.GetComponent<RectTransform>();
        }

        VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = go.AddComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.spacing = 4f;

        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null)
            le = go.AddComponent<LayoutElement>();
        le.minHeight = preferredHeight * 0.5f;
        le.preferredHeight = preferredHeight;
        le.flexibleHeight = 1f;
        return rt;
    }

    static void EnsureWieldSlot(
        RectTransform wieldRoot,
        string name,
        WieldSlotId slot,
        TMP_FontAsset font,
        Color chromeColor)
    {
        Transform existing = wieldRoot.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(wieldRoot, false);
        }

        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null)
            le = go.AddComponent<LayoutElement>();
        le.minHeight = GearConstants.WieldSlotHeight;
        le.preferredHeight = GearConstants.WieldSlotHeight;
        le.flexibleWidth = 1f;

        Image bg = go.GetComponent<Image>();
        if (bg == null)
            bg = go.AddComponent<Image>();
        bg.color = new Color(chromeColor.r * 0.85f, chromeColor.g * 0.85f, chromeColor.b * 0.85f, 0.95f);
        bg.raycastTarget = true;

        UICharacterWieldSlotView view = go.GetComponent<UICharacterWieldSlotView>();
        if (view == null)
            view = go.AddComponent<UICharacterWieldSlotView>();
        view.EnsureChrome();

        // Shared chrome: action top-left, ammo top-right (same as HUD QuickSlot).
        Transform actionTf = go.transform.Find("ActionIcon");
        if (actionTf != null)
        {
            RectTransform actionRt = actionTf as RectTransform;
            if (actionRt != null)
            {
                actionRt.anchorMin = new Vector2(0f, 1f);
                actionRt.anchorMax = new Vector2(0f, 1f);
                actionRt.pivot = new Vector2(0f, 1f);
                actionRt.anchoredPosition = new Vector2(2f, -2f);
            }
        }

        Transform ammoTf = go.transform.Find("Ammo");
        if (ammoTf == null)
            ammoTf = go.transform.Find("tmp");
        if (ammoTf == null)
        {
            GameObject ammoGo = new GameObject("Ammo", typeof(RectTransform), typeof(CanvasRenderer));
            ammoGo.layer = LayerMask.NameToLayer("UI");
            ammoGo.transform.SetParent(go.transform, false);
            ammoTf = ammoGo.transform;
            RectTransform ammoRt = ammoGo.GetComponent<RectTransform>();
            ammoRt.anchorMin = new Vector2(1f, 1f);
            ammoRt.anchorMax = new Vector2(1f, 1f);
            ammoRt.pivot = new Vector2(1f, 1f);
            ammoRt.anchoredPosition = new Vector2(-2f, -2f);
            ammoRt.sizeDelta = new Vector2(48f, 14f);
            TextMeshProUGUI ammoTmp = ammoGo.AddComponent<TextMeshProUGUI>();
            ammoTmp.fontSize = GearConstants.UiFontSizeActionIcon;
            ammoTmp.alignment = TextAlignmentOptions.TopRight;
            ammoTmp.raycastTarget = false;
            ammoTmp.text = string.Empty;
            if (font != null)
                ammoTmp.font = font;
        }
        else
        {
            ammoTf.name = "Ammo";
        }

        // Name Label kept invisible for ItemNameStatusBar overlay only.
        Transform labelTf = go.transform.Find("Label");
        if (labelTf == null)
        {
            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            labelGo.layer = LayerMask.NameToLayer("UI");
            labelGo.transform.SetParent(go.transform, false);
            RectTransform labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.fontSize = 1f;
            tmp.color = new Color(1f, 1f, 1f, 0f);
            tmp.raycastTarget = false;
            if (font != null)
                tmp.font = font;
        }
    }

    static TMP_Text EnsureTmpChild(
        Transform parent,
        string name,
        string text,
        float fontSize,
        TMP_FontAsset font)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
        }

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = go.AddComponent<TextMeshProUGUI>();
        if (!string.IsNullOrEmpty(text))
            tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.raycastTarget = false;
        if (font != null)
            tmp.font = font;
        return tmp;
    }

    static Slider EnsureProgressSlider(Transform parent, Color chromeColor)
    {
        Transform existing = parent.Find("Progress");
        GameObject barGo;
        if (existing != null)
        {
            barGo = existing.gameObject;
        }
        else
        {
            barGo = new GameObject(
                "Progress",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Slider));
            barGo.layer = LayerMask.NameToLayer("UI");
            barGo.transform.SetParent(parent, false);
        }

        LayoutElement le = barGo.GetComponent<LayoutElement>();
        if (le == null)
            le = barGo.AddComponent<LayoutElement>();
        le.minHeight = 16f;
        le.preferredHeight = 16f;

        Image rootImage = barGo.GetComponent<Image>();
        if (rootImage == null)
            rootImage = barGo.AddComponent<Image>();
        rootImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        rootImage.raycastTarget = false;

        Transform bgTf = barGo.transform.Find("Background");
        GameObject bgGo;
        if (bgTf != null)
        {
            bgGo = bgTf.gameObject;
        }
        else
        {
            bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.layer = LayerMask.NameToLayer("UI");
            bgGo.transform.SetParent(barGo.transform, false);
        }

        RectTransform bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImage = bgGo.GetComponent<Image>();
        if (bgImage == null)
            bgImage = bgGo.AddComponent<Image>();
        bgImage.color = new Color(chromeColor.r * 0.7f, chromeColor.g * 0.7f, chromeColor.b * 0.7f, 1f);
        bgImage.raycastTarget = false;

        Transform fillAreaTf = barGo.transform.Find("Fill Area");
        GameObject fillAreaGo;
        if (fillAreaTf != null)
        {
            fillAreaGo = fillAreaTf.gameObject;
        }
        else
        {
            fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.layer = LayerMask.NameToLayer("UI");
            fillAreaGo.transform.SetParent(barGo.transform, false);
        }

        RectTransform fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.offsetMin = new Vector2(2f, 2f);
        fillAreaRt.offsetMax = new Vector2(-2f, -2f);

        Transform fillTf = fillAreaGo.transform.Find("Fill");
        GameObject fillGo;
        if (fillTf != null)
        {
            fillGo = fillTf.gameObject;
        }
        else
        {
            fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.layer = LayerMask.NameToLayer("UI");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
        }

        RectTransform fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        Image fillImage = fillGo.GetComponent<Image>();
        if (fillImage == null)
            fillImage = fillGo.AddComponent<Image>();
        fillImage.color = new Color(0.35f, 0.7f, 0.4f, 1f);
        fillImage.raycastTarget = false;

        Slider slider = barGo.GetComponent<Slider>();
        if (slider == null)
            slider = barGo.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.fillRect = fillRt;
        slider.targetGraphic = bgImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    [MenuItem(DistMcpMenus.PlayerStatusSetupCanvas)]
    static void SetupCanvasInOpenScene()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[PlayerStatusUISetupMenu] Canvas not found.");
            return;
        }

        UICanvasLayerHost layerHost = canvas.GetComponent<UICanvasLayerHost>();
        if (layerHost == null)
            layerHost = Undo.AddComponent<UICanvasLayerHost>(canvas.gameObject);
        layerHost.EditorSetupLayerHierarchy();

        Transform systemRoot = SystemHierarchySetup.ResolveSystemRoot();
        if (systemRoot == null)
        {
            Debug.LogError(
                "[PlayerStatusUISetupMenu] InputManager parent (System root) not found.");
            return;
        }

        Transform playerStatusRoot = SystemHierarchySetup.EnsureCategory(
            systemRoot,
            SystemHierarchySetup.PlayerStatus);

        PlayerStatusUIBridge bridge = EnsureBridge(playerStatusRoot);
        RemoveLegacyCanvasBridge(canvas, bridge);

        UICharacterController controller = Object.FindAnyObjectByType<UICharacterController>();
        if (controller == null)
        {
            GameObject go = new("PlayerStatusController");
            Undo.RegisterCreatedObjectUndo(go, "Create PlayerStatusController");
            go.transform.SetParent(playerStatusRoot, false);
            controller = Undo.AddComponent<UICharacterController>(go);
        }
        else
        {
            SystemHierarchySetup.EnsureChildUnder(
                playerStatusRoot,
                controller.transform,
                "Move PlayerStatusController Under System/PlayerStatus");
        }

        SerializedObject so = new(controller);
        so.FindProperty("_uiCanvas").objectReferenceValue = canvas;
        so.FindProperty("_layerHost").objectReferenceValue = layerHost;

        UICharacterWindow prefab =
            AssetDatabase.LoadAssetAtPath<UICharacterWindow>(WindowPrefabPath);
        if (prefab == null)
        {
            Debug.LogError(
                $"[PlayerStatusUISetupMenu] Prefab missing: {WindowPrefabPath}. " +
                "Hand-author or restore the prefab — do not full-bake over layout.");
            return;
        }

        so.FindProperty("_windowPrefab").objectReferenceValue = prefab;
        so.FindProperty("_window").objectReferenceValue = null;

        PlayerStatusWindowLauncher launcher = EnsureHudLauncher(layerHost, controller);
        so.FindProperty("_launcher").objectReferenceValue = launcher;

        SerializedProperty bridgeOnController = so.FindProperty("_bridge");
        if (bridgeOnController != null)
            bridgeOnController.objectReferenceValue = bridge;

        UIPlayerStatusSummaryController summaryController =
            EnsureSummaryController(playerStatusRoot);
        so.ApplyModifiedPropertiesWithoutUndo();

        UIPlayerStatusSummaryPanel summaryPrefab =
            AssetDatabase.LoadAssetAtPath<UIPlayerStatusSummaryPanel>(SummaryPrefabPath);
        if (summaryPrefab == null)
        {
            Debug.LogError(
                $"[PlayerStatusUISetupMenu] Prefab missing: {SummaryPrefabPath}. " +
                "Hand-author or restore the prefab — do not full-bake over layout.");
            return;
        }

        UIPlayerStatusSummaryPanel summaryPanel = EnsureHudPrefabInstance(
            layerHost,
            summaryPrefab,
            "Grp_PlayerStatusSummary");
        if (summaryPanel == null)
            return;

        // Prefab root starts inactive (empty mood strip); enable for Edit Mode layout.
        if (!summaryPanel.gameObject.activeSelf)
            Undo.RecordObject(summaryPanel.gameObject, "Activate summary HUD");
        summaryPanel.gameObject.SetActive(true);

        SerializedObject summarySo = new(summaryController);
        summarySo.FindProperty("_panel").objectReferenceValue = summaryPanel;
        SerializedProperty bridgeOnSummary = summarySo.FindProperty("_bridge");
        if (bridgeOnSummary != null)
            bridgeOnSummary.objectReferenceValue = bridge;
        SerializedProperty characterCtrlProp = summarySo.FindProperty("_characterController");
        if (characterCtrlProp != null)
            characterCtrlProp.objectReferenceValue = controller;
        summarySo.ApplyModifiedPropertiesWithoutUndo();

        SerializedProperty summaryPanelProp = so.FindProperty("_summaryPanel");
        if (summaryPanelProp != null)
        {
            summaryPanelProp.objectReferenceValue = summaryPanel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        WireLifeThreatBridge(layerHost, bridge);

        MergeLocalizationKeys();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log(
            "[PlayerStatusUISetupMenu] Bridge SSOT + controller + summary HUD wired.",
            summaryPanel);
    }

    static PlayerStatusUIBridge EnsureBridge(Transform playerStatusRoot)
    {
        PlayerStatusUIBridge onRoot = playerStatusRoot.GetComponent<PlayerStatusUIBridge>();
        if (onRoot != null)
            return onRoot;

        PlayerStatusUIBridge existing = Object.FindAnyObjectByType<PlayerStatusUIBridge>();
        if (existing != null && existing.transform.IsChildOf(playerStatusRoot))
            return existing;

        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        return Undo.AddComponent<PlayerStatusUIBridge>(playerStatusRoot.gameObject);
    }

    static void RemoveLegacyCanvasBridge(Canvas canvas, PlayerStatusUIBridge keep)
    {
        if (canvas == null)
            return;

        PlayerStatusUIBridge onCanvas = canvas.GetComponent<PlayerStatusUIBridge>();
        if (onCanvas == null || onCanvas == keep)
            return;

        Undo.DestroyObjectImmediate(onCanvas);
    }

    static void WireLifeThreatBridge(UICanvasLayerHost layerHost, PlayerStatusUIBridge bridge)
    {
        if (layerHost == null || bridge == null)
            return;

        Transform hud = layerHost.GetLayerRoot(UICanvasLayer.HUD);
        if (hud == null)
            return;

        UIHudLifeThreatOverlay overlay = hud.GetComponentInChildren<UIHudLifeThreatOverlay>(true);
        if (overlay == null)
            return;

        SerializedObject overlaySo = new(overlay);
        SerializedProperty bridgeProp = overlaySo.FindProperty("_bridge");
        if (bridgeProp == null)
            return;

        bridgeProp.objectReferenceValue = bridge;
        overlaySo.ApplyModifiedPropertiesWithoutUndo();
    }

    static UIPlayerStatusSummaryController EnsureSummaryController(Transform playerStatusRoot)
    {
        UIPlayerStatusSummaryController controller =
            Object.FindAnyObjectByType<UIPlayerStatusSummaryController>();
        if (controller == null)
        {
            GameObject go = new("PlayerStatusSummaryController");
            Undo.RegisterCreatedObjectUndo(go, "Create PlayerStatusSummaryController");
            go.transform.SetParent(playerStatusRoot, false);
            controller = Undo.AddComponent<UIPlayerStatusSummaryController>(go);
        }
        else
        {
            SystemHierarchySetup.EnsureChildUnder(
                playerStatusRoot,
                controller.transform,
                "Move Summary Controller Under System/PlayerStatus");
        }

        return controller;
    }

    static T EnsureHudPrefabInstance<T>(
        UICanvasLayerHost layerHost,
        T prefab,
        string instanceName) where T : Component
    {
        Transform hud = layerHost.GetLayerRoot(UICanvasLayer.HUD);
        Transform existing = hud.Find(instanceName);
        if (existing != null)
        {
            T panel = existing.GetComponent<T>();
            if (panel != null)
                return panel;

            Debug.LogError(
                $"[PlayerStatusUISetupMenu] '{instanceName}' under HUD lacks {typeof(T).Name}.",
                existing);
            return null;
        }

        T underHud = hud.GetComponentInChildren<T>(true);
        if (underHud != null)
        {
            underHud.gameObject.name = instanceName;
            return underHud;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, hud);
        if (instance == null)
        {
            Debug.LogError(
                $"[PlayerStatusUISetupMenu] PrefabUtility.InstantiatePrefab failed for {instanceName}.",
                prefab);
            return null;
        }

        Undo.RegisterCreatedObjectUndo(instance, $"Place {instanceName}");
        instance.name = instanceName;
        Debug.Log(
            $"[PlayerStatusUISetupMenu] Placed HUD instance '{instanceName}' under {hud.name}.",
            instance);
        return instance.GetComponent<T>();
    }

    // Legacy vitals/body-effect icons (MoodIconId 0–7) — keep order stable for serialization.
    static readonly (MoodIconId Id, string FileName)[] LegacyMoodCatalogEntries =
    {
        (MoodIconId.Hunger, "Mood_Hunger.png"),
        (MoodIconId.Thirst, "Mood_Thirst.png"),
        (MoodIconId.Stamina, "Mood_Stamina.png"),
        (MoodIconId.Bleed, "Mood_Bleed.png"),
        (MoodIconId.Fracture, "Mood_Fracture.png"),
        (MoodIconId.Infected, "Mood_Infected.png"),
        (MoodIconId.Regenerating, "Mood_Regenerating.png"),
        (MoodIconId.Adrenaline, "Mood_Adrenaline.png"),
    };

    // Appended MoodIconId values — file Mood_<Name>.png matches enum member name.
    static readonly MoodIconId[] ExtendedMoodIconIds =
    {
        MoodIconId.GoodMood,
        MoodIconId.Happy,
        MoodIconId.VeryHappy,
        MoodIconId.Stable,
        MoodIconId.SlightlyHappy,
        MoodIconId.Neutral,
        MoodIconId.SlightlySad,
        MoodIconId.Sad,
        MoodIconId.VerySad,
        MoodIconId.Depressed,
        MoodIconId.Stressed,
        MoodIconId.SeverelyStressed,
        MoodIconId.Fear,
        MoodIconId.ExtremeFear,
        MoodIconId.Angry,
        MoodIconId.Furious,
        MoodIconId.Tired,
        MoodIconId.VeryTired,
        MoodIconId.NeedRest,
        MoodIconId.WellRested,
        MoodIconId.Hungry,
        MoodIconId.VeryHungry,
        MoodIconId.Fed,
        MoodIconId.Full,
        MoodIconId.Thirsty,
        MoodIconId.VeryThirsty,
        MoodIconId.ThirstQuenched,
        MoodIconId.Discomfort,
        MoodIconId.Pain,
        MoodIconId.SeverePain,
        MoodIconId.Injured,
        MoodIconId.SeverelyInjured,
        MoodIconId.Sick,
        MoodIconId.SeverelySick,
        MoodIconId.LowImmunity,
        MoodIconId.Recovering,
        MoodIconId.Pale,
        MoodIconId.Overheated,
        MoodIconId.Hypothermia,
        MoodIconId.Comfortable,
        MoodIconId.Dirty,
        MoodIconId.VeryDirty,
        MoodIconId.NeedShower,
        MoodIconId.Attractive,
        MoodIconId.Warm,
        MoodIconId.TooHot,
        MoodIconId.TooCold,
        MoodIconId.Dark,
        MoodIconId.Lonely,
        MoodIconId.Bored,
        MoodIconId.Idle,
        MoodIconId.PleasantConversation,
        MoodIconId.GoodMeal,
        MoodIconId.RestArea,
        MoodIconId.SuitableEnvironment,
        MoodIconId.NatureFriendly,
        MoodIconId.Inspired,
        MoodIconId.Motivated,
        MoodIconId.SkillUp,
        MoodIconId.RelationshipImproved,
        MoodIconId.Loved,
        MoodIconId.MarriedEngaged,
        MoodIconId.Trust,
        MoodIconId.Respect,
        MoodIconId.Overencumbered,
        MoodIconId.OffBalance,
        MoodIconId.Fading,
        MoodIconId.StatCollapse,
        MoodIconId.Suffocating,
        MoodIconId.PainShocked,
        MoodIconId.CapacityDown,
        MoodIconId.Dying,
        MoodIconId.Defeated,
    };

    static void EnsureMoodAssets()
    {
        EnsureSoFolder();
        if (!AssetDatabase.IsValidFolder(MoodSpriteFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Dist/Visual/Sprites/UI/PlayerStatus"))
                AssetDatabase.CreateFolder("Assets/Dist/Visual/Sprites/UI", "PlayerStatus");
            AssetDatabase.CreateFolder("Assets/Dist/Visual/Sprites/UI/PlayerStatus", "Mood");
        }

        EnsureMoodIconsAtlas();

        EnsureCircleSprite(MoodSpriteFolder + "/Mood_Back.png", 64, Color.white, filled: true);
        for (int i = 0; i < LegacyMoodCatalogEntries.Length; i++)
            EnsureCircleSprite(
                MoodSpriteFolder + "/" + LegacyMoodCatalogEntries[i].FileName,
                48,
                Color.white,
                filled: false);

        // Atlas covers ExtendedMoodIconIds[0..63]; remaining keep single-file placeholders.
        for (int i = MoodIconsAtlasCount; i < ExtendedMoodIconIds.Length; i++)
        {
            string fileName = "Mood_" + ExtendedMoodIconIds[i] + ".png";
            EnsureCircleSprite(MoodSpriteFolder + "/" + fileName, 48, Color.white, filled: false);
        }

        DeleteObsoleteAtlasIndividualPngs();

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

        PlayerStatusMoodIconCatalog catalog =
            DistScriptableObjectEnsure.LoadOrCreate<PlayerStatusMoodIconCatalog>(MoodCatalogAssetPath);

        SerializedObject catalogSo = new(catalog);
        catalogSo.FindProperty("_backPlate").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(MoodSpriteFolder + "/Mood_Back.png");

        SerializedProperty entries = catalogSo.FindProperty("_entries");
        int totalCount = LegacyMoodCatalogEntries.Length + ExtendedMoodIconIds.Length;
        entries.arraySize = totalCount;

        for (int i = 0; i < LegacyMoodCatalogEntries.Length; i++)
        {
            SetCatalogEntry(
                entries,
                i,
                LegacyMoodCatalogEntries[i].Id,
                LoadMoodFrontSprite(LegacyMoodCatalogEntries[i].Id));
        }

        for (int i = 0; i < ExtendedMoodIconIds.Length; i++)
        {
            MoodIconId iconId = ExtendedMoodIconIds[i];
            SetCatalogEntry(
                entries,
                LegacyMoodCatalogEntries.Length + i,
                iconId,
                LoadMoodFrontSprite(iconId));
        }

        catalogSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Icons.png → Multiple sprites named Mood_&lt;Id&gt; (icon-only rect, no sheet number/label).
    /// </summary>
    static void EnsureMoodIconsAtlas()
    {
        if (!File.Exists(MoodIconsAtlasPath))
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] Missing mood atlas: {MoodIconsAtlasPath}");
            return;
        }

        if (ExtendedMoodIconIds.Length < MoodIconsAtlasCount)
        {
            Debug.LogError(
                "[PlayerStatusUISetupMenu] ExtendedMoodIconIds shorter than MoodIconsAtlasCount.");
            return;
        }

        string absPath = Path.Combine(
            Application.dataPath,
            "Dist/Visual/Sprites/UI/PlayerStatus/Mood/Icons.png");
        byte[] bytes = File.ReadAllBytes(absPath);
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!src.LoadImage(bytes))
        {
            Object.DestroyImmediate(src);
            Debug.LogError("[PlayerStatusUISetupMenu] Icons.png LoadImage failed.");
            return;
        }

        int w = src.width;
        int h = src.height;
        int cMinX = w;
        int cMinY = h;
        int cMaxX = 0;
        int cMaxY = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = src.GetPixel(x, y);
                float lum = (c.r + c.g + c.b) / 3f;
                if (c.a < 0.1f || lum > 0.92f)
                    continue;
                if (x < cMinX)
                    cMinX = x;
                if (y < cMinY)
                    cMinY = y;
                if (x > cMaxX)
                    cMaxX = x;
                if (y > cMaxY)
                    cMaxY = y;
            }
        }

        const int cols = 8;
        const int rows = 8;
        float cellW = (cMaxX - cMinX + 1) / (float)cols;
        float cellH = (cMaxY - cMinY + 1) / (float)rows;

        var metas = new SpriteMetaData[MoodIconsAtlasCount];
        for (int i = 0; i < MoodIconsAtlasCount; i++)
        {
            int col = i % cols;
            int row = i / cols;
            int x0 = cMinX + Mathf.RoundToInt(col * cellW);
            int x1 = cMinX + Mathf.RoundToInt((col + 1) * cellW) - 1;
            int yTop = cMaxY - Mathf.RoundToInt(row * cellH);
            int yBot = cMaxY - Mathf.RoundToInt((row + 1) * cellH) + 1;
            int ch = yTop - yBot + 1;

            int dropTop = Mathf.RoundToInt(ch * 0.30f);
            int dropBot = Mathf.RoundToInt(ch * 0.34f);
            int yHi = yTop - dropTop;
            int yLo = yBot + dropBot;

            double sumX = 0;
            double sumY = 0;
            double weight = 0;
            double sumXc = 0;
            double sumYc = 0;
            double weightC = 0;
            for (int y = yLo; y <= yHi; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Color c = src.GetPixel(x, y);
                    float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    float chroma = max - min;
                    float lum = (c.r + c.g + c.b) / 3f;
                    if (c.a < 0.15f || lum > 0.88f)
                        continue;

                    float wgt = 1f - lum;
                    sumX += x * wgt;
                    sumY += y * wgt;
                    weight += wgt;

                    if (chroma > 0.10f && max < 0.95f)
                    {
                        float wc = chroma * wgt;
                        sumXc += x * wc;
                        sumYc += y * wc;
                        weightC += wc;
                    }
                }
            }

            int cx;
            int cy;
            if (weightC > 5.0)
            {
                cx = (int)System.Math.Round(sumXc / weightC);
                cy = (int)System.Math.Round(sumYc / weightC);
            }
            else if (weight > 5.0)
            {
                cx = (int)System.Math.Round(sumX / weight);
                cy = (int)System.Math.Round(sumY / weight);
            }
            else
            {
                cx = (x0 + x1) / 2;
                cy = (yLo + yHi) / 2;
            }

            cy = Mathf.Clamp(cy + 2, yLo, yHi);

            int half = MoodIconsAtlasCropSize / 2;
            int sx0 = Mathf.Clamp(cx - half, 0, w - MoodIconsAtlasCropSize);
            int sy0 = Mathf.Clamp(cy - half, 0, h - MoodIconsAtlasCropSize);

            MoodIconId iconId = ExtendedMoodIconIds[i];
            metas[i] = new SpriteMetaData
            {
                name = "Mood_" + iconId,
                rect = new Rect(sx0, sy0, MoodIconsAtlasCropSize, MoodIconsAtlasCropSize),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero,
            };
        }

        Object.DestroyImmediate(src);

        var importer = (TextureImporter)AssetImporter.GetAtPath(MoodIconsAtlasPath);
        if (importer == null)
        {
            Debug.LogError($"[PlayerStatusUISetupMenu] No TextureImporter at {MoodIconsAtlasPath}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritesheet = metas;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        Debug.Log(
            $"[PlayerStatusUISetupMenu] Mood atlas sliced: {MoodIconsAtlasCount} sprites on {MoodIconsAtlasPath}");
    }

    static void DeleteObsoleteAtlasIndividualPngs()
    {
        for (int i = 0; i < MoodIconsAtlasCount && i < ExtendedMoodIconIds.Length; i++)
        {
            string path = MoodSpriteFolder + "/Mood_" + ExtendedMoodIconIds[i] + ".png";
            if (!File.Exists(path))
                continue;
            AssetDatabase.DeleteAsset(path);
        }
    }

    static Sprite LoadMoodFrontSprite(MoodIconId iconId)
    {
        for (int i = 0; i < MoodIconsAtlasCount && i < ExtendedMoodIconIds.Length; i++)
        {
            if (ExtendedMoodIconIds[i] != iconId)
                continue;

            string spriteName = "Mood_" + iconId;
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(MoodIconsAtlasPath);
            for (int a = 0; a < assets.Length; a++)
            {
                if (assets[a] is Sprite sprite && sprite.name == spriteName)
                    return sprite;
            }

            Debug.LogWarning(
                $"[PlayerStatusUISetupMenu] Atlas sprite missing: {spriteName} on {MoodIconsAtlasPath}");
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(
            MoodSpriteFolder + "/Mood_" + iconId + ".png");
    }

    static void SetCatalogEntry(
        SerializedProperty entries,
        int index,
        MoodIconId iconId,
        Sprite front)
    {
        SerializedProperty entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("IconId").enumValueIndex = (int)iconId;
        entry.FindPropertyRelative("FrontSprite").objectReferenceValue = front;
    }

    static void EnsureCircleSprite(string assetPath, int size, Color color, bool filled)
    {
        bool exists = System.IO.File.Exists(assetPath);
        if (exists)
        {
            EnsureSpriteImportSettings(assetPath);
            if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) != null)
                return;
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = (size - 1) * 0.5f;
        float outer = size * 0.45f;
        float inner = size * 0.28f;
        float outerSq = outer * outer;
        float innerSq = inner * inner;

        Color32 c = color;
        Color32 clear = new(0, 0, 0, 0);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distSq = dx * dx + dy * dy;
                bool draw = filled ? distSq <= outerSq : distSq <= outerSq && distSq >= innerSq;
                tex.SetPixel(x, y, draw ? c : clear);
            }
        }

        tex.Apply();
        System.IO.File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        EnsureSpriteImportSettings(assetPath);
    }

    static void EnsureSpriteImportSettings(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    static void EnsureSoFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Dist/SOData"))
            AssetDatabase.CreateFolder("Assets/Dist", "SOData");
        if (!AssetDatabase.IsValidFolder(SoGameplayFolder))
            AssetDatabase.CreateFolder("Assets/Dist/SOData", "Gameplay");
        if (!AssetDatabase.IsValidFolder(SoPlayerStatusFolder))
            AssetDatabase.CreateFolder(SoGameplayFolder, "PlayerStatus");
    }

    [MenuItem(DistMcpMenus.PlayerStatusMergeLocalizationKeys)]
    static void MergeLocalizationKeysMenu() => MergeLocalizationKeys();

    static PlayerStatusWindowLauncher EnsureHudLauncher(
        UICanvasLayerHost layerHost,
        UICharacterController controller)
    {
        Transform hud = layerHost.GetLayerRoot(UICanvasLayer.HUD);
        Transform existing = hud.Find("Btn_PlayerStatusLauncher");
        GameObject buttonGo;
        if (existing != null)
        {
            buttonGo = existing.gameObject;
        }
        else
        {
            buttonGo = new GameObject("Btn_PlayerStatusLauncher", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(buttonGo, "Create status launcher");
            buttonGo.transform.SetParent(hud, false);
            buttonGo.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -52f);
            rect.sizeDelta = new Vector2(40f, 40f);
            buttonGo.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.9f, 0.85f);
        }

        PlayerStatusWindowLauncher launcher = buttonGo.GetComponent<PlayerStatusWindowLauncher>();
        if (launcher == null)
            launcher = Undo.AddComponent<PlayerStatusWindowLauncher>(buttonGo);

        SerializedObject launcherSo = new(launcher);
        launcherSo.FindProperty("_controller").objectReferenceValue = controller;
        launcherSo.FindProperty("_button").objectReferenceValue = buttonGo.GetComponent<Button>();
        launcherSo.FindProperty("_iconImage").objectReferenceValue = buttonGo.GetComponent<Image>();
        launcherSo.ApplyModifiedPropertiesWithoutUndo();
        return launcher;
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Dist/Visual/Prefabs/UIComponents"))
            AssetDatabase.CreateFolder("Assets/Dist/Visual/Prefabs", "UIComponents");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Dist/Visual/Prefabs/UIComponents", "PlayerStatus");
    }

    static void MergeLocalizationKeys()
    {
        LocalizationTable table =
            AssetDatabase.LoadAssetAtPath<LocalizationTable>(LocalizationTable.AssetPath);
        if (table == null)
        {
            Debug.LogError(
                "[PlayerStatusUISetupMenu] UI_ko table missing. Run " +
                DistMcpMenus.LocalizationSelectOrCreateUiKo + ".");
            return;
        }

        var map = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
        for (int i = 0; i < table.Entries.Count; i++)
        {
            LocalizationTable.Entry e = table.Entries[i];
            if (e != null && !string.IsNullOrEmpty(e.key))
                map[e.key] = e.text ?? string.Empty;
        }

        void Put(string key, string text)
        {
            if (!map.ContainsKey(key))
                map[key] = text;
        }

        Put("PlayerStatus.Title", "상태");
        Put("PlayerStatus.VitalsSection", "생존");
        Put("PlayerStatus.SkillsSection", "스킬");
        Put("PlayerStatus.DetailHeader", "부위 상세");
        Put("PlayerStatus.DetailSubparts", "세부 부위");
        Put("PlayerStatus.DetailEffects", "상태 이상");
        Put("PlayerStatus.NoEffects", "이상 없음");
        Put("PlayerStatus.Lost", "상실");
        Put("PlayerStatus.Kind.Prosthetic", "의체");
        Put("PlayerStatus.ConditionFormat", "{0}/{1}");
        Put("PlayerStatus.VitalFormat", "{0}/{1}");
        Put("PlayerStatus.SkillFormat", "{0}  Lv.{1}");
        Put("PlayerStatus.DebugSeverArmL", "절단(왼팔)");

        Put("PlayerStatus.Part.head", "머리");
        Put("PlayerStatus.Part.neck", "목");
        Put("PlayerStatus.Part.chest", "가슴");
        Put("PlayerStatus.Part.belly", "배");
        Put("PlayerStatus.Part.pelvis", "골반");
        Put("PlayerStatus.Part.upper_arm_l", "왼윗팔");
        Put("PlayerStatus.Part.lower_arm_l", "왼아래팔");
        Put("PlayerStatus.Part.upper_arm_r", "오른윗팔");
        Put("PlayerStatus.Part.lower_arm_r", "오른아래팔");
        Put("PlayerStatus.Part.thigh_l", "왼허벅지");
        Put("PlayerStatus.Part.calf_l", "왼종아리");
        Put("PlayerStatus.Part.thigh_r", "오른허벅지");
        Put("PlayerStatus.Part.calf_r", "오른종아리");
        Put("PlayerStatus.Part.torso", "몸통");
        Put("PlayerStatus.Part.arm_l", "왼팔");
        Put("PlayerStatus.Part.arm_r", "오른팔");
        Put("PlayerStatus.Part.leg_l", "왼다리");
        Put("PlayerStatus.Part.leg_r", "오른다리");
        Put("PlayerStatus.Part.eyes", "눈");
        Put("PlayerStatus.Part.mouth", "입");
        Put("PlayerStatus.Part.hand_l", "왼손");
        Put("PlayerStatus.Part.hand_r", "오른손");
        Put("PlayerStatus.Part.foot_l", "왼발");
        Put("PlayerStatus.Part.foot_r", "오른발");
        Put("PlayerStatus.Part.finger_thumb_l", "왼엄지");
        Put("PlayerStatus.Part.finger_index_l", "왼검지");
        Put("PlayerStatus.Part.finger_thumb_r", "오른엄지");
        Put("PlayerStatus.Part.finger_index_r", "오른검지");

        Put("PlayerStatus.Vital.Hunger", "공복");
        Put("PlayerStatus.Vital.Thirst", "갈증");
        Put("PlayerStatus.Vital.Stamina", "스태미나");

        Put("PlayerStatus.Skill.survival", "생존술");
        Put("PlayerStatus.Trait.survival", "생존술");
        Put("PlayerStatus.Trait.omniscience", "전지");
        Put("PlayerStatus.Trait.omnivision", "만시");

        Put("PlayerStatus.VitalProse.Hunger.Full", "배가 부르다");
        Put("PlayerStatus.VitalProse.Hunger.Ok", "배가 든든하다");
        Put("PlayerStatus.VitalProse.Hunger.Low", "배가 고프다");
        Put("PlayerStatus.VitalProse.Hunger.Critical", "굶주리고 있다");
        Put("PlayerStatus.VitalProse.Hunger.Engorged", "배가 터질 듯하다");
        Put("PlayerStatus.VitalProse.Hunger.Sated", "배가 든든하다");
        Put("PlayerStatus.VitalProse.Hunger.Hungry", "배가 고프다");
        Put("PlayerStatus.VitalProse.Hunger.VeryHungry", "매우 배가 고프다");
        Put("PlayerStatus.VitalProse.Hunger.Famished", "허기에 시달린다");
        Put("PlayerStatus.VitalProse.Hunger.Starving", "굶주리고 있다");

        Put("PlayerStatus.VitalProse.Thirst.Full", "목이 충분히 축인다");
        Put("PlayerStatus.VitalProse.Thirst.Ok", "목이 마르지 않았다");
        Put("PlayerStatus.VitalProse.Thirst.Low", "목이 마르다");
        Put("PlayerStatus.VitalProse.Thirst.Critical", "목이 타는 것 같다");
        Put("PlayerStatus.VitalProse.Thirst.Quenched", "목이 축여 있다");
        Put("PlayerStatus.VitalProse.Thirst.NotThirsty", "목이 마르지 않았다");
        Put("PlayerStatus.VitalProse.Thirst.Thirsty", "목이 마르다");
        Put("PlayerStatus.VitalProse.Thirst.VeryThirsty", "목이 매우 마르다");
        Put("PlayerStatus.VitalProse.Thirst.Parched", "목이 타들어간다");

        Put("PlayerStatus.VitalProse.Stamina.Full", "몸이 가볍다");
        Put("PlayerStatus.VitalProse.Stamina.Ok", "아직 버틸 만하다");
        Put("PlayerStatus.VitalProse.Stamina.Low", "몸이 무겁다");
        Put("PlayerStatus.VitalProse.Stamina.Critical", "기진맥진하다");

        Put("PlayerStatus.Effect.bleed", "출혈");
        Put("PlayerStatus.Effect.bruise", "타박상");
        Put("PlayerStatus.Effect.cut", "베임");
        Put("PlayerStatus.Effect.gunshot", "총상");
        Put("PlayerStatus.Effect.fracture", "골절");
        Put("PlayerStatus.Effect.infected", "감염");
        Put("PlayerStatus.Effect.regenerating", "재생 중");
        Put("PlayerStatus.Effect.adrenaline", "아드레날린");
        Put("PlayerStatus.Effect.bloated", "팽만");
        Put("PlayerStatus.Effect.bandaged", "붕대");
        Put("PlayerStatus.Effect.bandage_dirty", "더러운 붕대");
        Put("PlayerStatus.BandageDirtyFormat", "오염 {0}%");
        Put("PlayerStatus.Effect.hemostatic", "지혈");

        Put("PlayerStatus.Mood.Full", "배가 부르다");
        Put("PlayerStatus.Mood.Fed", "배가 든든하다");
        Put("PlayerStatus.Mood.Hungry", "배가 고프다");
        Put("PlayerStatus.Mood.VeryHungry", "매우 배가 고프다");
        Put("PlayerStatus.Mood.ThirstQuenched", "목이 축여 있다");
        Put("PlayerStatus.Mood.Thirsty", "목이 마르다");
        Put("PlayerStatus.Mood.VeryThirsty", "목이 매우 마르다");
        Put("PlayerStatus.Mood.GoodMood", "기분이 좋다");
        Put("PlayerStatus.Mood.Sad", "기분이 처진다");
        Put("PlayerStatus.Mood.Sick", "몸이 아프다");
        Put("PlayerStatus.Mood.Adrenaline", "아드레날린");
        Put("PlayerStatus.Mood.Tired", "피곤하다");
        Put("PlayerStatus.Mood.VeryTired", "매우 피곤하다");
        Put("PlayerStatus.Mood.NeedRest", "잠이 쏟아진다");
        Put("PlayerStatus.Mood.WellRested", "개운하다");
        Put("PlayerStatus.Mood.Overencumbered", "과적");
        Put("PlayerStatus.Mood.Overencumbered.Light", "짐이 조금 무겁다");
        Put("PlayerStatus.Mood.Overencumbered.Medium", "짐이 무겁다");
        Put("PlayerStatus.Mood.Overencumbered.Heavy", "짐이 너무 무겁다");
        Put("PlayerStatus.Mood.Overencumbered.Extreme", "움직일 수 없을 만큼 무겁다");
        Put("PlayerStatus.Mood.OffBalance", "중심이 흔들린다");
        Put("PlayerStatus.Mood.OffBalance.Fallen", "중심을 잃고 쓰러졌다");
        Put("PlayerStatus.Mood.Pale", "핏기가 없다");
        Put("PlayerStatus.Mood.Pale.Critical", "과다출혈로 쓰러질 것 같다");
        Put("PlayerStatus.Bleed.DrainRateFormat", "혈량 감소: {0}%/초");
        Put("PlayerStatus.Bleed.EtaFormat", "완전 출혈까지: {0}");
        Put("PlayerStatus.Bleed.BandagedBlock", "붕대로 출혈이 막혀 있다");
        Put("PlayerStatus.Bleed.Prose.Bandaged", "붕대로 출혈이 막혀 있다");
        Put("PlayerStatus.Bleed.Prose.Mild", "피가 서서히 빠진다");
        Put("PlayerStatus.Bleed.Prose.Moderate", "피가 계속 빠진다");
        Put("PlayerStatus.Bleed.Prose.Severe", "피가 빠르게 줄어든다");
        Put("PlayerStatus.Bleed.VitalsNumeric", "출혈  감소 {0}%/초 · 완전 출혈 {1}");
        Put("PlayerStatus.Bleed.DurationMinutes", "{0}분 {1}초");
        Put("PlayerStatus.Bleed.DurationSeconds", "{0}초");
        Put("PlayerStatus.Mood.Fading", "의식이 흐릿하다");
        Put("PlayerStatus.Mood.Fading.Downed", "의식이 가물거린다");
        Put("PlayerStatus.Mood.Fading.Fatal", "의식이 끊겼다");
        Put("PlayerStatus.Mood.StatCollapse", "정신이 무너졌다");

        foreach (Garunnir.Runtime.Gameplay.Data.MoodIconId iconId in
                 System.Enum.GetValues(typeof(Garunnir.Runtime.Gameplay.Data.MoodIconId)))
            PutMoodIconDefault(map, iconId);

        foreach (ThoughtId thoughtId in System.Enum.GetValues(typeof(ThoughtId)))
        {
            if (thoughtId == ThoughtId.None)
                continue;
            PutThoughtDefault(map, thoughtId);
        }

        foreach (MoodBreakKind breakKind in System.Enum.GetValues(typeof(MoodBreakKind)))
        {
            if (breakKind == MoodBreakKind.None)
                continue;
            PutBreakDefault(map, breakKind);
        }

        Put("msg.status.mood_break_flee", "공포에 질려 도망치기 시작했다.");
        Put("msg.status.mood_break_berserk", "분노가 폭발했다.");
        Put("msg.status.mood_break_catatonic", "멍하니 굳어 버렸다.");

        Put("ItemContextMenu.Eat", "먹기");
        Put("ItemContextMenu.Drink", "마시기");
        Put("ItemContextMenu.Use", "사용");
        Put("ItemContextMenu.Unwrap", "붕대 벗기");
        Put("msg.status.needs_vomit", "너무 많이 먹어 토했다.");
        Put("msg.status.needs_starve", "굶주림으로 쓰러졌다.");
        Put("msg.status.needs_dehydrate", "갈증으로 쓰러졌다.");
        Put("msg.status.needs_hunger_70", "배가 고프다.");
        Put("msg.status.needs_hunger_50", "많이 배가 고프다.");
        Put("msg.status.needs_hunger_25", "매우 배가 고프다.");
        Put("msg.status.needs_hunger_10", "굶주리고 있다.");
        Put("msg.status.needs_thirst_danger", "목이 타들어간다.");

        var list = new System.Collections.Generic.List<LocalizationTable.Entry>(map.Count);
        foreach (var kv in map)
            list.Add(new LocalizationTable.Entry { key = kv.Key, text = kv.Value });

        table.EditorSetEntries(list);
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PlayerStatusUISetupMenu] Localization merged ({map.Count} keys).");
    }

    static void PutMoodIconDefault(
        System.Collections.Generic.Dictionary<string, string> map,
        Garunnir.Runtime.Gameplay.Data.MoodIconId iconId)
    {
        string key = "PlayerStatus.Mood." + iconId;
        if (map.ContainsKey(key))
            return;
        if (MoodIconDefaultKo.TryGetValue(iconId, out string text))
            map[key] = text;
        else
            map[key] = iconId.ToString();
    }

    static void PutThoughtDefault(
        System.Collections.Generic.Dictionary<string, string> map,
        ThoughtId thoughtId)
    {
        string key = "PlayerStatus.Thought." + thoughtId;
        if (map.ContainsKey(key))
            return;
        if (ThoughtDefaultKo.TryGetValue(thoughtId, out string text))
            map[key] = text;
        else
            map[key] = thoughtId.ToString();
    }

    static void PutBreakDefault(
        System.Collections.Generic.Dictionary<string, string> map,
        MoodBreakKind breakKind)
    {
        string key = "PlayerStatus.Thought.Breaking" + breakKind;
        if (map.ContainsKey(key))
            return;
        if (BreakDefaultKo.TryGetValue(breakKind, out string text))
            map[key] = text;
        else
            map[key] = breakKind.ToString();
    }

    static readonly System.Collections.Generic.Dictionary<
        Garunnir.Runtime.Gameplay.Data.MoodIconId,
        string> MoodIconDefaultKo =
        new()
        {
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Hunger] = "배고프다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Thirst] = "목마르다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Stamina] = "지쳤다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Bleed] = "피가 난다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Fracture] = "골절",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Infected] = "감염",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Regenerating] = "재생 중",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.GoodMood] = "기분이 좋다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Happy] = "행복하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.VeryHappy] = "매우 행복하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Stable] = "마음이 안정되었다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.SlightlyHappy] = "조금 기분이 좋다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Neutral] = "평온하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.SlightlySad] = "조금 우울하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Sad] = "우울하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.VerySad] = "매우 우울하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Depressed] = "절망했다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Stressed] = "스트레스가 쌓였다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.SeverelyStressed] = "스트레스에 짓눌린다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Fear] = "두렵다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.ExtremeFear] = "극도로 두렵다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Angry] = "화가 난다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Furious] = "분노가 치민다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Discomfort] = "불편하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Pain] = "아프다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.SeverePain] = "심하게 아프다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Injured] = "다쳤다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.SeverelyInjured] = "심하게 다쳤다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Sick] = "몸이 아프다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.SeverelySick] = "몹시 아프다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.LowImmunity] = "면역이 약하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Recovering] = "회복 중이다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Pale] = "핏기가 없다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Overheated] = "과열되었다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Hypothermia] = "저체온",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Comfortable] = "쾌적하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Dirty] = "더럽다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.VeryDirty] = "매우 더럽다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.NeedShower] = "씻고 싶다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Attractive] = "기분이 좋아 보인다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Warm] = "따뜻하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.TooHot] = "덥다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.TooCold] = "춥다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Dark] = "어둡다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Lonely] = "외롭다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Bored] = "지루하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Idle] = "한가하다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.PleasantConversation] = "즐거운 대화",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.GoodMeal] = "맛있는 식사",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.RestArea] = "쉬기 좋은 곳",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.SuitableEnvironment] = "환경이 좋다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.NatureFriendly] = "자연 친화적",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Inspired] = "영감이 샘솟다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Motivated] = "의욕이 생긴다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.SkillUp] = "실력이 늘었다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.RelationshipImproved] = "관계가 좋아졌다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Loved] = "사랑받는다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.MarriedEngaged] = "결혼·약혼",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Trust] = "신뢰한다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Respect] = "존경한다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Suffocating] = "숨이 막힌다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.PainShocked] = "고통에 쓰러졌다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.CapacityDown] = "몸이 버티지 못한다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Dying] = "죽을 것 같다",
            [Garunnir.Runtime.Gameplay.Data.MoodIconId.Defeated] = "쓰러졌다",
        };

    static readonly System.Collections.Generic.Dictionary<ThoughtId, string> ThoughtDefaultKo =
        new()
        {
            [ThoughtId.Crafted] = "무언가를 만들었다",
            [ThoughtId.AteHotMeal] = "따뜻한 식사를 했다",
            [ThoughtId.LowOxygen] = "숨이 막힌다",
            [ThoughtId.PainShock] = "고통에 쓰러졌다",
            [ThoughtId.CapacityDown] = "몸이 버티지 못한다",
            [ThoughtId.Dirty] = "더럽다",
            [ThoughtId.VeryDirty] = "매우 더럽다",
            [ThoughtId.Lonely] = "외롭다",
            [ThoughtId.Bored] = "지루하다",
            [ThoughtId.Uncomfortable] = "불편하다",
            [ThoughtId.Cramped] = "답답하다",
            [ThoughtId.Dark] = "어둡다",
            [ThoughtId.Stressed] = "스트레스가 쌓였다",
            [ThoughtId.SeverelyStressed] = "스트레스에 짓눌린다",
            [ThoughtId.SeverelySick] = "몹시 아프다",
            [ThoughtId.Recovering] = "회복 중이다",
            [ThoughtId.NeedShower] = "씻고 싶다",
            [ThoughtId.FreshlyBathed] = "깨끗하게 씻었다",
            [ThoughtId.Attractive] = "기분이 좋아 보인다",
            [ThoughtId.PleasantConversation] = "즐거운 대화를 나눴다",
            [ThoughtId.RestArea] = "쉬기 좋은 곳이다",
            [ThoughtId.SuitableEnvironment] = "환경이 좋다",
            [ThoughtId.NatureFriendly] = "자연 친화적이다",
            [ThoughtId.Inspired] = "영감이 샘솟다",
            [ThoughtId.Motivated] = "의욕이 생긴다",
            [ThoughtId.SkillUp] = "실력이 늘었다",
            [ThoughtId.RelationshipImproved] = "관계가 좋아졌다",
            [ThoughtId.Loved] = "사랑받는다",
            [ThoughtId.MarriedEngaged] = "결혼·약혼했다",
            [ThoughtId.Trust] = "신뢰한다",
            [ThoughtId.Respect] = "존경한다",
        };

    static readonly System.Collections.Generic.Dictionary<MoodBreakKind, string> BreakDefaultKo =
        new()
        {
            [MoodBreakKind.Wander] = "정신붕괴: 배회 중",
            [MoodBreakKind.Flee] = "정신붕괴: 도주 중",
            [MoodBreakKind.Berserk] = "정신붕괴: 광폭화",
            [MoodBreakKind.Catatonic] = "정신붕괴: 긴장성 실신",
        };
}
#endif

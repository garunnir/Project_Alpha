// ============================================================
// DataDefinitionsCreateActions — Data Definitions + Create (Odin)
// ============================================================

using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

[HideReferenceObjectPicker]
public sealed class TileDefinitionCreateAction
{
    const string DraftKey = "TileDefinition";

    bool AlwaysShow => true;

    [Title("Create Tile Definition", "Map / Tiles", TitleAlignments.Split)]
    [GUIColor(0.55f, 1f, 0.65f)]
    [InfoBox(
        "에셋은 " + DataDefinitionsSoCatalogs.TileFolder + "/ 에 생성됩니다.\n저장은 Unity Ctrl+S.",
        SdfIconType.PlusSquareFill,
        nameof(AlwaysShow))]
    [ShowInInspector, HideLabel, DisplayAsString(EnableRichText = true)]
    [PropertyOrder(-5)]
    string Badge => "<color=#77ff99><b>NEW</b></color>  ·  TileDefinition SO";

    [LabelText("Base name")]
    [PropertyOrder(-1)]
    [OnValueChanged(nameof(SaveDraft))]
    public string BaseName;

    [OnInspectorInit]
    void InitBaseName()
    {
        BaseName = DataDefinitionsCreateNameDraft.GetOrRecommend(
            DraftKey,
            DataDefinitionsSoCatalogs.RecommendTileBaseName);
    }

    [Button(SdfIconType.PlusCircleFill, "Create Tile Definition")]
    [GUIColor(0.4f, 0.95f, 0.55f)]
    void Create()
    {
        DataDefinitionsSoCatalogs.FinishCreate(
            DataDefinitionsSoCatalogs.CreateTileDefinition(BaseName));
    }

    void SaveDraft() => DataDefinitionsCreateNameDraft.Set(DraftKey, BaseName);
}

[HideReferenceObjectPicker]
public sealed class CharacterFactionCreateAction
{
    const string DraftKey = "CharacterFaction";

    bool AlwaysShow => true;

    [Title("Create Character Faction", "Characters / Factions", TitleAlignments.Split)]
    [GUIColor(0.55f, 1f, 0.65f)]
    [InfoBox(
        "에셋은 " + CharacterDefinitionCatalog.AssetFolder + "/ 에 생성됩니다.\n저장은 Unity Ctrl+S.",
        SdfIconType.FlagFill,
        nameof(AlwaysShow))]
    [ShowInInspector, HideLabel, DisplayAsString(EnableRichText = true)]
    [PropertyOrder(-5)]
    string Badge => "<color=#77ff99><b>NEW</b></color>  ·  CharacterFaction SO";

    [LabelText("Base name")]
    [PropertyOrder(-1)]
    [OnValueChanged(nameof(SaveDraft))]
    public string BaseName;

    [OnInspectorInit]
    void InitBaseName()
    {
        BaseName = DataDefinitionsCreateNameDraft.GetOrRecommend(
            DraftKey,
            DataDefinitionsSoCatalogs.RecommendFactionBaseName);
    }

    [Button(SdfIconType.PlusCircleFill, "Create Character Faction")]
    [GUIColor(0.4f, 0.95f, 0.55f)]
    void Create()
    {
        DataDefinitionsSoCatalogs.FinishCreate(
            DataDefinitionsSoCatalogs.CreateFaction(BaseName));
    }

    void SaveDraft() => DataDefinitionsCreateNameDraft.Set(DraftKey, BaseName);
}

[HideReferenceObjectPicker]
public sealed class WeaponPresentationCreateAction
{
    const string DraftKey = "WeaponPresentation";

    bool AlwaysShow => true;

    [Title("Create Weapon Presentation", "Combat / Presentations", TitleAlignments.Split)]
    [GUIColor(0.55f, 1f, 0.65f)]
    [InfoBox(
        "에셋은 " + GameDataWeaponPresentationEditor.PresentationsFolder + "/ 에 생성됩니다.\n저장은 Unity Ctrl+S.",
        SdfIconType.PlayFill,
        nameof(AlwaysShow))]
    [ShowInInspector, HideLabel, DisplayAsString(EnableRichText = true)]
    [PropertyOrder(-5)]
    string Badge => "<color=#77ff99><b>NEW</b></color>  ·  WeaponPresentation SO";

    [LabelText("Base name")]
    [PropertyOrder(-1)]
    [OnValueChanged(nameof(SaveDraft))]
    public string BaseName;

    [OnInspectorInit]
    void InitBaseName()
    {
        BaseName = DataDefinitionsCreateNameDraft.GetOrRecommend(
            DraftKey,
            DataDefinitionsSoCatalogs.RecommendPresentationBaseName);
    }

    [Button(SdfIconType.PlusCircleFill, "Create Weapon Presentation")]
    [GUIColor(0.4f, 0.95f, 0.55f)]
    void Create()
    {
        DataDefinitionsSoCatalogs.FinishCreate(
            DataDefinitionsSoCatalogs.CreatePresentation(BaseName));
    }

    void SaveDraft() => DataDefinitionsCreateNameDraft.Set(DraftKey, BaseName);
}

[HideReferenceObjectPicker]
public sealed class WeaponAttackCreateAction
{
    const string DraftKey = "WeaponAttack";

    bool AlwaysShow => true;

    [Title("Create Weapon Attack", "Combat / Attacks", TitleAlignments.Split)]
    [GUIColor(0.55f, 1f, 0.65f)]
    [InfoBox(
        "에셋은 " + DataDefinitionsSoCatalogs.AttacksFolder + "/ 에 생성됩니다.\n저장은 Unity Ctrl+S.",
        SdfIconType.LightningFill,
        nameof(AlwaysShow))]
    [ShowInInspector, HideLabel, DisplayAsString(EnableRichText = true)]
    [PropertyOrder(-5)]
    string Badge => "<color=#77ff99><b>NEW</b></color>  ·  WeaponAttack SO";

    [LabelText("Base name")]
    [PropertyOrder(-1)]
    [OnValueChanged(nameof(SaveDraft))]
    public string BaseName;

    [OnInspectorInit]
    void InitBaseName()
    {
        BaseName = DataDefinitionsCreateNameDraft.GetOrRecommend(
            DraftKey,
            DataDefinitionsSoCatalogs.RecommendAttackBaseName);
    }

    [Button(SdfIconType.PlusCircleFill, "Create Weapon Attack")]
    [GUIColor(0.4f, 0.95f, 0.55f)]
    void Create()
    {
        DataDefinitionsSoCatalogs.FinishCreate(
            DataDefinitionsSoCatalogs.CreateAttack(BaseName));
    }

    void SaveDraft() => DataDefinitionsCreateNameDraft.Set(DraftKey, BaseName);
}

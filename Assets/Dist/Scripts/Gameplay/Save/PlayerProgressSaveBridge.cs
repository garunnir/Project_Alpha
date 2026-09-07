// ============================================================
// PlayerProgressSaveBridge — possessed 플레이어 진행 스냅샷 ↔ map JSON
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

public static class PlayerProgressSaveBridge
{
    static PlayerProgressSaveDto s_pendingRestore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterMapSaveHook()
    {
        MapSaveLayerCarryOver.MergePlayerProgress = MergeIntoMapDto;
        PlayerProgressSnapshotPending.OnMapDtoLoaded = SetPendingFromMapDto;
        s_pendingRestore = null;
    }

    static void SetPendingFromMapDto(MapSaveJsonDto dto)
    {
        s_pendingRestore = null;
        if (dto == null || !dto.hasPlayerProgressSnapshot || string.IsNullOrEmpty(dto.playerProgressJson))
            return;

        try
        {
            s_pendingRestore = JsonUtility.FromJson<PlayerProgressSaveDto>(dto.playerProgressJson);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerProgressSaveBridge] playerProgressJson 파싱 실패: {e}");
            s_pendingRestore = null;
        }
    }

    public static bool TryRestorePossessed(GameObject possessedBody)
    {
        if (s_pendingRestore == null || possessedBody == null)
            return false;

        PlayerProgressSaveDto snapshot = s_pendingRestore;
        s_pendingRestore = null;
        ApplySnapshot(possessedBody, snapshot);
        return true;
    }

    public static void MergeIntoMapDto(MapSaveJsonDto target, MapSaveJsonDto existing)
    {
        if (target == null)
            return;

        if (TryCapture(out PlayerProgressSaveDto captured))
        {
            target.hasPlayerProgressSnapshot = true;
            target.playerProgressJson = JsonUtility.ToJson(captured);
            return;
        }

        CarryPlayerProgress(target, existing);
    }

    static void CarryPlayerProgress(MapSaveJsonDto target, MapSaveJsonDto existing)
    {
        if (existing == null || !existing.hasPlayerProgressSnapshot)
        {
            target.hasPlayerProgressSnapshot = false;
            target.playerProgressJson = null;
            return;
        }

        target.hasPlayerProgressSnapshot = true;
        target.playerProgressJson = existing.playerProgressJson;
    }

    public static bool TryCapture(out PlayerProgressSaveDto dto)
    {
        dto = null;
        CharacterSessionHub hub = CharacterSessionHub.Player;
        if (hub == null)
            return false;

        GameObject body = hub.gameObject;
        if (body == null)
            return false;

        Vector3 pos = body.transform.position;
        Vector3 facing = body.transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 1e-6f)
            facing = Vector3.forward;
        else
            facing.Normalize();

        dto = new PlayerProgressSaveDto
        {
            worldX = pos.x,
            worldY = pos.y,
            worldZ = pos.z,
            facingX = facing.x,
            facingZ = facing.z
        };

        if (hub.BodyHost?.Body is CharacterBody characterBody)
            dto.body = characterBody.ToDto();

        if (body.TryGetBodyComponent(out CharacterClimateHost climate))
            dto.bodyTemp = ToBodyTempSave(climate.BodyTemperature.ToDto());

        if (ResolveSkills(body) is DefaultCharacterSkills skills)
            dto.skills = CharacterProgressSaveMapper.ToDto(skills);

        if (GameplayData.Vitals is DefaultPlayerVitals vitals)
            dto.vitals = CharacterProgressSaveMapper.ToDto(vitals);

        if (GameplayData.Proficiencies is DefaultCharacterProficiencies proficiencies)
            dto.proficiencies = CharacterProgressSaveMapper.ToDto(proficiencies);

        if (GameplayData.RecipeMemory is DefaultCharacterRecipeMemory recipeMemory)
            dto.recipeMemory = CharacterProgressSaveMapper.ToDto(recipeMemory);

        if (hub.TraitsHost?.Traits is DefaultCharacterTraits traits)
            dto.traits = CharacterProgressSaveMapper.ToDto(traits);
        else if (GameplayData.Traits is DefaultCharacterTraits globalTraits)
            dto.traits = CharacterProgressSaveMapper.ToDto(globalTraits);

        if (hub.Inventory != null)
        {
            body.TryGetBodyComponent(out PlayerGearHost gearHost);
            InventoryGearSaveDto inventory = InventoryProgressSaveMapper.Capture(hub.Inventory, gearHost?.Service);
            if (inventory != null)
                dto.inventoryJson = JsonUtility.ToJson(inventory);
        }

        return true;
    }

    static void ApplySnapshot(GameObject body, PlayerProgressSaveDto snapshot)
    {
        if (snapshot == null)
            return;

        SnapBodyTransform(body, snapshot);

        CharacterSessionHub hub = body.GetBodyComponent<CharacterSessionHub>();
        if (hub == null)
            return;

        if (snapshot.body != null && hub.BodyHost?.Body != null)
            hub.BodyHost.ApplyBodyDto(snapshot.body);

        if (snapshot.bodyTemp != null && body.TryGetBodyComponent(out CharacterClimateHost climate))
            climate.BodyTemperature.FromDto(FromBodyTempSave(snapshot.bodyTemp));

        DefaultCharacterSkills skills = ResolveSkills(body);
        if (skills != null)
            CharacterProgressSaveMapper.ApplyDto(skills, snapshot.skills);

        if (GameplayData.Vitals is DefaultPlayerVitals vitals)
            CharacterProgressSaveMapper.ApplyDto(vitals, snapshot.vitals);

        if (GameplayData.Proficiencies is DefaultCharacterProficiencies proficiencies)
            CharacterProgressSaveMapper.ApplyDto(proficiencies, snapshot.proficiencies);

        if (GameplayData.RecipeMemory is DefaultCharacterRecipeMemory recipeMemory)
            CharacterProgressSaveMapper.ApplyDto(recipeMemory, snapshot.recipeMemory);

        DefaultCharacterTraits traits = ResolveTraits(hub);
        if (traits != null)
            CharacterProgressSaveMapper.ApplyDto(traits, snapshot.traits);

        if (!string.IsNullOrEmpty(snapshot.inventoryJson)
            && hub.Inventory != null
            && body.TryGetBodyComponent(out PlayerGearHost gearHost))
        {
            InventoryGearSaveDto inventory = JsonUtility.FromJson<InventoryGearSaveDto>(snapshot.inventoryJson);
            gearHost.BindDomainIfNeeded();
            InventoryProgressSaveMapper.TryApply(inventory, hub.Inventory, gearHost.Service);
            gearHost.RefreshPrimaryWield();
        }

        PlayerStatusUIBridge.RebindFromGameplayData();
    }

    static DefaultCharacterSkills ResolveSkills(GameObject body)
    {
        if (GameplayData.Stats is DefaultPlayerStats stats)
            return stats.Skills;

        if (body != null
            && body.TryGetBodyComponent(out CharacterSkillsHost skillsHost)
            && skillsHost.Skills is DefaultCharacterSkills owned)
            return owned;

        return null;
    }

    static DefaultCharacterTraits ResolveTraits(CharacterSessionHub hub)
    {
        if (hub.TraitsHost?.Traits is DefaultCharacterTraits owned)
            return owned;

        if (GameplayData.Traits is DefaultCharacterTraits global)
            return global;

        return null;
    }

    static void SnapBodyTransform(GameObject body, PlayerProgressSaveDto snapshot)
    {
        Vector3 pos = new Vector3(snapshot.worldX, snapshot.worldY, snapshot.worldZ);
        if (body.TryGetComponent(out Rigidbody rb))
        {
            rb.position = pos;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        body.transform.position = pos;

        if (CharacterBodyResolve.TryGetInBody(body.transform, out CharacterState state))
            state.SnapWorldPosition(pos);

        Vector3 facing = new Vector3(snapshot.facingX, 0f, snapshot.facingZ);
        if (facing.sqrMagnitude > 1e-6f)
        {
            facing.Normalize();
            body.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        }
    }

    static BodyTempSaveDto ToBodyTempSave(BodyTempDto dto)
    {
        if (dto?.parts == null || dto.parts.Length == 0)
            return null;

        var parts = new BodyTempPartSaveDto[dto.parts.Length];
        for (int i = 0; i < dto.parts.Length; i++)
        {
            BodyTempPartDto src = dto.parts[i];
            parts[i] = new BodyTempPartSaveDto
            {
                partId = src.partId,
                tempC = src.tempC
            };
        }

        return new BodyTempSaveDto { parts = parts };
    }

    static BodyTempDto FromBodyTempSave(BodyTempSaveDto dto)
    {
        if (dto?.parts == null || dto.parts.Length == 0)
            return null;

        var parts = new BodyTempPartDto[dto.parts.Length];
        for (int i = 0; i < dto.parts.Length; i++)
        {
            BodyTempPartSaveDto src = dto.parts[i];
            parts[i] = new BodyTempPartDto
            {
                partId = src.partId,
                tempC = src.tempC
            };
        }

        return new BodyTempDto { parts = parts };
    }
}

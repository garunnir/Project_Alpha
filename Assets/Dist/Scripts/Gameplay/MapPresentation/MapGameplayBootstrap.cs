// ============================================================
// MapGameplayBootstrap — 맵 로드 후 플레이어·컨테이너에 Map 서비스 바인딩
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

[DefaultExecutionOrder(-49)]
[DisallowMultipleComponent]
public sealed class MapGameplayBootstrap : MonoBehaviour
{
    [SerializeField] TileMapManager _tileMapManager;
    [SerializeField] CharacterFactionCatalog _factionCatalog;
    [SerializeField] VaultClipCatalog _vaultClipCatalog;
    [SerializeField] FishingLootCatalog _fishingLootCatalog;
    [SerializeField] FishWorkClipCatalog _fishWorkClipCatalog;
    [SerializeField] FarmWorkClipCatalog _farmWorkClipCatalog;

    void Start()
    {
        if (_tileMapManager == null)
            _tileMapManager = GetComponent<TileMapManager>();

        if (_tileMapManager == null)
            return;

        if (_factionCatalog == null)
        {
            Debug.LogError(
                "[MapGameplayBootstrap] CharacterFactionCatalog is not assigned.",
                this);
        }

        CharacterHostility.BindCatalog(_factionCatalog);
        MapFishRuntimeBridge.BindCatalogs(_fishingLootCatalog, _fishWorkClipCatalog);
        FarmWorkClipCatalog.BindRuntime(_farmWorkClipCatalog);

        IWorldGrid worldGrid = _tileMapManager.WorldGrid;
        if (worldGrid != null)
            BindWorldGridToCharacters(worldGrid);

        BindMapCollisionServices(_tileMapManager);
        BindWorldGridToContainers(worldGrid);
        BindWorldGridToSmallItems(worldGrid);
        BindMapDigService();
    }

    static void BindMapDigService()
    {
        MapDigService.Configure(new MapDigRuntimeHooks
        {
            IsMoodBlocked = () => MoodGameplayGate.IsBlocked,
            HasDigQuality = MapPlantService.HasDigQuality,
            PlayerHasDigTool = PlayerHasDigTool,
            DigBlockedLabel = () => HarvestContextLabels.TillBlocked,
            GrantItem = GrantDigItem,
            TryResolveActorWorld = TryResolveDigActorWorld,
        });
    }

    static bool TryResolveDigActorWorld(out Vector3 actorWorld)
    {
        actorWorld = default;
        PlayerGearHost gear = PlayerGearHost.Active;
        if (gear == null || !CharacterBodyResolve.TryGetInBody(gear, out CharacterState state))
            return false;

        actorWorld = CharacterFeetPose.GetFeetWorld(state.transform);
        return true;
    }

    static bool PlayerHasDigTool()
    {
        PlayerGearHost gearHost = PlayerGearHost.Active;
        if (gearHost == null)
            return false;

        CharacterAttacker attacker = CharacterBodyResolve.GetInBody<CharacterAttacker>(gearHost);
        ItemStack stack = attacker != null ? attacker.WieldedStack : null;
        return stack?.Item != null &&
               stack.Count > 0 &&
               MapPlantService.HasDigQuality(stack.Item);
    }

    static void GrantDigItem(string itemId, int count, Vector3 world)
    {
        ItemData item = GameplayData.GetItem(itemId);
        if (item == null || count < 1)
            return;

        var stack = new ItemStack(item, count);
        CharacterGearService gear = PlayerGearHost.Active?.Service;
        if (gear != null && gear.CanDepositToBody(stack))
        {
            gear.DepositToBody(stack);
            return;
        }

        InventoryContainer body = PlayerInventoryRuntime.Active?.Host?.Container;
        if (body != null && body.CapacityPolicy != null && body.CapacityPolicy.CanAccept(body, stack))
        {
            body.AddItem(item, count);
            PlayerInventoryRuntime.Active.Session?.NotifyExternalStacksChanged(body);
            return;
        }

        SmallItemObject prefab = FindSmallItemPrefabForDig();
        if (prefab == null)
        {
            Debug.LogWarning("[MapGameplayBootstrap] SmallItem prefab missing; dig drop skipped for " + itemId);
            return;
        }

        IWorldGrid grid = null;
        TileMapManager map = Object.FindFirstObjectByType<TileMapManager>();
        if (map != null)
            grid = map.WorldGrid;

        SmallItemSpawner.Spawn(prefab, item, count, world, grid);
    }

    static SmallItemObject FindSmallItemPrefabForDig()
    {
        SmallItemObject[] all = Resources.FindObjectsOfTypeAll<SmallItemObject>();
        for (int i = 0; i < all.Length; i++)
        {
            SmallItemObject obj = all[i];
            if (obj == null || obj.gameObject.scene.IsValid())
                continue;
            return obj;
        }

        return Object.FindFirstObjectByType<SmallItemObject>(FindObjectsInactive.Include);
    }

    public void BindSpawnedCharacter(GameObject instance)
    {
        if (instance == null)
            return;

        CharacterBodyRefs.EnsureResolved(instance);

        if (_tileMapManager == null)
            _tileMapManager = GetComponent<TileMapManager>();
        if (_tileMapManager == null)
            return;

        IWorldGrid worldGrid = _tileMapManager.WorldGrid;
        CharacterState state = instance.GetBodyComponent<CharacterState>();
        if (state != null && worldGrid != null)
            state.BindWorldGrid(worldGrid);

        if (_tileMapManager.Model is not TileMapModel)
            return;

        MapCollisionServices services = _tileMapManager.MapCollisionServices;
        if (services == null)
            return;

        CharacterMotor motor = instance.GetBodyComponent<CharacterMotor>();
        motor?.BindMapCollision(services);

        EnsureSwimHosts(instance);
        EnsureVaultHost(instance, services, _vaultClipCatalog);
        BindCellWorkClips(instance, _farmWorkClipCatalog, _fishWorkClipCatalog);

        CharacterAttacker attacker = instance.GetBodyComponent<CharacterAttacker>();
        attacker?.BindMapCollision(services.LineCast);

        CharacterHearing hearing = instance.GetBodyComponent<CharacterHearing>();
        hearing?.BindMapCollision(services.LineCast);

        DirectionalRaycaster raycaster = instance.GetComponent<DirectionalRaycaster>();
        if (raycaster != null && state != null)
            raycaster.BindMapCollision(services.LineCast, state);
    }

    static void BindWorldGridToCharacters(IWorldGrid worldGrid)
    {
        var states = FindObjectsByType<CharacterState>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < states.Length; i++)
            states[i].BindWorldGrid(worldGrid);
    }

    void BindMapCollisionServices(TileMapManager manager)
    {
        if (manager.Model is not TileMapModel tileModel)
            return;

        MapCollisionServices services = manager.MapCollisionServices;
        if (services == null)
            return;

        BindCharacterLocomotions<CharacterMotor>(services);
        BindCharacterHearing(services.LineCast);
        EnsureSwimHostsOnSceneCharacters();
        EnsureVaultHostsOnSceneCharacters(services, _vaultClipCatalog);
        BindCellWorkClipsOnSceneCharacters(_farmWorkClipCatalog, _fishWorkClipCatalog);
        BindWorkAnimOnSceneCharacters();

        var attackers = FindObjectsByType<CharacterAttacker>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < attackers.Length; i++)
            attackers[i].BindMapCollision(services.LineCast);

        var aimControllers = FindObjectsByType<PlayerAimController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < aimControllers.Length; i++)
            aimControllers[i].BindMapCollision(services.LineCast);

        var raycasters = FindObjectsByType<DirectionalRaycaster>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < raycasters.Length; i++)
        {
            var state = raycasters[i].GetBodyComponent<CharacterState>();
            if (state == null)
                continue;

            raycasters[i].BindMapCollision(services.LineCast, state);
        }
    }

    static void BindCharacterLocomotions<T>(MapCollisionServices services)
        where T : MonoBehaviour, ICharacterLocomotion
    {
        var locomotions = FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < locomotions.Length; i++)
            locomotions[i].BindMapCollision(services);
    }

    static void BindCharacterHearing(MapTopologyLineCast lineCast)
    {
        if (lineCast == null)
            return;

        var hearings = FindObjectsByType<CharacterHearing>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < hearings.Length; i++)
            hearings[i].BindMapCollision(lineCast);
    }

    static void BindWorldGridToContainers(IWorldGrid worldGrid)
    {
        if (worldGrid == null)
            return;

        var interactables = FindObjectsByType<ContainerInteractable>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < interactables.Length; i++)
            interactables[i].BindWorldGrid(worldGrid);
    }

    static void BindWorldGridToSmallItems(IWorldGrid worldGrid)
    {
        if (worldGrid == null)
            return;

        var items = FindObjectsByType<SmallItemObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < items.Length; i++)
            items[i].BindWorldGrid(worldGrid);
    }

    static void EnsureSwimHostsOnSceneCharacters()
    {
        var states = FindObjectsByType<CharacterState>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < states.Length; i++)
            EnsureSwimHosts(states[i].gameObject);
    }

    static void EnsureSwimHosts(GameObject instance)
    {
        if (instance == null || instance.GetBodyComponent<CharacterState>() == null)
            return;

        if (instance.GetBodyComponent<CharacterBodyHost>() == null)
            return;

        if (instance.GetBodyComponent<CharacterSwimHost>() != null)
            return;

        Debug.LogError(
            $"[MapGameplayBootstrap] '{instance.name}' needs CharacterSwimHost on the prefab (root with CharacterState).",
            instance);
    }

    static void EnsureVaultHostsOnSceneCharacters(
        MapCollisionServices services,
        VaultClipCatalog vaultClips)
    {
        var states = FindObjectsByType<CharacterState>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < states.Length; i++)
            EnsureVaultHost(states[i].gameObject, services, vaultClips);
    }

    static void BindWorkAnimOnSceneCharacters()
    {
        var roots = FindObjectsByType<CharacterBodyRoot>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < roots.Length; i++)
            CharacterWorkAnimBinder.BindBody(roots[i].gameObject);
    }

    static void EnsureVaultHost(
        GameObject instance,
        MapCollisionServices services,
        VaultClipCatalog vaultClips)
    {
        if (instance == null || instance.GetBodyComponent<CharacterMotor>() == null)
            return;

        CharacterVaultHost vault = instance.GetBodyComponent<CharacterVaultHost>();
        if (vault == null)
        {
            Debug.LogError(
                $"[MapGameplayBootstrap] '{instance.name}' needs CharacterVaultHost on prefab.",
                instance);
            return;
        }

        vault.BindMapCollision(services);
        if (vaultClips != null)
            vault.SetClipCatalog(vaultClips);

        Animator animator = instance.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.GetComponent<CharacterVaultIkHost>() == null)
        {
            Debug.LogError(
                $"[MapGameplayBootstrap] '{instance.name}' needs CharacterVaultIkHost on the Animator GO (prefab).",
                animator);
        }
    }

    static void BindCellWorkClipsOnSceneCharacters(
        FarmWorkClipCatalog farmClips,
        FishWorkClipCatalog fishClips)
    {
        var hosts = FindObjectsByType<CharacterActionHost>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < hosts.Length; i++)
            ApplyCellWorkClips(hosts[i], farmClips, fishClips);
    }

    static void BindCellWorkClips(
        GameObject instance,
        FarmWorkClipCatalog farmClips,
        FishWorkClipCatalog fishClips)
    {
        if (instance == null)
            return;

        CharacterActionHost host = instance.GetBodyComponent<CharacterActionHost>();
        ApplyCellWorkClips(host, farmClips, fishClips);
    }

    static void ApplyCellWorkClips(
        CharacterActionHost host,
        FarmWorkClipCatalog farmClips,
        FishWorkClipCatalog fishClips)
    {
        if (host == null)
            return;

        if (farmClips != null)
            host.SetFarmWorkClips(farmClips);
        if (fishClips != null)
            host.SetFishWorkClips(fishClips);
    }
}

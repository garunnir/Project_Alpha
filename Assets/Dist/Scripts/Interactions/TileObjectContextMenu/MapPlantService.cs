// ============================================================
// MapPlantService — 심기·수확·시듦·경작 오케스트레이션 (OccupiedCell plant)
// ============================================================

using System;
using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

public static class MapPlantService
{
    const string NullItemId = "null";

    static readonly List<Vector3Int> RemoveScratch = new();
    static MapPlantHost _boundHost;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void DomainReset()
    {
        MapPlantHost.RuntimeAssigned -= OnHostAssigned;
        MapPlantHost.AfterLoaded -= CatchUpAll;
        _boundHost = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        RegisterClock();
        MapPlantHost.RuntimeAssigned -= OnHostAssigned;
        MapPlantHost.AfterLoaded -= CatchUpAll;
        MapPlantHost.RuntimeAssigned += OnHostAssigned;
        MapPlantHost.AfterLoaded += CatchUpAll;
    }

    static void RegisterClock()
    {
        MapClockSnapshot.GetDayIndex = () =>
        {
            WorldClock clock = WorldClock.Instance;
            return clock != null ? clock.DayIndex : 0;
        };
        MapClockSnapshot.GetMinuteOfDay = () =>
        {
            WorldClock clock = WorldClock.Instance;
            return clock != null ? clock.MinuteOfDay : 0;
        };
        MapClockSnapshot.SetTime = (day, minute) =>
        {
            WorldClock.Instance?.SetTime(day, minute);
        };
        MapClockSnapshot.TrySubscribeMinuteChanged = handler =>
        {
            WorldClock clock = WorldClock.Instance;
            if (clock == null || handler == null)
                return false;

            clock.MinuteChanged += handler;
            return true;
        };
        MapClockSnapshot.UnsubscribeMinuteChanged = handler =>
        {
            WorldClock clock = WorldClock.Instance;
            if (clock != null && handler != null)
                clock.MinuteChanged -= handler;
        };
    }

    static void OnHostAssigned(MapPlantHost host) => _boundHost = host;

    public static bool CanPlant(ItemStack stack, InventoryContainer container) =>
        GetPlantSessionBlockedReason(stack, container) == null;

    public static string GetPlantSessionBlockedReason(ItemStack stack, InventoryContainer container)
    {
        if (MoodGameplayGate.IsBlocked)
            return HarvestContextLabels.HarvestBlocked;
        if (stack?.Item?.seed == null || stack.Count < 1)
            return HarvestContextLabels.HarvestBlocked;
        if (!PlayerItemAccess.OwnsInBodyOrWield(stack, container))
            return HarvestContextLabels.HarvestBlocked;
        if (MapPlantHost.Runtime == null)
            return HarvestContextLabels.HarvestBlocked;

        return null;
    }

    public static bool CanPlantAt(Vector3Int cell, ItemStack stack, InventoryContainer container) =>
        GetPlantBlockedReasonAt(cell, stack, container) == null;

    public static string GetPlantBlockedReasonAt(
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container)
    {
        string session = GetPlantSessionBlockedReason(stack, container);
        if (session != null)
            return session;

        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null || !host.IsPlantable(cell) || host.HasPlant(cell))
            return HarvestContextLabels.HarvestBlocked;

        return null;
    }

    public static bool TryPlant(ItemStack stack, InventoryContainer container) =>
        TryPlantAt(default, stack, container, usePlayerCellFallback: true);

    public static bool TryPlantAt(
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container,
        bool usePlayerCellFallback = false)
    {
        if (usePlayerCellFallback)
        {
            if (GetPlantSessionBlockedReason(stack, container) != null)
                return false;
            if (!TryResolvePlayerCell(out cell))
                return false;
        }
        else if (GetPlantBlockedReasonAt(cell, stack, container) != null)
        {
            return false;
        }

        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null)
            return false;

        string seedId = stack.Item.id;
        int planted = ItemRot.CurrentWorldMinute();

        if (PlayerItemAccess.TryTakeOne(stack, container) <= 0)
            return false;

        if (host.TryAddPlant(cell, seedId, planted))
            return true;

        RefundSeed(seedId, container);
        return false;
    }

    public static string GetHarvestBlockedReason(Vector3Int cell)
    {
        CatchUpCell(cell);

        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null || !host.TryGetPlant(cell, out PlantCell plant))
            return HarvestContextLabels.HarvestBlocked;

        if (IsTreeGrowthDormant(plant))
            return HarvestContextLabels.HarvestNotReady;

        PlantGrowthStage stage = ResolvePlantStage(plant);
        if (PlantGrowth.IsHarvestable(stage))
            return null;

        return HarvestContextLabels.HarvestNotReady;
    }

    public static bool HasAxeQuality(ItemData item) =>
        ItemQualityUtil.HasQuality(item, MapPlantConsts.AxeQualityId, MapPlantConsts.MinAxeQualityLevel);

    public static string GetChopSessionBlockedReason()
    {
        if (MoodGameplayGate.IsBlocked)
            return HarvestContextLabels.ChopBlocked;
        if (!PlayerHasAxeTool())
            return HarvestContextLabels.ChopBlocked;
        if (MapPlantHost.Runtime == null)
            return HarvestContextLabels.ChopBlocked;

        return null;
    }

    public static string GetChopBlockedReason(Vector3Int cell)
    {
        string session = GetChopSessionBlockedReason();
        if (session != null)
            return session;

        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null || !host.TryGetPlant(cell, out PlantCell plant))
            return HarvestContextLabels.ChopBlocked;
        if (!IsTreePlant(plant))
            return HarvestContextLabels.ChopBlocked;

        return null;
    }

    public static bool HasDigQuality(ItemData item)
    {
        if (item?.qualities == null)
            return false;

        for (int i = 0; i < item.qualities.Count; i++)
        {
            QualityEntry quality = item.qualities[i];
            if (quality == null || string.IsNullOrEmpty(quality.id))
                continue;
            if (!quality.id.Equals(MapPlantConsts.DigQualityId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (quality.level >= MapPlantConsts.MinDigQualityLevel)
                return true;
        }

        return false;
    }

    public static bool IsFertilizerItem(ItemData item)
    {
        if (item?.flags == null)
            return false;

        for (int i = 0; i < item.flags.Count; i++)
        {
            string flag = item.flags[i];
            if (!string.IsNullOrEmpty(flag) &&
                flag.Equals(MapPlantConsts.FertilizerFlag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool CanTill(ItemStack stack, InventoryContainer container) =>
        GetTillSessionBlockedReason(stack, container) == null;

    public static string GetTillSessionBlockedReason(ItemStack stack, InventoryContainer container)
    {
        if (MoodGameplayGate.IsBlocked)
            return HarvestContextLabels.TillBlocked;
        if (!HasDigQuality(stack?.Item) || !PlayerItemAccess.OwnsInBodyOrWield(stack, container))
            return HarvestContextLabels.TillBlocked;
        if (MapPlantHost.Runtime == null)
            return HarvestContextLabels.TillBlocked;

        return null;
    }

    public static string GetTillBlockedReason(Vector3Int cell)
    {
        if (MoodGameplayGate.IsBlocked)
            return HarvestContextLabels.TillBlocked;

        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null || !host.IsTillable(cell))
            return HarvestContextLabels.TillBlocked;
        if (!PlayerHasDigTool())
            return HarvestContextLabels.TillBlocked;

        return null;
    }

    public static bool TryTill(ItemStack stack, InventoryContainer container) =>
        TryTillAt(default, stack, container, usePlayerCellFallback: true);

    public static bool TryTillAt(
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container,
        bool usePlayerCellFallback = false)
    {
        if (usePlayerCellFallback)
        {
            if (GetTillSessionBlockedReason(stack, container) != null)
                return false;
            if (!TryResolvePlayerCell(out cell))
                return false;
        }
        else if (GetTillBlockedReason(cell) != null)
        {
            return false;
        }

        MapPlantHost host = MapPlantHost.Runtime;
        return host != null && host.TryTill(cell);
    }

    public static bool TryTill(Vector3Int cell)
    {
        if (GetTillBlockedReason(cell) != null)
            return false;

        MapPlantHost host = MapPlantHost.Runtime;
        return host != null && host.TryTill(cell);
    }

    public static bool CanFertilize(ItemStack stack, InventoryContainer container) =>
        GetFertilizeSessionBlockedReason(stack, container) == null;

    public static string GetFertilizeSessionBlockedReason(ItemStack stack, InventoryContainer container)
    {
        if (MoodGameplayGate.IsBlocked)
            return HarvestContextLabels.FertilizeBlocked;
        if (stack == null ||
            !IsFertilizerItem(stack.Item) ||
            stack.Count < 1 ||
            !PlayerItemAccess.OwnsInBodyOrWield(stack, container))
            return HarvestContextLabels.FertilizeBlocked;
        if (MapPlantHost.Runtime == null)
            return HarvestContextLabels.FertilizeBlocked;

        return null;
    }

    public static string GetFertilizeBlockedReason(Vector3Int cell)
    {
        if (MoodGameplayGate.IsBlocked)
            return HarvestContextLabels.FertilizeBlocked;
        if (FindFertilizer(out _, out _) == null)
            return HarvestContextLabels.FertilizeBlocked;

        return GetFertilizePlantBlockedReason(cell);
    }

    public static bool TryFertilize(ItemStack stack, InventoryContainer container) =>
        TryFertilizeAt(default, stack, container, usePlayerCellFallback: true);

    public static bool TryFertilizeAt(
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container,
        bool usePlayerCellFallback = false)
    {
        if (usePlayerCellFallback)
        {
            if (GetFertilizeSessionBlockedReason(stack, container) != null)
                return false;
            if (!TryResolvePlayerCell(out cell))
                return false;
        }
        else if (GetFertilizeBlockedReason(cell) != null)
        {
            return false;
        }

        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null)
            return false;
        if (PlayerItemAccess.TryTakeOne(stack, container) <= 0)
            return false;
        if (host.TryFertilize(cell))
            return true;

        RefundSeed(stack.Item.id, container);
        return false;
    }

    public static bool TryFertilize(Vector3Int cell)
    {
        if (GetFertilizeBlockedReason(cell) != null)
            return false;

        ItemStack stack = FindFertilizer(out InventoryContainer container, out _);
        if (stack == null)
            return false;

        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null)
            return false;
        if (PlayerItemAccess.TryTakeOne(stack, container) <= 0)
            return false;
        if (host.TryFertilize(cell))
            return true;

        RefundSeed(stack.Item.id, container);
        return false;
    }

    public static bool CanApplyAtCell(
        FarmCellActionKind kind,
        Vector3Int cell,
        ItemStack stack = null,
        InventoryContainer container = null)
    {
        switch (kind)
        {
            case FarmCellActionKind.Plant:
                return CanPlantAt(cell, stack, container);
            case FarmCellActionKind.Till:
                return GetTillBlockedReason(cell) == null;
            case FarmCellActionKind.Fertilize:
                return GetFertilizeBlockedReason(cell) == null;
            case FarmCellActionKind.Harvest:
                return GetHarvestBlockedReason(cell) == null;
            case FarmCellActionKind.Chop:
                return GetChopBlockedReason(cell) == null;
            default:
                return false;
        }
    }

    public static Vector3 CellArriveWorld(Vector3Int cell)
    {
        MapPlantHost host = MapPlantHost.Runtime;
        float cellSize = host != null ? host.CellSize : 1f;
        return TileHelper.ConvertGridToWorldPos(cell, cellSize);
    }

    /// <summary>비-Plant 농사 Arrive 월드 stoppingDistance (인접 칸 접근 허용).</summary>
    public static float CellArriveStoppingDistance()
    {
        MapPlantHost host = MapPlantHost.Runtime;
        float cellSize = host != null ? host.CellSize : 1f;
        return cellSize * MapPlantConsts.CellArriveStoppingCellFraction;
    }

    /// <summary>
    /// 심기 수행 범위 — XZ Chebyshev ≤ <see cref="MapPlantConsts.PlantActionRangeCells"/>, 동일 Y.
    /// </summary>
    public static bool IsWithinPlantActionRange(Vector3Int playerCell, Vector3Int targetCell)
    {
        if (playerCell.y != targetCell.y)
            return false;

        int dx = Mathf.Abs(playerCell.x - targetCell.x);
        int dz = Mathf.Abs(playerCell.z - targetCell.z);
        return Mathf.Max(dx, dz) <= MapPlantConsts.PlantActionRangeCells;
    }

    public static bool TryResolveActorCell(out Vector3Int cell) =>
        TryResolvePlayerCell(out cell);

    public static bool TryHarvest(Vector3Int cell)
    {
        CatchUpCell(cell);

        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null || !host.TryGetPlant(cell, out PlantCell plant))
            return false;

        PlantGrowthStage stage = ResolvePlantStage(plant);
        if (!PlantGrowth.IsHarvestable(stage))
            return false;

        ItemData seedItem = GameplayData.GetItem(plant.SeedItemId);
        SeedDetailData seed = seedItem?.seed;
        Vector3 world = CellWorld(host, cell);

        if (seed != null && IsGrantedItemId(seed.fruit))
            GrantItem(seed.fruit, 1, world);
        if (seed != null && seed.seeds && IsGrantedItemId(plant.SeedItemId))
            GrantItem(plant.SeedItemId, 1, world);

        if (seed != null && seed.IsTree)
        {
            int now = ItemRot.CurrentWorldMinute();
            if (!host.TryRecordFruitHarvest(cell, now))
                return false;

            host.TrySetPlantStage(cell, PlantTileIds.PrefabIdForStage(PlantGrowthStage.Mature));
            return true;
        }

        return host.TryRemovePlant(cell);
    }

    public static bool TryChop(Vector3Int cell)
    {
        CatchUpCell(cell);

        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null || !host.TryGetPlant(cell, out PlantCell plant))
            return false;
        if (!IsTreePlant(plant))
            return false;

        ItemData seedItem = GameplayData.GetItem(plant.SeedItemId);
        SeedDetailData seed = seedItem?.seed;
        if (seed == null)
            return false;

        PlantGrowthStage stage = ResolvePlantStage(plant);
        Vector3 world = CellWorld(host, cell);
        if (seed.TryGetChopYield(stage, out string itemId, out int count) && IsGrantedItemId(itemId))
            GrantItem(itemId, count, world);

        return host.TryRemovePlant(cell);
    }

    public static PlantGrowthStage ResolvePlantStage(PlantCell plant)
    {
        ItemData item = GameplayData.GetItem(plant.SeedItemId);
        SeedDetailData seed = item?.seed;
        int current = ItemRot.CurrentWorldMinute();
        PlantGrowthContext context = BuildGrowthContext(plant, seed, current, out CalendarSnapshot calendar);

        if (seed != null && seed.IsTree)
        {
            int growthElapsed = WorldCalendar.ElapsedMinutesExcludingSeason(
                plant.PlantedWorldMinute,
                current,
                WorldSeason.Winter,
                calendar.MinutesPerDay,
                calendar.DaysPerYear,
                calendar.DaysPerSeason);
            int regrowElapsed = plant.LastFruitHarvestWorldMinute > PlantGrowth.NoFruitHarvestMinute
                ? WorldCalendar.ElapsedMinutesExcludingSeason(
                    plant.LastFruitHarvestWorldMinute,
                    current,
                    WorldSeason.Winter,
                    calendar.MinutesPerDay,
                    calendar.DaysPerYear,
                    calendar.DaysPerSeason)
                : 0;
            return PlantGrowth.ResolveTree(
                seed,
                growthElapsed,
                plant.LastFruitHarvestWorldMinute,
                regrowElapsed,
                in context);
        }

        int elapsed = PlantGrowth.ElapsedMinutes(plant.PlantedWorldMinute, current);
        return PlantGrowth.ResolveField(seed, elapsed, in context);
    }

    public static void CatchUpAll()
    {
        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null)
            return;

        IReadOnlyList<PlantCell> plants = host.Overlay.Plants;
        RemoveScratch.Clear();
        for (int i = 0; i < plants.Count; i++)
            RemoveScratch.Add(plants[i].Cell);

        for (int i = 0; i < RemoveScratch.Count; i++)
            CatchUpCell(RemoveScratch[i]);
    }

    public static void CatchUpCell(Vector3Int cell)
    {
        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null || !host.TryGetPlant(cell, out PlantCell plant))
            return;

        PlantGrowthStage stage = ResolvePlantStage(plant);
        string desiredPrefab = PlantTileIds.PrefabIdForStage(stage);
        host.TrySetPlantStage(cell, desiredPrefab);

        if (!PlantGrowth.IsWithered(stage))
            return;

        SeedDetailData seed = GameplayData.GetItem(plant.SeedItemId)?.seed;
        if (seed != null && seed.IsTree)
            return;

        if (!host.TryGetPlant(cell, out plant))
            return;
        Wither(host, plant);
    }

    static void Wither(MapPlantHost host, PlantCell plant)
    {
        ItemData seedItem = GameplayData.GetItem(plant.SeedItemId);
        SeedDetailData seed = seedItem?.seed;
        Vector3 world = CellWorld(host, plant.Cell);

        if (seed?.byproducts != null)
        {
            for (int i = 0; i < seed.byproducts.Count; i++)
            {
                string id = seed.byproducts[i];
                if (IsGrantedItemId(id))
                    GrantItem(id, 1, world);
            }
        }

        host.TryRemovePlant(plant.Cell);
    }

    static PlantGrowthContext BuildGrowthContext(
        PlantCell plant,
        SeedDetailData seed,
        int currentWorldMinute,
        out CalendarSnapshot calendar)
    {
        calendar = ResolveCalendar(currentWorldMinute);
        MapPlantHost host = MapPlantHost.Runtime;
        WeatherKind kind = WeatherKind.Clear;
        WorldWeatherHost weather = WorldWeatherHost.Instance;
        if (weather != null)
            weather.TryGetKindAt(plant.Cell.x, plant.Cell.z, out kind);
        else if (PlayerGearHost.Active != null)
            kind = PlayerGearHost.Active.WorldWeatherKind;

        int plantedDay = calendar.MinutesPerDay > 0
            ? plant.PlantedWorldMinute / calendar.MinutesPerDay
            : 0;
        bool winterSpan = WorldCalendar.SpanIncludesSeason(
            plantedDay,
            calendar.CurrentDay,
            WorldSeason.Winter,
            calendar.DaysPerYear,
            calendar.DaysPerSeason);
        bool outdoor = host == null || host.IsOutdoorCell(plant.Cell);
        bool greenhouse = host != null && host.IsGreenhouseCell(plant.Cell);
        bool isTree = seed != null && seed.IsTree;
        bool frostKills = !isTree && winterSpan && outdoor && !greenhouse;
        bool currentlyWinter = WorldCalendar.Season(
                calendar.CurrentDay,
                calendar.DaysPerYear,
                calendar.DaysPerSeason) == WorldSeason.Winter;
        bool growthDormant = isTree && currentlyWinter && outdoor && !greenhouse;

        return new PlantGrowthContext(plant.Fertilized, WeatherGrowFactor(kind), frostKills, growthDormant);
    }

    static bool IsTreePlant(PlantCell plant) =>
        GameplayData.GetItem(plant.SeedItemId)?.seed?.IsTree ?? false;

    static bool IsTreeGrowthDormant(PlantCell plant)
    {
        SeedDetailData seed = GameplayData.GetItem(plant.SeedItemId)?.seed;
        if (seed == null || !seed.IsTree)
            return false;

        PlantGrowthContext context = BuildGrowthContext(
            plant,
            seed,
            ItemRot.CurrentWorldMinute(),
            out _);
        return context.GrowthDormant;
    }

    readonly struct CalendarSnapshot
    {
        public readonly int CurrentDay;
        public readonly int DaysPerYear;
        public readonly int DaysPerSeason;
        public readonly int MinutesPerDay;

        public CalendarSnapshot(int currentDay, int daysPerYear, int daysPerSeason, int minutesPerDay)
        {
            CurrentDay = currentDay;
            DaysPerYear = daysPerYear;
            DaysPerSeason = daysPerSeason;
            MinutesPerDay = minutesPerDay;
        }
    }

    static CalendarSnapshot ResolveCalendar(int currentWorldMinute)
    {
        int daysPerYear = WorldClockSettings.DefaultDaysPerYear;
        int daysPerSeason = WorldClockSettings.DefaultDaysPerSeason;
        int minutesPerDay = WorldClockSettings.DefaultMinutesPerDay;
        int currentDay = minutesPerDay > 0 ? currentWorldMinute / minutesPerDay : 0;
        WorldClock clock = WorldClock.Instance;
        if (clock != null)
        {
            currentDay = clock.DayIndex;
            if (clock.Settings != null)
            {
                daysPerYear = clock.Settings.DaysPerYear;
                daysPerSeason = clock.Settings.DaysPerSeason;
                minutesPerDay = clock.Settings.MinutesPerDay;
            }
        }

        return new CalendarSnapshot(currentDay, daysPerYear, daysPerSeason, minutesPerDay);
    }

    static PlantGrowthContext BuildGrowthContext(PlantCell plant)
    {
        SeedDetailData seed = GameplayData.GetItem(plant.SeedItemId)?.seed;
        return BuildGrowthContext(plant, seed, ItemRot.CurrentWorldMinute(), out _);
    }

    static float WeatherGrowFactor(WeatherKind kind)
    {
        switch (kind)
        {
            case WeatherKind.Rain:
                return PlantGrowth.WeatherRainGrowFactor;
            case WeatherKind.Wind:
                return PlantGrowth.WeatherWindGrowFactor;
            case WeatherKind.Snow:
                return PlantGrowth.WeatherSnowGrowFactor;
            default:
                return PlantGrowth.WeatherClearGrowFactor;
        }
    }

    static string GetFertilizePlantBlockedReason(Vector3Int cell)
    {
        CatchUpCell(cell);

        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null || !host.TryGetPlant(cell, out PlantCell plant))
            return HarvestContextLabels.FertilizeBlocked;
        if (plant.Fertilized)
            return HarvestContextLabels.FertilizeBlocked;
        if (PlantGrowth.IsWithered(ResolvePlantStage(plant)))
            return HarvestContextLabels.FertilizeBlocked;

        return null;
    }

    static bool TryResolvePlayerCell(out Vector3Int cell)
    {
        cell = default;
        PlayerGearHost gear = PlayerGearHost.Active;
        if (gear != null && CharacterBodyResolve.TryGetInBody(gear, out CharacterState state))
        {
            cell = state.GridPos;
            return true;
        }

        MapPlantHost host = MapPlantHost.Runtime;
        if (host == null || !TryResolvePlayerWorld(out Vector3 world))
            return false;

        cell = host.ResolveCellFromWorld(world);
        return true;
    }

    static bool PlayerHasAxeTool()
    {
        if (FindInBodyAndWield(HasAxeQuality, out _, out _) != null)
            return true;
        return false;
    }

    static bool PlayerHasDigTool()
    {
        if (FindInBodyAndWield(HasDigQuality, out _, out _) != null)
            return true;
        return false;
    }

    static ItemStack FindFertilizer(out InventoryContainer container, out bool fromWield)
    {
        return FindInBodyAndWield(IsFertilizerItem, out container, out fromWield);
    }

    static ItemStack FindInBodyAndWield(
        Func<ItemData, bool> match,
        out InventoryContainer container,
        out bool fromWield)
    {
        container = null;
        fromWield = false;
        if (match == null)
            return null;

        InventoryContainer body = PlayerInventoryRuntime.Active?.Host?.Container;
        if (body?.Stacks != null)
        {
            IReadOnlyList<ItemStack> stacks = body.Stacks;
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack?.Item == null || stack.Count < 1 || !match(stack.Item))
                    continue;
                container = body;
                return stack;
            }
        }

        WieldSlots wield = PlayerGearHost.Active?.Service?.Wield;
        if (wield == null)
            return null;

        if (wield.Left?.Item != null && wield.Left.Count >= 1 && match(wield.Left.Item))
        {
            fromWield = true;
            return wield.Left;
        }

        if (wield.Right?.Item != null &&
            wield.Right != wield.Left &&
            wield.Right.Count >= 1 &&
            match(wield.Right.Item))
        {
            fromWield = true;
            return wield.Right;
        }

        return null;
    }

    static Vector3 CellWorld(MapPlantHost host, Vector3Int cell)
    {
        Vector3 pos = TileHelper.ConvertGridToWorldPos(cell, host.CellSize);
        pos.y += MapPlantConsts.OverlayYOffset;
        return pos;
    }

    static bool TryResolvePlayerWorld(out Vector3 world)
    {
        world = default;
        PlayerGearHost gear = PlayerGearHost.Active;
        if (gear != null && CharacterBodyResolve.TryGetInBody(gear, out CharacterState state))
        {
            world = state.BodyWorldPoint.sqrMagnitude > 1e-6f
                ? state.BodyWorldPoint
                : gear.transform.position;
            return true;
        }

        PlayerInventoryRuntime runtime = PlayerInventoryRuntime.Active;
        if (runtime?.Host == null)
            return false;

        world = runtime.Host.WorldPosition;
        return true;
    }

    static void RefundSeed(string seedId, InventoryContainer container)
    {
        ItemData item = GameplayData.GetItem(seedId);
        if (item == null)
            return;

        if (container != null)
        {
            container.AddItem(item, 1);
            PlayerInventoryRuntime.Active?.Session?.NotifyExternalStacksChanged(container);
            return;
        }

        GrantItem(seedId, 1, Vector3.zero);
    }

    static void GrantItem(string itemId, int count, Vector3 world)
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

        SpawnFloor(item, count, world);
    }

    static void SpawnFloor(ItemData item, int count, Vector3 world)
    {
        SmallItemObject prefab = FindSmallItemPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[MapPlantService] SmallItem prefab missing; floor grant skipped for " + item.id);
            return;
        }

        IWorldGrid grid = null;
        TileMapManager map = UnityEngine.Object.FindFirstObjectByType<TileMapManager>();
        if (map != null)
            grid = map.WorldGrid;

        SmallItemSpawner.Spawn(prefab, item, count, world, grid);
    }

    static SmallItemObject FindSmallItemPrefab()
    {
        SmallItemObject[] all = Resources.FindObjectsOfTypeAll<SmallItemObject>();
        for (int i = 0; i < all.Length; i++)
        {
            SmallItemObject obj = all[i];
            if (obj == null)
                continue;
            if (obj.gameObject.scene.IsValid())
                continue;
            return obj;
        }

        return UnityEngine.Object.FindFirstObjectByType<SmallItemObject>(FindObjectsInactive.Include);
    }

    static bool IsGrantedItemId(string id) =>
        !string.IsNullOrEmpty(id) &&
        !id.Equals(NullItemId, StringComparison.OrdinalIgnoreCase);
}

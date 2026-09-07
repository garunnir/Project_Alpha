// ============================================================
// CharacterCellFarmPipeline — 농사 Arrive + Work + MapPlantService
// ============================================================

using IsoTilemap;
using UnityEngine;

public sealed class CharacterCellFarmPipeline : CharacterCellPipelineBase
{
    FarmWorkClipCatalog _clips;

    public CharacterCellFarmPipeline(
        CharacterActionHost actionHost,
        CharacterArriveHost arriveHost,
        CharacterMotor motor,
        FarmWorkClipCatalog clips)
        : base(actionHost, arriveHost, motor)
    {
        _clips = clips;
    }

    public void SetClipCatalog(FarmWorkClipCatalog clips) => _clips = clips;

    public bool TryRun(
        FarmCellActionKind kind,
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container)
    {
        if (ArriveHost == null)
            return false;

        if (ActionHost == null)
            return BeginPipeline(kind, cell, stack, container);

        return ActionHost.TryRunOrEnqueue(
            CharacterActionKind.Cell,
            () => BeginPipeline(kind, cell, stack, container));
    }

    bool BeginPipeline(
        FarmCellActionKind kind,
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container)
    {
        Vector3 destination = MapPlantService.CellArriveWorld(cell);
        float stopping = MapPlantService.CellArriveStoppingDistance();
        System.Func<bool> tryIsArrived = null;

        if (kind == FarmCellActionKind.Plant)
        {
            tryIsArrived = () =>
                MapPlantService.TryResolveActorCell(out Vector3Int playerCell) &&
                MapPlantService.IsWithinPlantActionRange(playerCell, cell);
        }

        return BeginArrive(
            destination,
            stopping,
            () => OnArrived(kind, cell, stack, container),
            tryIsArrived);
    }

    void OnArrived(
        FarmCellActionKind kind,
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container)
    {
        if (kind == FarmCellActionKind.Plant &&
            !(MapPlantService.TryResolveActorCell(out Vector3Int playerCell) &&
              MapPlantService.IsWithinPlantActionRange(playerCell, cell)))
        {
            EndPipeline();
            return;
        }

        if (!NeedsWork(kind))
        {
            Apply(kind, cell, stack, container);
            EndPipeline();
            return;
        }

        AnimationClip clip = _clips != null ? _clips.Resolve(kind) : null;
        float configured = ResolveConfiguredDuration(kind);
        if (!Work.TryBegin(clip, configured, () =>
            {
                Apply(kind, cell, stack, container);
                EndPipeline();
            }))
        {
            EndPipeline();
        }
    }

    static bool NeedsWork(FarmCellActionKind kind) =>
        kind == FarmCellActionKind.Plant ||
        kind == FarmCellActionKind.Till ||
        kind == FarmCellActionKind.Harvest ||
        kind == FarmCellActionKind.Chop;

    float ResolveConfiguredDuration(FarmCellActionKind kind)
    {
        if (_clips != null)
            return _clips.ResolveDuration(kind);

        return kind switch
        {
            FarmCellActionKind.Plant => MapPlantConsts.PlantWorkDurationSeconds,
            FarmCellActionKind.Till => MapPlantConsts.TillWorkDurationSeconds,
            FarmCellActionKind.Chop => MapPlantConsts.ChopWorkDurationSeconds,
            _ => 0f,
        };
    }

    static void Apply(
        FarmCellActionKind kind,
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container)
    {
        switch (kind)
        {
            case FarmCellActionKind.Plant:
                MapPlantService.TryPlantAt(cell, stack, container);
                break;
            case FarmCellActionKind.Till:
                if (stack != null && container != null)
                    MapPlantService.TryTillAt(cell, stack, container);
                else
                    MapPlantService.TryTill(cell);
                break;
            case FarmCellActionKind.Fertilize:
                if (stack != null && container != null)
                    MapPlantService.TryFertilizeAt(cell, stack, container);
                else
                    MapPlantService.TryFertilize(cell);
                break;
            case FarmCellActionKind.Harvest:
                MapPlantService.TryHarvest(cell);
                break;
            case FarmCellActionKind.Chop:
                MapPlantService.TryChop(cell);
                break;
        }
    }
}

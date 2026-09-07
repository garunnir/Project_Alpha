// ============================================================
// CharacterCellFishPipeline — 낚시 Arrive + Work + MapFishService
// ============================================================

using IsoTilemap;
using UnityEngine;

public sealed class CharacterCellFishPipeline : CharacterCellPipelineBase
{
    FishWorkClipCatalog _clips;

    public CharacterCellFishPipeline(
        CharacterActionHost actionHost,
        CharacterArriveHost arriveHost,
        CharacterMotor motor,
        FishWorkClipCatalog clips)
        : base(actionHost, arriveHost, motor)
    {
        _clips = clips != null ? clips : FishWorkClipCatalog.Runtime;
    }

    public void SetClipCatalog(FishWorkClipCatalog clips) => _clips = clips;

    public bool TryRun(
        FishCellActionKind kind,
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
        FishCellActionKind kind,
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container)
    {
        Vector3 destination = MapFishService.CellArriveWorld(cell);
        float stopping = MapFishService.CellArriveStoppingDistance();
        System.Func<bool> tryIsArrived = null;

        if (kind == FishCellActionKind.Cast ||
            kind == FishCellActionKind.DeployTrap ||
            kind == FishCellActionKind.CollectTrap)
        {
            tryIsArrived = () =>
                MapFishService.TryResolveActorCell(out Vector3Int playerCell) &&
                MapFishService.IsWithinCastActionRange(playerCell, cell);
        }

        return BeginArrive(
            destination,
            stopping,
            () => OnArrived(kind, cell, stack, container),
            tryIsArrived);
    }

    void OnArrived(
        FishCellActionKind kind,
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container)
    {
        if ((kind == FishCellActionKind.Cast ||
             kind == FishCellActionKind.DeployTrap ||
             kind == FishCellActionKind.CollectTrap) &&
            !(MapFishService.TryResolveActorCell(out Vector3Int playerCell) &&
              MapFishService.IsWithinCastActionRange(playerCell, cell)))
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

        if (_clips == null)
            _clips = FishWorkClipCatalog.Runtime;

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

    static bool NeedsWork(FishCellActionKind kind) =>
        kind == FishCellActionKind.Cast ||
        kind == FishCellActionKind.DeployTrap ||
        kind == FishCellActionKind.CollectTrap;

    float ResolveConfiguredDuration(FishCellActionKind kind)
    {
        if (_clips != null)
            return _clips.ResolveDuration(kind);

        return kind switch
        {
            FishCellActionKind.Cast => MapFishConsts.CastWorkDurationSeconds,
            FishCellActionKind.DeployTrap => MapFishConsts.DeployTrapWorkDurationSeconds,
            FishCellActionKind.CollectTrap => MapFishConsts.CollectTrapWorkDurationSeconds,
            _ => 0f,
        };
    }

    static void Apply(
        FishCellActionKind kind,
        Vector3Int cell,
        ItemStack stack,
        InventoryContainer container)
    {
        switch (kind)
        {
            case FishCellActionKind.Cast:
                MapFishService.TryCastAt(cell, stack, container);
                break;
            case FishCellActionKind.DeployTrap:
                MapFishService.TryDeployTrapAt(cell, stack, container);
                break;
            case FishCellActionKind.CollectTrap:
                MapFishService.TryCollectTrapAt(cell);
                break;
        }
    }
}

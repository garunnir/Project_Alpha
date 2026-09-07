// ============================================================
// CharacterCellConstructionPipeline — 건설 Arrive + Work + ConstructionService
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

public sealed class CharacterCellConstructionPipeline : CharacterCellPipelineBase
{
    public CharacterCellConstructionPipeline(
        CharacterActionHost actionHost,
        CharacterArriveHost arriveHost,
        CharacterMotor motor)
        : base(actionHost, arriveHost, motor)
    {
    }

    public bool TryRun(ConstructionData data, Vector3Int cell, int facingQuarters)
    {
        if (ArriveHost == null || data == null)
            return false;

        if (ActionHost == null)
            return BeginPipeline(data, cell, facingQuarters);

        return ActionHost.TryRunOrEnqueue(
            CharacterActionKind.Cell,
            () => BeginPipeline(data, cell, facingQuarters));
    }

    bool BeginPipeline(ConstructionData data, Vector3Int cell, int facingQuarters)
    {
        Vector3 destination = ConstructionService.CellArriveWorld(cell);
        float stopping = ConstructionService.CellArriveStoppingDistance();

        return BeginArrive(
            destination,
            stopping,
            () => OnArrived(data, cell, facingQuarters));
    }

    void OnArrived(ConstructionData data, Vector3Int cell, int facingQuarters)
    {
        float duration = ConstructionService.ResolveWorkDurationSeconds(data);
        if (!Work.TryBegin(null, duration, () =>
            {
                Apply(data, cell, facingQuarters);
                EndPipeline();
            }))
        {
            EndPipeline();
        }
    }

    static void Apply(ConstructionData data, Vector3Int cell, int facingQuarters)
    {
        CraftingMaterialPool pool = ConstructionService.CreatePoolFromActivePlayer();
        TileMapManager map = Object.FindFirstObjectByType<TileMapManager>();
        TileMapController controller = Object.FindFirstObjectByType<TileMapController>();
        InventorySession session = PlayerInventoryRuntime.Active?.Session;

        ConstructionService.TryBuildAt(
            data,
            cell,
            pool,
            map,
            controller,
            facingQuarters,
            session);
    }
}

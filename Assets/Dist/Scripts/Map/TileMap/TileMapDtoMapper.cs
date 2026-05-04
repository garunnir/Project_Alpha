using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    //Json->DTO
    public class TileMapDtoMapper : IMapMapper
    {

        public MapModelDTO ToPrepared(MapSaveJsonDto tileMapData)
        {
            if (tileMapData == null || tileMapData.tiles == null)
            {
                Debug.LogWarning("TileMapData or its tiles are null.");
                return null;
            }
            List<TileData> prepareData = new List<TileData>();
            foreach (var td in tileMapData.tiles)
            {
                byte edgeFace = TileIdentity.EdgeFaceNone;
                if (td.tileType == (byte)TileView.TileType.EdgeWall)
                    edgeFace = (byte)Mathf.Clamp((int)td.face, 0, 1);

                prepareData.Add(new TileData
                {
                    tileDefId = Guid.NewGuid(),
                    state = new TileState { },
                    identity = new TileIdentity
                    {
                        PrefabId = td.prefabId,
                        tileType = td.tileType,
                        GridPos = new Vector3Int(td.x, td.y, td.z),
                        sizeUnit = new Vector3Int(td.sizeX, td.sizeY, td.sizeZ),
                        edgeFace = edgeFace,
                    }
                });
            }

            if (tileMapData.wallEdges != null)
            {
                foreach (var we in tileMapData.wallEdges)
                {
                    byte faceClamped = (byte)Mathf.Clamp((int)we.face, 0, 1);
                    prepareData.Add(new TileData
                    {
                        tileDefId = Guid.NewGuid(),
                        state = new TileState(),
                        identity = new TileIdentity
                        {
                            PrefabId = we.prefabId,
                            GridPos = new Vector3Int(we.x, we.y, we.z),
                            sizeUnit = Vector3Int.one,
                            tileType = (byte)TileView.TileType.EdgeWall,
                            edgeFace = faceClamped,
                        }
                    });
                }
            }

            return new MapModelDTO(prepareData);
        }

        public MapSaveJsonDto FromPrepared(MapModelDTO prepared)
        {
            IReadOnlyList<TileData> tiles = prepared.TilesData;
            MapSaveJsonDto tile = new MapSaveJsonDto();

            foreach (var ti in tiles)
            {
                if (ti.identity.tileType == (byte)TileView.TileType.EdgeWall)
                {
                    tile.wallEdges.Add(new WallEdgeSaveData
                    {
                        x = ti.identity.GridPos.x,
                        y = ti.identity.GridPos.y,
                        z = ti.identity.GridPos.z,
                        face = ti.identity.edgeFace,
                        prefabId = ti.identity.PrefabId,
                    });
                }
                else
                {
                    tile.tiles.Add(new TileSaveData
                    {
                        sizeX = ti.identity.sizeUnit.x,
                        sizeY = ti.identity.sizeUnit.y,
                        sizeZ = ti.identity.sizeUnit.z,
                        x = ti.identity.GridPos.x,
                        y = ti.identity.GridPos.y,
                        z = ti.identity.GridPos.z,
                        tileType = ti.identity.tileType,
                        prefabId = ti.identity.PrefabId,
                    });
                }
            }

            return tile;
        }
    }
}

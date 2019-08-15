using UnityEngine;
//using UnityEditor;
using System.Collections.Generic;
//using System.ComponentModel;
//using UnityEngine.Bindings;
//using UnityEngine.Scripting;


public class TerrainMap
{
    public TileCoord GetTerrainCoord(Vector3 pointWorldSpace)
    {
        return new TileCoord(
            Mathf.FloorToInt((pointWorldSpace.x - gridOrigin.x) / gridSize.x),
            Mathf.FloorToInt((pointWorldSpace.z - gridOrigin.y) / gridSize.y));
    }

    public Terrain GetTerrain(int tileX, int tileZ)
    {
        Terrain result = null;
        m_terrainTiles.TryGetValue(new TileCoord(tileX, tileZ), out result);
        return result;
    }

    public delegate bool TerrainFilter(Terrain terrain);

    // create a terrain map of ALL terrains, by using only their placement to fit them to a grid
    // the position and size of originTerrain defines the grid alignment and origin.  if NULL, we use the first active terrain
    static public TerrainMap CreateFromPlacement(Terrain originTerrain, TerrainFilter filter = null, bool fullValidation = true)
    {
        if ((Terrain.activeTerrains == null) || (Terrain.activeTerrains.Length == 0) || (originTerrain == null))
            return null;

        if (originTerrain.terrainData == null)
            return null;

        int groupID = originTerrain.groupingID;
        float gridOriginX = originTerrain.transform.position.x;
        float gridOriginZ = originTerrain.transform.position.z;
        float gridSizeX = originTerrain.terrainData.size.x;
        float gridSizeZ = originTerrain.terrainData.size.z;

        if (filter == null)
            filter = (x => (x.groupingID == groupID));

        return CreateFromPlacement(new Vector2(gridOriginX, gridOriginZ), new Vector2(gridSizeX, gridSizeZ), filter, fullValidation);
    }

    // create a terrain map of ALL terrains, by using only their placement to fit them to a grid
    // the position and size of originTerrain defines the grid alignment and origin.  if NULL, we use the first active terrain
    static public TerrainMap CreateFromPlacement(Vector2 gridOrigin, Vector2 gridSize, TerrainFilter filter = null, bool fullValidation = true)
    {
        if ((Terrain.activeTerrains == null) || (Terrain.activeTerrains.Length == 0))
            return null;

        TerrainMap terrainMap = new TerrainMap();

        terrainMap.gridOrigin = gridOrigin;
        terrainMap.gridSize = gridSize;

        float gridScaleX = 1.0f / gridSize.x;
        float gridScaleZ = 1.0f / gridSize.y;

        // iterate all active terrains
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            // some integration tests just create a terrain component without terrain data
            if (terrain.terrainData == null)
                continue;

            if ((filter == null) || filter(terrain))
            {
                // convert position to a grid index, with proper rounding
                Vector3 pos = terrain.transform.position;
                int tileX = Mathf.RoundToInt((pos.x - gridOrigin.x) * gridScaleX);
                int tileZ = Mathf.RoundToInt((pos.z - gridOrigin.y) * gridScaleZ);
                // attempt to add the terrain at that grid position
                terrainMap.TryToAddTerrain(tileX, tileZ, terrain);
            }
        }

        // run validation to check alignment status
        if (fullValidation)
            terrainMap.Validate();

        return (terrainMap.m_terrainTiles.Count > 0) ? terrainMap : null;
    }

    public struct TileCoord
    {
        public readonly int tileX;
        public readonly int tileZ;

        public TileCoord(int tileX, int tileZ)
        {
            this.tileX = tileX;
            this.tileZ = tileZ;
        }
    };

    public enum ErrorCode
    {
        OK = 0,
        Overlapping = 1 << 0,
        SizeMismatch = 1 << 2,
        EdgeAlignmentMismatch = 1 << 3,
    };

    public Vector2 gridOrigin           { get; private set; }       // XZ coordinate grid origin
    public Vector2 gridSize             { get; private set; }       // XZ coordinate grid size
    public ErrorCode m_errorCode;
    public Dictionary<TileCoord, Terrain> m_terrainTiles;

    private Vector3 m_patchSize;        // size of the first terrain, used for consistency checks

    public TerrainMap()
    {
        m_errorCode = ErrorCode.OK;
        m_terrainTiles = new Dictionary<TileCoord, Terrain>();
    }

    private void AddTerrainInternal(int x, int z, Terrain terrain)
    {
        if (m_terrainTiles.Count == 0)
            m_patchSize = terrain.terrainData.size;
        else
        {
            // check consistency with existing terrains
            if (terrain.terrainData.size != m_patchSize)
            {
                // ERROR - terrain is not the same size as other terrains
                m_errorCode |= ErrorCode.SizeMismatch;
            }
        }
        m_terrainTiles.Add(new TileCoord(x, z), terrain);
    }

    // attempt to place the specified terrain tile at the specified (x,z) position, with consistency checks
    private bool TryToAddTerrain(int tileX, int tileZ, Terrain terrain)
    {
        bool added = false;
        if (terrain != null)
        {
            Terrain existing = GetTerrain(tileX, tileZ);
            if (existing != null)
            {
                // already a terrain in the location -- check it is the same tile
                if (existing != terrain)
                {
                    // ERROR - multiple different terrains at the same coordinate!
                    m_errorCode |= ErrorCode.Overlapping;
                }
            }
            else
            {
                // add terrain to the terrain map
                AddTerrainInternal(tileX, tileZ, terrain);
                added = true;
            }
        }
        return added;
    }

    private void ValidateTerrain(int tileX, int tileZ)
    {
        Terrain terrain = GetTerrain(tileX, tileZ);
        if (terrain != null)
        {
            // grab neighbors (according to grid)
            Terrain left = GetTerrain(tileX - 1, tileZ);
            Terrain right = GetTerrain(tileX + 1, tileZ);
            Terrain top = GetTerrain(tileX, tileZ + 1);
            Terrain bottom = GetTerrain(tileX, tileZ - 1);

            // check edge alignment
            {
                if (left)
                {
                    if (!Mathf.Approximately(terrain.transform.position.x, left.transform.position.x + left.terrainData.size.x) ||
                        !Mathf.Approximately(terrain.transform.position.z, left.transform.position.z))
                    {
                        // unaligned edge, tile doesn't match expected location
                        m_errorCode |= ErrorCode.EdgeAlignmentMismatch;
                    }
                }
                if (right)
                {
                    if (!Mathf.Approximately(terrain.transform.position.x + terrain.terrainData.size.x, right.transform.position.x) ||
                        !Mathf.Approximately(terrain.transform.position.z, right.transform.position.z))
                    {
                        // unaligned edge, tile doesn't match expected location
                        m_errorCode |= ErrorCode.EdgeAlignmentMismatch;
                    }
                }
                if (top)
                {
                    if (!Mathf.Approximately(terrain.transform.position.x, top.transform.position.x) ||
                        !Mathf.Approximately(terrain.transform.position.z + terrain.terrainData.size.z, top.transform.position.z))
                    {
                        // unaligned edge, tile doesn't match expected location
                        m_errorCode |= ErrorCode.EdgeAlignmentMismatch;
                    }
                }
                if (bottom)
                {
                    if (!Mathf.Approximately(terrain.transform.position.x, bottom.transform.position.x) ||
                        !Mathf.Approximately(terrain.transform.position.z, bottom.transform.position.z + bottom.terrainData.size.z))
                    {
                        // unaligned edge, tile doesn't match expected location
                        m_errorCode |= ErrorCode.EdgeAlignmentMismatch;
                    }
                }
            }
        }
    }

    // perform all validation checks on the terrain map
    private ErrorCode Validate()
    {
        // iterate all tiles and validate them
        foreach (TileCoord coord in m_terrainTiles.Keys)
        {
            ValidateTerrain(coord.tileX, coord.tileZ);
        }
        return m_errorCode;
    }

    private struct QueueElement
    {
        public readonly int tileX;
        public readonly int tileZ;
        public readonly Terrain terrain;
        public QueueElement(int tileX, int tileZ, Terrain terrain)
        {
            this.tileX = tileX;
            this.tileZ = tileZ;
            this.terrain = terrain;
        }
    };
};


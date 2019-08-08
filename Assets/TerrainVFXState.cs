using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEngine.Experimental.TerrainAPI;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif


internal static class TerrainVFXProperties
{
    static private bool idsSetup = false;

    // prop ids
    static public int heightmap               { get; private set; }
    static public int heightmapPosition       { get; private set; }
    static public int heightmapSize           { get; private set; }
    static public int tilingVolumeCenter      { get; private set; }
    static public int tilingVolumeSize        { get; private set; }
    static public int lodTarget               { get; private set; }
    static public int alphamap                { get; private set; }
    static public int alphamapSize            { get; private set; }
    static public int fadeCenter              { get; private set; }
    static public int fadeDistance            { get; private set; }
    static public int createCount             { get; private set; }
    static public int createBoundsMin         { get; private set; }
    static public int createBoundsMax         { get; private set; }

    static public void Reset()
    {
        idsSetup = false;
    }

    static public void Setup()
    {
        if (!idsSetup)
        {
            heightmap = Shader.PropertyToID("Heightmap");
            heightmapPosition = Shader.PropertyToID("Heightmap_Position");
            heightmapSize = Shader.PropertyToID("Heightmap_Size");
            tilingVolumeCenter = Shader.PropertyToID("tilingVolume_center");
            tilingVolumeSize = Shader.PropertyToID("tilingVolume_size");
            lodTarget = Shader.PropertyToID("lodTarget");
            alphamap = Shader.PropertyToID("alphamap");
            alphamapSize = Shader.PropertyToID("alphamapSize");
            fadeCenter = Shader.PropertyToID("fadeCenter");
            fadeDistance = Shader.PropertyToID("fadeDistance");

            createCount = Shader.PropertyToID("createCount");
            createBoundsMin = Shader.PropertyToID("createBounds_min");
            createBoundsMax = Shader.PropertyToID("createBounds_max");

            idsSetup = true;
        }
    }
}


public class TerrainState
{
//    public Rect liveBounds;
    public Rect terrainBounds;
}


// TODO: this doesn't have to be a MonoBehaviour, other than we want to attach it to the same game object that is running the VisualEffect
// We could merge this with VisualEffect in the future, to make one component type "TerrainVisualEffect" ?
[ExecuteInEditMode]
public class TerrainVFXState : MonoBehaviour
{
    // serialized settings
    public float volumeSize;
    public float forwardBiasDistance;
    public float density;
    public bool debugLiveBounds;

    // runtime state
    VisualEffect vfx;
    Dictionary<Terrain, TerrainState> terrainStates;
    Rect liveBounds;


    // stupid amortized spawn state:  (HACK around not being able to send multiple events in a single frame)
    bool isSpawning;
    Rect spawnBounds;
    TerrainMap terrainMap;
    TerrainMap.TileCoord minCoord;
    TerrainMap.TileCoord maxCoord;
    int tileZ;
    int tileX;

#if UNITY_EDITOR
    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }
    void OnSceneGUI(SceneView sv)
    {
        //Draw your handles here
        if (debugLiveBounds)
        {
            Vector3[] points = new Vector3[5];

            if (terrainStates != null)
                foreach (TerrainState terrainState in terrainStates.Values)
                {
                    // Rect liveBounds = terrainState.liveBounds;
                    Rect terrainBounds = terrainState.terrainBounds;

                    points[0] = new Vector3(terrainBounds.min.x, 0.0f, terrainBounds.min.y);
                    points[1] = new Vector3(terrainBounds.min.x, 0.0f, terrainBounds.max.y);
                    points[2] = new Vector3(terrainBounds.max.x, 0.0f, terrainBounds.max.y);
                    points[3] = new Vector3(terrainBounds.max.x, 0.0f, terrainBounds.min.y);
                    points[4] = points[0];
                    Handles.color = new Color(0.0f, 1.0f, 0.0f);
                    Handles.DrawAAPolyLine(4.0f, points);
                }

            points[0] = new Vector3(liveBounds.min.x, 0.0f, liveBounds.min.y);
            points[1] = new Vector3(liveBounds.min.x, 0.0f, liveBounds.max.y);
            points[2] = new Vector3(liveBounds.max.x, 0.0f, liveBounds.max.y);
            points[3] = new Vector3(liveBounds.max.x, 0.0f, liveBounds.min.y);
            points[4] = points[0];
            Handles.color = new Color(1.0f, UnityEngine.Random.value, UnityEngine.Random.value);
            Handles.DrawAAPolyLine(2.0f, points);
        }
    }
#endif


    static Rect ClipRect(Rect A, Rect B)
    {
        // TODO: probably have to fix this so that it has a hard clip and a soft clip bounds
        // (ensuring the resulting Rect is always within the hard clip bounds)
        return Rect.MinMaxRect(
                Mathf.Max(A.xMin, B.xMin),
                Mathf.Max(A.yMin, B.yMin),
                Mathf.Min(A.xMax, B.xMax),
                Mathf.Min(A.yMax, B.yMax)
            );
    }

    static Rect ExpandRect(Rect A, Rect B)
    {
        return Rect.MinMaxRect(
                Mathf.Min(A.xMin, B.xMin),
                Mathf.Min(A.yMin, B.yMin),
                Mathf.Max(A.xMax, B.xMax),
                Mathf.Max(A.yMax, B.yMax)
            );
    }

    public void TerrainVFXUpdate(TerrainVFXController controller, TerrainMap terrainMap, Transform lodTransform, bool resetAndRespawn)
    {
        if (terrainMap == null)
            return;

        if (vfx == null)
        {
            vfx = GetComponent<VisualEffect>();
            resetAndRespawn = true;
        }
        if (vfx == null)
            return;

        if (terrainStates == null)
        {
            terrainStates = new Dictionary<Terrain, TerrainState>();

            // initialize live bounds
            foreach (Terrain terrain in terrainMap.m_terrainTiles.Values)
            {
                terrainStates[terrain] =
                    new TerrainState()
                    {
                        terrainBounds = new Rect(
                            terrain.transform.position.x, terrain.transform.position.z,
                            terrain.terrainData.size.x, terrain.terrainData.size.z)
                    };
            }
        }

        Vector3 tilingVolumeCenter = lodTransform.position + lodTransform.forward * forwardBiasDistance;
        Vector3 tilingVolumeSize = new Vector3(volumeSize, 2000.0f, volumeSize);

        // target Bounds is what we would ideally want as our live bounds (fully populated area)
        Rect targetBounds = new Rect(
            tilingVolumeCenter.x - tilingVolumeSize.x * 0.5f,
            tilingVolumeCenter.z - tilingVolumeSize.z * 0.5f,
            tilingVolumeSize.x,
            tilingVolumeSize.z);

        // always update render parameters
        {
            if (vfx.HasVector3(TerrainVFXProperties.lodTarget))
                vfx.SetVector3(TerrainVFXProperties.lodTarget, tilingVolumeCenter);

            vfx.SetVector3(TerrainVFXProperties.tilingVolumeCenter, tilingVolumeCenter);
            vfx.SetVector3(TerrainVFXProperties.tilingVolumeSize, tilingVolumeSize);

            vfx.SetVector3(TerrainVFXProperties.fadeCenter, lodTransform.position);
            vfx.SetFloat(TerrainVFXProperties.fadeDistance, volumeSize * 0.5f + forwardBiasDistance * 0.5f);
        }

        if (isSpawning)
        {
            RunSpawnStep();
            return;
        }
        // else start spawn step below

        // if the current target does not overlap with the previous target at all, then reset and respawn;
        // because doing the incremental expansion algorithm is potentially much worse
        if (!targetBounds.Overlaps(liveBounds))
        {
            resetAndRespawn = true;
        }

        if (resetAndRespawn)
        {
            // clear all particles
            vfx.Reinit();

            // set live bounds to 0 area thin rect along the xMin edge (to minimize expansion steps)
            liveBounds = Rect.MinMaxRect(targetBounds.xMin, targetBounds.yMin, targetBounds.xMin, targetBounds.yMax);
        }
        else
        {
            // cull particles against tiling volume
            vfx.Simulate(0.0f, 1);
            liveBounds = ClipRect(liveBounds, targetBounds);
        }

        // spawn towards target bounds
        UpdateLiveBounds(targetBounds, terrainMap);
    }

    void UpdateLiveBounds(Rect targetBounds, TerrainMap terrainMap)
    {
        // TODO: really should do something akin to PaintContext to gather Terrains within the tiling volume
        minCoord = terrainMap.GetTerrainCoord(new Vector3(targetBounds.min.x, 0.0f, targetBounds.min.y));
        maxCoord = terrainMap.GetTerrainCoord(new Vector3(targetBounds.max.x, 0.0f, targetBounds.max.y));

        // calculate the spawn bounds for the best expansion direction for live bounds
        spawnBounds = CalculateBestSpawnBounds(liveBounds, targetBounds);

        // spawn in spawn bounds across all relevant terrain tiles
        if ((spawnBounds.width > 0.0f) && (spawnBounds.height > 0.0f))
        {
            this.terrainMap = terrainMap;
            this.tileZ = minCoord.tileZ;
            this.tileX = minCoord.tileX;
            this.isSpawning = true;
            RunSpawnStep();

//             for (int tileZ = minCoord.tileZ; tileZ <= maxCoord.tileZ; tileZ++)
//             {
//                 for (int tileX = minCoord.tileX; tileX <= maxCoord.tileX; tileX++)
//                 {
//                     Terrain terrain = terrainMap.GetTerrain(tileX, tileZ);
//                     if (terrain == null)
//                         continue;
//                     TerrainState terrainState = terrainStates[terrain];
// 
//                     // clip spawnBounds to the terrain tile
//                     Rect terrainSpawnBounds = ClipRect(spawnBounds, terrainState.terrainBounds);
// 
//                     float spawnArea = terrainSpawnBounds.width * terrainSpawnBounds.height;
//                     if (spawnArea > 0.0f)
//                     {
//                         SpawnInRect(terrain, terrainSpawnBounds);
//                     }
//                 }
//             }
            liveBounds = ExpandRect(liveBounds, spawnBounds);
        }
    }

    void RunSpawnStep()
    {
        while(isSpawning)
        {
            Terrain terrain = terrainMap.GetTerrain(tileX, tileZ);

            // advance coord to next tile
            tileX++;
            if (tileX > maxCoord.tileX)
            {
                tileX = minCoord.tileX;
                tileZ++;
                if (tileZ > maxCoord.tileZ)
                    isSpawning = false;
            }

            if (terrain != null)
            {
                TerrainState terrainState = terrainStates[terrain];

                // clip spawnBounds to the terrain tile
                Rect terrainSpawnBounds = ClipRect(spawnBounds, terrainState.terrainBounds);

                float spawnArea = terrainSpawnBounds.width * terrainSpawnBounds.height;
                if (spawnArea > 0.0f)
                {
                    SpawnInRect(terrain, terrainSpawnBounds);
                    return;
                }
            }

            // otherwise we didn't spawn anything yet
            // so go to the next terrain tile
        }
    }

    void SpawnInRect(Terrain terrain, Rect spawnBounds)
    {
        // setup properties for Terrain
        vfx.SetTexture(TerrainVFXProperties.heightmap, terrain.terrainData.heightmapTexture);
        vfx.SetVector3(TerrainVFXProperties.heightmapPosition, terrain.transform.position);

        Vector3 size = terrain.terrainData.size;
        size.y *= 2.0f;     // faked because our heightmap is only [0-0.5]
        vfx.SetVector3(TerrainVFXProperties.heightmapSize, size);

        vfx.SetTexture(TerrainVFXProperties.alphamap, terrain.terrainData.alphamapTextures[0]);
        // vfx.SetVector4(TerrainVFXProperties.alphamapSize, new Vector4(1.0f, 0.4f, 0.1f, 0.0f));

        if ((spawnBounds.width > 0.0f) && (spawnBounds.height > 0.0f))
        {
            SpawnInRect(
                spawnBounds.xMin, spawnBounds.xMax,
                spawnBounds.yMin, spawnBounds.yMax);
        }

        /* // This was the first algorithm I tried...  it requires more than one event per frame

                // first spawn along +/- X border
                if (spawnBounds.xMin < lastBounds.xMin)
                {
                    // spawn between spawn.xMin and last.xMin
                    SpawnInRect(
                        eventAttr,
                        spawnBounds.xMin, lastBounds.xMin,
                        spawnBounds.yMin, spawnBounds.yMax);

                    // clip away the spawned region from the spawnBounds
                    spawnBounds.xMin = lastBounds.xMin;
                }
                if (spawnBounds.xMax > lastBounds.xMax)
                {
                    // spawn between last.xMax and spawn.xMax
                    SpawnInRect(
                        eventAttr2,
                        lastBounds.xMax, spawnBounds.xMax,
                        spawnBounds.yMin, spawnBounds.yMax);

                    // clip away the spawned region from the spawnBounds
                    spawnBounds.xMax = lastBounds.xMax;
                }

                // spawn along +/- Y border
                if (spawnBounds.yMin < lastBounds.yMin)
                {
                    // spawn between spawn.yMin and last.yMin
                    SpawnInRect(
                        eventAttr3,
                        spawnBounds.xMin, spawnBounds.xMax,
                        spawnBounds.yMin, lastBounds.yMin, 2);
                }
                if (spawnBounds.yMax > lastBounds.yMax)
                {
                    // spawn between last.yMax and spawn.yMax
                    SpawnInRect(
                        eventAttr4,
                        spawnBounds.xMin, spawnBounds.xMax,
                        lastBounds.yMax, spawnBounds.yMax, 2);
                }
                lastBounds = newBounds;
        */
    }

    // incremental update -- calculate next bounds to spawn in
    Rect CalculateBestSpawnBounds(Rect liveBounds, Rect targetBounds)
    {
        // default is empty
        Rect spawnBounds = new Rect(liveBounds.min, Vector2.zero);

        // assuming we can only do one spawn event, figure out the best border to spawn along...
        float xMinDelta = liveBounds.xMin - targetBounds.xMin;
        float xMaxDelta = targetBounds.xMax - liveBounds.xMax;
        float yMinDelta = liveBounds.yMin - targetBounds.yMin;
        float yMaxDelta = targetBounds.yMax - liveBounds.yMax;
        float xDelta = Mathf.Max(xMinDelta, xMaxDelta);
        float yDelta = Mathf.Max(yMinDelta, yMaxDelta);
        float delta = Mathf.Max(xDelta, yDelta);

        if (delta > 0.0f)
        {
            if (xDelta > yDelta)
            {
                // spawn along x -- first trim live bounds along y
                liveBounds.yMin = Mathf.Max(liveBounds.yMin, targetBounds.yMin);
                liveBounds.yMax = Mathf.Min(liveBounds.yMax, targetBounds.yMax);
                if (xMinDelta > xMaxDelta)
                {
                    // spawn along xMin, and last.xMin to indicate we covered it
                    spawnBounds = Rect.MinMaxRect(
                        targetBounds.xMin, liveBounds.yMin,
                        liveBounds.xMin, liveBounds.yMax);
                }
                else
                {
                    // spawn along xMax, and last.xMax to indicate we covered it
                    spawnBounds = Rect.MinMaxRect(
                        liveBounds.xMax, liveBounds.yMin,
                        targetBounds.xMax, liveBounds.yMax);
                }
            }
            else
            {
                // spawn along y -- first trim along x
                liveBounds.xMin = Mathf.Max(liveBounds.xMin, targetBounds.xMin);
                liveBounds.xMax = Mathf.Min(liveBounds.xMax, targetBounds.xMax);
                if (yMinDelta > yMaxDelta)
                {
                    // spawn along yMin, and last.yMin to indicate we covered it
                    spawnBounds = Rect.MinMaxRect(
                        liveBounds.xMin, targetBounds.yMin,
                        liveBounds.xMax, liveBounds.yMin);
                }
                else
                {
                    // spawn along yMax, and last.yMax to indicate we covered it
                    spawnBounds = Rect.MinMaxRect(
                        liveBounds.xMin, liveBounds.yMax,
                        liveBounds.xMax, targetBounds.yMax);
                }
            }
        }
        return spawnBounds;
    }


    void SpawnInRect(
        float minX, float maxX,
        float minZ, float maxZ)
    {
        float deltaX = Mathf.Max(maxX - minX, 0.0f);
        float deltaZ = Mathf.Max(maxZ - minZ, 0.0f);
        float area = deltaX * deltaZ;

        if (area > 0.0f)
        {
            // dither the count to 'fix' rounding error for small areas
            int count = Mathf.FloorToInt(area * density + UnityEngine.Random.value - 0.001f);

            if (count > 0)
            {
                // BUG : can't seem to get attributes to work, just going to use properties for now
                // eventAttr.SetInt(TerrainVFXProperties.createCountID, count);        
                // eventAttr.SetVector3(TerrainVFXProperties.createBoundsMinID, new Vector3(minX, 0.0f, minZ));
                // eventAttr.SetVector3(TerrainVFXProperties.createBoundsMaxID, new Vector3(maxX, 0.0f, maxZ));

                vfx.SetInt(TerrainVFXProperties.createCount, count);
                vfx.SetVector3(TerrainVFXProperties.createBoundsMin, new Vector3(minX, 0.0f, minZ));
                vfx.SetVector3(TerrainVFXProperties.createBoundsMax, new Vector3(maxX, 0.0f, maxZ));

                vfx.SendEvent("CreateInBounds", null);
                vfx.Simulate(1.0f, 1);                  // HACK: workaround for one event per frame limit -- not necessary with the incremental update algorithm
            }
        }
    }
}


/*
[Serializable]
public class TerrainVFXType
{
    // serialized settings
    public GameObject vfxPrefab;
    public float volumeSize;
    public float forwardBiasDistance;
    public float density;

    // runtime state
    GameObject vfxObject;
    VisualEffect vfx;
    // VFXEventAttribute eventAttr;
    Rect liveBounds;                // describes the area that is currently fully populated with spawned particles

    public void Update(TerrainVFXController controller, TerrainMap terrainMap, Transform lodTransform, bool resetAndRespawn)
    {
        if ((vfxPrefab == null) || (terrainMap == null))
            return;

        if (vfxObject == null)
        {
            vfxObject = GameObject.Instantiate(vfxPrefab, new Vector3(0, 0, 0), Quaternion.identity, controller.transform);
            vfxObject.name = "VFX instance (hidden)";
            vfxObject.hideFlags = HideFlags.HideAndDontSave;

            vfx = vfxObject.GetComponent<VisualEffect>();
            liveBounds = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);
        }

        if (vfx == null)
            return;

        Vector3 tilingVolumeCenter = lodTransform.position + lodTransform.forward * forwardBiasDistance;
        Vector3 tilingVolumeSize = new Vector3(volumeSize, 2000.0f, volumeSize);

        // target Bounds is what we would ideally want as our live bounds (fully populated area)
        Rect targetBounds = new Rect(
            tilingVolumeCenter.x - tilingVolumeSize.x * 0.5f,
            tilingVolumeCenter.z - tilingVolumeSize.z * 0.5f,
            tilingVolumeSize.x,
            tilingVolumeSize.z);

        if (resetAndRespawn)
        {
            vfx.Reinit();
            liveBounds = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);
        }

        if (vfx != null)
        {
            // TODO: really should do something akin to PaintContext to gather Terrains within the tiling volume
            TerrainMap.TileCoord terrainCoord = terrainMap.GetTerrainCoord(tilingVolumeCenter);
            Terrain terrain = terrainMap.GetTerrain(terrainCoord.tileX, terrainCoord.tileZ);
            if (terrain == null)
                return;

            vfx.SetTexture(TerrainVFXProperties.heightmap, terrain.terrainData.heightmapTexture);
            vfx.SetVector3(TerrainVFXProperties.heightmapPosition, terrain.transform.position);

            Vector3 size = terrain.terrainData.size;
            size.y *= 2.0f;     // faked because our heightmap is only [0-0.5]
            vfx.SetVector3(TerrainVFXProperties.heightmapSize, size);

            vfx.SetTexture(TerrainVFXProperties.alphamap, terrain.terrainData.alphamapTextures[0]);
            // vfx.SetVector4(TerrainVFXProperties.alphamapSize, new Vector4(1.0f, 0.4f, 0.1f, 0.0f));

            if (vfx.HasVector3(TerrainVFXProperties.lodTarget))
                vfx.SetVector3(TerrainVFXProperties.lodTarget, tilingVolumeCenter);

            vfx.SetVector3(TerrainVFXProperties.tilingVolumeCenter, tilingVolumeCenter);
            vfx.SetVector3(TerrainVFXProperties.tilingVolumeSize, tilingVolumeSize);

            vfx.SetVector3(TerrainVFXProperties.fadeCenter, lodTransform.position);
            vfx.SetFloat(TerrainVFXProperties.fadeDistance, volumeSize * 0.5f + forwardBiasDistance * 0.5f);

            UpdateLiveBounds(targetBounds);
        }
    }

    void UpdateLiveBounds(Rect targetBounds)
    {
        // if the overlap region between live Bounds and newBounds is empty
        // then we should just nuke and pave
        // because doing the incremental expansion algorithm below is potentially much worse
        // especially if the bounds have moved a long distance
        if (!targetBounds.Overlaps(liveBounds))
        {
            vfx.Reinit();           // nuke
            SpawnInRect(            // pave
                targetBounds.xMin, targetBounds.xMax,
                targetBounds.yMin, targetBounds.yMax);
            liveBounds = targetBounds;
            return;
        }

        // incremental update:

        // first we calculate the clipped live bounds, to check what we should cull
        Rect clippedBounds = liveBounds;
        clippedBounds.xMin = Mathf.Max(clippedBounds.xMin, targetBounds.xMin);
        clippedBounds.xMax = Mathf.Min(clippedBounds.xMax, targetBounds.xMax);
        clippedBounds.yMin = Mathf.Max(clippedBounds.yMin, targetBounds.yMin);
        clippedBounds.yMax = Mathf.Min(clippedBounds.yMax, targetBounds.yMax);
        //         float clippedWidth = Mathf.Max(clippedBounds.xMax - clippedBounds.xMin, 0.0f);
        //         float clippedHeight = Mathf.Max(clippedBounds.yMax - clippedBounds.yMin, 0.0f);
        //         float clippedArea = clippedWidth * clippedHeight;

        // if clipping results in a significantly reduced area
        //         float liveArea = liveBounds.width * liveBounds.height;
        // if (clippedArea < 0.90f * liveArea)                      // BUG: not sure why, but if I skip the cull here, it drops particle spawns below
        {
            // first do a culling pass against the new tiling area to free up particles
            vfx.Simulate(0.0f, 1);
            liveBounds = clippedBounds;
        }

        // assuming we can only do one spawn event, figure out the best border to spawn along...
        float xMinDelta = liveBounds.xMin - targetBounds.xMin;
        float xMaxDelta = targetBounds.xMax - liveBounds.xMax;
        float yMinDelta = liveBounds.yMin - targetBounds.yMin;
        float yMaxDelta = targetBounds.yMax - liveBounds.yMax;
        float xDelta = Mathf.Max(xMinDelta, xMaxDelta);
        float yDelta = Mathf.Max(yMinDelta, yMaxDelta);
        float delta = Mathf.Max(xDelta, yDelta);

        if (delta > 0.0f)
        {
            Rect spawnBounds = targetBounds;
            if (xDelta > yDelta)
            {
                // spawn along x -- first trim along y
                liveBounds.yMin = Mathf.Max(liveBounds.yMin, spawnBounds.yMin);
                liveBounds.yMax = Mathf.Min(liveBounds.yMax, spawnBounds.yMax);
                if (xMinDelta > xMaxDelta)
                {
                    // spawn along xMin, and last.xMin to indicate we covered it
                    SpawnInRect(
                        spawnBounds.xMin, liveBounds.xMin,
                        liveBounds.yMin, liveBounds.yMax);

                    liveBounds.xMin = spawnBounds.xMin;
                }
                else
                {
                    // spawn along xMax, and last.xMax to indicate we covered it
                    SpawnInRect(
                        liveBounds.xMax, spawnBounds.xMax,
                        liveBounds.yMin, liveBounds.yMax);

                    liveBounds.xMax = spawnBounds.xMax;
                }
            }
            else
            {
                // spawn along y -- first trim along x
                liveBounds.xMin = Mathf.Max(liveBounds.xMin, spawnBounds.xMin);
                liveBounds.xMax = Mathf.Min(liveBounds.xMax, spawnBounds.xMax);
                if (yMinDelta > yMaxDelta)
                {
                    // spawn along yMin, and last.yMin to indicate we covered it
                    SpawnInRect(
                        liveBounds.xMin, liveBounds.xMax,
                        spawnBounds.yMin, liveBounds.yMin);

                    liveBounds.yMin = spawnBounds.yMin;
                }
                else
                {
                    // spawn along yMax, and last.yMax to indicate we covered it
                    SpawnInRect(
                        liveBounds.xMin, liveBounds.xMax,
                        liveBounds.yMax, spawnBounds.yMax);

                    liveBounds.yMax = spawnBounds.yMax;
                }
            }
        }
    }

    void SpawnInRect(
        float minX, float maxX,
        float minZ, float maxZ)
    {
        float deltaX = Mathf.Max(maxX - minX, 0.0f);
        float deltaZ = Mathf.Max(maxZ - minZ, 0.0f);
        float area = deltaX * deltaZ;

        if (area > 0.0f)
        {
            // dither the count to 'fix' rounding error for small areas
            int count = Mathf.FloorToInt(area * density + UnityEngine.Random.value - 0.001f);

            if (count > 0)
            {
                // BUG : can't seem to get attributes to work, just going to use properties for now
                // eventAttr.SetInt(TerrainVFXProperties.createCountID, count);        
                // eventAttr.SetVector3(TerrainVFXProperties.createBoundsMinID, new Vector3(minX, 0.0f, minZ));
                // eventAttr.SetVector3(TerrainVFXProperties.createBoundsMaxID, new Vector3(maxX, 0.0f, maxZ));

                vfx.SetInt(TerrainVFXProperties.createCount, count);
                vfx.SetVector3(TerrainVFXProperties.createBoundsMin, new Vector3(minX, 0.0f, minZ));
                vfx.SetVector3(TerrainVFXProperties.createBoundsMax, new Vector3(maxX, 0.0f, maxZ));

                vfx.SendEvent("CreateInBounds", null);
                // vfx.Simulate(0.0f, 1);                  // HACK: workaround for one event per frame limit -- not necessary with the incremental update algorithm
            }
        }
    }
};
*/

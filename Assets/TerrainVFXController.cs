using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEngine.Experimental.TerrainAPI;
#if UNITY_EDITOR
using UnityEditor;
#endif


internal static class TerrainVFXProperties
{
    static private bool idsSetup = false;

    // prop ids
    static public int heightmapID               { get; private set; }
    static public int heightmapPositionID       { get; private set; }
    static public int heightmapSizeID           { get; private set; }
    static public int tilingVolumeCenterID      { get; private set; }
    static public int tilingVolumeSizeID        { get; private set; }
    static public int lodTargetID               { get; private set; }
    static public int alphamapID                { get; private set; }
    static public int alphamapSizeID            { get; private set; }
    static public int fadeCenterID              { get; private set; }
    static public int fadeDistanceID            { get; private set; }
    static public int createCountID             { get; private set; }
    static public int createBoundsCenterID      { get; private set; }
    static public int createBoundsSizeID        { get; private set; }
    static public int createBoundsMinID         { get; private set; }
    static public int createBoundsMaxID         { get; private set; }

    static public void Reset()
    {
        idsSetup = false;
    }

    static public void Setup()
    {
        if (!idsSetup)
        {
            heightmapID = Shader.PropertyToID("Heightmap");
            heightmapPositionID = Shader.PropertyToID("Heightmap_Position");
            heightmapSizeID = Shader.PropertyToID("Heightmap_Size");
            tilingVolumeCenterID = Shader.PropertyToID("tilingVolume_center");
            tilingVolumeSizeID = Shader.PropertyToID("tilingVolume_size");
            lodTargetID = Shader.PropertyToID("lodTarget");
            alphamapID = Shader.PropertyToID("alphamap");
            alphamapSizeID = Shader.PropertyToID("alphamapSize");
            fadeCenterID = Shader.PropertyToID("fadeCenter");
            fadeDistanceID = Shader.PropertyToID("fadeDistance");

            createCountID = Shader.PropertyToID("createCount");
            createBoundsCenterID = Shader.PropertyToID("createBounds_center");
            createBoundsSizeID = Shader.PropertyToID("createBounds_size");
            createBoundsMinID = Shader.PropertyToID("createBounds_min");
            createBoundsMaxID = Shader.PropertyToID("createBounds_max");

            idsSetup = true;
        }
    }
}


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
    VFXEventAttribute eventAttr;
    Rect liveBounds;                // area that is currently fully spawned
};


[ExecuteInEditMode]
public class TerrainVFXController : MonoBehaviour
{
    public Transform lodTarget;
    public bool resetAndRespawn;
    public bool followSceneCameraInEditor;
    public float volumeSize;
    public float forwardBiasDistance;
    public float density;

    Terrain terrain;
    VisualEffect vfx;
    VFXEventAttribute eventAttr;
//     VFXEventAttribute eventAttr2;
//     VFXEventAttribute eventAttr3;
//     VFXEventAttribute eventAttr4;
    Rect liveBounds;                // area that is currently fully spawned
    Vector3 tilingVolumeCenter;
    Vector3 tilingVolumeSize;

    // Start is called before the first frame update
    void Start()
    {
        resetAndRespawn = true;
        TerrainVFXProperties.Reset();
    }

    private void OnEnable()
    {
        TerrainVFXProperties.Reset();
        TerrainCallbacks.heightmapChanged += TerrainHeightmapChangedCallback;
        TerrainCallbacks.textureChanged += TerrainTextureChangedCallback;
        resetAndRespawn = true;
    }
    private void OnDisable()
    {
        TerrainVFXProperties.Reset();
        TerrainCallbacks.heightmapChanged -= TerrainHeightmapChangedCallback;
        TerrainCallbacks.textureChanged -= TerrainTextureChangedCallback;
        resetAndRespawn = true;
    }

    void TerrainHeightmapChangedCallback(Terrain terrain, RectInt heightRegion, bool synched)
    {
        resetAndRespawn = true;
    }

    void TerrainTextureChangedCallback(Terrain terrain, string textureName, RectInt texelRegion, bool synched)
    {
        resetAndRespawn = true;
    }

    void SpawnInRect(
        VFXEventAttribute attr,
        float minX, float maxX,
        float minZ, float maxZ)
    {
        float deltaX = Mathf.Max(maxX - minX, 0.0f);
        float deltaZ = Mathf.Max(maxZ - minZ, 0.0f);
        float area = deltaX * deltaZ;

        if (area > 0.0f)
        {
            // dither the count to 'fix' rounding error for small areas
            int count = Mathf.FloorToInt(area * density + Random.value - 0.001f);

            if (count > 0)
            {
//                 attr.SetInt(createCountID, count);                                       // BUG : attributes don't seem to work
//                 attr.SetVector3(createBoundsMinID, new Vector3(minX, 0.0f, minZ));
//                 attr.SetVector3(createBoundsMaxID, new Vector3(maxX, 0.0f, maxZ));

                vfx.SetInt(TerrainVFXProperties.createCountID, count);
                vfx.SetVector3(TerrainVFXProperties.createBoundsMinID, new Vector3(minX, 0.0f, minZ));
                vfx.SetVector3(TerrainVFXProperties.createBoundsMaxID, new Vector3(maxX, 0.0f, maxZ));

//                 vfx.SetVector3(createBoundsCenterID, new Vector3(minX + deltaX * 0.5f, 0.0f, minZ + deltaZ * 0.5f));
//                 vfx.SetVector3(createBoundsSizeID, new Vector3(deltaX, 0.0f, deltaZ));

                vfx.SendEvent("CreateInBounds", null);
//                vfx.Simulate(0.0f, 1);                  // HACK: workaround for one event per frame limit -- not necessary with the incremental update algorithm
            }
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
            vfx.Reinit();
            SpawnInRect(
                eventAttr,
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
        float clippedWidth = Mathf.Max(clippedBounds.xMax - clippedBounds.xMin, 0.0f);
        float clippedHeight = Mathf.Max(clippedBounds.yMax - clippedBounds.yMin, 0.0f);
        float clippedArea = clippedWidth * clippedHeight;

        // if clipping results in a significantly reduced area
        float liveArea = liveBounds.width * liveBounds.height;
        if (clippedArea < 0.80f * liveArea)
        {
            // first do a culling pass to free up particles
//            vfx.SetVector3(TerrainVFXProperties.tilingVolumeCenterID, new Vector3(clippedBounds.center.x, 0.0f, clippedBounds.center.y));
//            vfx.SetVector3(TerrainVFXProperties.tilingVolumeSizeID, new Vector3(clippedBounds.width, 2000.0f, clippedBounds.height));
            vfx.Simulate(0.0f, 1);
//            vfx.SetVector3(TerrainVFXProperties.tilingVolumeCenterID, tilingVolumeCenter);
//            vfx.SetVector3(TerrainVFXProperties.tilingVolumeSizeID, tilingVolumeSize);
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
                        eventAttr,
                        spawnBounds.xMin, liveBounds.xMin,
                        liveBounds.yMin, liveBounds.yMax);

                    liveBounds.xMin = spawnBounds.xMin;
                }
                else
                {
                    // spawn along xMax, and last.xMax to indicate we covered it
                    SpawnInRect(
                        eventAttr,
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
                        eventAttr,
                        liveBounds.xMin, liveBounds.xMax,
                        spawnBounds.yMin, liveBounds.yMin);

                    liveBounds.yMin = spawnBounds.yMin;
                }
                else
                {
                    // spawn along yMax, and last.yMax to indicate we covered it
                    SpawnInRect(
                        eventAttr,
                        liveBounds.xMin, liveBounds.xMax,
                        liveBounds.yMax, spawnBounds.yMax);

                    liveBounds.yMax = spawnBounds.yMax;
                }
            }
        }

        /*
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

    // Update is called once per frame
    void Update()
    {
        if (vfx == null)
        {
            vfx = GetComponent<VisualEffect>();
        }

        if (terrain == null)
        {
            terrain = GetComponent<Terrain>();
        }

        if ((eventAttr == null) && (vfx != null))
        {
            eventAttr = vfx.CreateVFXEventAttribute();
//             eventAttr2 = vfx.CreateVFXEventAttribute();
//             eventAttr3 = vfx.CreateVFXEventAttribute();
//             eventAttr4 = vfx.CreateVFXEventAttribute();
        }

        // compute tiling volume
        Transform lodTransform = lodTarget;
#if UNITY_EDITOR
        if (followSceneCameraInEditor && Application.isEditor && !Application.isPlaying)
        {
            lodTransform = SceneView.lastActiveSceneView.camera.transform;
        }
#endif
        tilingVolumeCenter = lodTransform.position + lodTransform.forward * forwardBiasDistance;
        tilingVolumeSize = new Vector3(volumeSize, 2000.0f, volumeSize);

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

            resetAndRespawn = false;
        }

        if ((vfx != null) && (terrain != null))
        {
            TerrainVFXProperties.Setup();

            // terrain.terrainData.alphamapTextures[0]

            vfx.SetTexture(TerrainVFXProperties.heightmapID, terrain.terrainData.heightmapTexture);
            vfx.SetVector3(TerrainVFXProperties.heightmapPositionID, terrain.transform.position);

            Vector3 size = terrain.terrainData.size;
            size.y *= 2.0f;     // faked because our heightmap is only [0-0.5]
            vfx.SetVector3(TerrainVFXProperties.heightmapSizeID, size);

            vfx.SetTexture(TerrainVFXProperties.alphamapID, terrain.terrainData.alphamapTextures[0]);
            vfx.SetVector4(TerrainVFXProperties.alphamapSizeID, new Vector4(1.0f, 0.4f, 0.1f, 0.0f));

            if (vfx.HasVector3(TerrainVFXProperties.lodTargetID))
                vfx.SetVector3(TerrainVFXProperties.lodTargetID, tilingVolumeCenter);

            vfx.SetVector3(TerrainVFXProperties.tilingVolumeCenterID, tilingVolumeCenter);
            vfx.SetVector3(TerrainVFXProperties.tilingVolumeSizeID, tilingVolumeSize);

            vfx.SetVector3(TerrainVFXProperties.fadeCenterID, lodTransform.position);
            vfx.SetFloat(TerrainVFXProperties.fadeDistanceID, volumeSize * 0.5f + forwardBiasDistance * 0.5f);

            UpdateLiveBounds(targetBounds);
        }
    }
}

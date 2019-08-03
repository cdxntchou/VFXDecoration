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

    public void Update(TerrainVFXController controller, Terrain terrain, Transform lodTransform, bool resetAndRespawn)
    {
        if ((vfxPrefab == null) || (terrain == null))
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

        if ((vfx != null) && (terrain != null))
        {
            // terrain.terrainData.alphamapTextures[0]

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


[ExecuteInEditMode]
public class TerrainVFXController : MonoBehaviour
{
    // serialized settings and UI
    public Transform lodTarget;
    public bool resetAndRespawn;
    public bool followSceneCameraInEditor;
    public float volumeSize;
    public float forwardBiasDistance;
    public float density;

    [SerializeField]
    public TerrainVFXType vfx0;

    [SerializeField]
    public TerrainVFXType vfx1;

    // runtime state
    Terrain terrain;

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

    // Update is called once per frame
    void Update()
    {
        // compute tiling volume
        Transform lodTransform = lodTarget;
#if UNITY_EDITOR
        if (followSceneCameraInEditor && Application.isEditor && !Application.isPlaying)
        {
            lodTransform = SceneView.lastActiveSceneView.camera.transform;
        }
#endif

        if (terrain == null)
        {
            terrain = GetComponent<Terrain>();
        }
        TerrainVFXProperties.Setup();

        vfx0.Update(this, terrain, lodTransform, resetAndRespawn);
        vfx1.Update(this, terrain, lodTransform, resetAndRespawn);
        resetAndRespawn = false;
    }
}

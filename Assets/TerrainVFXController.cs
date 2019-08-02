using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEngine.Experimental.TerrainAPI;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    VFXEventAttribute eventAttr2;
    VFXEventAttribute eventAttr3;
    VFXEventAttribute eventAttr4;
    Rect liveBounds;                // area that is currently fully spawned

    // prop ids
    bool idsSet = false;
    int heightmapID;
    int heightmapPositionID;
    int heightmapSizeID;
    int tilingVolumeCenterID;
    int tilingVolumeSizeID;
    int lodTargetID;
    int alphamapID;
    int alphamapMaskID;
    int fadeCenterID;
    int fadeDistanceID;
    int createCountID;
    int createBoundsCenterID;
    int createBoundsSizeID;
    int createBoundsMinID;
    int createBoundsMaxID;
    //    int destroyBoundsCenterID;
    //    int destroyBoundsSizeID;

    // Start is called before the first frame update
    void Start()
    {
        resetAndRespawn = true;
        idsSet = false;
        liveBounds.Set(0.0f, 0.0f, 0.0f, 0.0f);
    }

    private void OnEnable()
    {
        idsSet = false;
        liveBounds.width = 0.0f;
        liveBounds.height = 0.0f;

        TerrainCallbacks.heightmapChanged += TerrainHeightmapChangedCallback;
        TerrainCallbacks.textureChanged += TerrainTextureChangedCallback;
        resetAndRespawn = true;
    }
    private void OnDisable()
    {
        idsSet = false;
        liveBounds.width = 0.0f;
        liveBounds.height = 0.0f;

        TerrainCallbacks.heightmapChanged -= TerrainHeightmapChangedCallback;
        TerrainCallbacks.textureChanged -= TerrainTextureChangedCallback;
        resetAndRespawn = true;
    }

    void TerrainHeightmapChangedCallback(Terrain terrain, RectInt heightRegion, bool synched)
    {
        Debug.Log("Heightmap changed");
        resetAndRespawn = true;
    }

    void TerrainTextureChangedCallback(Terrain terrain, string textureName, RectInt texelRegion, bool synched)
    {
        Debug.Log("Texture changed");
        resetAndRespawn = true;
    }

    void SetupIDS()
    {
        if (!idsSet)
        {
            heightmapID = Shader.PropertyToID("Heightmap");
            heightmapPositionID = Shader.PropertyToID("Heightmap_Position");
            heightmapSizeID = Shader.PropertyToID("Heightmap_Size");
            tilingVolumeCenterID = Shader.PropertyToID("tilingVolume_center");
            tilingVolumeSizeID = Shader.PropertyToID("tilingVolume_size");
            lodTargetID = Shader.PropertyToID("lodTarget");
            alphamapID = Shader.PropertyToID("alphamap");
            alphamapMaskID = Shader.PropertyToID("alphamapMask");
            fadeCenterID = Shader.PropertyToID("fadeCenter");
            fadeDistanceID = Shader.PropertyToID("fadeDistance");

            createCountID = Shader.PropertyToID("createCount");
            createBoundsCenterID = Shader.PropertyToID("createBounds_center");
            createBoundsSizeID = Shader.PropertyToID("createBounds_size");
            createBoundsMinID = Shader.PropertyToID("createBounds_min");
            createBoundsMaxID = Shader.PropertyToID("createBounds_max");

//            destroyBoundsCenterID = Shader.PropertyToID("destroyBounds_center");
//            destroyBoundsSizeID = Shader.PropertyToID("destroyBounds_size");

            idsSet = true;
        }
    }

    void ResetAndRespawn()
    { 
    }

    void SpawnInRect(
        VFXEventAttribute attr,
        float minX, float maxX,
        float minZ, float maxZ, int pass = 1)
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
                attr.SetInt(createCountID, count);
                attr.SetVector3(createBoundsMinID, new Vector3(minX, 0.0f, minZ));
                attr.SetVector3(createBoundsMaxID, new Vector3(maxX, 0.0f, maxZ));

//                 vfx.SetInt(createCountID, count);
//                 vfx.SetVector3(createBoundsCenterID, new Vector3(minX + deltaX * 0.5f, 0.0f, minZ + deltaZ * 0.5f));
//                 vfx.SetVector3(createBoundsSizeID, new Vector3(deltaX, 0.0f, deltaZ));

                vfx.SendEvent("CreateInBounds", attr);
                vfx.Simulate(0.0f, 1);                  // HACK: workaround for one event per frame limit
            }
        }
    }

    void SpawnForNewBoundsHAX(Rect newBounds)
    {
        Rect spawnBounds = newBounds;
        if ((spawnBounds.xMin < liveBounds.xMin) ||
            (spawnBounds.yMin < liveBounds.yMin) ||
            (spawnBounds.xMax > liveBounds.xMax) ||
            (spawnBounds.yMax > liveBounds.yMax))
        {
            // spawn within spawnBounds, but kill any particles in lastBounds
            float deltaX = spawnBounds.width;
            float deltaZ = spawnBounds.height;
            float area = deltaX * deltaZ;
            if (area > 0.0f)
            {
                // dither the count to work around rounding error
                int count = Mathf.FloorToInt(area * density + Random.value - 0.001f);

                if (count > 0)
                {
//                     eventAttr.SetInt(createCountID, count);
//                     eventAttr.SetVector3(createBoundsCenterID, new Vector3(spawnBounds.center.x, 0.0f, spawnBounds.center.y));
//                     eventAttr.SetVector3(createBoundsSizeID, new Vector3(deltaX, 0.0f, deltaZ));

                    vfx.SetInt(createCountID, count);
                    vfx.SetVector3(createBoundsCenterID, new Vector3(spawnBounds.center.x, 0.0f, spawnBounds.center.y));
                    vfx.SetVector3(createBoundsSizeID, new Vector3(deltaX, 0.0f, deltaZ));

//                     vfx.SetVector3(destroyBoundsCenterID, new Vector3(liveBounds.center.x, 0.0f, liveBounds.center.y));
//                     vfx.SetVector3(destroyBoundsSizeID, new Vector3(liveBounds.width, 2000.0f, liveBounds.height));

                    vfx.SendEvent("CreateInBounds", null);
                }
            }
        }
        liveBounds = newBounds;
    }

    void SpawnForNewBounds(Rect newBounds)
    {
        Rect spawnBounds = newBounds;

        // assuming we can only do one spawn, figure out the best border to spawn along...
        float xMinDelta = liveBounds.xMin - spawnBounds.xMin;
        float xMaxDelta = spawnBounds.xMax - liveBounds.xMax;
        float yMinDelta = liveBounds.yMin - spawnBounds.yMin;
        float yMaxDelta = spawnBounds.yMax - liveBounds.yMax;
        float xDelta = Mathf.Max(xMinDelta, xMaxDelta);
        float yDelta = Mathf.Max(yMinDelta, yMaxDelta);
        float delta = Mathf.Max(xDelta, yDelta);

        // TODO: if the overlap region is empty, we should just nuke and pave...
        // doing the incremental expansion algorithm below is potentially much worse, especially if the bounds have moved a long distance

        if (delta > 0.0f)
        {
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
            eventAttr2 = vfx.CreateVFXEventAttribute();
            eventAttr3 = vfx.CreateVFXEventAttribute();
            eventAttr4 = vfx.CreateVFXEventAttribute();
        }

        Transform lodTransform = lodTarget;
#if UNITY_EDITOR
        if (followSceneCameraInEditor && Application.isEditor && !Application.isPlaying)
        {
            lodTransform = SceneView.lastActiveSceneView.camera.transform;
        }
#endif

        if (resetAndRespawn)
        {
            SetupIDS();

            vfx.Reinit();
            liveBounds.Set(
                lodTransform.position.x, lodTransform.position.z, 0.0f, 0.0f);

            resetAndRespawn = false;
        }

        if ((vfx != null) && (terrain != null))
        {
            SetupIDS();

            // terrain.terrainData.alphamapTextures[0]

            vfx.SetTexture(heightmapID, terrain.terrainData.heightmapTexture);
            vfx.SetVector3(heightmapPositionID, terrain.transform.position);

            Vector3 size = terrain.terrainData.size;
            size.y *= 2.0f;     // faked because our heightmap is only [0-0.5]
            vfx.SetVector3(heightmapSizeID, size);

            vfx.SetTexture(alphamapID, terrain.terrainData.alphamapTextures[0]);
            vfx.SetVector4(alphamapMaskID, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));

            Vector3 tilingVolumeCenter = lodTransform.position + lodTransform.forward * forwardBiasDistance;
            Vector3 tilingVolumeSize = new Vector3(volumeSize, 200.0f, volumeSize);
            if (vfx.HasVector3(lodTargetID))
                vfx.SetVector3(lodTargetID, tilingVolumeCenter);

            vfx.SetVector3(tilingVolumeCenterID, tilingVolumeCenter);
            vfx.SetVector3(tilingVolumeSizeID, tilingVolumeSize);

            vfx.SetVector3(fadeCenterID, lodTransform.position);
            vfx.SetFloat(fadeDistanceID, volumeSize * 0.5f + forwardBiasDistance * 0.5f);

            // now spawn new areas
            Rect newBounds = new Rect(
                tilingVolumeCenter.x - tilingVolumeSize.x * 0.5f,
                tilingVolumeCenter.z - tilingVolumeSize.z * 0.5f,
                tilingVolumeSize.x,
                tilingVolumeSize.z);

            SpawnForNewBounds(newBounds);
        }
    }
}

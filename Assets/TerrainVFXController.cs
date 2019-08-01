using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class TerrainVFXController : MonoBehaviour
{
    public Transform lodTarget;
    public float forwardBiasDistance;
    public bool spawnTrigger;
    public bool followSceneCameraInEditor;
    public float density;

    Terrain terrain;
    VisualEffect vfx;
    VFXEventAttribute eventAttr;
    Rect lastBounds;

    // prop ids
    bool idsSet = false;
    int spawnTriggerID;
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
    int destroyBoundsCenterID;
    int destroyBoundsSizeID;

    // Start is called before the first frame update
    void Start()
    {
        spawnTrigger = true;
    }

    private void OnEnable()
    {
        idsSet = false;
    }
    private void OnDisable()
    {
        idsSet = false;
    }

    void SetupIDS()
    {
        if (!idsSet)
        {
            spawnTriggerID = Shader.PropertyToID("SpawnTrigger");
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
            destroyBoundsCenterID = Shader.PropertyToID("destroyBounds_center");
            destroyBoundsSizeID = Shader.PropertyToID("destroyBounds_size");

            idsSet = true;
        }
    }

    void SpawnInRect(
        float minX, float maxX,
        float minZ, float maxZ, int pass = 1)
    {
        float deltaX = maxX - minX;
        float deltaZ = maxZ - minZ;
        float area = deltaX * deltaZ;

        if (area > 0.0f)
        {
            // dither the count to work around rounding error
            int count = Mathf.FloorToInt(area * density + Random.value - 0.001f);

            if (count > 0)
            {
                eventAttr.SetInt(createCountID, count);
                eventAttr.SetVector3(createBoundsCenterID, new Vector3(minX + deltaX * 0.5f, 0.0f, minZ + deltaZ * 0.5f));
                eventAttr.SetVector3(createBoundsSizeID, new Vector3(deltaX, 0.0f, deltaZ));

                vfx.SetInt(createCountID, count);
                vfx.SetVector3(createBoundsCenterID, new Vector3(minX + deltaX * 0.5f, 0.0f, minZ + deltaZ * 0.5f));
                vfx.SetVector3(createBoundsSizeID, new Vector3(deltaX, 0.0f, deltaZ));

                if (pass == 2)
                    vfx.SendEvent("CreateInBounds2", eventAttr);
                else
                    vfx.SendEvent("CreateInBounds", eventAttr);
            }
        }
    }

    void SpawnForNewBoundsHAX(Rect newBounds)
    {
        Rect spawnBounds = newBounds;
        if ((spawnBounds.xMin < lastBounds.xMin) ||
            (spawnBounds.yMin < lastBounds.yMin) ||
            (spawnBounds.xMax > lastBounds.xMax) ||
            (spawnBounds.yMax > lastBounds.yMax))
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

                    vfx.SetVector3(destroyBoundsCenterID, new Vector3(lastBounds.center.x, 0.0f, lastBounds.center.y));
                    vfx.SetVector3(destroyBoundsSizeID, new Vector3(lastBounds.width, 2000.0f, lastBounds.height));

                    vfx.SendEvent("CreateInBounds", eventAttr);
                }
            }
        }
    }

    void SpawnForNewBounds(Rect newBounds)
    {
        Rect spawnBounds = newBounds;

        // first spawn along +/- X border
        if (spawnBounds.xMin < lastBounds.xMin)
        {
            // spawn between spawn.xMin and last.xMin
            SpawnInRect(
                spawnBounds.xMin, lastBounds.xMin,
                spawnBounds.yMin, spawnBounds.yMax);

            // clip away the spawned region from the spawnBounds
            spawnBounds.xMin = lastBounds.xMin;
        }
        if (spawnBounds.xMax > lastBounds.xMax)
        {
            // spawn between last.xMax and spawn.xMax
            SpawnInRect(
                lastBounds.xMax, spawnBounds.xMax,
                spawnBounds.yMin, spawnBounds.yMax);

            // clip away the spawned region from the spawnBounds
            spawnBounds.xMax = lastBounds.xMax;
        }

        // spawn aloong +/- Y border
        if (spawnBounds.yMin < lastBounds.yMin)
        {
            // spawn between spawn.yMin and last.yMin
            SpawnInRect(
                spawnBounds.xMin, spawnBounds.xMax,
                spawnBounds.yMin, lastBounds.yMin, 2);
        }
        if (spawnBounds.yMax > lastBounds.yMax)
        {
            // spawn between last.yMax and spawn.yMax
            SpawnInRect(
                spawnBounds.xMin, spawnBounds.xMax,
                lastBounds.yMax, spawnBounds.yMax, 2);
        }
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

            Transform lodTransform = lodTarget;
#if UNITY_EDITOR
            if (followSceneCameraInEditor && Application.isEditor && !Application.isPlaying)
            {
                lodTransform = SceneView.lastActiveSceneView.camera.transform;
            }
#endif

            Vector3 tilingVolumeCenter = lodTransform.position + lodTransform.forward * forwardBiasDistance;
            Vector3 tilingVolumeSize = new Vector3(10.0f, 200.0f, 10.0f);
            if (vfx.HasVector3(lodTargetID))
                vfx.SetVector3(lodTargetID, tilingVolumeCenter);

            vfx.SetVector3(tilingVolumeCenterID, tilingVolumeCenter);
            vfx.SetVector3(tilingVolumeSizeID, tilingVolumeSize);

            vfx.SetVector3(fadeCenterID, lodTransform.position);
            vfx.SetFloat(fadeDistanceID, 160.0f * 0.5f + forwardBiasDistance * 0.5f);

            // now spawn new areas
            Rect newBounds = new Rect(
                tilingVolumeCenter.x - tilingVolumeSize.x * 0.5f,
                tilingVolumeCenter.z - tilingVolumeSize.z * 0.5f,
                tilingVolumeSize.x,
                tilingVolumeSize.z);

            SpawnForNewBoundsHAX(newBounds);

            lastBounds = newBounds;
        }

        if (spawnTrigger && (eventAttr != null))
        {
            SetupIDS();
//             eventAttr.SetVector3("grenadeVolume_center", new Vector3(0.0f, 0.0f, 0.0f));
//             eventAttr.SetFloat("grenadeVolume_radius", 1.0f);
//             vfx.SendEvent("GrenadeExplosion", eventAttr);

            spawnTrigger = false;
        }
    }
}

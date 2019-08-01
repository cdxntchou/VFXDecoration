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
            idsSet = true;
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
            Vector3 tilingVolumeSize = new Vector3(160.0f, 200.0f, 160.0f);
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

            lastBounds = newBounds;
        }

        if (spawnTrigger && (eventAttr != null))
        {
            SetupIDS();
            eventAttr.SetVector3("grenadeVolume_center", new Vector3(0.0f, 0.0f, 0.0f));
            eventAttr.SetFloat("grenadeVolume_radius", 1.0f);
            vfx.SendEvent("GrenadeExplosion", eventAttr);

            spawnTrigger = false;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEngine.Experimental.TerrainAPI;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif


[ExecuteInEditMode]
public class TerrainVFXController : MonoBehaviour
{
    // serialized settings and UI
    public Transform lodTarget;
    public bool resetAndRespawn;
    public bool continuousRespawn;
    public bool followSceneCameraInEditor;
    public float volumeSize;
    public float forwardBiasDistance;
    public float density;
    public int terrainGroupingID;       // TODO: or should we do a grouping id mask?

    // TODO: this should be a list of vfx -- and it should just look at children to find the vfx elements...
    [SerializeField]
    public TerrainVFXType vfx0;

    [SerializeField]
    public TerrainVFXType vfx1;

    // runtime state
    TerrainMap terrainMap;
    TerrainVFXState[] vfxStates;

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
        // TODO: have to rebuild terrain map whenever the active terrain set changes
        if (terrainMap == null)
        {
            // find a terrain with the correct grouping ID
            Terrain originTerrain = null;
            foreach (Terrain t in Terrain.activeTerrains)
            {
                if (t.groupingID == terrainGroupingID)
                {
                    originTerrain = t;
                    break;
                }
            }

            if (originTerrain)
            {
                terrainMap = TerrainMap.CreateFromPlacement(originTerrain, null, true);

                if (terrainMap.m_errorCode != TerrainMap.ErrorCode.OK)
                {
                    terrainMap = null;
                }
            }
        }

        if (vfxStates == null)
        {
            vfxStates = GetComponentsInChildren<TerrainVFXState>();

            // double check all VisualEffects have TerrainVFXStates attached
            VisualEffect[] visualFX = GetComponentsInChildren<VisualEffect>();
            foreach (VisualEffect vfx in visualFX)
            {
                TerrainVFXState vfxstate = vfx.GetComponent<TerrainVFXState>();
                if (vfxstate == null)
                {
                    Debug.LogError("Terrain VFX Object '" + vfx.gameObject.name + "' has a VisualEffect but no TerrainVFXState component, please add one");
                }
            }
        }

        if (continuousRespawn)
            resetAndRespawn = true;

        // compute tiling volume
        Transform lodTransform = lodTarget;
#if UNITY_EDITOR
        if (followSceneCameraInEditor && Application.isEditor && !Application.isPlaying)
        {
            lodTransform = SceneView.lastActiveSceneView.camera.transform;
        }
#endif

//         if (terrain == null)
//         {
//             terrain = GetComponent<Terrain>();
//         }
        TerrainVFXProperties.Setup();

//         vfx0.Update(this, terrainMap, lodTransform, resetAndRespawn);
//         vfx1.Update(this, terrainMap, lodTransform, resetAndRespawn);

        foreach(TerrainVFXState vfxState in vfxStates)
        {
            vfxState.TerrainVFXUpdate(this, terrainMap, lodTransform, resetAndRespawn);
        }
        resetAndRespawn = false;
    }
}

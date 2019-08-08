using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEngine.Experimental.TerrainAPI;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif


#if UNITY_EDITOR
// hack -- VFX systems are reset on save, so we need some way to force them to respawn
public class FileModificationWarning : UnityEditor.AssetModificationProcessor
{
    static string[] OnWillSaveAssets(string[] paths)
    {
        UnityEngine.Object[] controllers = UnityEngine.Object.FindObjectsOfType<TerrainVFXController>();
        foreach (var controller in controllers)
        {
            TerrainVFXController vfxController = controller as TerrainVFXController;
            vfxController.resetAndRespawn = true;
        }
        return paths;
    }
}
#endif // UNITY_EDITOR

[ExecuteInEditMode]
public class TerrainVFXController : MonoBehaviour
{
    // serialized settings and UI
    public Transform lodTarget;
    public bool resetAndRespawn;
    public bool continuousRespawnInEditor;
    public bool followSceneCameraInEditor;
    public bool tick;
    public bool alwaysTick;
    public int terrainGroupingID;       // TODO: or should we do a grouping id mask?
    public int lastUndoGroup;

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
// #if UNITY_EDITOR         // this isn't working
//         Undo.undoRedoPerformed += TerrainUndoCallback;  // hack to workaround TerrainCallbacks not catching undo/redo modifications
// #endif // UNITY_EDITOR
        resetAndRespawn = true;
    }
    private void OnDisable()
    {
        TerrainVFXProperties.Reset();
        TerrainCallbacks.heightmapChanged -= TerrainHeightmapChangedCallback;
        TerrainCallbacks.textureChanged -= TerrainTextureChangedCallback;
// #if UNITY_EDITOR
//         Undo.undoRedoPerformed -= TerrainUndoCallback;  // hack to workaround TerrainCallbacks not catching undo/redo modifications
// #endif // UNITY_EDITOR
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

//     void TerrainUndoCallback()
//     {
//         Debug.Log("Undo Reset");
//         resetAndRespawn = true;
//     }

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

#if UNITY_EDITOR
        if (continuousRespawnInEditor && Application.isEditor && !Application.isPlaying)
            resetAndRespawn = true;
#endif

        // compute tiling volume
        Transform lodTransform = lodTarget;
#if UNITY_EDITOR
        if (followSceneCameraInEditor && Application.isEditor && !Application.isPlaying)
        {
            lodTransform = SceneView.lastActiveSceneView.camera.transform;
        }

        int undoGroup = Undo.GetCurrentGroup();
        if (undoGroup != lastUndoGroup)
        {
            resetAndRespawn = true;
        }
        lastUndoGroup = undoGroup;
#endif

        TerrainVFXProperties.Setup();

        if (tick || alwaysTick)
        {
            foreach (TerrainVFXState vfxState in vfxStates)
            {
                vfxState.TerrainVFXUpdate(this, terrainMap, lodTransform, resetAndRespawn);
            }
            tick = false;
        }
        resetAndRespawn = false;
    }
}

// using System.Collections;
// using System.Collections.Generic;
using UnityEngine;
// using UnityEngine.Experimental.VFX;
// using UnityEngine.Experimental.TerrainAPI;
// using System;
// #if UNITY_EDITOR
// using UnityEditor;
// #endif


internal static class TerrainVFXProperties
{
    static private bool idsSetup = false;

    // prop ids
    static public int heightmap               { get; private set; }
    static public int heightmapResolution     { get; private set; }
    static public int normalmap               { get; private set; }
    static public int normalmapResolution     { get; private set; }
    static public int heightmapPosition       { get; private set; }
    static public int heightmapSize           { get; private set; }
    static public int tilingVolumeCenter      { get; private set; }
    static public int tilingVolumeSize        { get; private set; }
    static public int lodTarget               { get; private set; }
    static public int alphamap                { get; private set; }
    static public int alphamapResolution      { get; private set; }
    static public int alphamapSize            { get; private set; }
    static public int fadeCenter              { get; private set; }
    static public int fadeDistance            { get; private set; }
    static public int createCount             { get; private set; }
    static public int createParams            { get; private set; }
    static public int createBoundsMin         { get; private set; }
    static public int createBoundsMax         { get; private set; }
    static public int createBoundsScale       { get; private set; }
    static public int createPattern           { get; private set; }
    static public int createPatternResolution { get; private set; }
    static public int createPatternTransform  { get; private set; }

    static public void Reset()
    {
        idsSetup = false;
    }

    static public void Setup()
    {
        if (!idsSetup)
        {
            heightmap = Shader.PropertyToID("Heightmap");
            heightmapResolution = Shader.PropertyToID("terrain_heightmap_resolution");
            normalmap = Shader.PropertyToID("terrain_normalmap");
            normalmapResolution = Shader.PropertyToID("terrain_normalmap_resolution");
            heightmapPosition = Shader.PropertyToID("Heightmap_Position");
            heightmapSize = Shader.PropertyToID("Heightmap_Size");
            tilingVolumeCenter = Shader.PropertyToID("tilingVolume_center");
            tilingVolumeSize = Shader.PropertyToID("tilingVolume_size");
            lodTarget = Shader.PropertyToID("lodTarget");
            alphamap = Shader.PropertyToID("alphamap");
            alphamapResolution = Shader.PropertyToID("terrain_alphamap_resolution");
            alphamapSize = Shader.PropertyToID("alphamapSize");
            fadeCenter = Shader.PropertyToID("fadeCenter");
            fadeDistance = Shader.PropertyToID("fadeDistance");

            createCount = Shader.PropertyToID("createCount");
            createParams = Shader.PropertyToID("createParams");
            createBoundsMin = Shader.PropertyToID("createBounds_min");
            createBoundsMax = Shader.PropertyToID("createBounds_max");
            createBoundsScale = Shader.PropertyToID("createBoundsScale");
            createPattern = Shader.PropertyToID("createPattern");
            createPatternResolution = Shader.PropertyToID("createPatternResolution");
            createPatternTransform = Shader.PropertyToID("createPatternTransform");

            idsSetup = true;
        }
    }
}

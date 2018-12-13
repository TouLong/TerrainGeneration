﻿using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

public class TerrainGenerator : MonoBehaviour
{
    [HideInInspector]
    public MapSetting setting;
    Material mapMaterial;
    public List<TerrainChunk> terrainChunkList = new List<TerrainChunk>();

    [HideInInspector]
    public Vector2 noiseMinMax;
    [HideInInspector]
    public Vector2 mapMinMax;
    [HideInInspector]
    public Texture2D texture;
    GameObject terrain;
    GameObject mapObject;
    public GameObject testPlacement;

    private bool Initialization()
    {
        if (mapMaterial == null)
            if (Shader.Find("Custom/Terrain") != null)
                mapMaterial = new Material(Shader.Find("Custom/Terrain"));
            else
            {
                Debug.Log("揾吾到著色器");
                return false;
            }
        if (setting == null)
            if (GetComponent<MapSetting>() != null)
                setting = GetComponent<MapSetting>();
            else
            {
                Debug.Log("冇MapSetting");
                return false;
            }
        
        return true;
    }

    public void GenerateChunks()
    {
        if (!Initialization())
        {
            return;
        }
        ClearChunk();
        terrain = new GameObject("Terrain");
        terrain.transform.parent = transform;
        terrain.transform.localPosition = Vector3.zero;
        List<Vector2> chunkNoiseMinMax = new List<Vector2>();
        List<Vector2> chunkMapMinMax = new List<Vector2>();
        int meshSize = setting.chunkSideLength;
        for (int x = 0; x < setting.mapDimension; x++)
        {
            for (int y = 0; y < setting.mapDimension; y++)
            {
                Vector2 chunkPos = new Vector2((x - (setting.mapDimension - 1) / 2f) * meshSize, (y - (setting.mapDimension - 1) / 2f) * meshSize);
                TerrainChunk newChunk = new TerrainChunk(chunkPos, setting, terrain.transform, mapMaterial);
                terrainChunkList.Add(newChunk);
                chunkNoiseMinMax.Add(newChunk.noiseMinMax);
            }
        }
        noiseMinMax = new Vector2(chunkNoiseMinMax.Min(x => x.x), chunkNoiseMinMax.Max(x => x.y));
        foreach(TerrainChunk chunk in terrainChunkList)
        {
            chunk.Create(noiseMinMax);
            chunkMapMinMax.Add(chunk.mapMinMax);
        }
        mapMinMax = new Vector2(chunkMapMinMax.Min(x => x.x), chunkMapMinMax.Max(x => x.y));
        terrain.transform.localScale = new Vector3(setting.mapSize, 1, setting.mapSize);
        texture = MapImage.Generate(setting, terrainChunkList.Select(x=>x.mapHeight).ToList(), setting.mapSize);
        UpdateMaterial();
    }

    public void ClearChunk()
    {
        terrainChunkList.Clear();
        if (terrain != null)
        {
            while (terrain.transform.childCount != 0)
                DestroyImmediate(terrain.transform.GetChild(0).gameObject);
            DestroyImmediate(terrain);
        }
        if (mapObject != null)
        {
            while (mapObject.transform.childCount != 0)
                DestroyImmediate(mapObject.transform.GetChild(0).gameObject);
            DestroyImmediate(mapObject);
        }
    }

    public void UpdateMaterial()
    {
        mapMaterial.SetInt("layerCount", setting.layers.Count);
        mapMaterial.SetColorArray("baseColors", setting.layers.Select(x => x.color).ToArray());
        mapMaterial.SetFloatArray("baseStartHeights", setting.layers.Select(x => x.height).ToArray());
        mapMaterial.SetFloatArray("baseBlends", setting.layers.Select(x => x.blendStrength).ToArray());

        mapMaterial.SetFloat("minHeight", mapMinMax.x + terrain.transform.position.y);
        mapMaterial.SetFloat("maxHeight", mapMinMax.y + terrain.transform.position.y);
    }

    public void PDS()
    {
        if (mapObject == null)
            mapObject = new GameObject("Map Object");
        List<Vector2> points = PoissonDiscSampling.GeneratePoints(2.5f, new Vector2(setting.mapSideLength, setting.mapSideLength));
        for(int i = 0; i < points.Count; i++)
        {
            GameObject newGO = Instantiate(testPlacement, mapObject.transform);
            newGO.transform.position = new Vector3(points[i].x, setting.heightScale, points[i].y);
        }
    }

    public float[] CountHeightLayer()
    {
        List<float> layerHeight = new List<float>();
        
        for (int i = 0; i < setting.layers.Count; i++)
        {
            layerHeight.Add(setting.layers[i].height * (mapMinMax.y - mapMinMax.x) + mapMinMax.x);
        }

        float[] heightCount = new float[setting.layers.Count];
        foreach (TerrainChunk chunk in terrainChunkList)
        {
            float[,] height = chunk.mapHeight.values;
            int size = height.GetLength(0);
            for (int x = 0; x < size; x++)
            {
                for(int y = 0; y < size; y++)
                {
                    float v = height[x, y];
                    for (int i = heightCount.Length - 1; i >= 0; i--)
                    {
                        if (v >= layerHeight[i]) 
                        {
                            heightCount[i]++;
                            break;
                        }
                    }
                }
            }
        }

        float heightCountSum = heightCount.Sum();
        for (int i = 0; i < heightCount.Length; i++)
        {
            layerHeight[i] = heightCount[i] / heightCountSum;
        }
        return layerHeight.ToArray();
    }
}

[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
    AnimationCurve heightCurve;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        TerrainGenerator terrain = (TerrainGenerator)target;

        EditorGUILayout.LabelField(string.Format("地圖尺寸 : {0}x{0}  地圖高度 : {1}~{2}  Noise範圍 : {3:0.00}~{4:0.00}",
          terrain.setting.mapSideLength, terrain.mapMinMax.x, terrain.mapMinMax.y, terrain.noiseMinMax.x, terrain.noiseMinMax.y));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate"))
            terrain.GenerateChunks();
        if (GUILayout.Button("Clear"))
            terrain.ClearChunk();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Update Material"))
            terrain.UpdateMaterial();
        if (GUILayout.Button("PDS"))
            terrain.PDS();
        EditorGUILayout.EndHorizontal();

        heightCurve = terrain.setting.heightCurve;
        if (terrain.setting.layers.Count < heightCurve.keys.Length - 1)
        {
            for (int i = 0; i < heightCurve.keys.Length - 1; i++)
            {
                if (!terrain.setting.layers.Exists(x => x.height == heightCurve.keys[i].value))
                    terrain.setting.layers.Add(new MapSetting.Layer(heightCurve.keys[i].value));
            }
        }
        else if (terrain.setting.layers.Count > heightCurve.keys.Length - 1)
        {
            for (int i = 0; i < terrain.setting.layers.Count; i++)
            {
                if (!heightCurve.keys.ToList().Exists(x => x.value == terrain.setting.layers[i].height))
                    terrain.setting.layers.Remove(terrain.setting.layers[i]);
            }
        }
        EditorGUILayout.CurveField(heightCurve, GUILayout.MinHeight(100f));

        terrain.setting.layers.Sort((x, y) => { return x.height.CompareTo(y.height); });
        float[] layerHeights = terrain.CountHeightLayer();
        for (int i = 0; i < terrain.setting.layers.Count ; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.Format("區域{0}-高度:{1};佔比:{2}", i + 1, terrain.setting.layers[i].height.ToString("f2"), layerHeights[i].ToString("f2")), GUILayout.MaxWidth(160f));
            Color color = EditorGUILayout.ColorField(terrain.setting.layers[i].color, GUILayout.MinWidth(80f));
            EditorGUILayout.EndHorizontal();
            terrain.setting.layers[i].color = color;
            terrain.setting.layers[i].height = heightCurve.keys[i].value;
        }
        GUILayout.Label(terrain.texture);
    }


}

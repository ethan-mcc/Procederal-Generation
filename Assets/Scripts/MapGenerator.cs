using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class MapGenerator : MonoBehaviour
{

    public enum DrawMode { NoiseMap, ColorMap, DrawMesh};
    public DrawMode drawMode;
    public Noise.NormalizeMode normalizeMode;
    public const int mapChunkSize = 200;

    [Range(0,6)]
    public int editorPreviewLOD;

    public float noiseScale;
    public AnimationCurve meshHeightCurve;
    public bool autoUpdate;
    public int octaves;

    [Range(0, 1)]
    public float persistance;

    public float lacunarity;
    public int seed;
    public Vector2 offset;
    public float meshHeightMultiplier;
    public TerrianType[] regions;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQuene = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQuene = new Queue<MapThreadInfo<MeshData>>();

    public void DrawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);
        MapDisplay display = FindObjectOfType<MapDisplay>();

        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.ColorMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));

        }
        else if (drawMode == DrawMode.DrawMesh)
        {
            display.DrawMesh(MeshGenerator.GenerateTerrianMesh(mapData.heightMap,
                meshHeightMultiplier, meshHeightCurve, editorPreviewLOD),
                TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        }
    }

    public void RequestMapData(Vector2 center, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MapDataThread(center, callback);
        };

        new Thread(threadStart).Start(); 
    }

    void MapDataThread(Vector2 center, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(center);
        // When a thread hits this no other thread can hit it.
        lock (mapDataThreadInfoQuene)
        {
            mapDataThreadInfoQuene.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }

    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData, lod, callback);
        };

        new Thread(threadStart).Start();
    }

    void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateTerrianMesh(mapData.heightMap,
            meshHeightMultiplier, meshHeightCurve, lod);
        lock (meshDataThreadInfoQuene)
        {
            meshDataThreadInfoQuene.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    private void Update()
    {
        if(mapDataThreadInfoQuene.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQuene.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQuene.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (meshDataThreadInfoQuene.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQuene.Count; i ++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQuene.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    MapData GenerateMapData(Vector2 center)
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize,
            seed, noiseScale, octaves, persistance, lacunarity,
            center + offset, normalizeMode);

        Color[] colorMap = new Color[mapChunkSize * mapChunkSize];

        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                float currentHeight = noiseMap[x, y];
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight <= regions[i].height)
                    {
                        colorMap[y * mapChunkSize + x] = regions[i].color;
                        break;
                    }
                }
            }
        }


        return new MapData(noiseMap, colorMap);
    }

    private void OnValidate()
    {

        if (lacunarity < 1)
        {
            lacunarity = 1;
        }
        if (octaves < 0)
        {
            octaves = 0;
        }
    }

    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;

        public readonly T parameter;

        public MapThreadInfo (Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}

[System.Serializable]
public struct TerrianType
{
    public string name;
    public float height;
    public Color color;
}

public struct MapData
{
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;

    public MapData (float[,] heightMap, Color[] colorMap)
    {
        this.heightMap = heightMap;
        this.colorMap = colorMap;
    }
}

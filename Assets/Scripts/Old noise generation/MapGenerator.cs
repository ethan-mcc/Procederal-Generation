using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{

	public enum DrawMode { NoiseMap, ColorMap, Mesh, FalloffMap};
	public DrawMode drawMode;

	public Noise.NormalizeMode normalizeMode;
	public bool useFlatShading;

	[Range(0, 6)]
	public int editorPreviewLOD;
	public float noiseScale;

	public int octaves;
	[Range(0, 1)]
	public float persistance;
	public float lacunarity;

	public int seed;
	public Vector2 offset;

	public bool useFalloff;

	public float meshHeightMultiplier;
	public AnimationCurve meshHeightCurve;

	public bool autoUpdate;

	public TerrainType[] regions;
	static MapGenerator instance;

	float[,] falloffMap;

	Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
	Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    private void Awake()
    {
		falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
    }

	public static int mapChunkSize
    {
		get
        {
			if (instance == null)
            {
				instance = FindObjectOfType<MapGenerator>();
            }
			if (instance.useFlatShading)
            {
				// Flat shading uses more vertics
				return 95;
            }
			else
            {
				// No flat shading
				return 239;
            }
        }
    }

    public void DrawMapInEditor()
	{
		MapData mapData = GenerateMapData(Vector2.zero);

		MapDisplay display = FindObjectOfType<MapDisplay>();

		// NOISE MAP
		if (drawMode == DrawMode.NoiseMap)
		{
			display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
		}
		// COLOR MAP
		else if (drawMode == DrawMode.ColorMap)
		{
			display.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
		}
		// MESH
		else if (drawMode == DrawMode.Mesh)
		{
			display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap,
				meshHeightMultiplier, meshHeightCurve, editorPreviewLOD, useFlatShading),
				TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
		}
		// FALLOFF MAP
		else if (drawMode == DrawMode.FalloffMap)
        {
			display.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunkSize)));
        }
	}

	public void RequestMapData(Vector2 centre, Action<MapData> callback)
	{
		ThreadStart threadStart = delegate {
			MapDataThread(centre, callback);
		};

		new Thread(threadStart).Start();
	}

	void MapDataThread(Vector2 centre, Action<MapData> callback)
	{
		MapData mapData = GenerateMapData(centre);
		lock (mapDataThreadInfoQueue)
		{
			mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
		}
	}

	public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
	{
		ThreadStart threadStart = delegate {
			MeshDataThread(mapData, lod, callback);
		};

		new Thread(threadStart).Start();
	}

	void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
	{
		MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap,
			meshHeightMultiplier, meshHeightCurve, lod, useFlatShading);
		lock (meshDataThreadInfoQueue)
		{
			meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
		}
	}

	void Update()
	{
		if (mapDataThreadInfoQueue.Count > 0)
		{
			for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
			{
				MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
				threadInfo.callback(threadInfo.parameter);
			}
		}

		if (meshDataThreadInfoQueue.Count > 0)
		{
			for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
			{
				MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
				threadInfo.callback(threadInfo.parameter);
			}
		}
	}

	MapData GenerateMapData(Vector2 center)
	{
		// Get perlin noise from Noise.cs
		float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize + 2, mapChunkSize + 2,
			seed, noiseScale, octaves, persistance, lacunarity, center + offset,
			normalizeMode);



		Color[] colorMap = new Color[mapChunkSize * mapChunkSize];
		for (int y = 0; y < mapChunkSize; y++)
		{
			for (int x = 0; x < mapChunkSize; x++)
			{
				// Falloff map
				if (useFalloff)
                {
					noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - falloffMap[x, y]);
                }
				// Get height of current x,y pos
				float currentHeight = noiseMap[x, y];

				// Color map
				for (int i = 0; i < regions.Length; i++)
				{
					if (currentHeight >= regions[i].height)
					{
						colorMap[y * mapChunkSize + x] = regions[i].colour;
					}
					else
					{
						break;
					}
				}
			}
		}


		return new MapData(noiseMap, colorMap);
	}

	void OnValidate()
	{
		if (lacunarity < 1)
		{
			lacunarity = 1;
		}
		if (octaves < 0)
		{
			octaves = 0;
		}
		falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
	}

	struct MapThreadInfo<T>
	{
		public readonly Action<T> callback;
		public readonly T parameter;

		public MapThreadInfo(Action<T> callback, T parameter)
		{
			this.callback = callback;
			this.parameter = parameter;
		}

	}

}

[System.Serializable]
public struct TerrainType
{
	public string name;
	public float height;
	public Color colour;
}

public struct MapData
{
	public readonly float[,] heightMap;
	public readonly Color[] colorMap;

	public MapData(float[,] heightMap, Color[] colourMap)
	{
		this.heightMap = heightMap;
		this.colorMap = colourMap;
	}
}
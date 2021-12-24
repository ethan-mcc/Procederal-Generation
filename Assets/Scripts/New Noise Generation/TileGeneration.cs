using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileGeneration : MonoBehaviour
{
    // This script is attached the mesh renderer, used to show the height map
    // generated in NoiseMapGeration.cs

    [SerializeField]
    NoiseMapGeneration noiseMapGeneration;

    [SerializeField]
    private MeshRenderer tileRenderer;

    [SerializeField]
    private MeshFilter meshFilter;

    [SerializeField]
    private MeshCollider meshCollider;

    [SerializeField]
    private float mapScale;

    private void Start()
    {
        GenerateTile();
    }

    void GenerateTile ()
    {
        //Calculate tile depth and width based on the mesh vertices
        Vector3[] meshVertices = this.meshFilter.mesh.vertices;
        int tileDepth = (int)Mathf.Sqrt(meshVertices.Length);
        int tileWidth = tileDepth;

        //Calculate the offsets based on the tile position
        float[,] heightMap = this.noiseMapGeneration.GenerateNoiseMap(tileDepth, tileWidth, this.mapScale);

        // Generate a heightMap using noise
        Texture2D tileTexture = BuildTexture(heightMap);

        this.tileRenderer.material.mainTexture = tileTexture;
    }

    private Texture2D BuildTexture(float[,] heightMap)
    {
        int tileDepth = noiseMap.GetLength(0);
        int tileWidth = noiseMap.GetLength(1);

        Color[] colorMap = new Color[tileDepth * tileWidth];
        for (int zIndex = 0; zIndex < tileDepth; zIndex++)
        {
            for (int xIndex = 0; xIndex < tileWidth; xIndex++)
            {
                //Transform the 2D map index is an Array index
                int colorIndex = zIndex * tileWidth + xIndex;
                float height = heightMap[zIndex, xIndex];

                //Assign as color a shade of grey proportional to the height value
                colorMap[colorIndex] = Color.Lerp(Color.black, Color.white, height);
            }
        }

        //Create a new texture and set its pixel colors
        Texture2D tileTexture = new Texture2D(tileWidth, tileDepth);
        tileTexture.wrapMode = TextureWrapMode.Clamp;
        tileTexture.SetPixels(colorMap);
        tileTexture.Apply();

        return tileTexture;
    }
}

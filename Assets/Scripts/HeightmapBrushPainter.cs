using UnityEngine;
using UnityEditor;

public class HeightmapBrushPainter : MonoBehaviour
{
    public Terrain terrain;
    public Texture2D heightmapBrush; // Your heightmap to use as brush
    public int targetLayer = 0; // Which terrain texture layer to paint
    public float brushSize = 20f;
    public float opacity = 1f;

    public void PaintAtPosition(Vector3 worldPosition)
    {
        if (terrain == null || heightmapBrush == null) return;

        TerrainData terrainData = terrain.terrainData;

        // Convert world position to terrain-relative position
        Vector3 terrainPos = worldPosition - terrain.transform.position;

        // Convert to alphamap coordinates
        int alphamapWidth = terrainData.alphamapWidth;
        int alphamapHeight = terrainData.alphamapHeight;

        int centerX = (int)((terrainPos.x / terrainData.size.x) * alphamapWidth);
        int centerZ = (int)((terrainPos.z / terrainData.size.z) * alphamapHeight);

        // Calculate brush size in alphamap coordinates
        int brushSizeInAlphamap = (int)((brushSize / terrainData.size.x) * alphamapWidth);

        // Get current alphamap data
        int startX = Mathf.Max(0, centerX - brushSizeInAlphamap / 2);
        int startZ = Mathf.Max(0, centerZ - brushSizeInAlphamap / 2);
        int width = Mathf.Min(alphamapWidth - startX, brushSizeInAlphamap);
        int height = Mathf.Min(alphamapHeight - startZ, brushSizeInAlphamap);

        float[,,] alphamap = terrainData.GetAlphamaps(startX, startZ, width, height);
        int numLayers = alphamap.GetLength(2);

        // Paint using heightmap
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // Sample heightmap brush
                float u = (float)x / brushSizeInAlphamap;
                float v = (float)z / brushSizeInAlphamap;
                Color heightColor = heightmapBrush.GetPixelBilinear(u, v);
                float brushStrength = heightColor.grayscale * opacity;

                // Blend the target layer
                float currentValue = alphamap[z, x, targetLayer];
                alphamap[z, x, targetLayer] = Mathf.Lerp(currentValue, 1f, brushStrength);

                // Normalize other layers
                float sum = 0f;
                for (int layer = 0; layer < numLayers; layer++)
                {
                    sum += alphamap[z, x, layer];
                }

                if (sum > 0)
                {
                    for (int layer = 0; layer < numLayers; layer++)
                    {
                        alphamap[z, x, layer] /= sum;
                    }
                }
            }
        }

        terrainData.SetAlphamaps(startX, startZ, alphamap);
    }
}
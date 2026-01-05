using UnityEngine;

public class RuntimeHeightmapPainter : MonoBehaviour
{
    public Terrain MainTerrain;
    public Texture2D HeightMapCurrent;
    public PaintBrushKernel Brush;

    [Header("Input Settings")]
    public KeyCode sculptKey = KeyCode.Mouse0; // Left mouse button
    public KeyCode eraseKey = KeyCode.Mouse1; //rb
    public PaintMode paintingMode = PaintMode.PAINT;

    [Header("Visual Feedback")]
    public bool ShowBrushPreview = true;
    public GameObject BrushPreviewPrefab;

    private Camera mainCamera;
    private GameObject brushPreviewInstance;

    const string HM_NAME = "heightmap";

    void Start()
    {
        mainCamera = Camera.main;

        // Create brush preview if enabled
        if (ShowBrushPreview)
        {
            if (BrushPreviewPrefab != null)
            {
                brushPreviewInstance = Instantiate(BrushPreviewPrefab);
            }
            else
            {
                // Create a simple sphere as preview
                brushPreviewInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                brushPreviewInstance.GetComponent<Collider>().enabled = false;
                Material previewMat = new Material(Shader.Find("Standard"));
                previewMat.color = new Color(0f, 1f, 1f, 0.5f);
                previewMat.SetFloat("_Mode", 3); // Transparent
                previewMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                previewMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                previewMat.SetInt("_ZWrite", 0);
                previewMat.DisableKeyword("_ALPHATEST_ON");
                previewMat.EnableKeyword("_ALPHABLEND_ON");
                previewMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                previewMat.renderQueue = 3000;
                brushPreviewInstance.GetComponent<Renderer>().material = previewMat;
            }
            brushPreviewInstance.name = "BrushPreview";
        }

        if(MainTerrain)
        {
            //get stats
            var terrainData = MainTerrain.terrainData;
            int tdWidth = terrainData.heightmapResolution;
            int tdHeight = terrainData.heightmapResolution;
            float[,] heights = terrainData.GetHeights(0, 0, tdWidth, tdHeight);
            if (!HeightMapCurrent)
            {

                for(int i = 0; i < tdWidth; i++)
                {
                    for (int j = 0; j < tdHeight; j++)
                    {
                        heights[j, i] = 0;
                    }
                }
                HeightMapCurrent = HeightmapUtility.CreateTextureFromMap(heights, TextureFormat.RGBA32, 1, HM_NAME);
            }
            else
            {
                Color[] pixels = HeightMapCurrent.GetPixels();
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Terrain terrain = hit.collider.GetComponent<Terrain>();
            if (terrain && terrain == MainTerrain)
            {
                // Update brush preview position
                if (ShowBrushPreview && brushPreviewInstance != null)
                {
                    brushPreviewInstance.SetActive(true);
                    brushPreviewInstance.transform.position = hit.point + Vector3.up * 2f;
                    brushPreviewInstance.transform.localScale = Vector3.one * Brush.Radius * 2f;
                }
                if (Input.GetKeyDown(sculptKey))
                    {
                        paintingMode = PaintMode.PAINT;
                    }
                    else if (Input.GetKeyDown(eraseKey))
                    {
                        paintingMode = PaintMode.ERASE;
                    }
                PaintAtPosition(terrain, hit.point);
            }
            else
            {
                // Hide preview if no terrain hit
                if (ShowBrushPreview && brushPreviewInstance != null)
                {
                    brushPreviewInstance.SetActive(false);
                }
            }
        }
        else
        {
            // Hide preview if no raycast hit
            if (ShowBrushPreview && brushPreviewInstance != null)
            {
                brushPreviewInstance.SetActive(false);
            }
        }
    }

    void PaintAtPosition(Terrain terrain, Vector3 worldPosition)
    {
        if (terrain == null || Brush == null)
            return;

        if (!Input.GetKey(sculptKey) && !Input.GetKey(eraseKey))
            return;

        TerrainData data = terrain.terrainData;

        Vector3 localPos = worldPosition - terrain.transform.position;

        int hmResolution = data.heightmapResolution;

        int centerX = Mathf.RoundToInt((localPos.x / data.size.x) * hmResolution);
        int centerZ = Mathf.RoundToInt((localPos.z / data.size.z) * hmResolution);

        float brushWorldDiameter = Brush.Radius * 2f;
        int brushSizeHM = Mathf.RoundToInt((brushWorldDiameter / data.size.x) * hmResolution);

        int halfBrush = brushSizeHM / 2;

        int startX = Mathf.Clamp(centerX - halfBrush, 0, hmResolution - 1);
        int startZ = Mathf.Clamp(centerZ - halfBrush, 0, hmResolution - 1);

        int endX = Mathf.Clamp(centerX + halfBrush, 0, hmResolution);
        int endZ = Mathf.Clamp(centerZ + halfBrush, 0, hmResolution);

        int width = endX - startX;
        int height = endZ - startZ;

        if (width <= 0 || height <= 0)
            return;

        float[,] heights = data.GetHeights(startX, startZ, width, height);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int hmX = startX + x;
                int hmZ = startZ + z;

                float dx = (hmX - centerX) / (float)halfBrush;
                float dz = (hmZ - centerZ) / (float)halfBrush;

                float distance01 = Mathf.Sqrt(dx * dx + dz * dz);

                if (distance01 > 1f)
                    continue;

                float brushStrength = Brush.GetStrength(dx, dz);

                float delta =
                    brushStrength *
                    Brush.Strength *
                    Time.deltaTime *
                    (paintingMode == PaintMode.PAINT ? 1f : -1f);

                heights[z, x] = Mathf.Clamp01(heights[z, x] + delta);
            }
        }

        data.SetHeights(startX, startZ, heights);
    }

}

public enum PaintMode
{
    PAINT = 0,
    ERASE = 1,
}
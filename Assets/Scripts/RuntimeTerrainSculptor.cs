using UnityEngine;

public class RuntimeTerrainSculptor : MonoBehaviour
{
    [Header("Brush Settings")]

    public Texture2D heightmapBrush; // Your heightmap to use as brush
    public Texture2D nextHeightMap;
    public Texture2D thirdHeightMap;
    public HeightmapGenerator heightmapGeneratorSettings;
    public float brushSize = 20f;
    public float strength = 0.5f; // How much the heightmap affects terrain
    public float distToNewBrush = 10f;


    [Header("Sculpt Mode")]
    public SculptMode mode = SculptMode.Add;
    public enum SculptMode
    {
        Add,        // Add height from brush
        Subtract,   // Subtract height
        Set,        // Set absolute height from brush
        Blend       // Blend current height with brush
    }

    [Header("Input Settings")]
    public KeyCode sculptKey = KeyCode.Mouse0; // Left mouse button
    public KeyCode eraseKey = KeyCode.Mouse1; //rb
    public bool continuousSculpt = false; // Sculpt while holding vs click once

    [Header("Visual Feedback")]
    public bool showBrushPreview = true;
    public GameObject brushPreviewPrefab; // Optional: assign a sphere or plane

    private Camera mainCamera;
    private GameObject brushPreviewInstance;
    private float distPainted = 0f;
    private Vector3? lastHitPoint = null;
    const string DEFAULT_HM_NAME = "generatedTexture";

    void Start()
    {
        mainCamera = Camera.main;

        // Create brush preview if enabled
        if (showBrushPreview)
        {
            if (brushPreviewPrefab != null)
            {
                brushPreviewInstance = Instantiate(brushPreviewPrefab);
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

        if(heightmapBrush != null && heightmapGeneratorSettings != null) 
        {
            nextHeightMap = heightmapGeneratorSettings.GenerateTexture(DEFAULT_HM_NAME);
            thirdHeightMap = heightmapGeneratorSettings.GenerateTexture(DEFAULT_HM_NAME);
        }
    }

    void Update()
    {
        // Raycast to find terrain under mouse
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Terrain terrain = hit.collider.GetComponent<Terrain>();

            if (terrain != null)
            {
                // Update brush preview position
                if (showBrushPreview && brushPreviewInstance != null)
                {
                    brushPreviewInstance.SetActive(true);
                    brushPreviewInstance.transform.position = hit.point + Vector3.up * 2f;
                    brushPreviewInstance.transform.localScale = Vector3.one * brushSize;
                }

                // Check for sculpt input
                int shouldSculpt = 0;
                if (continuousSculpt)
                {
                    if (Input.GetKey(sculptKey))
                    {
                        shouldSculpt = 1;
                    }
                    else if (Input.GetKey(eraseKey))
                    {
                        shouldSculpt = -1;
                    }
                }
                else
                {
                    if (Input.GetKeyDown(sculptKey))
                    {
                        shouldSculpt = 1;
                    }
                    else if (Input.GetKeyDown(eraseKey))
                    {
                        shouldSculpt = -1;
                    }
                }



                if (shouldSculpt == 1 && heightmapBrush != null)
                {
                    SculptAtPosition(terrain, hit.point);
                }
                else if (shouldSculpt == -1 && heightmapBrush != null)
                {
                    EraseAtPosition(terrain, hit.point);
                }
                else if(shouldSculpt == 0)
                {
                    lastHitPoint = null;
                    distPainted = 0f;
                }
            }
            else
            {
                // Hide preview if not over terrain
                if (showBrushPreview && brushPreviewInstance != null)
                {
                    brushPreviewInstance.SetActive(false);
                }
            }
        }
        else
        {
            // Hide preview if no raycast hit
            if (showBrushPreview && brushPreviewInstance != null)
            {
                brushPreviewInstance.SetActive(false);
            }
        }
    }

    void SculptAtPosition(Terrain terrain, Vector3 worldPosition)
    {
        if (terrain == null || heightmapBrush == null) return;

        if(lastHitPoint != null)
        {
            var dist = (worldPosition - lastHitPoint).Value.magnitude;
            distPainted += dist;
        }
        if(distPainted > distToNewBrush)
        {
            distPainted -= distToNewBrush;
            while(thirdHeightMap == null) { }
            ProgressMaps();
        }
        lastHitPoint = worldPosition;

        TerrainData terrainData = terrain.terrainData;

        // Convert world position to terrain-relative position
        Vector3 terrainPos = worldPosition - terrain.transform.position;

        // Convert to heightmap coordinates
        int heightmapWidth = terrainData.heightmapResolution;
        int heightmapHeight = terrainData.heightmapResolution;

        int centerX = (int)((terrainPos.x / terrainData.size.x) * heightmapWidth);
        int centerZ = (int)((terrainPos.z / terrainData.size.z) * heightmapHeight);

        // Calculate brush size in heightmap coordinates
        int brushSizeInHeightmap = (int)((brushSize / terrainData.size.x) * heightmapWidth);

        // current heightmap data
        int startX = Mathf.Max(0, centerX - brushSizeInHeightmap / 2);
        int startZ = Mathf.Max(0, centerZ - brushSizeInHeightmap / 2);
        int width = Mathf.Min(heightmapWidth - startX, brushSizeInHeightmap);
        int height = Mathf.Min(heightmapHeight - startZ, brushSizeInHeightmap);

        // Ensure we have valid dimensions
        if (width <= 0 || height <= 0) return;

        float[,] heights = terrainData.GetHeights(startX, startZ, width, height);
        var interpolatedHmBrush = InterpolateHeightmaps(heightmapBrush, nextHeightMap, distPainted / distToNewBrush);
        // Sculpt using heightmap brush
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // Calculate position relative to brush center
                float relX = (x + startX - centerX) / (float)brushSizeInHeightmap + 0.5f;
                float relZ = (z + startZ - centerZ) / (float)brushSizeInHeightmap + 0.5f;

                // Sample heightmap brush (only if within brush bounds)
                if (relX >= 0f && relX <= 1f && relZ >= 0f && relZ <= 1f)
                {
                    

                    Color heightColor = interpolatedHmBrush.GetPixelBilinear(relX, relZ);
                    float brushValue = heightColor.grayscale;

                    float currentHeight = heights[z, x];
                    float newHeight = currentHeight;

                    switch (mode)
                    {
                        case SculptMode.Add:
                            newHeight = currentHeight + (brushValue * strength);
                            break;

                        case SculptMode.Subtract:
                            newHeight = currentHeight - (brushValue * strength);
                            break;

                        case SculptMode.Set:
                            newHeight = brushValue * strength;
                            break;

                        case SculptMode.Blend:
                            newHeight = Mathf.Lerp(currentHeight, brushValue, strength);
                            break;
                    }

                    // Clamp to valid height range (0-1 in Unity's heightmap)
                    heights[z, x] = Mathf.Clamp01(newHeight);
                }
            }
        }

        terrainData.SetHeights(startX, startZ, heights);
    }

    void OnDestroy()
    {
        if (brushPreviewInstance != null)
        {
            Destroy(brushPreviewInstance);
        }
    }

    void SubtractAtPosition(Terrain terrain, Vector3 worldPosition)
    {
        if (terrain == null || heightmapBrush == null) return;

        TerrainData terrainData = terrain.terrainData;

        // Convert world position to terrain-relative position
        Vector3 terrainPos = worldPosition - terrain.transform.position;

        // Convert to heightmap coordinates
        int heightmapWidth = terrainData.heightmapResolution;
        int heightmapHeight = terrainData.heightmapResolution;

        int centerX = (int)((terrainPos.x / terrainData.size.x) * heightmapWidth);
        int centerZ = (int)((terrainPos.z / terrainData.size.z) * heightmapHeight);

        // Calculate brush size in heightmap coordinates
        int brushSizeInHeightmap = (int)((brushSize / terrainData.size.x) * heightmapWidth);

        // Get current heightmap data
        int startX = Mathf.Max(0, centerX - brushSizeInHeightmap / 2);
        int startZ = Mathf.Max(0, centerZ - brushSizeInHeightmap / 2);
        int width = Mathf.Min(heightmapWidth - startX, brushSizeInHeightmap);
        int height = Mathf.Min(heightmapHeight - startZ, brushSizeInHeightmap);

        // Ensure we have valid dimensions
        if (width <= 0 || height <= 0) return;

        float[,] heights = terrainData.GetHeights(startX, startZ, width, height);

        // Erase using heightmap brush as alpha/mask
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // Calculate position relative to brush center
                float relX = (x + startX - centerX) / (float)brushSizeInHeightmap + 0.5f;
                float relZ = (z + startZ - centerZ) / (float)brushSizeInHeightmap + 0.5f;

                // Sample heightmap brush (only if within brush bounds)
                if (relX >= 0f && relX <= 1f && relZ >= 0f && relZ <= 1f)
                {
                    Color heightColor = heightmapBrush.GetPixelBilinear(relX, relZ);
                    float brushAlpha = heightColor.grayscale; // Use brush as mask

                    float currentHeight = heights[z, x];
                    // Lerp toward zero based on brush alpha and strength
                    float newHeight = Mathf.Lerp(currentHeight, 0f, brushAlpha * strength);

                    heights[z, x] = Mathf.Clamp01(newHeight);
                }
            }
        }

        terrainData.SetHeights(startX, startZ, heights);
    }

    private void EraseAtPosition(Terrain terrain, Vector3 worldPosition)
    {
        if (terrain == null || heightmapBrush == null) return;

        TerrainData terrainData = terrain.terrainData;

        // Convert world position to terrain-relative position
        Vector3 terrainPos = worldPosition - terrain.transform.position;

        // Convert to heightmap coordinates
        int heightmapWidth = terrainData.heightmapResolution;
        int heightmapHeight = terrainData.heightmapResolution;

        int centerX = (int)((terrainPos.x / terrainData.size.x) * heightmapWidth);
        int centerZ = (int)((terrainPos.z / terrainData.size.z) * heightmapHeight);

        // Calculate brush size in heightmap coordinates
        int brushSizeInHeightmap = (int)((brushSize / terrainData.size.x) * heightmapWidth);

        // Get current heightmap data
        int startX = Mathf.Max(0, centerX - brushSizeInHeightmap / 2);
        int startZ = Mathf.Max(0, centerZ - brushSizeInHeightmap / 2);
        int width = Mathf.Min(heightmapWidth - startX, brushSizeInHeightmap);
        int height = Mathf.Min(heightmapHeight - startZ, brushSizeInHeightmap);

        // Ensure we have valid dimensions
        if (width <= 0 || height <= 0) return;

        float[,] heights = terrainData.GetHeights(startX, startZ, width, height);

        // Erase using heightmap brush as alpha/mask
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // Calculate position relative to brush center
                float relX = (x + startX - centerX) / (float)brushSizeInHeightmap + 0.5f;
                float relZ = (z + startZ - centerZ) / (float)brushSizeInHeightmap + 0.5f;

                // Sample heightmap brush (only if within brush bounds)
                if (relX >= 0f && relX <= 1f && relZ >= 0f && relZ <= 1f)
                {
                    heights[z, x] = 0f;
                }
            }
        }

        terrainData.SetHeights(startX, startZ, heights);
    }

    private Texture2D InterpolateHeightmaps(Texture2D heightmap1, Texture2D heightmap2, float factor = 0.5f) //factor must be between 0 and 1
    {
        factor = Mathf.Clamp01(factor);
            if (heightmap1 == null || heightmap2 == null)
            {
                Debug.LogError("Assign both heightmap textures in the Inspector.");
                return null;
            }

            // Ensure both heightmaps have the same dimensions for accurate averaging
            if (heightmap1.width != heightmap2.width || heightmap1.height != heightmap2.height)
            {
                Debug.LogError("Heightmap textures must have the same dimensions.");
                return null;
            }

            var resultHeightmap = new Texture2D(heightmap1.width, heightmap1.height, TextureFormat.RFloat, false); // RFloat for height data

            // Make textures readable
            heightmap1.filterMode = FilterMode.Point;
            heightmap2.filterMode = FilterMode.Point;
            heightmap1.wrapMode = TextureWrapMode.Clamp;
            heightmap2.wrapMode = TextureWrapMode.Clamp;

            // Get pixel data from both heightmaps
            Color[] pixels1 = heightmap1.GetPixels();
            Color[] pixels2 = heightmap2.GetPixels();
            Color[] resultPixels = new Color[pixels1.Length];

            // Iterate through pixels and average the height values
            for (int i = 0; i < pixels1.Length; i++)
            {
                // Assuming heightmap data is stored in the red channel (or grayscale)
                float height1 = pixels1[i].r * (1 - factor);
                float height2 = pixels2[i].r * factor;

                float interpolatedHeight = height1 + height2;

                // Assign the averaged height to the red channel of the result pixel
                resultPixels[i] = new Color(interpolatedHeight, interpolatedHeight, interpolatedHeight, 1f);
            }

            // Apply the averaged pixels to the result heightmap
            resultHeightmap.SetPixels(resultPixels);
            resultHeightmap.Apply();

        return resultHeightmap;
        
    }

    private void ProgressMaps()
    {
        heightmapBrush = nextHeightMap;
        nextHeightMap = thirdHeightMap;
        thirdHeightMap = null;
        StartCoroutine(heightmapGeneratorSettings.GenerateTextureAsync(DEFAULT_HM_NAME, SetThirdHeightMapCallback));
    }

    public void SetThirdHeightMapCallback(Texture2D hm)
    {
        thirdHeightMap = hm;
    }
}
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
        if (terrain == null || Brush == null) return;
        var data = terrain.terrainData;

        //conv hit pos to terrain pos
        Vector3 pos = worldPosition - terrain.transform.position;

        int tmWidth = data.heightmapResolution;
        int tmHeight = data.heightmapResolution;


        int centerX = (int)((pos.x / data.size.x) * tmWidth);
        int centerZ = (int)((pos.z / data.size.z) * tmHeight);

        //Brush size in hm coord
        int brushSizeInHm = (int)((Brush.Radius * 2f) / data.size.x) * tmWidth;

        // current heightmap data
        int startX = Mathf.Max(0, centerX - brushSizeInHm / 2);
        int startZ = Mathf.Max(0, centerZ - brushSizeInHm / 2);
        int width = Mathf.Min(tmWidth - startX, brushSizeInHm);
        int height = Mathf.Min(tmHeight - startZ, brushSizeInHm);


    }
}

public enum PaintMode
{
    PAINT = 0,
    ERASE = 1,
}
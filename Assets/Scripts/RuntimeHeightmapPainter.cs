using UnityEngine;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class RuntimeHeightmapPainter : MonoBehaviour
{
    public Terrain MainTerrain;
    public PaintBrushKernel Brush;

    [Header("Input")]
    public KeyCode sculptKey = KeyCode.Mouse0;
    public KeyCode eraseKey = KeyCode.Mouse1;

    [Header("Brush Controls")]
    public KeyCode decreaseRadiusKey = KeyCode.LeftBracket;
    public KeyCode increaseRadiusKey = KeyCode.RightBracket;
    public KeyCode decreaseStrengthKey = KeyCode.Minus;
    public KeyCode increaseStrengthKey = KeyCode.Equals;

    public int radiusStep = 1;
    public float strengthStep = 0.01f;

    public int minRadius = 1;
    public int maxRadius = 100;
    public float minStrength = 0.001f;
    public float maxStrength = 5f;

    [Header("Brush Preview")]
    public bool showBrushPreview = true;
    public GameObject brushPreviewPrefab;

    [Header("Height Painting")]
    public bool paintHeight = true;

    [Header("Texture Painting")]
    public bool paintTexture = true;
    public float texturePaintStrength = 0.5f;
    public int colorTextureSize = 32;
    public string texturesSavePath = "TerrainTextures/Generated";

    [SerializeField]
    private Color brushColor = Color.white;

    private Camera cam;
    private GameObject brushPreview;
    private int currentLayer = -1;

    private readonly Dictionary<string, int> colorHexToLayer = new();

    // =====================================================
    // Unity lifecycle
    // =====================================================

    void Awake()
    {
        cam = Camera.main;

        if (!MainTerrain || !Brush)
        {
            Debug.LogError("Missing terrain or brush.");
            enabled = false;
            return;
        }

        EnsureBaseLayer();
        IndexExistingLayers();
        CreateBrushPreview();
    }

    void Update()
    {
        HandleBrushKeyboardInput();

        if (paintTexture)
            currentLayer = GetOrCreateLayerForColor(brushColor);

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        bool hitTerrain = Physics.Raycast(ray, out RaycastHit hit)
                          && hit.collider.GetComponent<Terrain>() == MainTerrain;

        UpdateBrushPreview(hitTerrain, hit);

        if (!hitTerrain)
            return;

        bool painting = Input.GetKey(sculptKey);
        bool erasing = Input.GetKey(eraseKey);

        if (painting || erasing)
            PaintAt(hit.point, painting);
    }

    // =====================================================
    // Painting logic
    // =====================================================

    void PaintAt(Vector3 worldPos, bool paint)
    {
        TerrainData data = MainTerrain.terrainData;
        Vector3 local = worldPos - MainTerrain.transform.position;

        if (paintHeight)
            PaintHeight(data, local, paint);

        if (paintTexture)
            PaintTexture(data, local, paint);
    }

    void PaintHeight(TerrainData data, Vector3 local, bool paint)
    {
        int res = data.heightmapResolution;

        int cx = Mathf.RoundToInt(local.x / data.size.x * res);
        int cz = Mathf.RoundToInt(local.z / data.size.z * res);

        int diameter = Mathf.RoundToInt((Brush.Radius * 2f / data.size.x) * res);
        int half = Mathf.Max(1, diameter / 2);

        int sx = Mathf.Clamp(cx - half, 0, res - 1);
        int sz = Mathf.Clamp(cz - half, 0, res - 1);
        int ex = Mathf.Clamp(cx + half, 0, res);
        int ez = Mathf.Clamp(cz + half, 0, res);

        float[,] heights = data.GetHeights(sx, sz, ex - sx, ez - sz);

        for (int z = 0; z < heights.GetLength(0); z++)
            for (int x = 0; x < heights.GetLength(1); x++)
            {
                float dx = (sx + x - cx) / (float)half;
                float dz = (sz + z - cz) / (float)half;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > 1f) continue;

                float delta =
                    Brush.GetStrength(dx, dz) *
                    Brush.Strength *
                    Time.deltaTime *
                    (paint ? 1f : -1f);

                heights[z, x] = Mathf.Clamp01(heights[z, x] + delta);
            }

        data.SetHeights(sx, sz, heights);
    }

    void PaintTexture(TerrainData data, Vector3 local, bool paint)
    {
        int aw = data.alphamapWidth;
        int ah = data.alphamapHeight;

        int cx = Mathf.RoundToInt(local.x / data.size.x * aw);
        int cz = Mathf.RoundToInt(local.z / data.size.z * ah);

        int diameter = Mathf.RoundToInt((Brush.Radius * 2f / data.size.x) * aw);
        int half = Mathf.Max(1, diameter / 2);

        int sx = Mathf.Clamp(cx - half, 0, aw - 1);
        int sz = Mathf.Clamp(cz - half, 0, ah - 1);
        int ex = Mathf.Clamp(cx + half, 0, aw);
        int ez = Mathf.Clamp(cz + half, 0, ah);

        float[,,] map = data.GetAlphamaps(sx, sz, ex - sx, ez - sz);
        int layers = map.GetLength(2);

        int targetLayer = paint ? currentLayer : 0;
        if (targetLayer < 0 || targetLayer >= layers)
            return;

        for (int z = 0; z < map.GetLength(0); z++)
            for (int x = 0; x < map.GetLength(1); x++)
            {
                float dx = (sx + x - cx) / (float)half;
                float dz = (sz + z - cz) / (float)half;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > 1f) continue;

                float amt =
                    Brush.GetStrength(dx, dz) *
                    texturePaintStrength *
                    Time.deltaTime;

                map[z, x, targetLayer] =
                    Mathf.Lerp(map[z, x, targetLayer], 1f, amt);

                float sum = 0f;
                for (int l = 0; l < layers; l++) sum += map[z, x, l];
                for (int l = 0; l < layers; l++) map[z, x, l] /= sum;
            }

        data.SetAlphamaps(sx, sz, map);

#if UNITY_EDITOR
        EditorUtility.SetDirty(data);
#endif
    }

    // =====================================================
    // Brush controls & preview
    // =====================================================

    void HandleBrushKeyboardInput()
    {
        if (Input.GetKeyDown(decreaseRadiusKey))
            Brush.Radius = Mathf.Max(minRadius, Brush.Radius - radiusStep);

        if (Input.GetKeyDown(increaseRadiusKey))
            Brush.Radius = Mathf.Min(maxRadius, Brush.Radius + radiusStep);

        if (Input.GetKeyDown(decreaseStrengthKey))
            Brush.Strength = Mathf.Max(minStrength, Brush.Strength - strengthStep);

        if (Input.GetKeyDown(increaseStrengthKey))
            Brush.Strength = Mathf.Min(maxStrength, Brush.Strength + strengthStep);
    }

    void CreateBrushPreview()
    {
        if (!showBrushPreview) return;

        brushPreview = brushPreviewPrefab
            ? Instantiate(brushPreviewPrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);

        brushPreview.name = "BrushPreview";
        Destroy(brushPreview.GetComponent<Collider>());
    }

    void UpdateBrushPreview(bool hit, RaycastHit hitInfo)
    {
        if (!brushPreview) return;

        brushPreview.SetActive(hit);
        if (!hit) return;

        brushPreview.transform.position = hitInfo.point + Vector3.up * 0.1f;
        brushPreview.transform.localScale = Vector3.one * Brush.Radius * 2f;
    }

    // =====================================================
    // Terrain layer management
    // =====================================================

    void EnsureBaseLayer()
    {
        TerrainData data = MainTerrain.terrainData;

        if (data.terrainLayers != null && data.terrainLayers.Length > 0)
            return;

        TerrainLayer baseLayer = GetOrCreateTerrainLayer(Color.gray);
        data.terrainLayers = new TerrainLayer[] { baseLayer };

#if UNITY_EDITOR
        EditorUtility.SetDirty(data);
#endif
    }

    void IndexExistingLayers()
    {
        colorHexToLayer.Clear();
        TerrainLayer[] layers = MainTerrain.terrainData.terrainLayers;

        for (int i = 0; i < layers.Length; i++)
        {
            TerrainLayer layer = layers[i];
            if (!layer || !layer.diffuseTexture) continue;

#if UNITY_EDITOR
            string path = AssetDatabase.GetAssetPath(layer.diffuseTexture);
            EnsureReadable(path);
#endif

            Texture2D tex = layer.diffuseTexture;
            Color c = tex.GetPixel(tex.width / 2, tex.height / 2);
            colorHexToLayer[ColorToHex(c)] = i;
        }
    }

    int GetOrCreateLayerForColor(Color color)
    {
        string hex = ColorToHex(color);
        if (colorHexToLayer.TryGetValue(hex, out int index))
            return index;

        TerrainLayer layer = GetOrCreateTerrainLayer(color);
        TerrainData data = MainTerrain.terrainData;

        var old = data.terrainLayers;
        var next = new TerrainLayer[old.Length + 1];
        old.CopyTo(next, 0);
        next[^1] = layer;

        data.terrainLayers = next;
        colorHexToLayer[hex] = next.Length - 1;

#if UNITY_EDITOR
        EditorUtility.SetDirty(data);
#endif

        return next.Length - 1;
    }

    TerrainLayer GetOrCreateTerrainLayer(Color color)
    {
#if UNITY_EDITOR
        string hex = ColorToHex(color);
        string dir = Path.Combine("Assets", texturesSavePath);
        Directory.CreateDirectory(dir);

        string texPath = Path.Combine(dir, $"Tex_{hex}.png");
        string layerPath = Path.Combine(dir, $"Layer_{hex}.asset");

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (!tex)
        {
            tex = new Texture2D(colorTextureSize, colorTextureSize, TextureFormat.RGBA32, false);
            var pixels = new Color[colorTextureSize * colorTextureSize];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();

            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
        }

        EnsureReadable(texPath);

        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (!layer)
        {
            layer = new TerrainLayer
            {
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath),
                tileSize = new Vector2(10, 10)
            };

            AssetDatabase.CreateAsset(layer, layerPath);
        }

        return AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    static void EnsureReadable(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return;

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (!importer || importer.isReadable) return;

        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
#endif

    static string ColorToHex(Color c)
    {
        return ColorUtility.ToHtmlStringRGBA(c);
    }
}

using UnityEngine;
using UnityEngine.LightTransport;

[CreateAssetMenu(fileName = "PaintBrushKernel", menuName = "Terrain/Paint Brush Kernel")]
public class PaintBrushKernel : ScriptableObject
{
    [Header("Brush Settings")]
    [Tooltip("Brush radius in world units")]
    [Range(1f, 100f)]
    public float Radius = 10f;

    [Tooltip("Overall brush strength multiplier")]
    [Range(0.001f, 5f)]
    public float Strength = 1f;

    [Tooltip("Brush shape/falloff preset")]
    public BrushPreset Preset = BrushPreset.Smooth;

    [Header("Advanced Settings")]
    [Tooltip("Falloff curve exponent (higher = sharper edges)")]
    [Range(0.1f, 5f)]
    public float Falloff = 1f;

    [Tooltip("Hardness of the brush (0 = soft, 1 = hard)")]
    [Range(0f, 1f)]
    public float Hardness = 0.5f;

    [Tooltip("Custom falloff curve (optional, overrides preset if set)")]
    public AnimationCurve CustomCurve = null;

    [Header("Procedural Settings")]
    public float NoiseScale = 4f;
    public float NoiseContrast = 1f;
    public int RandomSeed = 12345;

    [Range(1, 20)]
    public int VoronoiCellCount = 6;

    [Range(0f, 1f)]
    public float SpikeDensity = 0.2f;

    /// <summary>
    /// Get the brush strength at a given position relative to the brush center
    /// </summary>
    /// <param name="dx">Normalized X distance from center (-1 to 1)</param>
    /// <param name="dz">Normalized Z distance from center (-1 to 1)</param>
    /// <returns>Strength value (0 to 1)</returns>
    public float GetStrength(float dx, float dz, float worldX = 0f, float worldZ = 0f)
    {
        // Calculate distance from center (0 to 1, where 1 is at the edge)
        float distance = Mathf.Sqrt(dx * dx + dz * dz);

        // Clamp to brush radius
        if (distance > 1f)
            return 0f;

        // Use custom curve if provided
        if (CustomCurve != null && CustomCurve.length > 0)
        {
            return CustomCurve.Evaluate(distance) * Strength;
        }

        // Otherwise use preset
        float strength = GetPresetStrength(distance, worldX, worldZ);

        // Apply hardness (makes the brush more uniform in the center)
        strength = ApplyHardness(strength, distance);

        return strength * Strength;
    }

    /// <summary>
    /// Get brush strength based on the selected preset
    /// </summary>
    private float GetPresetStrength(float distance, float worldX, float worldZ)
    {
        switch (Preset)
        {
            case BrushPreset.Hard:
                // Sharp falloff at edges
                return distance < 0.9f ? 1f : Mathf.Pow(1f - (distance - 0.9f) / 0.1f, 3f);

            case BrushPreset.Soft:
                // Very gradual falloff
                return Mathf.Pow(1f - distance, 3f);

            case BrushPreset.Smooth:
                // Smooth cosine falloff
                return Mathf.Cos(distance * Mathf.PI * 0.5f);

            case BrushPreset.Linear:
                // Simple linear falloff
                return 1f - distance;

            case BrushPreset.Quadratic:
                // Quadratic falloff
                return Mathf.Pow(1f - distance, 2f);

            case BrushPreset.Gaussian:
                // Gaussian/bell curve falloff
                float sigma = 0.4f;
                return Mathf.Exp(-(distance * distance) / (2f * sigma * sigma));

            case BrushPreset.Plateau:
                // Flat center with sharp edges
                if (distance < 0.5f)
                    return 1f;
                else
                    return 1f - Mathf.Pow((distance - 0.5f) * 2f, 2f);

            case BrushPreset.Spike:
                // Sharp peak in center
                return Mathf.Pow(1f - distance, 5f);

            case BrushPreset.Custom:
                // Customizable with falloff parameter
                return Mathf.Pow(1f - distance, Falloff);

            case BrushPreset.Noise:
                {
                    float nx = worldX * NoiseScale;
                    float nz = worldZ * NoiseScale;

                    float n = Mathf.PerlinNoise(nx + RandomSeed, nz + RandomSeed);

                    // increase contrast
                    n = Mathf.Pow(n, NoiseContrast);

                    return n;
                }

            case BrushPreset.RandomSpikes:
                {
                    float cellX = Mathf.Floor(worldX * NoiseScale);
                    float cellZ = Mathf.Floor(worldZ * NoiseScale);

                    float hash = Hash(cellX, cellZ);

                    return (hash < SpikeDensity) ? 1f : 0f;
                }

            case BrushPreset.Voronoi:
                {
                    float px = worldX * NoiseScale;
                    float pz = worldZ * NoiseScale;

                    float minDist = 999f;

                    for (int i = 0; i < VoronoiCellCount; i++)
                    {
                        float ox = Hash(i, 0) * 2f - 1f;
                        float oz = Hash(0, i) * 2f - 1f;

                        float dxv = px - ox;
                        float dzv = pz - oz;

                        float d = dxv * dxv + dzv * dzv;

                        if (d < minDist)
                            minDist = d;
                    }

                    return Mathf.Clamp01(1f - Mathf.Sqrt(minDist));
                }

            case BrushPreset.CellularBlobs:
                {
                    float n = Mathf.PerlinNoise(worldX * NoiseScale, worldZ * NoiseScale);
                    return Mathf.SmoothStep(0.4f, 0.6f, n);
                }

            default:
                return 1f - distance;
        }
    }

    /// <summary>
    /// Apply hardness to make the brush more uniform in the center
    /// </summary>
    private float ApplyHardness(float strength, float distance)
    {
        if (Hardness <= 0f)
            return strength;

        // Hardness creates a more uniform center
        float hardnessThreshold = 1f - Hardness;
        if (distance < hardnessThreshold)
        {
            // Blend between full strength and falloff
            float blend = distance / hardnessThreshold;
            return Mathf.Lerp(1f, strength, blend);
        }

        return strength;
    }

    /// <summary>
    /// Preview the brush falloff for debugging/visualization
    /// </summary>
    public Texture2D GeneratePreviewTexture(int resolution = 128)
    {
        Texture2D preview = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // Convert to normalized coordinates (-1 to 1)
                float dx = (x / (float)resolution - 0.5f) * 2f;
                float dz = (y / (float)resolution - 0.5f) * 2f;

                // Get strength
                float strength = GetStrength(dx, dz) / Strength; // Normalize by dividing by Strength

                // Visualize as grayscale
                Color color = new Color(strength, strength, strength, 1f);
                preview.SetPixel(x, y, color);
            }
        }

        preview.Apply();
        return preview;
    }
    float Hash(float x, float y)
    {
        float h = Mathf.Sin(x * 127.1f + y * 311.7f + RandomSeed) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

}

/// <summary>
/// Predefined brush shape presets
/// </summary>
public enum BrushPreset
{
    Hard,       // Sharp edges, mostly uniform
    Soft,       // Very gradual falloff
    Smooth,     // Smooth cosine falloff (default)
    Linear,     // Simple linear gradient
    Quadratic,  // Quadratic falloff
    Gaussian,   // Bell curve / Gaussian distribution
    Plateau,    // Flat center with edges
    Spike,      // Sharp peak in center
    Custom,      // Use Falloff parameter for custom shape
    Noise,
    Voronoi,
    RandomSpikes,
    CellularBlobs
}
using UnityEngine;

[CreateAssetMenu(fileName = "PaintBrushKernel", menuName = "Scriptable Objects/PaintBrushKernel")]
public class PaintBrushKernel : ScriptableObject
{
    public int Radius = 1;
    public float Strength = 1f;
    public float GetStrength(float dx, float dz)
    {
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        return Mathf.Clamp01(1f - dist);
    }
}

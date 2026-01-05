using UnityEngine;

[CreateAssetMenu(fileName = "PaintBrushKernel", menuName = "Scriptable Objects/PaintBrushKernel")]
public class PaintBrushKernel : ScriptableObject
{
    public int Radius = 1;
    public float Strength = 1f;
}

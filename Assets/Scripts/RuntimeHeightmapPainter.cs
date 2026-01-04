using UnityEngine;

public class RuntimeHeightmapPainter : MonoBehaviour
{
    public Texture2D heightMapCurrent;
    public PaintBrushKernel brush;

    public bool Erase = false;

    [Header("Input Settings")]
    public KeyCode sculptKey = KeyCode.Mouse0; // Left mouse button
    public KeyCode eraseKey = KeyCode.Mouse1; //rb

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

public enum PaintMode
{
    PAINT = 0,
    ERASE = 1,
}

public class PaintBrushKernel
{

}

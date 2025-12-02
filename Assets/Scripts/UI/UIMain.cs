using UnityEngine;

public class UIMain : MonoBehaviour
{
    public RuntimeTerrainSculptor sculptor;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetHmSettings(HeightmapGenerator.HeightmapType hmType)
    {
        sculptor.heightmapGeneratorSettings.heightmapType = hmType;
    }
}

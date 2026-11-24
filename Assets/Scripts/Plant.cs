using UnityEngine;

/// <summary>View for one planted crop. State lives in the GardenSimulation.</summary>
public class Plant : MonoBehaviour
{
    public PlantInstance plantInstance;

    private SpriteRenderer spriteRenderer;

    public void Init(PlantInstance instance)
    {
        plantInstance = instance;
        Refresh();
    }

    /// <summary>Shows the seed or grown sprite depending on maturity.</summary>
    public void Refresh()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        PlantData data = plantInstance.data;
        spriteRenderer.sprite = plantInstance.IsMature ? data.plantSprite : data.seedSprite;
    }
}

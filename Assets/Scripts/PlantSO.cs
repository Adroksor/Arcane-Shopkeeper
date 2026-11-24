using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "New Plant", menuName = "Arcane Shopkeeper/Garden/Plants")]
public class PlantData : ScriptableObject
{
    public string plantName;
    [TextArea] public string description;
    public Sprite seedSprite;
    public Sprite plantSprite;
    [Min(0)] public int maturityAge;
    public int decayTime;

    /// <summary>Chance per tick to spread into each empty neighbouring plot once mature.</summary>
    [FormerlySerializedAs("spreadBaseMultiplier")]
    [Range(0f, 1f)] public float spreadChance;
}

[System.Serializable]
public class PlantInstance
{
    public PlantData data;
    public int age;

    public bool IsMature => data != null && age >= data.maturityAge;

    public PlantInstance(PlantData plantData)
    {
        data = plantData;
        age = 0;
    }
}

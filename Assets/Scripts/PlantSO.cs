using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Plant", menuName = "Arcane Shopkeeper/Garden/Plants")]
public class PlantData : ScriptableObject
{
    public int plantId => GetInstanceID();
    public string plantName;
    [TextArea] public string description;
    public Sprite seedSprite;
    public Sprite plantSprite;
    public int maturityAge;
    public int decayTime;
    public float spreadBaseMultiplier;
}

[System.Serializable]
public class PlantInstance
{
    public PlantData data;
    public GameObject gameObject;
    public bool isMature;
    public int age;
    
    public PlantInstance(PlantData plantData, bool isMature = false)
    {
        this.data = plantData;
        this.isMature = isMature;
        age = 0;
    }
}
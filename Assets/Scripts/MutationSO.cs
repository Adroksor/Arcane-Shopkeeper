using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "New Mutation", menuName = "Arcane Shopkeeper/Garden/Mutations")]
public class MutationSO : ScriptableObject
{
    public int mutationId => GetInstanceID();
    public List<MutationIngredient> mutationIngredients;
    public PlantData mutant;
    public float mutationBaseChance;
}

[Serializable]
public struct MutationIngredient
{
    public PlantData plant;
    public int amount;
}

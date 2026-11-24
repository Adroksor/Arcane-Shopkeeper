using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


[CreateAssetMenu(fileName = "New Mutation", menuName = "Arcane Shopkeeper/Garden/Mutations")]
public class MutationSO : ScriptableObject
{
    /// <summary>A hex plot has at most 6 neighbours, so no recipe can need more.</summary>
    public const int MaxIngredients = 6;

    public List<MutationIngredient> mutationIngredients;
    public PlantData mutant;
    [Range(0f, 1f)] public float mutationBaseChance;

    /// <summary>
    /// Checks the recipe and sums ingredients per plant (duplicate entries are added together).
    /// Returns null if valid, otherwise a description of the first blocking problem.
    /// </summary>
    public string Validate(out Dictionary<PlantData, int> needs)
    {
        needs = new Dictionary<PlantData, int>();

        if (mutant == null) return "no mutant plant set";
        if (mutationIngredients == null || mutationIngredients.Count == 0) return "no ingredients";

        foreach (MutationIngredient ing in mutationIngredients)
        {
            if (ing.plant == null) return "an ingredient has no plant set";
            if (ing.amount <= 0) return $"ingredient '{ing.plant.name}' has amount {ing.amount}";
            needs[ing.plant] = needs.TryGetValue(ing.plant, out int n) ? n + ing.amount : ing.amount;
        }

        int total = needs.Values.Sum();
        if (total > MaxIngredients) return $"needs {total} neighbours, but a hex only has {MaxIngredients}";

        return null;
    }

    private void OnValidate()
    {
        string problem = Validate(out _);
        if (problem != null)
            Debug.LogWarning($"Mutation '{name}': {problem}", this);

        if (mutationIngredients != null)
        {
            var duplicates = mutationIngredients
                .Where(i => i.plant != null)
                .GroupBy(i => i.plant)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.name);
            foreach (string plant in duplicates)
                Debug.LogWarning($"Mutation '{name}': '{plant}' is listed more than once; amounts are added together.", this);
        }
    }
}

[Serializable]
public struct MutationIngredient
{
    public PlantData plant;
    public int amount;
}

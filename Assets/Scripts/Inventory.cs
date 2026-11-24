using System;
using System.Collections.Generic;

/// <summary>
/// Seed counts per plant type. Plain C# data, no UI.
/// Subscribe to Changed to react to updates (e.g. a future inventory panel).
/// </summary>
public class Inventory
{
    /// <summary>Raised with the plant type and its new seed count.</summary>
    public event Action<PlantData, int> Changed;

    public IReadOnlyDictionary<PlantData, int> Seeds => seeds;

    private readonly Dictionary<PlantData, int> seeds = new Dictionary<PlantData, int>();

    public int GetSeeds(PlantData plant) =>
        plant != null && seeds.TryGetValue(plant, out int n) ? n : 0;

    public bool HasSeeds(PlantData plant, int amount = 1) => GetSeeds(plant) >= amount;

    public void AddSeeds(PlantData plant, int amount)
    {
        if (plant == null || amount <= 0) return;

        int total = GetSeeds(plant) + amount;
        seeds[plant] = total;
        Changed?.Invoke(plant, total);
    }

    /// <summary>Removes seeds if there are enough; otherwise changes nothing and returns false.</summary>
    public bool TryRemoveSeeds(PlantData plant, int amount = 1)
    {
        if (plant == null || amount <= 0) return false;

        int have = GetSeeds(plant);
        if (have < amount) return false;

        int total = have - amount;
        if (total == 0) seeds.Remove(plant);
        else seeds[plant] = total;
        Changed?.Invoke(plant, total);
        return true;
    }
}

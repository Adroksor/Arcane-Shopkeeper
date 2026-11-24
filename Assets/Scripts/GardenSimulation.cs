using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// One hex plot in the garden. Plant == null means the plot is empty.
/// </summary>
public class GardenTile
{
    public Vector2Int Position { get; }
    public PlantInstance Plant { get; internal set; }
    public bool IsEmpty => Plant == null;

    public GardenTile(Vector2Int position)
    {
        Position = position;
    }
}

/// <summary>
/// Pure garden rules: no GameObjects, no UnityEngine.Random.
/// The view layer (GardenManager) subscribes to the events and mirrors the state.
/// </summary>
public class GardenSimulation
{
    public event Action<GardenTile> PlotAdded;
    public event Action<GardenTile> Planted;
    public event Action<GardenTile, PlantInstance> Removed;
    public event Action<GardenTile> Matured;

    /// <summary>Seeds gained from harvesting a mature plant. Removing a seed (immature plant) gives nothing.</summary>
    public const int SeedsPerHarvest = 3;

    public IReadOnlyDictionary<Vector2Int, GardenTile> Tiles => tiles;
    public Inventory Inventory { get; }

    private readonly Dictionary<Vector2Int, GardenTile> tiles = new Dictionary<Vector2Int, GardenTile>();
    private readonly List<Recipe> recipes;
    private readonly Random rng;

    /// <param name="mutations">Mutation recipes; invalid ones are skipped with a warning.</param>
    /// <param name="seed">Fixed seed for reproducible runs (tests); null = random.</param>
    /// <param name="inventory">Where harvested seeds go; a new empty one if null.</param>
    public GardenSimulation(IEnumerable<MutationSO> mutations, int? seed = null, Inventory inventory = null)
    {
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
        Inventory = inventory ?? new Inventory();

        // Most specific recipe (most ingredients) first. OrderByDescending is stable,
        // so recipes with equal ingredient totals keep their Inspector order.
        recipes = (mutations ?? Enumerable.Empty<MutationSO>())
            .Where(m => m != null)
            .Select(Recipe.TryCreate)
            .Where(r => r != null)
            .OrderByDescending(r => r.TotalIngredients)
            .ToList();
    }

    // ---------------------------------------------------------------- queries

    public bool HasPlot(Vector2Int pos) => tiles.ContainsKey(pos);

    public bool TryGetTile(Vector2Int pos, out GardenTile tile) => tiles.TryGetValue(pos, out tile);

    public IEnumerable<GardenTile> Neighbours(Vector2Int pos)
    {
        foreach (Vector2Int dir in HexMath.Directions)
            if (tiles.TryGetValue(pos + dir, out GardenTile tile))
                yield return tile;
    }

    public IEnumerable<PlantInstance> MatureNeighbours(Vector2Int pos)
    {
        foreach (GardenTile n in Neighbours(pos))
            if (!n.IsEmpty && n.Plant.IsMature)
                yield return n.Plant;
    }

    public int CountMatureNeighbours(Vector2Int pos) => MatureNeighbours(pos).Count();

    // --------------------------------------------------------------- commands

    public bool AddPlot(Vector2Int pos)
    {
        if (tiles.ContainsKey(pos)) return false;

        var tile = new GardenTile(pos);
        tiles.Add(pos, tile);
        PlotAdded?.Invoke(tile);
        return true;
    }

    /// <summary>Player planting: costs 1 seed of that type from the inventory.</summary>
    public bool Plant(Vector2Int pos, PlantData data)
    {
        if (!CanPlantAt(pos, data, out GardenTile tile)) return false;
        if (!Inventory.TryRemoveSeeds(data, 1)) { Debug.Log($"No {data.plantName} seeds left"); return false; }

        PlaceNewPlant(tile, data);
        return true;
    }

    private bool CanPlantAt(Vector2Int pos, PlantData data, out GardenTile tile)
    {
        tile = null;
        if (data == null) { Debug.LogWarning("Plant: no plant data given"); return false; }
        if (!tiles.TryGetValue(pos, out tile)) { Debug.Log("No hex there"); return false; }
        if (!tile.IsEmpty) { Debug.Log("There is already a plant there"); return false; }
        return true;
    }

    /// <summary>Puts a new plant on an empty tile. Free: used by spreading and mutation.</summary>
    private void PlaceNewPlant(GardenTile tile, PlantData data)
    {
        tile.Plant = new PlantInstance(data);
        Planted?.Invoke(tile);
    }

    /// <summary>
    /// Clears the plot. A mature plant is harvested for SeedsPerHarvest seeds of its type;
    /// an immature one (still a seed) is destroyed for nothing.
    /// </summary>
    /// <returns>Seeds gained, or -1 if there was nothing to remove.</returns>
    public int Remove(Vector2Int pos)
    {
        if (!tiles.TryGetValue(pos, out GardenTile tile) || tile.IsEmpty) return -1;

        PlantInstance removed = tile.Plant;
        tile.Plant = null;

        int gained = removed.IsMature ? SeedsPerHarvest : 0;
        Inventory.AddSeeds(removed.data, gained);

        Removed?.Invoke(tile, removed);
        return gained;
    }

    // ------------------------------------------------------------------- tick

    /// <summary>
    /// One day passes. Order:
    ///   1. Every plant ages; plants reaching maturityAge mature.
    ///   2. Mutation and spread are both decided from the state after step 1,
    ///      so plants born this tick cannot spread or mutate until the next tick.
    ///   3. Mutation comes first: a plot that matches any recipe never receives a spread,
    ///      even if its mutation roll failed this tick.
    /// </summary>
    public void Tick()
    {
        AgePlants();

        Dictionary<Vector2Int, PlantData> mutations = RollMutations(out HashSet<Vector2Int> recipePlots);
        Dictionary<Vector2Int, PlantData> spreads = RollSpreads(skip: recipePlots);

        // Natural growth is free; only player planting costs seeds.
        foreach (var kv in mutations) PlaceNewPlant(tiles[kv.Key], kv.Value);
        foreach (var kv in spreads) PlaceNewPlant(tiles[kv.Key], kv.Value);
    }

    private void AgePlants()
    {
        foreach (GardenTile tile in tiles.Values)
        {
            if (tile.IsEmpty) continue;

            bool wasMature = tile.Plant.IsMature;
            tile.Plant.age++;
            if (!wasMature && tile.Plant.IsMature)
                Matured?.Invoke(tile);
        }
    }

    /// <param name="recipePlots">Every plot that matched a recipe, whether or not its roll succeeded.</param>
    private Dictionary<Vector2Int, PlantData> RollMutations(out HashSet<Vector2Int> recipePlots)
    {
        var result = new Dictionary<Vector2Int, PlantData>();
        recipePlots = new HashSet<Vector2Int>();
        if (recipes.Count == 0) return result;

        foreach (GardenTile tile in tiles.Values)
        {
            if (!tile.IsEmpty) continue;

            Dictionary<PlantData, int> counts = CountMatureNeighboursBySpecies(tile.Position);
            if (counts.Count == 0) continue;

            Recipe recipe = recipes.FirstOrDefault(r => r.Matches(counts));
            if (recipe == null) continue;

            recipePlots.Add(tile.Position);
            if (rng.NextDouble() < recipe.Chance)
                result.Add(tile.Position, recipe.Mutant);
        }
        return result;
    }

    /// <summary>
    /// For each empty plot that matched no mutation recipe and has mature neighbours:
    /// one of the neighbouring species is picked at random (each species equally likely,
    /// however many neighbours it has), then rolled once against its spreadChance.
    /// </summary>
    private Dictionary<Vector2Int, PlantData> RollSpreads(HashSet<Vector2Int> skip)
    {
        var result = new Dictionary<Vector2Int, PlantData>();
        var species = new List<PlantData>(6);

        foreach (GardenTile tile in tiles.Values)
        {
            if (!tile.IsEmpty || skip.Contains(tile.Position)) continue;

            species.Clear();
            foreach (PlantInstance p in MatureNeighbours(tile.Position))
                if (!species.Contains(p.data))
                    species.Add(p.data);
            if (species.Count == 0) continue;

            PlantData chosen = species[rng.Next(species.Count)];
            if (rng.NextDouble() < Mathf.Clamp01(chosen.spreadChance))
                result.Add(tile.Position, chosen);
        }
        return result;
    }

    private Dictionary<PlantData, int> CountMatureNeighboursBySpecies(Vector2Int pos)
    {
        var counts = new Dictionary<PlantData, int>();
        foreach (PlantInstance p in MatureNeighbours(pos))
            counts[p.data] = counts.TryGetValue(p.data, out int c) ? c + 1 : 1;
        return counts;
    }

    // ----------------------------------------------------------------- recipe

    /// <summary>Validated, pre-aggregated form of a MutationSO.</summary>
    private class Recipe
    {
        public PlantData Mutant;
        public float Chance;
        public int TotalIngredients;
        private Dictionary<PlantData, int> needs;

        public static Recipe TryCreate(MutationSO so)
        {
            string problem = so.Validate(out Dictionary<PlantData, int> needs);
            if (problem != null)
            {
                Debug.LogWarning($"Mutation '{so.name}' skipped: {problem}", so);
                return null;
            }
            return new Recipe
            {
                Mutant = so.mutant,
                Chance = Mathf.Clamp01(so.mutationBaseChance),
                TotalIngredients = needs.Values.Sum(),
                needs = needs,
            };
        }

        /// <summary>"At least N of each ingredient" among the mature neighbours.</summary>
        public bool Matches(Dictionary<PlantData, int> counts)
        {
            foreach (var kv in needs)
                if (!counts.TryGetValue(kv.Key, out int have) || have < kv.Value)
                    return false;
            return true;
        }
    }
}

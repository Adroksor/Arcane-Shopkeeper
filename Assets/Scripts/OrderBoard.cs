using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

[Serializable]
public class OrderSettings
{
    [Tooltip("Orders created at the start of each week (Monday), inclusive range.")]
    [Min(0)] public int mondayOrdersMin = 2;
    [Min(0)] public int mondayOrdersMax = 3;

    [Tooltip("Chance on each day from Tuesday to Sunday that one new order arrives.")]
    [Range(0f, 1f)] public float newOrderChance = 0.6f;

    [Tooltip("No new orders arrive while this many are open.")]
    [Min(1)] public int maxOrders = 5;

    [Tooltip("Most different plants one order can ask for (the order UI fits 3).")]
    [Min(1)] public int maxIngredientTypes = 3;
}

public class OrderIngredient
{
    public PlantData Plant { get; }
    public int Amount { get; }

    public OrderIngredient(PlantData plant, int amount)
    {
        Plant = plant;
        Amount = amount;
    }
}

public class Order
{
    public IReadOnlyList<OrderIngredient> Ingredients { get; }
    /// <summary>Day of the week it arrived, 0 = Monday.</summary>
    public int DayCreated { get; }

    public Order(List<OrderIngredient> ingredients, int dayCreated)
    {
        Ingredients = ingredients;
        DayCreated = dayCreated;
    }
}

/// <summary>
/// Shop order rules, plain C# (no UI). ShopManager mirrors it on screen.
///   Monday:          all old orders are cleared, then 2-3 simple orders arrive.
///   Tuesday-Sunday:  each day may add one order; orders get harder as the week goes on.
/// Completing an order takes the listed seeds from the inventory. No reward yet.
/// </summary>
public class OrderBoard
{
    public event Action<Order> OrderAdded;
    public event Action<Order> OrderRemoved;

    public IReadOnlyList<Order> Orders => orders;

    /// <summary>
    /// How many breeding steps a plant is from base plants: 0 = base, 1 = mutant of base plants,
    /// 2 = needs a tier-1 plant, ... Worked out from the mutation recipes.
    /// </summary>
    public IReadOnlyDictionary<PlantData, int> Tiers => tiers;

    private readonly List<Order> orders = new List<Order>();
    private readonly List<PlantData> plants;
    private readonly Dictionary<PlantData, int> tiers;
    private readonly int highestTier;
    private readonly OrderSettings settings;
    private readonly Random rng;

    public OrderBoard(IEnumerable<PlantData> orderablePlants, IEnumerable<MutationSO> mutations,
                      OrderSettings settings, int? seed = null)
    {
        plants = (orderablePlants ?? Enumerable.Empty<PlantData>()).Where(p => p != null).Distinct().ToList();
        this.settings = settings ?? new OrderSettings();
        rng = seed.HasValue ? new Random(seed.Value) : new Random();

        tiers = ComputeTiers(plants, mutations);
        highestTier = tiers.Count > 0 ? tiers.Values.Max() : 0;
    }

    // ------------------------------------------------------------------ week

    /// <summary>Called on every day change (0 = Monday).</summary>
    public void OnDayStarted(int day)
    {
        if (day == 0)
        {
            StartWeek();
            return;
        }

        if (orders.Count < settings.maxOrders && rng.NextDouble() < settings.newOrderChance)
            AddOrder(GenerateOrder(day));
    }

    /// <summary>Clears last week's orders and creates Monday's.</summary>
    public void StartWeek()
    {
        ClearAll();

        int min = Mathf.Min(settings.mondayOrdersMin, settings.mondayOrdersMax);
        int max = Mathf.Max(settings.mondayOrdersMin, settings.mondayOrdersMax);
        int count = Mathf.Min(rng.Next(min, max + 1), settings.maxOrders);
        for (int i = 0; i < count; i++)
            AddOrder(GenerateOrder(0));
    }

    public void ClearAll()
    {
        for (int i = orders.Count - 1; i >= 0; i--)
        {
            Order order = orders[i];
            orders.RemoveAt(i);
            OrderRemoved?.Invoke(order);
        }
    }

    // ------------------------------------------------------------ completing

    public bool CanComplete(Order order, Inventory inventory) =>
        order != null && orders.Contains(order) &&
        order.Ingredients.All(ing => inventory.HasSeeds(ing.Plant, ing.Amount));

    /// <summary>Takes the seeds and removes the order; does nothing unless every ingredient is covered.</summary>
    public bool TryComplete(Order order, Inventory inventory)
    {
        if (!CanComplete(order, inventory)) return false;

        foreach (OrderIngredient ing in order.Ingredients)
            inventory.TryRemoveSeeds(ing.Plant, ing.Amount);

        orders.Remove(order);
        OrderRemoved?.Invoke(order);
        return true;
    }

    // ------------------------------------------------------------ generation

    // Difficulty by day (0 = Monday ... 6 = Sunday). Tweak these to change the weekly curve.
    //   ingredient types: Mon-Tue 1, Wed-Thu up to 2, Fri-Sun up to 3
    //   amount each:      Mon 1-2, Wed 1-3, Fri 1-4, Sun 1-5
    //   plant tier:       Mon-Tue base only, Wed-Thu up to tier 1, Fri-Sun up to tier 2 ...
    private static int MaxIngredientTypes(int day) => 1 + day / 2;
    private static int MaxAmount(int day) => 2 + day / 2;
    private static int MaxTier(int day) => day / 2;

    private void AddOrder(Order order)
    {
        if (order == null) return;
        orders.Add(order);
        OrderAdded?.Invoke(order);
    }

    private Order GenerateOrder(int day)
    {
        int maxTier = Mathf.Min(MaxTier(day), highestTier);
        List<PlantData> pool = plants.Where(p => tiers[p] <= maxTier).ToList();
        if (pool.Count == 0) return null;

        int maxTypes = Mathf.Min(MaxIngredientTypes(day), settings.maxIngredientTypes, pool.Count);
        int types = rng.Next(1, maxTypes + 1);

        // Pick `types` different plants (partial Fisher-Yates shuffle).
        for (int i = 0; i < types; i++)
        {
            int j = rng.Next(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        var ingredients = new List<OrderIngredient>(types);
        for (int i = 0; i < types; i++)
            ingredients.Add(new OrderIngredient(pool[i], rng.Next(1, MaxAmount(day) + 1)));

        return new Order(ingredients, day);
    }

    /// <summary>
    /// tier(base plant) = 0; tier(mutant) = 1 + highest tier among its ingredients.
    /// If a plant can be bred by several recipes, the easiest recipe counts.
    /// </summary>
    private static Dictionary<PlantData, int> ComputeTiers(List<PlantData> plants, IEnumerable<MutationSO> mutations)
    {
        const int Unknown = int.MaxValue;

        List<MutationSO> recipes = (mutations ?? Enumerable.Empty<MutationSO>())
            .Where(m => m != null && m.Validate(out _) == null)
            .ToList();
        var mutants = new HashSet<PlantData>(recipes.Select(r => r.mutant));

        var tiers = new Dictionary<PlantData, int>();
        foreach (PlantData p in plants) tiers[p] = mutants.Contains(p) ? Unknown : 0;
        foreach (MutationSO r in recipes)
        {
            if (!tiers.ContainsKey(r.mutant)) tiers[r.mutant] = Unknown;
            foreach (MutationIngredient ing in r.mutationIngredients)
                if (!tiers.ContainsKey(ing.plant)) tiers[ing.plant] = mutants.Contains(ing.plant) ? Unknown : 0;
        }

        // Relax until stable; each pass can only lower a tier, so this ends.
        bool changed = true;
        for (int pass = 0; changed && pass <= tiers.Count; pass++)
        {
            changed = false;
            foreach (MutationSO r in recipes)
            {
                int highestIngredient = r.mutationIngredients.Max(ing => tiers[ing.plant]);
                if (highestIngredient == Unknown) continue;

                int candidate = highestIngredient + 1;
                if (candidate < tiers[r.mutant])
                {
                    tiers[r.mutant] = candidate;
                    changed = true;
                }
            }
        }

        // Plants only reachable through a recipe loop: treat as the hardest tier.
        int known = tiers.Values.Where(t => t != Unknown).DefaultIfEmpty(0).Max();
        foreach (PlantData p in tiers.Keys.ToList())
            if (tiers[p] == Unknown) tiers[p] = known + 1;

        return tiers;
    }
}

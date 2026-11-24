using System.Collections.Generic;
using System.Linq;
using IngameDebugConsole;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Unity side of the shop: owns the OrderBoard, spawns an order card per order,
/// and passes day changes and "Complete order" clicks to it. Rules live in OrderBoard.
/// </summary>
public class ShopManager : MonoBehaviour
{
    public GameObject shopOrderPrefab;
    public GameObject shopOrderIngredientPrefab;

    [Tooltip("Where order cards are placed. A VerticalLayoutGroup is added if it has no layout group.")]
    public RectTransform ordersParent;

    public GardenManager garden;
    public Week week;

    public OrderSettings settings = new OrderSettings();

    public OrderBoard Board { get; private set; }

    private Inventory Inventory => garden.Simulation.Inventory;

    private readonly Dictionary<Order, ShopOrder> views = new Dictionary<Order, ShopOrder>();

    private void Start()
    {
        if (!EnsureLayout()) return;

        Board = new OrderBoard(garden.plantDatas, garden.mutations, settings);
        Board.OrderAdded += OnOrderAdded;
        Board.OrderRemoved += OnOrderRemoved;

        Inventory.Changed += OnInventoryChanged;
        week.DayChanged += Board.OnDayStarted;

        DebugLogConsole.AddCommand("Orders", "Lists open shop orders", OrdersCommand);
        DebugLogConsole.AddCommand("PlantTiers", "Lists each plant's breeding tier", TiersCommand);

        Board.StartWeek(); // the game starts on Monday
    }

    private void OnDestroy()
    {
        if (week != null && Board != null) week.DayChanged -= Board.OnDayStarted;
        if (garden != null && garden.Simulation != null) Inventory.Changed -= OnInventoryChanged;
    }

    // ---------------------------------------------------------- board events

    private void OnOrderAdded(Order order)
    {
        GameObject go = Instantiate(shopOrderPrefab, ordersParent, false);
        if (!go.TryGetComponent(out ShopOrder view))
            view = go.AddComponent<ShopOrder>();

        view.Init(order, shopOrderIngredientPrefab, Complete);
        view.Refresh(Inventory);
        views.Add(order, view);
    }

    private void OnOrderRemoved(Order order)
    {
        if (!views.TryGetValue(order, out ShopOrder view)) return;
        Destroy(view.gameObject);
        views.Remove(order);
    }

    private void OnInventoryChanged(PlantData plant, int seeds)
    {
        foreach (ShopOrder view in views.Values)
            view.Refresh(Inventory);
    }

    // ------------------------------------------------------------- commands

    private void Complete(ShopOrder view)
    {
        if (!Board.TryComplete(view.Order, Inventory))
            Debug.Log("Not enough seeds to complete this order");
    }

    private void OrdersCommand()
    {
        if (Board.Orders.Count == 0) { Debug.Log("No open orders"); return; }
        foreach (Order order in Board.Orders)
        {
            string items = string.Join(", ", order.Ingredients.Select(i => $"{i.Amount}x {i.Plant.plantName}"));
            Debug.Log($"[day {order.DayCreated}] {items}");
        }
    }

    private void TiersCommand()
    {
        foreach (var kv in Board.Tiers.OrderBy(kv => kv.Value))
            Debug.Log($"{kv.Key.plantName}: tier {kv.Value}");
    }

    // --------------------------------------------------------------- layout

    /// <returns>False if required references are missing (the shop then stays off).</returns>
    private bool EnsureLayout()
    {
        if (ordersParent == null || shopOrderPrefab == null || shopOrderIngredientPrefab == null || garden == null || week == null)
        {
            Debug.LogError("ShopManager: assign ordersParent, both prefabs, garden and week in the Inspector", this);
            enabled = false;
            return false;
        }
        if (ordersParent.GetComponent<LayoutGroup>() != null) return true;

        var layout = ordersParent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 10f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return true;
    }
}

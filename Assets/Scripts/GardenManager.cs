using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using IngameDebugConsole;

/// <summary>
/// Unity side of the garden: owns the GardenSimulation, spawns/destroys GameObjects
/// in response to its events, and exposes commands to the UI and debug console.
/// All garden rules live in GardenSimulation.
/// </summary>
public class GardenManager : MonoBehaviour
{
    public Tilemap tilemap;
    public GameObject hexPrefab;
    public GameObject plantPrefab;
    public Transform plotsParent;
    public Transform plantsParent;
    public PlantData[] plantDatas;
    public MutationSO[] mutations;

    [Tooltip("Use a fixed random seed so ticks are reproducible.")]
    public bool useFixedSeed;
    public int seed;

    [Tooltip("Seeds of every plant type in plantDatas the player starts with.")]
    [Min(0)] public int startingSeeds = 10;

    public Week weekManager;

    public GardenSimulation Simulation { get; private set; }

    private readonly Dictionary<Vector2Int, HexCoords> plotViews = new Dictionary<Vector2Int, HexCoords>();
    private readonly Dictionary<Vector2Int, Plant> plantViews = new Dictionary<Vector2Int, Plant>();
    private Dictionary<string, PlantData> tempPlantNameToData;

    private void Awake()
    {
        Simulation = new GardenSimulation(mutations, useFixedSeed ? seed : (int?)null);
        Simulation.PlotAdded += OnPlotAdded;
        Simulation.Planted += OnPlanted;
        Simulation.Removed += OnRemoved;
        Simulation.Matured += OnMatured;

        foreach (PlantData plant in plantDatas)
            Simulation.Inventory.AddSeeds(plant, startingSeeds);
    }

    private void Start()
    {
        InitCommands();
        TestSpawn(8);

        tempPlantNameToData = new Dictionary<string, PlantData>();
        tempPlantNameToData.Add("fire",plantDatas[0]);
        tempPlantNameToData.Add("nature",plantDatas[1]);
        tempPlantNameToData.Add("water",plantDatas[2]);
        tempPlantNameToData.Add("stone",plantDatas[3]);
        tempPlantNameToData.Add("dark",plantDatas[4]);
        tempPlantNameToData.Add("light",plantDatas[5]);
        tempPlantNameToData.Add("black",plantDatas[6]);
        tempPlantNameToData.Add("mud",plantDatas[7]);
    }

    private void TestSpawn(int size)
    {
        for (int i = -size; i < size; i++)
            for (int j = -size; j < size; j++)
                CreateHex(i, j);
    }

    private void InitCommands()
    {
        DebugLogConsole.AddCommand<int, int>("Spawn","Creates a hex at axial coordinates", CreateHex);
        DebugLogConsole.AddCommand<int, int>("SpawnNeighbours","Creates a neighbours around axial coordinates", CreateNeighbours);

        DebugLogConsole.AddCommand<int, int, string>("Plant","Plants a crop", PlantCrop);
        DebugLogConsole.AddCommand<int, int>("Remove","Removes a crop", RemoveCrop);

        DebugLogConsole.AddCommand("GetCrops","List all crops on board", GetCropsCommand);
        DebugLogConsole.AddCommand("Tick","Performs a time tick", GardenTick);
        DebugLogConsole.AddCommand("Inventory","Lists seeds in the inventory", InventoryCommand);
    }

    // --------------------------------------------------------------- commands

    public bool HasPlot(Vector2Int pos) => Simulation.HasPlot(pos);

    public void CreateHex(int x, int y) => Simulation.AddPlot(new Vector2Int(x, y));

    public void CreateNeighbours(int x, int y)
    {
        var center = new Vector2Int(x, y);
        foreach (Vector2Int dir in HexMath.Directions)
            Simulation.AddPlot(center + dir);
    }

    public void PlantCrop(int x, int y, string plantName)
    {
        if (!tempPlantNameToData.TryGetValue(plantName, out PlantData data))
        {
            Debug.LogWarning($"Unknown plant name: {plantName}");
            return;
        }
        PlantCrop(new Vector2Int(x, y), data);
    }

    public void PlantCrop(Vector2Int pos, PlantData data) => Simulation.Plant(pos, data);

    public void RemoveCrop(int x, int y) => RemoveCrop(new Vector2Int(x, y));

    public void RemoveCrop(Vector2Int pos)
    {
        int gained = Simulation.Remove(pos);
        if (gained > 0)
            Debug.Log($"Harvested {gained} seeds");
    }

    public void InventoryCommand()
    {
        if (Simulation.Inventory.Seeds.Count == 0) { Debug.Log("Inventory is empty"); return; }
        foreach (var kv in Simulation.Inventory.Seeds)
            Debug.Log($"{kv.Key.plantName}: {kv.Value} seeds");
    }

    public void GetCropsCommand()
    {
        foreach (GardenTile tile in Simulation.Tiles.Values)
            if (!tile.IsEmpty)
                Debug.Log($"{tile.Position} {tile.Plant.data.plantName} (age {tile.Plant.age}{(tile.Plant.IsMature ? ", mature" : "")})");
    }

    public void GardenTick()
    {
        Simulation.Tick();
        weekManager.NextDay();
    }

    // ------------------------------------------------------- simulation events

    private void OnPlotAdded(GardenTile tile)
    {
        Vector3 spawnPosition = tilemap.CellToWorld(HexMath.AxialToOffset(tile.Position));
        GameObject go = Instantiate(hexPrefab, spawnPosition, Quaternion.identity);
        go.transform.SetParent(plotsParent, false);

        HexCoords view = go.GetComponent<HexCoords>();
        view.SetText(tile.Position);
        plotViews.Add(tile.Position, view);
        RefreshLabel(tile.Position);
    }

    private void OnPlanted(GardenTile tile)
    {
        Vector3 spawnPosition = tilemap.CellToWorld(HexMath.AxialToOffset(tile.Position));
        GameObject go = Instantiate(plantPrefab, spawnPosition, Quaternion.identity);
        go.transform.SetParent(plantsParent, false);

        Plant view = go.GetComponent<Plant>();
        view.Init(tile.Plant);
        plantViews.Add(tile.Position, view);

        // A plant may start mature (maturityAge 0), which changes neighbour counts.
        RefreshLabelsAround(tile.Position);
    }

    private void OnRemoved(GardenTile tile, PlantInstance removed)
    {
        if (plantViews.TryGetValue(tile.Position, out Plant view))
        {
            Destroy(view.gameObject);
            plantViews.Remove(tile.Position);
        }
        RefreshLabelsAround(tile.Position);
    }

    private void OnMatured(GardenTile tile)
    {
        if (plantViews.TryGetValue(tile.Position, out Plant view))
            view.Refresh();
        RefreshLabelsAround(tile.Position);
    }

    // ------------------------------------------------------------ debug labels

    private void RefreshLabelsAround(Vector2Int pos)
    {
        RefreshLabel(pos);
        foreach (Vector2Int dir in HexMath.Directions)
            RefreshLabel(pos + dir);
    }

    /// <summary>Empty plots show their mature-neighbour count; planted plots show 0.</summary>
    private void RefreshLabel(Vector2Int pos)
    {
        if (!plotViews.TryGetValue(pos, out HexCoords view)) return;
        if (!Simulation.TryGetTile(pos, out GardenTile tile)) return;

        view.NeighbourCount = tile.IsEmpty ? Simulation.CountMatureNeighbours(pos) : 0;
    }
}

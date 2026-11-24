using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using IngameDebugConsole;

public class GardenManager : MonoBehaviour
{
    public Tilemap tilemap;
    public GameObject hexPrefab;
    public GameObject plantPrefab;
    public Transform plotsParent;
    public Transform plantsParent;
    public PlantData[] plantDatas;
    public MutationSO[] mutations;

    public Dictionary<Vector2Int, int> adjacentPlantCount = new Dictionary<Vector2Int, int>();
    public Dictionary<Vector2Int, GameObject> plots = new Dictionary<Vector2Int, GameObject>();
    public Dictionary<Vector2Int, Plant> plants = new Dictionary<Vector2Int, Plant>();
    private Dictionary<string, PlantData> tempPlantNameToData;


    public Week weekManager;
    
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
        {
            for (int j = -size; j < size; j++)
            {
                CreateHex(i, j);
            }
        }
    }

    private void InitCommands()
    {
        DebugLogConsole.AddCommand<int, int>("Spawn","Creates a hex at axial coordinates", CreateHex);
        DebugLogConsole.AddCommand<int, int>("SpawnNeighbours","Creates a neighbours around axial coordinates", CreateNeighbours);
        
        DebugLogConsole.AddCommand<int, int, string>("Plant","Plants a crop", PlantCrop);
        DebugLogConsole.AddCommand<int, int>("Remove","Removes a crop", RemoveCrop);

        DebugLogConsole.AddCommand("GetCrops","List all crops on board", GetCropsCommand);
        DebugLogConsole.AddCommand("Tick","Performs a time tick", GardenTick);
    }
    
    public void CreateHex(int x, int y)
    {
        Vector2Int axialPosition = new Vector2Int(x, y);
        Vector3Int tilePosition = HexMath.AxialToOffset(axialPosition);
        Vector3 spawnPosition = tilemap.CellToWorld(tilePosition);
        GameObject newHex = Instantiate(hexPrefab, spawnPosition, Quaternion.identity);
        newHex.transform.SetParent(plotsParent, false);
        newHex.GetComponent<HexCoords>().SetText(axialPosition);
        adjacentPlantCount.Add(axialPosition, 0);
        plots.Add(axialPosition, newHex);
    }

    public void CreateNeighbours(int x, int y)
    {
        Vector2Int axialPosition = new Vector2Int(x, y);

        foreach (Vector2Int neighbourOffset in HexMath.Directions)
        {
            Vector2Int neighbourPosition = axialPosition + neighbourOffset;
            CreateHex(neighbourPosition.x, neighbourPosition.y);
        }
    }

    public void PlantCrop(int x, int y, string plantName)
    {
        if (!tempPlantNameToData.TryGetValue(plantName, out PlantData data))
        {
            Debug.LogWarning($"Unknown plant name: {plantName}");
            return;
        }

        PlantCrop(x, y, data.plantId);
    }
    
    public void PlantCrop(int x, int y, int plantID)
    {
        
        Vector2Int axialPosition = new Vector2Int(x, y);
        
        if (!plots.ContainsKey(axialPosition)) { Debug.Log("No hex there"); return; }
        if (plants.ContainsKey(axialPosition)) { Debug.Log("There is already a plant there"); return; }
        
        adjacentPlantCount[axialPosition] = 0;
        SetAdjacentCount(axialPosition, adjacentPlantCount[axialPosition]);
        adjacentPlantCount.Remove(axialPosition);

        
        Vector3Int tilePosition = HexMath.AxialToOffset(axialPosition);
        Vector3 spawnPosition = tilemap.CellToWorld(tilePosition);
        
        PlantData plantData = plantDatas.FirstOrDefault(n => n.plantId == plantID);
        GameObject newPlant = Instantiate(plantPrefab, spawnPosition, Quaternion.identity);
        newPlant.transform.SetParent(plantsParent, false);
        Plant plant = newPlant.GetComponent<Plant>();
        plant.plantInstance.data = plantData;
        plant.InitiatePlant();

        plants.Add(axialPosition, plant);
    }
    
    public void RemoveCrop(int x, int y)
    {
        Vector2Int axialPosition = new Vector2Int(x, y);

        if (!plants.TryGetValue(axialPosition, out Plant plant)) return;
        bool wasMature = plant.plantInstance.isMature;
        Destroy(plant.gameObject);
        plants.Remove(axialPosition);
        
        adjacentPlantCount.Add(axialPosition, 0);
        List<Vector2Int> neighboursPositions = GetNeighbours(axialPosition);
        foreach (Vector2Int neighbour in neighboursPositions)
        {
            if (plants.TryGetValue(neighbour, out Plant neighbourPlant))
            {
                if(neighbourPlant.plantInstance.isMature)
                    adjacentPlantCount[axialPosition]++;
            }
        }
        SetAdjacentCount(axialPosition, adjacentPlantCount[axialPosition]);
        
        if(wasMature)
            DecrementNeighbourCounts(axialPosition);
        
    }

    public void GetCropsCommand()
    {
        foreach (var kv in plants)
            Debug.Log($"{kv.Key} {kv.Value.plantInstance.data.plantName}");
    }
    
    public List<Vector2Int> GetNeighbours(Vector2Int center)
    {
        List<Vector2Int> neighbours = new List<Vector2Int>();
        foreach (Vector2Int location in HexMath.Directions)
        {
            neighbours.Add(location + center);
        }
        return neighbours;
    }


    public void GardenTick()
    {
        UpdateMaturity();
        Mutate();
        Spread();
        weekManager.NextDay();
    }

    private void UpdateMaturity()
    {
        foreach (var kv in plants)
        {
            PlantInstance pi = kv.Value.plantInstance;
            pi.age += 1;
            if (!pi.isMature && pi.age >= pi.data.maturityAge)
            {
                kv.Value.SetMature();
                pi.isMature = true;
                IncrementNeighbourCounts(kv.Key);

            }
        }
    }

    public void IncrementNeighbourCounts(Vector2Int axialPosition)
    {
        List<Vector2Int> tempNeighbours = GetNeighbours(axialPosition);
        foreach (Vector2Int neighbour in tempNeighbours)
        {
            if (adjacentPlantCount.ContainsKey(neighbour))
            {
                adjacentPlantCount[neighbour]++;
                SetAdjacentCount(neighbour, adjacentPlantCount[neighbour]);

            }
        }
    }
    
    public void DecrementNeighbourCounts(Vector2Int axialPosition)
    {
        List<Vector2Int> tempNeighbours = GetNeighbours(axialPosition);
        foreach (Vector2Int neighbour in tempNeighbours)
        {
            if (adjacentPlantCount.ContainsKey(neighbour))
            {
                adjacentPlantCount[neighbour]--;
                SetAdjacentCount(neighbour, adjacentPlantCount[neighbour]);
            }
        }
    }
    
    private void SetAdjacentCount(Vector2Int pos, int value)
    {
        if (!plots.TryGetValue(pos, out GameObject go)) return;
        if (!adjacentPlantCount.ContainsKey(pos)) return;

        adjacentPlantCount[pos] = value;
        go.GetComponent<HexCoords>().NeighbourCount = value;   // property setter updates text
    }

    public void Spread()
    {
        Dictionary<Vector2Int, int> toPlant = new Dictionary<Vector2Int, int>();
        foreach (var kv in plants)
        {
            PlantInstance pi = kv.Value.plantInstance;
            if (!pi.isMature) continue;

            foreach (Vector2Int neighbourPosition in GetNeighbours(kv.Key))
            {
                if (!plots.ContainsKey(neighbourPosition)) continue;
                if (plants.ContainsKey(neighbourPosition)) continue;

                if (Random.value < pi.data.spreadBaseMultiplier)
                    toPlant.TryAdd(neighbourPosition, pi.data.plantId);
            }
        }
        var taken = new HashSet<Vector2Int>();
        foreach (var (pos, type) in toPlant)
        {
            if (!taken.Add(pos)) continue;
            PlantCrop(pos.x, pos.y, type);
        }
    }
    
    public void Mutate()
    {
        var toPlant = new List<(Vector2Int spot, PlantData mutant)>();

        foreach (var kv in adjacentPlantCount)
        {
            Vector2Int spot = kv.Key;

            if (kv.Value < 2) continue;
            if (plants.ContainsKey(spot)) continue;

            var counts = new Dictionary<PlantData, int>();
            foreach (Vector2Int n in GetNeighbours(spot))
            {
                if (!plants.TryGetValue(n, out Plant plant)) continue;
                if (!plant.plantInstance.isMature) continue;

                PlantData data = plant.plantInstance.data;
                counts[data] = counts.TryGetValue(data, out int c) ? c + 1 : 1;
            }

            MutationSO best = null;
            foreach (MutationSO m in mutations)
            {
                if (!Matches(m, counts)) continue;
                best = m;
            }

            if (best == null) continue;
            if (Random.value >= best.mutationBaseChance) continue;
            toPlant.Add((spot, best.mutant));
        }

        var taken = new HashSet<Vector2Int>();
        foreach (var (spot, mutant) in toPlant)
        {
            if (!taken.Add(spot)) continue;
            PlantCrop(spot.x, spot.y, mutant.plantId);
        }
    }
    
    private bool Matches(MutationSO m, Dictionary<PlantData, int> counts)
    {
        if (m.mutationIngredients == null || m.mutationIngredients.Count == 0)
            return false;
        foreach (MutationIngredient ing in m.mutationIngredients)
            if (!counts.TryGetValue(ing.plant, out int have) || have < ing.amount)
                return false;
        return true;
    }
}


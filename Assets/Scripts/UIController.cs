using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public List<GameObject> buttons;
    public GameObject removalButton;
    public GameObject tickButton;
    public PlantData selectedPlant;


    public GardenManager hexGrid;

    [Header("Seed count labels")]
    public float seedCountFontSize = 14f;
    public Color seedCountColor = Color.white;

    // plant type -> label on its button
    private readonly Dictionary<PlantData, TMP_Text> seedCountLabels = new Dictionary<PlantData, TMP_Text>();


    private void Start()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            int index = i; // capture per-iteration copy
            Button b = buttons[i].GetComponent<Button>();
            b.onClick.AddListener(() => SelectPlant(index));
        }
        Button rb = removalButton.GetComponent<Button>();
        rb.onClick.AddListener(() => SelectRemoval());

        Button tb = tickButton.GetComponent<Button>();
        tb.onClick.AddListener(() => Tick());

        CreateSeedCountLabels();
    }

    private void OnDestroy()
    {
        if (hexGrid != null && hexGrid.Simulation != null)
            hexGrid.Simulation.Inventory.Changed -= OnInventoryChanged;
    }

    /// <summary>
    /// Adds a small seed counter to the bottom-right corner of each seed button.
    /// Button i belongs to hexGrid.plantDatas[i].
    /// </summary>
    private void CreateSeedCountLabels()
    {
        Inventory inventory = hexGrid.Simulation.Inventory;

        int count = Mathf.Min(buttons.Count, hexGrid.plantDatas.Length);
        for (int i = 0; i < count; i++)
        {
            PlantData plant = hexGrid.plantDatas[i];
            if (plant == null || seedCountLabels.ContainsKey(plant)) continue;

            var go = new GameObject("SeedCount", typeof(RectTransform));
            go.transform.SetParent(buttons[i].transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f); // bottom-right
            rect.anchoredPosition = new Vector2(-2f, 1f);
            rect.sizeDelta = new Vector2(40f, 20f);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = seedCountFontSize;
            label.color = seedCountColor;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.BottomRight;
            label.raycastTarget = false; // clicks go through to the button
            label.text = inventory.GetSeeds(plant).ToString();

            seedCountLabels.Add(plant, label);
        }

        inventory.Changed += OnInventoryChanged;
    }

    private void OnInventoryChanged(PlantData plant, int seeds)
    {
        if (seedCountLabels.TryGetValue(plant, out TMP_Text label))
            label.text = seeds.ToString();
    }

    private void SelectPlant(int id)
    {
        selectedPlant = hexGrid.plantDatas[id];
    }

    private void SelectRemoval()
    {
        selectedPlant = null;
    }

    private void Tick()
    {
        hexGrid.GardenTick();
    }

    // Drag state: a press that starts on the garden keeps acting while the button is held.
    private bool isDragging;
    private Vector2Int? lastDragCell;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // A press that starts on a UI element (seed button, Tick...) must not plant or harvest.
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            isDragging = !overUI;
            lastDragCell = null;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            lastDragCell = null;
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            Vector2Int cell = MouseCell();
            if (cell != lastDragCell) // act once per cell entered, not every frame
            {
                lastDragCell = cell;
                ApplyToolAt(cell);
            }
        }
    }

    private Vector2Int MouseCell()
    {
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return HexMath.OffsetToAxial(hexGrid.tilemap.WorldToCell(mouseWorldPosition));
    }

    /// <summary>Plants the selected seed, or harvests when the removal tool is selected.</summary>
    private void ApplyToolAt(Vector2Int cell)
    {
        // Skip plots the tool can't affect, so dragging over them stays silent.
        if (!hexGrid.Simulation.TryGetTile(cell, out GardenTile tile)) return;

        if (selectedPlant == null)
        {
            if (!tile.IsEmpty) hexGrid.RemoveCrop(cell);
        }
        else
        {
            if (tile.IsEmpty) hexGrid.PlantCrop(cell, selectedPlant);
        }
    }
}
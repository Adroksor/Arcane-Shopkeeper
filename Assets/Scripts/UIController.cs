using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public List<GameObject> buttons;
    public GameObject removalButton;
    public GameObject tickButton;
    public PlantData selectedPlant;
    
    
    public GardenManager hexGrid;


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

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePosition = Input.mousePosition;
            Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
            
            Vector2Int axialPosition = HexMath.OffsetToAxial(hexGrid.tilemap.WorldToCell(mouseWorldPosition));
            if (hexGrid.plots.ContainsKey(axialPosition))
            {
                if (selectedPlant == null)
                {
                    hexGrid.RemoveCrop(axialPosition.x, axialPosition.y);
                }
                else
                {
                    hexGrid.PlantCrop(axialPosition.x, axialPosition.y, selectedPlant.plantId);
                }
            }
            
        }
    }
}
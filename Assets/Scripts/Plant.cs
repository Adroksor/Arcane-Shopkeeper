using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plant : MonoBehaviour
{
    public PlantInstance plantInstance;

    public void InitiatePlant()
    {
        transform.GetComponentInChildren<SpriteRenderer>().sprite = plantInstance.data.seedSprite;
    }

    public void SetMature()
    {
        transform.GetComponentInChildren<SpriteRenderer>().sprite = plantInstance.data.plantSprite;
    }
}

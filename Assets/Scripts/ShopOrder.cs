using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopOrder : MonoBehaviour
{
    public List<OrderIngredient> order;
}


[System.Serializable]
public class OrderIngredient
{
    public Sprite sprite;
    public int amount;
}
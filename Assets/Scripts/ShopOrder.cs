using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for one order card (ShopOrder prefab). State lives in OrderBoard.
/// The references are found by name if left empty, so the prefab works without setup:
///   "Ingredients" - row the ingredient entries go into (its placeholder children are removed)
///   Button        - the "Complete order" button
/// </summary>
public class ShopOrder : MonoBehaviour
{
    public Transform ingredientsParent;
    public Button completeButton;

    [Tooltip("Amount text colour when the player has fewer seeds than the order asks for.")]
    public Color missingColor = new Color(0.85f, 0.2f, 0.2f);

    public Order Order { get; private set; }

    private readonly List<(OrderIngredient ingredient, TMP_Text label, Color normalColor)> entries =
        new List<(OrderIngredient, TMP_Text, Color)>();

    public void Init(Order order, GameObject ingredientPrefab, Action<ShopOrder> onComplete)
    {
        Order = order;
        FindReferences();

        // Remove the placeholder entries that came with the prefab.
        foreach (Transform child in ingredientsParent)
            Destroy(child.gameObject);

        foreach (OrderIngredient ing in order.Ingredients)
        {
            GameObject go = Instantiate(ingredientPrefab, ingredientsParent, false);

            Image icon = FindIcon(go);
            if (icon != null)
            {
                icon.sprite = ing.Plant.plantSprite;
                icon.preserveAspect = true;
            }

            TMP_Text label = go.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = ing.Amount.ToString();
                entries.Add((ing, label, label.color));
            }
        }

        completeButton.onClick.AddListener(() => onComplete(this));
    }

    /// <summary>Greys out the button and colours missing amounts when seeds are short.</summary>
    public void Refresh(Inventory inventory)
    {
        bool canComplete = true;
        foreach (var (ingredient, label, normalColor) in entries)
        {
            bool enough = inventory.HasSeeds(ingredient.Plant, ingredient.Amount);
            label.color = enough ? normalColor : missingColor;
            canComplete &= enough;
        }
        completeButton.interactable = canComplete;
    }

    private void FindReferences()
    {
        if (ingredientsParent == null) ingredientsParent = transform.Find("Ingredients");
        if (completeButton == null) completeButton = GetComponentInChildren<Button>(true);

        if (ingredientsParent == null) Debug.LogError("ShopOrder: no child named 'Ingredients'", this);
        if (completeButton == null) Debug.LogError("ShopOrder: no Button found", this);
    }

    /// <summary>The ingredient prefab's icon is its child named "Image" (the root Image is the background).</summary>
    private static Image FindIcon(GameObject ingredient)
    {
        Transform child = ingredient.transform.Find("Image");
        if (child != null && child.TryGetComponent(out Image img)) return img;

        foreach (Image candidate in ingredient.GetComponentsInChildren<Image>(true))
            if (candidate.gameObject != ingredient) return candidate;
        return null;
    }
}

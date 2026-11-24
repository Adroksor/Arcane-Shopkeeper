using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class HexCoords : MonoBehaviour
{
    public TMP_Text text;
    public TMP_Text neighbourCount;
    
    private int _neighbourCount;
    public int NeighbourCount
    {
        get => _neighbourCount;
        set
        {
            if (_neighbourCount == value) return;
            _neighbourCount = value;
            neighbourCount.text = $"\n{value.ToString()}";
        }
    }

    public void SetText(Vector2Int position)
    {
        int q, r, s = 0;
        q = position.x;
        r = position.y;
        s = -q - r;
        text.text = $"{q} {r} {s}";
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IngameDebugConsole;

public static class HexMath
{
    public static Vector2Int OffsetToAxial(Vector3Int cell)
    {
        int r = cell.y;
        int q = cell.x - (cell.y - (cell.y & 1)) / 2;

        return new Vector2Int(q, r);
    }
    
    public static Vector3Int AxialToOffset(Vector2Int axial)
    {
        int y = axial.y;
        int x = axial.x + (axial.y - (axial.y & 1)) / 2;

        return new Vector3Int(x, y, 0);
    }
    
    public static Vector2Int[] Directions = new[]
    {
        new Vector2Int( 1,  0),
        new Vector2Int( 1, -1),
        new Vector2Int( 0, -1),
        new Vector2Int(-1,  0),
        new Vector2Int(-1,  1),
        new Vector2Int( 0,  1)
    };
    
    private static readonly float[] SpreadChance = { 0f, 0.16f, 0.33f, 0.50f, 0.65f, 0.80f, 0.95f };
}

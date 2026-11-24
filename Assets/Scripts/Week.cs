using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Week : MonoBehaviour
{
    public TMP_Text currentDayText;

    private static readonly string[] Days =
        { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

    private int currentDayIndex = 0; // 0..6

    /// <summary>Raised with the new day index (0 = Monday ... 6 = Sunday) when the day changes.</summary>
    public event System.Action<int> DayChanged;

    public string CurrentDay
    {
        get => Days[currentDayIndex];
        set
        {
            int idx = System.Array.IndexOf(Days, value);
            if (idx < 0 || idx == currentDayIndex) return;
            currentDayIndex = idx;
            if (currentDayText != null) currentDayText.text = $"{value}";
            DayChanged?.Invoke(currentDayIndex);
        }
    }

    private void Start()
    {
        if (currentDayText != null) currentDayText.text = $"{CurrentDay}";
    }

    public void NextDay()
    {
        CurrentDay = Days[(currentDayIndex + 1) % Days.Length];
    }

    public int GetCurrentDay()
    {
        return currentDayIndex;
    }
}
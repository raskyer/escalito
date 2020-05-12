﻿using UnityEngine;
using UnityEngine.UI;

public class ClockController : MonoBehaviour
{
    private const int SecondsPerPlay = 60;
    private const int SecondsPerPause = 300;
    private const int MinHour = 4;
    private const int MaxHour = 10;

    public Text clockText;
    
    private int _secondsPerDay;
    private float _time;

    public int Minutes => Mathf.FloorToInt(_time % 1f * 24f % 1f * 60f);
    public int Hours => Mathf.FloorToInt(_time % 1f * 24f);
    public int Day => Mathf.FloorToInt(_time % 1f);
    
    private void Update()
    {
        _secondsPerDay = InWorkingHour() ? SecondsPerPlay : SecondsPerPause;
        _time += Time.deltaTime / _secondsPerDay;

        print(Day);
        
        clockText.text = $"{Hours:00}:{Minutes:00}";
    }

    private bool InWorkingHour()
    {
        return Hours < MinHour || Hours > MaxHour;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Clock : MonoBehaviour
{
    public TextMeshProUGUI whiteClockText;
    public TextMeshProUGUI blackClockText;
    private float startingTimeMinutes = 15f;   // Change this for different time controls
    private float increment = 10f;

    private float whiteTimeLeft;
    private float blackTimeLeft;
    private bool isRunning = false;
    private bool whiteToMove = true;
    public GraphicalBoard graphicalBoard;

    private void Update()
    {
        if (!isRunning) return;

        if (whiteToMove)
        {
            whiteTimeLeft -= Time.deltaTime;
            if (whiteTimeLeft <= 0)
            {
                whiteTimeLeft = 0;
                isRunning = false;
                // Tell board White lost on time
                graphicalBoard.TimeLoss(true);   // white lost
            }
            UpdateDisplay();
        }
        else
        {
            blackTimeLeft -= Time.deltaTime;
            if (blackTimeLeft <= 0)
            {
                blackTimeLeft = 0;
                isRunning = false;
                graphicalBoard.TimeLoss(false);  // black lost
            }
            UpdateDisplay();
        }
    }

    public void StartClocks()
    {
        whiteToMove = true;
        isRunning = true;
    }

    public void SwitchTurn(bool isWhiteTurn)
    {
        AddTime(!isWhiteTurn);
        whiteToMove = isWhiteTurn;
        
    }

    public void StopClocks()
    {
        isRunning = false;
    }

    private void UpdateDisplay()
    {
        whiteClockText.text = FormatTime(whiteTimeLeft);
        blackClockText.text = FormatTime(blackTimeLeft);
    }

    private string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60);
        int secs = Mathf.FloorToInt(seconds % 60);
        return $"{minutes:00}:{secs:00}";
    }

    // Optional: Add time (for increment)
    public void AddTime(bool toWhite)
    {
        if (toWhite) whiteTimeLeft += increment;
        else blackTimeLeft += increment;
    }

    public void Start()
    {
        float seconds = startingTimeMinutes * 60f;
        whiteTimeLeft = seconds;
        blackTimeLeft = seconds;
        UpdateDisplay();
    }
}

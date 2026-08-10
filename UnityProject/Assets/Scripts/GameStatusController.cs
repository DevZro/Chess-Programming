using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameStatusController : MonoBehaviour
{
    [Header("References")]
    public Button actionButton;
    public TextMeshProUGUI statusText;
    public GraphicalBoard graphicalBoard;

    private void Start()
    {
        
        actionButton.onClick.AddListener(OnButtonclicked);
        gameObject.SetActive(true);
        ShowStartState();
    }

    // Called at the beginning and when game ends
    private void ShowStartState()
    {
        gameObject.SetActive(true);           // Make panel visible
        statusText.text = "START GAME";
        actionButton.interactable = true;
    }

    private void OnButtonclicked()
    {
        gameObject.SetActive(false);
        graphicalBoard.StartNewGame();
        
        // Hide the panel as soon as game starts
        
    }
    public void ShowGameResult(string result)
    {
        gameObject.SetActive(true);           // Show the panel again
        statusText.text = result;             // e.g. "White wins!" or "Draw"
        actionButton.interactable = false; 
    }
}
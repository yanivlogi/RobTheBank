using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI turnStatusText;
    private TurnManager turnManager;

    void Start()
    {
        turnManager = FindObjectOfType<TurnManager>();
        turnManager.onPlayerTurnChanged.AddListener(UpdateTurnStatus);
        turnManager.onDiceRolled.AddListener(UpdateDiceResult);
    }

    public void UpdateTurnStatus(int playerNumber)
    {
        if (turnStatusText != null)
            turnStatusText.text = $"תור שחקן {playerNumber + 1}";
    }

    public void UpdateDiceResult(int diceResult)
    {
        if (turnStatusText != null)
            turnStatusText.text = $"תוצאת קוביות: {diceResult}";
    }
}

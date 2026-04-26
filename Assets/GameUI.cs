using UnityEngine;
using TMPro;

public class GameUI : MonoBehaviour
{
    [Header("Turn Info")]
    public TMP_Text currentPlayerText;
    public TMP_Text timerText;
    public TMP_Text gameStateText;

    [Header("Player Panels (one per player slot, in order)")]
    public TMP_Text[] playerNameTexts;
    public TMP_Text[] playerPointsTexts;

    void Start()
    {
        if (TurnManager.instance != null)
        {
            TurnManager.instance.onPlayerTurnChanged.AddListener(OnTurnChanged);
            TurnManager.instance.onTurnTimerTick.AddListener(OnTimerTick);
        }
    }

    void OnDestroy()
    {
        if (TurnManager.instance != null)
        {
            TurnManager.instance.onPlayerTurnChanged.RemoveListener(OnTurnChanged);
            TurnManager.instance.onTurnTimerTick.RemoveListener(OnTimerTick);
        }
    }

    void Update()
    {
        if (TurnManager.instance == null) return;

        RefreshCurrentPlayer();
        RefreshPlayerPanels();

        if (gameStateText != null)
            gameStateText.text = TurnManager.instance.GetCurrentStateInfo();
    }

    private void OnTurnChanged(int playerIndex)
    {
        RefreshCurrentPlayer();
    }

    private void OnTimerTick(float remaining)
    {
        if (timerText != null)
            timerText.text = remaining > 0 ? Mathf.CeilToInt(remaining) + "s" : "—";
    }

    private void RefreshCurrentPlayer()
    {
        if (currentPlayerText == null) return;
        int idx = TurnManager.instance.currentPlayer;
        string name = GetName(idx);
        bool isMe   = PlayerManager.LocalPlayerIndex == idx;
        currentPlayerText.text = $"Current: {name}{(isMe ? " (You)" : "")}";
    }

    private void RefreshPlayerPanels()
    {
        int playerCount = TurnManager.instance.totalPlayers;
        for (int i = 0; i < (playerNameTexts?.Length ?? 0); i++)
        {
            if (playerNameTexts[i] == null) continue;
            if (i < playerCount)
            {
                playerNameTexts[i].text = GetName(i) + (PlayerManager.LocalPlayerIndex == i ? " (You)" : "");
            }
            else
            {
                playerNameTexts[i].text = "";
            }
        }

        for (int i = 0; i < (playerPointsTexts?.Length ?? 0); i++)
        {
            if (playerPointsTexts[i] == null) continue;
            playerPointsTexts[i].text = i < playerCount ? $"{GetVictoryPoints(i)} pts" : "";
        }
    }

    private string GetName(int playerIndex) =>
        PlayerManager.instance != null
            ? PlayerManager.instance.GetPlayerName(playerIndex)
            : $"Player {playerIndex + 1}";

    private int GetVictoryPoints(int playerIndex)
    {
        int pts = 0;
        foreach (Building b in FindObjectsOfType<Building>())
            if (b.ownerPlayerIndex == playerIndex)
                pts += b.type == Building.BuildingType.City ? 2 : 1;
        return pts;
    }
}

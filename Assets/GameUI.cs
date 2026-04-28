using UnityEngine;
using TMPro;

public class GameUI : MonoBehaviour
{
    [Header("Turn Info")]
    public TMP_Text currentPlayerText;
    public TMP_Text timerText;
    public TMP_Text gameStateText;

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
        if (gameStateText != null)
            gameStateText.text = TurnManager.instance.GetCurrentStateInfo();
    }

    private void OnTurnChanged(int playerIndex) => RefreshCurrentPlayer();

    private void OnTimerTick(float remaining)
    {
        if (timerText != null)
            timerText.text = remaining > 0 ? Mathf.CeilToInt(remaining) + "s" : "—";
    }

    private void RefreshCurrentPlayer()
    {
        if (currentPlayerText == null) return;
        int idx = TurnManager.instance.currentPlayer;
        string name = PlayerManager.instance != null
            ? PlayerManager.instance.GetPlayerName(idx)
            : $"Player {idx + 1}";
        bool isMe = PlayerManager.LocalPlayerIndex == idx;
        currentPlayerText.text  = $"Current: {name}{(isMe ? " (You)" : "")}";
        currentPlayerText.color = Building.GetPlayerColor(idx);
    }
}

using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VictoryManager : NetworkBehaviour
{
    public static VictoryManager instance;

    [Header("Victory Screen UI — assign in Inspector")]
    public GameObject victoryPanel;
    public TMP_Text   winnerNameText;
    public TMP_Text   winnerPointsText;
    public Image      winnerColorImage;

    private NetworkVariable<int>  netWinnerIndex = new NetworkVariable<int>(-1,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> netGameOver    = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsGameOver => netGameOver.Value;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        netWinnerIndex?.Dispose();
        netGameOver?.Dispose();
    }

    public override void OnNetworkSpawn()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);

        netGameOver.OnValueChanged    += (_, v) => { if (v) ShowVictoryScreen(netWinnerIndex.Value); };
        netWinnerIndex.OnValueChanged += (_, v) => { if (netGameOver.Value) ShowVictoryScreen(v); };
    }

    // Server only — call after any event that may change point totals
    public void CheckVictory()
    {
        if (!IsServer || netGameOver.Value) return;

        int needed = TurnManager.instance != null ? TurnManager.instance.pointsToWin : 10;
        int count  = TurnManager.instance != null ? TurnManager.instance.totalPlayers : 1;

        for (int i = 0; i < count; i++)
        {
            if (GetTotalPoints(i) >= needed)
            {
                netWinnerIndex.Value = i;
                netGameOver.Value    = true;
                return;
            }
        }
    }

    // נקודות אמיתיות — כולל קלפי VP (נראה רק לשחקן עצמו)
    public int GetTotalPoints(int playerIndex)
    {
        return GetVisiblePoints(playerIndex) +
               (DevCardManager.instance != null ? DevCardManager.instance.GetVPCards(playerIndex) : 0);
    }

    // נקודות גלויות — ללא קלפי VP (נראה לכל השחקנים)
    public int GetVisiblePoints(int playerIndex)
    {
        int pts = 0;
        foreach (Building b in FindObjectsOfType<Building>())
            if (b.ownerPlayerIndex == playerIndex)
                pts += b.type == Building.BuildingType.City ? 2 : 1;

        if (TurnManager.instance != null)
        {
            if (TurnManager.instance.longestRoadPlayer == playerIndex) pts += 2;
            if (TurnManager.instance.largestArmyPlayer == playerIndex) pts += 2;
        }
        return pts;
    }

    private void ShowVictoryScreen(int playerIndex)
    {
        if (victoryPanel == null) return;
        victoryPanel.SetActive(true);

        string name = PlayerManager.instance != null
            ? PlayerManager.instance.GetPlayerName(playerIndex)
            : $"Player {playerIndex + 1}";

        if (winnerNameText   != null) winnerNameText.text   = $"{name} Wins!";
        if (winnerPointsText != null) winnerPointsText.text = $"{GetTotalPoints(playerIndex)} Victory Points";
        if (winnerColorImage != null) winnerColorImage.color = Building.GetPlayerColor(playerIndex);
    }

    public void OnReturnToMenuClick()
    {
        if (NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
}

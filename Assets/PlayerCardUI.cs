using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerCardUI : MonoBehaviour
{
    [Header("Identity")]
    public Image    avatarImage;
    public TMP_Text nameText;

    [Header("Stats")]
    public TMP_Text pointsText;
    public TMP_Text roadsText;
    public TMP_Text cardsInHandText;
    public TMP_Text knightsText;
    public TMP_Text devCardsText;

    [Header("Player Color Backgrounds")]
    public Image avatarBackground;
    public Image nameTextBackground;

    [Header("Turn Indicators")]
    public GameObject diceIndicator;

    [Header("Bonus Indicators")]
    public GameObject longestRoadIndicator;
    public GameObject largestArmyIndicator;

    private int    playerIndex  = -1;
    private Sprite cachedAvatar;

    public void Setup(int index)
    {
        playerIndex  = index;
        cachedAvatar = null;
    }

    public void Refresh()
    {
        if (playerIndex < 0 || TurnManager.instance == null) return;

        int totalPlayers = TurnManager.instance.totalPlayers;
        if (playerIndex >= totalPlayers) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);

        bool isMyTurn      = TurnManager.instance.currentPlayer == playerIndex;
        bool needsRoll     = isMyTurn && TurnManager.instance.currentState == TurnManager.TurnState.WaitingForDiceRoll;
        bool isLocalPlayer = PlayerManager.LocalPlayerIndex == playerIndex;

        // Name
        if (nameText != null)
        {
            string pname = PlayerManager.instance != null
                ? PlayerManager.instance.GetPlayerName(playerIndex)
                : $"Player {playerIndex + 1}";
            nameText.text      = pname + (isLocalPlayer ? " ★" : "");
            nameText.color     = Color.white;
            nameText.fontStyle = isMyTurn ? FontStyles.Bold : FontStyles.Normal;
        }

        // Player color backgrounds
        Color playerColor = Building.GetPlayerColor(playerIndex);
        if (avatarBackground   != null) avatarBackground.color   = playerColor;
        if (nameTextBackground != null) nameTextBackground.color = playerColor;

        // Avatar
        if (avatarImage != null)
        {
            if (cachedAvatar == null && PlayerManager.instance != null)
            {
                string avatarName = PlayerManager.instance.GetPlayerAvatarName(playerIndex);
                if (!string.IsNullOrEmpty(avatarName))
                    cachedAvatar = Resources.Load<Sprite>("Avatars/" + avatarName);
            }
            if (cachedAvatar != null) avatarImage.sprite = cachedAvatar;
        }

        // Victory points
        if (pointsText != null)
            pointsText.text = (VictoryManager.instance != null
                ? VictoryManager.instance.GetTotalPoints(playerIndex)
                : GetBuildingPoints()).ToString();

        // Roads
        if (roadsText != null) roadsText.text = GetRoadCount().ToString();

        // Cards in hand
        if (cardsInHandText != null) cardsInHandText.text = GetTotalResources().ToString();

        // Knights + dev cards
        if (knightsText  != null)
            knightsText.text  = DevCardManager.instance?.GetPlayedKnights(playerIndex).ToString() ?? "0";
        if (devCardsText != null)
            devCardsText.text = DevCardManager.instance?.GetTotalCards(playerIndex).ToString()    ?? "0";

        // Dice indicator
        if (diceIndicator != null) diceIndicator.SetActive(needsRoll);

        // Bonus indicators
        if (longestRoadIndicator != null)
            longestRoadIndicator.SetActive(TurnManager.instance.longestRoadPlayer == playerIndex);
        if (largestArmyIndicator != null)
            largestArmyIndicator.SetActive(TurnManager.instance.largestArmyPlayer == playerIndex);
    }

    private int GetBuildingPoints()
    {
        int pts = 0;
        foreach (Building b in FindObjectsOfType<Building>())
            if (b.ownerPlayerIndex == playerIndex)
                pts += b.type == Building.BuildingType.City ? 2 : 1;
        return pts;
    }

    private int GetRoadCount()
    {
        int cnt = 0;
        foreach (Road r in FindObjectsOfType<Road>())
            if (r.ownerPlayerIndex == playerIndex) cnt++;
        return cnt;
    }

    private int GetTotalResources()
    {
        if (ResourceManager.instance == null) return 0;
        int total = 0;
        string[] names = ResourceManager.GetResourceNames();
        for (int r = 0; r < names.Length; r++)
            total += ResourceManager.instance.GetNetResource(playerIndex, r);
        return total;
    }
}

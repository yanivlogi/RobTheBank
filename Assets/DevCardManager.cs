using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DevCardManager : NetworkBehaviour
{
    public static DevCardManager instance;

    public enum CardType { Knight = 0, VP = 1, RoadBuilding = 2, YearOfPlenty = 3, Monopoly = 4 }
    const int CARD_TYPES = 5;

    // Per-player hand flattened: [p0_Knight, p0_VP, p0_RB, p0_YoP, p0_Mono, p1_Knight, ...]
    private NetworkList<int> playerCards;
    // Knights played (for Largest Army)
    private NetworkList<int> playedKnights;

    // Server-side deck
    private int deckKnight = 14, deckVP = 5, deckRoadBuilding = 2, deckYearOfPlenty = 2, deckMonopoly = 2;

    // Once-per-turn dev card limit (server-side only)
    private int devCardPlayedByPlayer = -1;

    public void ResetDevCardForNewTurn() { devCardPlayedByPlayer = -1; }

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }
        playerCards   = new NetworkList<int>(new List<int>(),
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        playedKnights = new NetworkList<int>(new List<int>(),
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        playerCards?.Dispose();
        playedKnights?.Dispose();
    }

    public void InitializeForPlayerCount(int count)
    {
        if (!IsServer) return;
        playerCards.Clear();
        playedKnights.Clear();
        for (int i = 0; i < count * CARD_TYPES; i++) playerCards.Add(0);
        for (int i = 0; i < count; i++) playedKnights.Add(0);
    }

    [ServerRpc(RequireOwnership = false)]
    public void BuyDevCardServerRpc(int playerIndex)
    {
        var cost = new Dictionary<string, int> { {"Wheat",1}, {"Sheep",1}, {"Ore",1} };
        if (!ResourceManager.instance.HasEnoughResources(playerIndex, cost)) return;

        int total = deckKnight + deckVP + deckRoadBuilding + deckYearOfPlenty + deckMonopoly;
        if (total == 0) return;

        // אתחול הגנתי — אם הרשימה לא אותחלה בזמן
        int needed = (playerIndex + 1) * CARD_TYPES;
        while (playerCards.Count < needed) playerCards.Add(0);

        ResourceManager.instance.SpendResources(playerIndex, cost);

        int roll = Random.Range(0, total);
        CardType drawn;
        int cum = deckKnight;
        if      (roll < cum)  { drawn = CardType.Knight;       deckKnight--; }
        else { cum += deckVP;
        if      (roll < cum)  { drawn = CardType.VP;           deckVP--; }
        else { cum += deckRoadBuilding;
        if      (roll < cum)  { drawn = CardType.RoadBuilding; deckRoadBuilding--; }
        else { cum += deckYearOfPlenty;
        if      (roll < cum)  { drawn = CardType.YearOfPlenty; deckYearOfPlenty--; }
        else                  { drawn = CardType.Monopoly;     deckMonopoly--; }}}}

        playerCards[playerIndex * CARD_TYPES + (int)drawn]++;

        if (drawn == CardType.VP)
            VictoryManager.instance?.CheckVictory();

        RevealCardClientRpc(playerIndex, (int)drawn);
    }

    [ClientRpc]
    private void RevealCardClientRpc(int playerIndex, int cardType)
    {
        if (PlayerManager.LocalPlayerIndex != playerIndex) return;
        string cardName = (CardType)cardType switch
        {
            CardType.Knight       => "פרש ⚔",
            CardType.VP           => "נקודת ניצחון 🏆",
            CardType.RoadBuilding => "בניית דרכים 🛤",
            CardType.YearOfPlenty => "שנת שפע ✨",
            CardType.Monopoly     => "מונופול 💰",
            _                     => "קלף"
        };
        BuildManager.onBuildFeedback?.Invoke($"קיבלת: {cardName}!");
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayRoadBuildingServerRpc(int playerIndex)
    {
        if (devCardPlayedByPlayer == playerIndex) return;
        int idx = playerIndex * CARD_TYPES + (int)CardType.RoadBuilding;
        if (idx >= playerCards.Count || playerCards[idx] <= 0) return;
        devCardPlayedByPlayer = playerIndex;
        playerCards[idx]--;
        BuildManager.instance?.AddFreeRoad(playerIndex, 2);
        RoadBuildingActivatedClientRpc(playerIndex);
    }

    [ClientRpc]
    private void RoadBuildingActivatedClientRpc(int playerIndex)
    {
        if (PlayerManager.LocalPlayerIndex == playerIndex)
        {
            Debug.Log("[DevCard] Road Building — place 2 free roads!");
            BuildManager.instance?.StartBuildingRoad();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayYearOfPlentyServerRpc(int playerIndex, int resource1Idx, int resource2Idx)
    {
        if (devCardPlayedByPlayer == playerIndex) return;
        int idx = playerIndex * CARD_TYPES + (int)CardType.YearOfPlenty;
        if (idx >= playerCards.Count || playerCards[idx] <= 0) return;
        devCardPlayedByPlayer = playerIndex;
        playerCards[idx]--;
        string[] names = ResourceManager.GetResourceNames();
        if (resource1Idx < names.Length) ResourceManager.instance.AddResource(playerIndex, names[resource1Idx], 1);
        if (resource2Idx < names.Length) ResourceManager.instance.AddResource(playerIndex, names[resource2Idx], 1);
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayMonopolyServerRpc(int playerIndex, int resourceIdx)
    {
        if (devCardPlayedByPlayer == playerIndex) return;
        int idx = playerIndex * CARD_TYPES + (int)CardType.Monopoly;
        if (idx >= playerCards.Count || playerCards[idx] <= 0) return;
        devCardPlayedByPlayer = playerIndex;
        playerCards[idx]--;
        string[] names = ResourceManager.GetResourceNames();
        if (resourceIdx >= names.Length) return;
        string resource = names[resourceIdx];
        int count  = TurnManager.instance != null ? TurnManager.instance.totalPlayers : 1;
        for (int i = 0; i < count; i++)
        {
            if (i == playerIndex) continue;
            int amount = ResourceManager.instance.GetResourceAmountServer(i, resource);
            if (amount <= 0) continue;
            ResourceManager.instance.RemoveResource(i, resource, amount);
            ResourceManager.instance.AddResource(playerIndex, resource, amount);
        }
        MonopolyResultClientRpc(playerIndex, resourceIdx);
    }

    [ClientRpc]
    private void MonopolyResultClientRpc(int playerIndex, int resourceIdx)
    {
        string name = ResourceManager.GetResourceNames()[resourceIdx];
        Debug.Log($"[DevCard] Monopoly on {name} by Player {playerIndex + 1}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayKnightServerRpc(int playerIndex)
    {
        if (devCardPlayedByPlayer == playerIndex) return;
        int idx = playerIndex * CARD_TYPES + (int)CardType.Knight;
        if (idx >= playerCards.Count || playerCards[idx] <= 0) return;
        devCardPlayedByPlayer = playerIndex;
        playerCards[idx]--;
        if (playerIndex < playedKnights.Count)
            playedKnights[playerIndex]++;

        TurnManager.instance?.UpdateLargestArmy();
        RobberManager.instance?.ActivateRobber(playerIndex);
    }

    public int GetCard(int playerIndex, CardType type)
    {
        if (playerIndex < 0) return 0;
        int idx = playerIndex * CARD_TYPES + (int)type;
        return (idx >= 0 && idx < playerCards.Count) ? playerCards[idx] : 0;
    }

    public int GetVPCards(int playerIndex)       => GetCard(playerIndex, CardType.VP);
    public int GetPlayedKnights(int playerIndex) =>
        (playerIndex >= 0 && playerIndex < playedKnights.Count) ? playedKnights[playerIndex] : 0;

    public int GetTotalCards(int playerIndex)
    {
        int total = 0;
        for (int t = 0; t < CARD_TYPES; t++) total += GetCard(playerIndex, (CardType)t);
        return total;
    }
}

using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager instance;

    // ── Game settings — server writes, everyone reads ──
    private NetworkVariable<int>  netTotalPlayers    = new NetworkVariable<int>(4);
    private NetworkVariable<int>  netPointsToWin     = new NetworkVariable<int>(10);
    private NetworkVariable<int>  netTurnTimeSeconds = new NetworkVariable<int>(60);
    private NetworkVariable<bool> netFriendlyRobber  = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> netAllowTrade      = new NetworkVariable<bool>(true);

    // ── Turn state — server writes ──
    private NetworkVariable<int>  netCurrentPlayer      = new NetworkVariable<int>(0,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int>  netCurrentState       = new NetworkVariable<int>(0,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> netIsInitialPlacement = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> netWaitingForRoad     = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Bonus tracking — server writes ──
    private NetworkVariable<int> netLongestRoadPlayer = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> netLargestArmyPlayer = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Local copies of settings ──
    public int  totalPlayers    { get; private set; } = 4;
    public int  pointsToWin     { get; private set; } = 10;
    public int  turnTimeSeconds { get; private set; } = 60;
    public bool friendlyRobber  { get; private set; } = false;
    public bool allowTrade      { get; private set; } = true;

    // ── Synced accessors ──
    public int       currentPlayer     => netCurrentPlayer.Value;
    public TurnState currentState      => (TurnState)netCurrentState.Value;
    public bool      isInitialPlacement => netIsInitialPlacement.Value;
    public bool      waitingForRoad    => netWaitingForRoad.Value;
    public int       longestRoadPlayer => netLongestRoadPlayer.Value;
    public int       largestArmyPlayer => netLargestArmyPlayer.Value;

    // ── Timer ──
    private float turnTimeRemaining = 0f;
    private bool  turnTimerActive   = false;

    // ── Server counters ──
    private int initialSettlementsPlaced = 0;

    public UnityEvent<int>   onPlayerTurnChanged;
    public UnityEvent<int>   onDiceRolled;
    public UnityEvent<float> onTurnTimerTick;

    // (d1, d2) — נרשם ע"י DiceUI
    public static System.Action<int, int> onDiceRolledPair;

    public enum TurnState
    {
        WaitingForSettlement,
        WaitingForDiceRoll,
        ResourceCollection,
        Building,
        Trading,
        TurnEnd
    }

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        netTotalPlayers?.Dispose();
        netPointsToWin?.Dispose();
        netTurnTimeSeconds?.Dispose();
        netFriendlyRobber?.Dispose();
        netAllowTrade?.Dispose();
        netCurrentPlayer?.Dispose();
        netCurrentState?.Dispose();
        netIsInitialPlacement?.Dispose();
        netWaitingForRoad?.Dispose();
        netLongestRoadPlayer?.Dispose();
        netLargestArmyPlayer?.Dispose();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            netTotalPlayers.Value    = GameSettings.MaxPlayers;
            netPointsToWin.Value     = GameSettings.PointsToWin;
            netTurnTimeSeconds.Value = GameSettings.TurnTimeSeconds;
            netFriendlyRobber.Value  = GameSettings.FriendlyRobber;
            netAllowTrade.Value      = GameSettings.AllowTrade;

            netCurrentPlayer.Value      = 0;
            netCurrentState.Value       = (int)TurnState.WaitingForSettlement;
            netIsInitialPlacement.Value = true;
            netWaitingForRoad.Value     = false;
            netLongestRoadPlayer.Value  = -1;
            netLargestArmyPlayer.Value  = -1;
        }

        ApplySettings();

        if (IsServer)
        {
            int mapSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            BroadcastMapSeedClientRpc(mapSeed);
        }

        netTotalPlayers.OnValueChanged    += (_, v) => { totalPlayers    = v; };
        netPointsToWin.OnValueChanged     += (_, v) => { pointsToWin     = v; };
        netTurnTimeSeconds.OnValueChanged += (_, v) => { turnTimeSeconds = v; };
        netFriendlyRobber.OnValueChanged  += (_, v) => { friendlyRobber  = v; };
        netAllowTrade.OnValueChanged      += (_, v) => { allowTrade      = v; };
        netCurrentPlayer.OnValueChanged   += (_, v) => onPlayerTurnChanged?.Invoke(v);

        Debug.Log($"[TurnManager] Spawned — totalPlayers:{totalPlayers} myIndex:{PlayerManager.LocalPlayerIndex}");
    }

    [ClientRpc]
    private void BroadcastMapSeedClientRpc(int seed)
    {
        HexGridGenerator.instance?.InitializeWithSeed(seed);
        ResourceManager.instance?.InitializeForPlayerCount(totalPlayers);
        DevCardManager.instance?.InitializeForPlayerCount(totalPlayers);
        RobberManager.instance?.PlaceOnDesert();
        BuildManager.instance?.ShowValidBuildPoints(); // הצג נקודות לשחקן הראשון
        Debug.Log($"[TurnManager] Map initialized with seed {seed}");
    }

    private void ApplySettings()
    {
        totalPlayers    = netTotalPlayers.Value;
        pointsToWin     = netPointsToWin.Value;
        turnTimeSeconds = netTurnTimeSeconds.Value;
        friendlyRobber  = netFriendlyRobber.Value;
        allowTrade      = netAllowTrade.Value;
    }

    void Update()
    {
        if (!turnTimerActive || isInitialPlacement) return;

        turnTimeRemaining -= Time.deltaTime;
        onTurnTimerTick?.Invoke(turnTimeRemaining);

        if (IsServer && turnTimeRemaining <= 0f)
            ForceEndTurn();
    }

    // ── Timer helpers ──

    private void StartTurnTimerOnAllClients()
    {
        if (turnTimeSeconds <= 0) return;
        StartTurnTimerClientRpc();
    }

    [ClientRpc]
    private void StartTurnTimerClientRpc()
    {
        turnTimeRemaining = turnTimeSeconds;
        turnTimerActive   = true;
    }

    private void StopTurnTimerOnAllClients() => StopTurnTimerClientRpc();

    [ClientRpc]
    private void StopTurnTimerClientRpc()
    {
        turnTimerActive   = false;
        turnTimeRemaining = 0f;
    }

    private void ForceEndTurn()
    {
        StopTurnTimerOnAllClients();
        netCurrentState.Value = (int)TurnState.TurnEnd;
        AdvanceTurn();
    }

    // ── Identity helpers ──

    public bool IsInInitialPhase() => isInitialPlacement;

    public bool IsMyTurn() =>
        PlayerManager.LocalPlayerIndex >= 0 &&
        PlayerManager.LocalPlayerIndex == currentPlayer;

    // ── Initial placement ──

    public void PlaceInitialSettlement(Vector3 position, int playerIndex)
    {
        if (!IsMyTurn()) { Debug.LogWarning("Not your turn!"); return; }
        PlaceInitialSettlementServerRpc(position);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlaceInitialSettlementServerRpc(Vector3 position)
    {
        BuildManager.instance.BuildInitialSettlement(position, netCurrentPlayer.Value);
        netWaitingForRoad.Value = true;
        HighlightRoadPointsClientRpc(position, netCurrentPlayer.Value);

        // ישוב שני — נותן משאב אחד מכל אריח סמוך
        if (initialSettlementsPlaced >= totalPlayers)
            GiveStartingResources(position, netCurrentPlayer.Value);
    }

    private void GiveStartingResources(Vector3 settlementPos, int playerIndex)
    {
        foreach (HexTile tile in FindObjectsOfType<HexTile>())
        {
            if (string.IsNullOrEmpty(tile.resourceType) || tile.resourceType == "Desert") continue;
            Transform[] pts = { tile.buildT, tile.buildTR, tile.buildTL, tile.buildD, tile.buildDR, tile.buildDL };
            foreach (var bp in pts)
            {
                if (bp != null && Vector3.Distance(bp.position, settlementPos) <= 0.5f)
                {
                    ResourceManager.instance?.AddResource(playerIndex, tile.resourceType, 1);
                    NotifyStartingResourceClientRpc(playerIndex, tile.resourceType);
                    break;
                }
            }
        }
    }

    [ClientRpc]
    private void NotifyStartingResourceClientRpc(int playerIndex, string resource)
    {
        if (PlayerManager.LocalPlayerIndex == playerIndex)
            Debug.Log($"[Start] +1 {resource} from starting settlement");
    }

    [ClientRpc]
    private void HighlightRoadPointsClientRpc(Vector3 settlementPos, int playerIdx)
    {
        HighlightValidRoadPoints(settlementPos);
        Debug.Log($"Player {playerIdx + 1} — place a road connected to your settlement");
    }

    private void HighlightValidRoadPoints(Vector3 settlementPosition)
    {
        foreach (BuildRoad rp in FindObjectsOfType<BuildRoad>())
            rp.SetVisibility(Vector3.Distance(settlementPosition, rp.transform.position) <= 1.5f);
    }

    public void HideAllRoadPoints()
    {
        foreach (BuildRoad rp in FindObjectsOfType<BuildRoad>())
            rp.SetVisibility(false);
    }

    [ClientRpc]
    private void HideAllRoadPointsClientRpc() => HideAllRoadPoints();

    public void PlaceInitialRoad(Vector3 position, float rotation)
    {
        if (!netWaitingForRoad.Value || !IsMyTurn()) return;
        if (!BuildManager.instance.IsRoadConnectedToSettlement(position, currentPlayer))
        { Debug.Log("Road must be connected to your settlement!"); return; }
        PlaceInitialRoadServerRpc(position, rotation);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlaceInitialRoadServerRpc(Vector3 position, float rotation)
    {
        if (!BuildManager.instance.IsRoadConnectedToSettlement(position, netCurrentPlayer.Value))
        { Debug.LogWarning("Server: road rejected — not connected"); return; }

        BuildManager.instance.BuildInitialRoad(position, rotation);
        netWaitingForRoad.Value = false;
        HideAllRoadPointsClientRpc();

        initialSettlementsPlaced++;

        if (initialSettlementsPlaced >= totalPlayers * 2)
        {
            netIsInitialPlacement.Value = false;
            netCurrentState.Value = (int)TurnState.WaitingForDiceRoll;
            Debug.Log("Initial placement complete — game starting!");
        }
        else if (initialSettlementsPlaced < totalPlayers)
        {
            // סיבוב ראשון — קדימה
            netCurrentPlayer.Value = netCurrentPlayer.Value + 1;
            Debug.Log($"Player {netCurrentPlayer.Value + 1}'s turn (round 1)");
            ShowBuildPointsForInitialPlacementClientRpc();
        }
        else if (initialSettlementsPlaced == totalPlayers)
        {
            // ציר הנחש — אותו שחקן (האחרון) שם ישוב שני מיד
            Debug.Log($"Player {netCurrentPlayer.Value + 1}'s turn (round 2, pivot)");
            ShowBuildPointsForInitialPlacementClientRpc();
        }
        else
        {
            // סיבוב שני — אחורה
            netCurrentPlayer.Value = netCurrentPlayer.Value - 1;
            Debug.Log($"Player {netCurrentPlayer.Value + 1}'s turn (round 2)");
            ShowBuildPointsForInitialPlacementClientRpc();
        }
    }

    [ClientRpc]
    private void ShowBuildPointsForInitialPlacementClientRpc()
    {
        BuildManager.instance?.ShowValidBuildPoints();
    }

    // ── Normal turn ──

    public void NextTurn()
    {
        if (!IsMyTurn()) { Debug.LogWarning("Not your turn!"); return; }
        if (currentState == TurnState.WaitingForSettlement ||
            currentState == TurnState.WaitingForDiceRoll)
        { Debug.LogWarning("Must roll dice before ending turn!"); return; }
        NextTurnServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void NextTurnServerRpc()
    {
        if (netCurrentState.Value == (int)TurnState.WaitingForSettlement ||
            netCurrentState.Value == (int)TurnState.WaitingForDiceRoll) return;
        AdvanceTurn();
    }

    private void AdvanceTurn()
    {
        if (VictoryManager.instance != null && VictoryManager.instance.IsGameOver) return;

        StopTurnTimerOnAllClients();

        int next = (netCurrentPlayer.Value + 1) % totalPlayers;
        // Skip disconnected players (up to a full cycle)
        for (int attempt = 0; attempt < totalPlayers; attempt++)
        {
            if (PlayerManager.instance == null || PlayerManager.instance.IsPlayerConnected(next))
                break;
            next = (next + 1) % totalPlayers;
        }

        netCurrentPlayer.Value = next;
        netCurrentState.Value  = (int)TurnState.WaitingForDiceRoll;
        BuildManager.instance?.CancelBuildingClientRpc();
        DevCardManager.instance?.ResetDevCardForNewTurn();
        Debug.Log($"Player {netCurrentPlayer.Value + 1}'s turn");
    }

    public void RollDice()
    {
        if (!IsMyTurn()) { Debug.LogWarning("Not your turn!"); return; }
        if (currentState != TurnState.WaitingForDiceRoll) { Debug.LogWarning("Can only roll dice at start of turn!"); return; }
        RollDiceServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RollDiceServerRpc()
    {
        if (netCurrentState.Value != (int)TurnState.WaitingForDiceRoll) return;

        int d1 = Random.Range(1, 7);
        int d2 = Random.Range(1, 7);
        int total = d1 + d2;

        BroadcastDiceResultClientRpc(d1, d2, total);

        if (total == 7)
        {
            // השעון לא יתחיל — יחודש ע"י RobberManager.OnRobberFullyResolved
            HandleRobber();
        }
        else
        {
            StartTurnTimerOnAllClients();
            ResourceManager.instance.DistributeResourcesForRoll(total);
        }

        netCurrentState.Value = (int)TurnState.ResourceCollection;
    }

    [ClientRpc]
    private void BroadcastDiceResultClientRpc(int d1, int d2, int total)
    {
        Debug.Log($"Rolled: {d1} + {d2} = {total}");
        onDiceRolled?.Invoke(total);
        onDiceRolledPair?.Invoke(d1, d2);
    }

    private void HandleRobber()
    {
        if (friendlyRobber) { Debug.Log("Robber (Friendly mode — skipped)"); return; }
        RobberManager.instance?.ActivateRobber(netCurrentPlayer.Value);
    }

    // ── Bonus tracking (called by BuildManager / DevCardManager) ──

    public void ResumeTimerAfterRobber()
    {
        if (IsServer) StartTurnTimerOnAllClients();
    }

    public void UpdateLongestRoad()
    {
        if (!IsServer) return;

        int maxLen = 0, maxPlayer = -1;
        for (int i = 0; i < totalPlayers; i++)
        {
            int len = CalculateLongestRoad(i);
            if (len > maxLen) { maxLen = len; maxPlayer = i; }
        }

        if (maxLen < 5) return;

        int holder = netLongestRoadPlayer.Value;
        if (holder < 0)
        {
            netLongestRoadPlayer.Value = maxPlayer;
        }
        else if (maxPlayer != holder)
        {
            if (maxLen > CalculateLongestRoad(holder))
                netLongestRoadPlayer.Value = maxPlayer;
        }

        VictoryManager.instance?.CheckVictory();
    }

    private int CalculateLongestRoad(int playerIndex)
    {
        Road[] allRoads = FindObjectsOfType<Road>();
        var roads = new System.Collections.Generic.List<Road>();
        foreach (var r in allRoads)
            if (r.ownerPlayerIndex == playerIndex) roads.Add(r);

        int n = roads.Count;
        if (n == 0) return 0;

        float radius = (BuildManager.instance != null ? BuildManager.instance.hexSize : 1f) * 0.65f;
        BuildingPoint[] bps = FindObjectsOfType<BuildingPoint>();

        // לכל דרך — מצא את נקודות-הקצה (building points) הסמוכות
        Vector3[][] endpoints = new Vector3[n][];
        for (int i = 0; i < n; i++)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            foreach (var bp in bps)
                if (Vector3.Distance(bp.transform.position, roads[i].transform.position) <= radius)
                    verts.Add(bp.transform.position);
            endpoints[i] = verts.ToArray();
        }

        bool[] vis = new bool[n];
        int max = 0;
        for (int s = 0; s < n; s++)
        {
            for (int k = 0; k < n; k++) vis[k] = false;
            int len = DfsRoad(roads, endpoints, vis, s);
            if (len > max) max = len;
        }
        return max;
    }

    private int DfsRoad(System.Collections.Generic.List<Road> roads, Vector3[][] endpoints, bool[] vis, int cur)
    {
        vis[cur] = true;
        int best = 1;
        for (int next = 0; next < roads.Count; next++)
        {
            if (vis[next]) continue;
            if (RoadsShareVertex(endpoints[cur], endpoints[next]))
            {
                int len = 1 + DfsRoad(roads, endpoints, vis, next);
                if (len > best) best = len;
            }
        }
        vis[cur] = false; // backtrack — נסה נתיבים אחרים
        return best;
    }

    private bool RoadsShareVertex(Vector3[] a, Vector3[] b)
    {
        if (a == null || b == null) return false;
        foreach (var va in a)
            foreach (var vb in b)
                if (Vector3.Distance(va, vb) <= 0.25f) return true;
        return false;
    }

    public void UpdateLargestArmy()
    {
        if (!IsServer || DevCardManager.instance == null) return;

        int maxKnights = 0, maxPlayer = -1;
        for (int i = 0; i < totalPlayers; i++)
        {
            int k = DevCardManager.instance.GetPlayedKnights(i);
            if (k > maxKnights) { maxKnights = k; maxPlayer = i; }
        }

        if (maxKnights < 3) return;

        int holder = netLargestArmyPlayer.Value;
        if (holder < 0)
        {
            netLargestArmyPlayer.Value = maxPlayer;
        }
        else if (maxPlayer != holder)
        {
            int holderKnights = DevCardManager.instance.GetPlayedKnights(holder);
            if (maxKnights > holderKnights)
                netLargestArmyPlayer.Value = maxPlayer;
        }

        VictoryManager.instance?.CheckVictory();
    }

    // ── Phase transitions ──

    public void StartBuildingPhase()
    {
        if (!IsMyTurn()) return;
        if (currentState != TurnState.ResourceCollection) { Debug.LogWarning("Must collect resources first!"); return; }
        StartBuildingPhaseServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartBuildingPhaseServerRpc()
    {
        if (netCurrentState.Value != (int)TurnState.ResourceCollection) return;
        netCurrentState.Value = (int)TurnState.Building;
    }

    public void StartTradingPhase()
    {
        if (!IsMyTurn()) return;
        if (currentState != TurnState.Building) { Debug.LogWarning("Must complete building first!"); return; }
        StartTradingPhaseServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartTradingPhaseServerRpc()
    {
        if (netCurrentState.Value != (int)TurnState.Building) return;
        netCurrentState.Value = allowTrade ? (int)TurnState.Trading : (int)TurnState.TurnEnd;
    }

    public void EndTradingPhase()
    {
        if (!IsMyTurn()) return;
        if (currentState != TurnState.Trading) { Debug.LogWarning("Not in trading phase!"); return; }
        EndTradingPhaseServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void EndTradingPhaseServerRpc()
    {
        if (netCurrentState.Value != (int)TurnState.Trading) return;
        netCurrentState.Value = (int)TurnState.TurnEnd;
    }

    public string GetCurrentStateInfo() =>
        $"Player {currentPlayer + 1}, State: {currentState}";
}

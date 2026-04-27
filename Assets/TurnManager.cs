using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager instance;

    // Game settings — synced from host
    private NetworkVariable<int>  netTotalPlayers    = new NetworkVariable<int>(4);
    private NetworkVariable<int>  netPointsToWin     = new NetworkVariable<int>(10);
    private NetworkVariable<int>  netTurnTimeSeconds = new NetworkVariable<int>(60);
    private NetworkVariable<bool> netFriendlyRobber  = new NetworkVariable<bool>(false);
    private NetworkVariable<bool> netAllowTrade      = new NetworkVariable<bool>(true);

    // Turn state — synced, server-write-only
    private NetworkVariable<int>  netCurrentPlayer     = new NetworkVariable<int>(0,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int>  netCurrentState      = new NetworkVariable<int>(0,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> netIsInitialPlacement = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> netWaitingForRoad    = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Local copies of settings for convenient read
    public int  totalPlayers    { get; private set; } = 4;
    public int  pointsToWin     { get; private set; } = 10;
    public int  turnTimeSeconds { get; private set; } = 60;
    public bool friendlyRobber  { get; private set; } = false;
    public bool allowTrade      { get; private set; } = true;

    // Synced accessors
    public int       currentPlayer     => netCurrentPlayer.Value;
    public TurnState currentState      => (TurnState)netCurrentState.Value;
    public bool      isInitialPlacement => netIsInitialPlacement.Value;
    public bool      waitingForRoad    => netWaitingForRoad.Value;

    // Timer — runs locally on each client for smooth display; only server acts on expiry
    private float turnTimeRemaining = 0f;
    private bool  turnTimerActive   = false;

    // Server-only counter
    private int initialSettlementsPlaced = 0;

    public UnityEvent<int>   onPlayerTurnChanged;
    public UnityEvent<int>   onDiceRolled;
    public UnityEvent<float> onTurnTimerTick;

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
        }

        ApplySettings();

        if (IsServer)
        {
            int mapSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            BroadcastMapSeedClientRpc(mapSeed);
        }

        // Setting changes
        netTotalPlayers.OnValueChanged    += (_, v) => { totalPlayers    = v; };
        netPointsToWin.OnValueChanged     += (_, v) => { pointsToWin     = v; };
        netTurnTimeSeconds.OnValueChanged += (_, v) => { turnTimeSeconds = v; };
        netFriendlyRobber.OnValueChanged  += (_, v) => { friendlyRobber  = v; };
        netAllowTrade.OnValueChanged      += (_, v) => { allowTrade      = v; };

        // Game state changes fire events locally on every client
        netCurrentPlayer.OnValueChanged += (_, v) => onPlayerTurnChanged?.Invoke(v);

        Debug.Log($"[TurnManager] Spawned — totalPlayers:{totalPlayers} myIndex:{PlayerManager.LocalPlayerIndex}");
    }

    [ClientRpc]
    private void BroadcastMapSeedClientRpc(int seed)
    {
        HexGridGenerator.instance?.InitializeWithSeed(seed);
        ResourceManager.instance?.InitializeForPlayerCount(totalPlayers);
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

        // Only the server actually enforces the timeout
        if (IsServer && turnTimeRemaining <= 0f)
            ForceEndTurn();
    }

    // ── Timer helpers (broadcast so all clients animate the same countdown) ──

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

    private void StopTurnTimerOnAllClients()
    {
        StopTurnTimerClientRpc();
    }

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
    }

    [ClientRpc]
    private void HighlightRoadPointsClientRpc(Vector3 settlementPos, int playerIdx)
    {
        HighlightValidRoadPoints(settlementPos);
        Debug.Log($"Player {playerIdx + 1} — now place a road connected to your settlement");
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
    private void HideAllRoadPointsClientRpc()
    {
        HideAllRoadPoints();
    }

    public void PlaceInitialRoad(Vector3 position, float rotation)
    {
        if (!netWaitingForRoad.Value || !IsMyTurn()) return;
        if (!BuildManager.instance.IsRoadConnectedToSettlement(position, currentPlayer))
        {
            Debug.Log("Road must be connected to your settlement!");
            return;
        }
        PlaceInitialRoadServerRpc(position, rotation);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlaceInitialRoadServerRpc(Vector3 position, float rotation)
    {
        if (!BuildManager.instance.IsRoadConnectedToSettlement(position, netCurrentPlayer.Value))
        {
            Debug.LogWarning("Server: road rejected — not connected");
            return;
        }

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
        else
        {
            netCurrentPlayer.Value = (netCurrentPlayer.Value + 1) % totalPlayers;
            Debug.Log($"Player {netCurrentPlayer.Value + 1}'s turn to place settlement");
        }
    }

    // ── Normal turn ──

    public void NextTurn()
    {
        if (!IsMyTurn()) { Debug.LogWarning("Not your turn!"); return; }
        if (currentState == TurnState.WaitingForSettlement ||
            currentState == TurnState.WaitingForDiceRoll)
        {
            Debug.LogWarning("Must roll dice before ending turn!");
            return;
        }
        NextTurnServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void NextTurnServerRpc()
    {
        if (netCurrentState.Value == (int)TurnState.WaitingForSettlement ||
            netCurrentState.Value == (int)TurnState.WaitingForDiceRoll)
            return;
        AdvanceTurn();
    }

    private void AdvanceTurn()
    {
        StopTurnTimerOnAllClients();
        netCurrentPlayer.Value = (netCurrentPlayer.Value + 1) % totalPlayers;
        netCurrentState.Value  = (int)TurnState.WaitingForDiceRoll;
        // Timer starts only after dice roll, not here
        Debug.Log($"Player {netCurrentPlayer.Value + 1}'s turn");
    }

    public void RollDice()
    {
        if (!IsMyTurn()) { Debug.LogWarning("Not your turn!"); return; }
        if (currentState != TurnState.WaitingForDiceRoll) { Debug.LogWarning("Can only roll dice at the start of your turn!"); return; }
        RollDiceServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RollDiceServerRpc()
    {
        if (netCurrentState.Value != (int)TurnState.WaitingForDiceRoll) return;

        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        int total = dice1 + dice2;

        BroadcastDiceResultClientRpc(dice1, dice2, total);
        StartTurnTimerOnAllClients();  // timer begins when dice are rolled

        if (total == 7)
            HandleRobber();
        else
            DistributeResources(total);

        netCurrentState.Value = (int)TurnState.ResourceCollection;
    }

    [ClientRpc]
    private void BroadcastDiceResultClientRpc(int d1, int d2, int total)
    {
        Debug.Log($"Rolled: {d1} + {d2} = {total}");
        onDiceRolled?.Invoke(total);
    }

    private void HandleRobber()
    {
        if (friendlyRobber) { Debug.Log("Robber activated (Friendly mode — no stealing)"); return; }
        Debug.Log("Robber activated!");
    }

    private void DistributeResources(int diceRoll) =>
        ResourceManager.instance.DistributeResourcesForRoll(diceRoll);

    public void StartBuildingPhase()
    {
        if (!IsMyTurn()) return;
        if (currentState != TurnState.ResourceCollection) { Debug.LogWarning("Must collect resources before building!"); return; }
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
        if (currentState != TurnState.Building) { Debug.LogWarning("Must complete building phase before trading!"); return; }
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

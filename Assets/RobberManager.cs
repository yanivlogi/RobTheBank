using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RobberManager : NetworkBehaviour
{
    public static RobberManager instance;

    [Header("Robber Visual")]
    public Sprite     robberSprite;
    public GameObject robberTokenPrefab;
    public float      robberScale = 1f;

    [Header("Steal UI — assign in Inspector")]
    public GameObject stealPanel;
    public TMP_Text   stealTitleText;
    public Button[]   stealTargetButtons;
    public TMP_Text[] stealTargetTexts;

    private GameObject activeRobberToken;

    private NetworkVariable<int>     netRobberActivePlayer  = new NetworkVariable<int>(-1,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool>    netWaitingForPlacement = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<Vector3> netRobberPos           = new NetworkVariable<Vector3>(
        new Vector3(float.MaxValue, float.MaxValue, 0),
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool    IsWaitingForPlacement => netWaitingForPlacement.Value;
    public bool    IsMyRobberTurn =>
        netWaitingForPlacement.Value &&
        PlayerManager.LocalPlayerIndex == netRobberActivePlayer.Value;
    public Vector3 RobberHexPos => netRobberPos.Value;

    // ── Server-side discard & steal tracking ──
    private readonly HashSet<int>         pendingDiscardPlayers  = new HashSet<int>();
    private readonly Dictionary<int, int> discardRequirements    = new Dictionary<int, int>();
    private int  robberRollingPlayer = -1;
    private bool stealChoicePending  = false;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        netRobberActivePlayer?.Dispose();
        netWaitingForPlacement?.Dispose();
        netRobberPos?.Dispose();
    }

    public override void OnNetworkSpawn()
    {
        if (stealPanel != null) stealPanel.SetActive(false);

        netWaitingForPlacement.OnValueChanged += (_, waiting) =>
        {
            if (waiting && PlayerManager.LocalPlayerIndex == netRobberActivePlayer.Value)
                BuildManager.onBuildFeedback?.Invoke("לחץ על אריח להנחת השודד!");
        };
    }

    // ── נקרא ע"י TurnManager ──

    public void ActivateRobber(int rollingPlayer)
    {
        if (!IsServer) return;

        robberRollingPlayer = rollingPlayer;
        pendingDiscardPlayers.Clear();
        discardRequirements.Clear();
        stealChoicePending = false;

        int totalPlayers = TurnManager.instance != null ? TurnManager.instance.totalPlayers : 1;
        for (int i = 0; i < totalPlayers; i++)
        {
            int total = ResourceManager.instance.GetTotalResourceCount(i);
            if (total > 7)
            {
                int needed = total / 2;
                pendingDiscardPlayers.Add(i);
                discardRequirements[i] = needed;
                ulong cid = GetClientId(i);
                RequestDiscardClientRpc(i, needed,
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { cid } } });
            }
        }

        // אם אין מי שצריך להשליך — פתח מיד
        if (pendingDiscardPlayers.Count == 0)
            OpenRobberPlacement();
    }

    private void OpenRobberPlacement()
    {
        netRobberActivePlayer.Value  = robberRollingPlayer;
        netWaitingForPlacement.Value = true;
    }

    // ── השלכת קלפים — ClientRpc ממוקד ─

    [ClientRpc]
    private void RequestDiscardClientRpc(int playerIndex, int amount, ClientRpcParams rpcParams = default)
    {
        if (PlayerManager.LocalPlayerIndex != playerIndex) return;
        DiscardUI.instance?.Show(playerIndex, amount);
    }

    // ── השלכת קלפים — ServerRpc ──

    [ServerRpc(RequireOwnership = false)]
    public void SubmitDiscardServerRpc(int playerIndex, int wood, int brick, int wheat, int sheep, int ore)
    {
        if (!pendingDiscardPlayers.Contains(playerIndex)) return;

        int[] amounts = { wood, brick, wheat, sheep, ore };
        string[] names = ResourceManager.GetResourceNames();

        // ולידציה: כמויות תקינות ושווה לדרישה
        int total = 0;
        for (int r = 0; r < names.Length; r++)
        {
            if (amounts[r] < 0) return;
            if (amounts[r] > ResourceManager.instance.GetResourceAmountServer(playerIndex, names[r])) return;
            total += amounts[r];
        }
        int expected = discardRequirements.ContainsKey(playerIndex) ? discardRequirements[playerIndex] : 0;
        if (total != expected) return;

        // הסר משאבים
        for (int r = 0; r < names.Length; r++)
            if (amounts[r] > 0)
                ResourceManager.instance.RemoveResource(playerIndex, names[r], amounts[r]);

        pendingDiscardPlayers.Remove(playerIndex);
        discardRequirements.Remove(playerIndex);

        NotifyDiscardClientRpc(playerIndex, total);

        // הסתר פאנל אצל השחקן שסיים
        HideDiscardClientRpc(new ClientRpcParams
            { Send = new ClientRpcSendParams { TargetClientIds = new[] { GetClientId(playerIndex) } } });

        // אם כולם סיימו — פתח הנחת שודד
        if (pendingDiscardPlayers.Count == 0)
            OpenRobberPlacement();
    }

    [ClientRpc]
    private void HideDiscardClientRpc(ClientRpcParams rpcParams = default)
        => DiscardUI.instance?.Hide();

    // ── הנחת שודד ──

    [ServerRpc(RequireOwnership = false)]
    public void PlaceRobberOnHexServerRpc(Vector3 hexPosition)
    {
        if (!netWaitingForPlacement.Value) return;

        int activePlayer = netRobberActivePlayer.Value;
        netWaitingForPlacement.Value = false;
        netRobberActivePlayer.Value  = -1;
        netRobberPos.Value           = hexPosition;

        MoveRobberClientRpc(hexPosition);

        List<int> targets = FindStealTargets(hexPosition, activePlayer);
        if (targets.Count == 0)
        {
            OnRobberFullyResolved();
        }
        else if (targets.Count == 1)
        {
            ExecuteSteal(activePlayer, targets[0]);
            OnRobberFullyResolved();
        }
        else
        {
            stealChoicePending = true;
            ulong cid = GetClientId(activePlayer);
            ShowStealChoiceClientRpc(
                activePlayer,
                targets.Count > 0 ? targets[0] : -1,
                targets.Count > 1 ? targets[1] : -1,
                targets.Count > 2 ? targets[2] : -1,
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { cid } } });
        }
    }

    private void OnRobberFullyResolved()
    {
        stealChoicePending = false;
        TurnManager.instance?.ResumeTimerAfterRobber();
    }

    // ── גנבה ──

    private List<int> FindStealTargets(Vector3 hexPos, int activePlayer)
    {
        var targets = new HashSet<int>();
        foreach (HexTile tile in FindObjectsOfType<HexTile>())
        {
            if (Vector3.Distance(tile.transform.position, hexPos) > 0.5f) continue;
            Transform[] verts = { tile.buildT, tile.buildTR, tile.buildTL,
                                  tile.buildD, tile.buildDR, tile.buildDL };
            foreach (Building b in FindObjectsOfType<Building>())
            {
                if (b.ownerPlayerIndex == activePlayer) continue;
                foreach (var v in verts)
                    if (v != null && Vector3.Distance(v.position, b.transform.position) <= 0.5f)
                        targets.Add(b.ownerPlayerIndex);
            }
        }
        return new List<int>(targets);
    }

    private void ExecuteSteal(int stealer, int target)
    {
        string[] names = ResourceManager.GetResourceNames();
        var pool = new List<int>();
        for (int r = 0; r < names.Length; r++)
        {
            int amt = ResourceManager.instance.GetResourceAmountServer(target, names[r]);
            for (int i = 0; i < amt; i++) pool.Add(r);
        }

        if (pool.Count == 0) { NotifyStealClientRpc(stealer, target, -1); return; }

        int stolen = pool[Random.Range(0, pool.Count)];
        ResourceManager.instance.RemoveResource(target, names[stolen], 1);
        ResourceManager.instance.AddResource(stealer, names[stolen], 1);
        NotifyStealClientRpc(stealer, target, stolen);
    }

    [ServerRpc(RequireOwnership = false)]
    public void StealFromPlayerServerRpc(int stealer, int target)
    {
        if (!stealChoicePending) return;
        ExecuteSteal(stealer, target);
        HideStealPanelClientRpc();
        OnRobberFullyResolved();
    }

    // ── Client UI ──

    [ClientRpc]
    private void ShowStealChoiceClientRpc(int callerPlayer, int t0, int t1, int t2,
        ClientRpcParams rpcParams = default)
    {
        if (PlayerManager.LocalPlayerIndex != callerPlayer) return;
        if (stealPanel == null) return;

        stealPanel.SetActive(true);
        if (stealTitleText != null) stealTitleText.text = "בחר שחקן לגנוב ממנו:";

        int[] targets = { t0, t1, t2 };
        for (int i = 0; i < 3; i++)
        {
            bool active = targets[i] >= 0 && stealTargetButtons != null && i < stealTargetButtons.Length;
            stealTargetButtons?[i]?.gameObject.SetActive(active);
            if (!active) continue;

            int captured = targets[i];
            int me = callerPlayer;
            if (stealTargetTexts != null && i < stealTargetTexts.Length && stealTargetTexts[i] != null)
            {
                string pname = PlayerManager.instance?.GetPlayerName(captured) ?? $"שחקן {captured + 1}";
                int cards = ResourceManager.instance.GetTotalResourceCount(captured);
                stealTargetTexts[i].text = $"{pname} ({cards} קלפים)";
            }
            if (stealTargetButtons != null && i < stealTargetButtons.Length && stealTargetButtons[i] != null)
            {
                stealTargetButtons[i].onClick.RemoveAllListeners();
                stealTargetButtons[i].onClick.AddListener(() => StealFromPlayerServerRpc(me, captured));
            }
        }
    }

    [ClientRpc] private void HideStealPanelClientRpc() { if (stealPanel != null) stealPanel.SetActive(false); }
    [ClientRpc] private void MoveRobberClientRpc(Vector3 pos) => PlaceToken(pos);

    [ClientRpc]
    private void NotifyStealClientRpc(int stealer, int target, int slot)
    {
        string sName = PlayerManager.instance?.GetPlayerName(stealer) ?? $"שחקן {stealer + 1}";
        string tName = PlayerManager.instance?.GetPlayerName(target)  ?? $"שחקן {target  + 1}";
        if (slot < 0)
        {
            if (PlayerManager.LocalPlayerIndex == stealer)
                BuildManager.onBuildFeedback?.Invoke($"{tName} — אין לו משאבים לגנוב");
            return;
        }
        string res = ResourceManager.GetResourceNames()[slot];
        if (PlayerManager.LocalPlayerIndex == stealer)
            BuildManager.onBuildFeedback?.Invoke($"גנבת 1 {res} מ-{tName}!");
        else if (PlayerManager.LocalPlayerIndex == target)
            BuildManager.onBuildFeedback?.Invoke($"{sName} גנב ממך 1 {res}!");
    }

    [ClientRpc]
    private void NotifyDiscardClientRpc(int playerIndex, int amount)
    {
        string who = PlayerManager.LocalPlayerIndex == playerIndex ? "אתה" : $"שחקן {playerIndex + 1}";
        BuildManager.onBuildFeedback?.Invoke($"{who} השליך {amount} משאבים");
    }

    // ── Initial placement ──

    public void PlaceOnDesert()
    {
        foreach (HexTile tile in FindObjectsOfType<HexTile>())
        {
            if (tile.resourceType != "Desert") continue;
            if (IsServer) netRobberPos.Value = tile.transform.position;
            PlaceToken(tile.transform.position);
            return;
        }
    }

    // ── Helpers ──

    private ulong GetClientId(int playerIndex) =>
        PlayerManager.instance?.GetClientIdForPlayerIndex(playerIndex) ?? 0;

    private void PlaceToken(Vector3 worldPos)
    {
        if (activeRobberToken != null) Destroy(activeRobberToken);
        Vector3 pos = new Vector3(worldPos.x, worldPos.y, -0.2f);

        if (robberTokenPrefab != null)
        {
            activeRobberToken = Instantiate(robberTokenPrefab, pos, Quaternion.identity);
            activeRobberToken.transform.localScale = Vector3.one * robberScale;
        }
        else if (robberSprite != null)
        {
            activeRobberToken = new GameObject("RobberToken");
            activeRobberToken.transform.position   = pos;
            activeRobberToken.transform.localScale = Vector3.one * robberScale;
            var sr = activeRobberToken.AddComponent<SpriteRenderer>();
            sr.sprite       = robberSprite;
            sr.sortingOrder = 5;
        }
    }
}

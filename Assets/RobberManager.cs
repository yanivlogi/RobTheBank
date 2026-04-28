using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RobberManager : NetworkBehaviour
{
    public static RobberManager instance;

    [Header("Robber Visual")]
    public Sprite     robberSprite;
    public GameObject robberTokenPrefab;
    public float      robberScale = 1f;

    private GameObject activeRobberToken;

    // מי מחכה להניח את השודד (server-write)
    private NetworkVariable<int> netRobberActivePlayer = new NetworkVariable<int>(-1,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> netWaitingForPlacement = new NetworkVariable<bool>(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsWaitingForPlacement => netWaitingForPlacement.Value;
    public bool IsMyRobberTurn =>
        netWaitingForPlacement.Value &&
        PlayerManager.LocalPlayerIndex == netRobberActivePlayer.Value;

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
    }

    public override void OnNetworkSpawn()
    {
        netWaitingForPlacement.OnValueChanged += (_, waiting) =>
        {
            if (waiting && PlayerManager.LocalPlayerIndex == netRobberActivePlayer.Value)
                BuildManager.onBuildFeedback?.Invoke("לחץ על אריח להנחת השודד!");
        };
    }

    // ── נקרא ע"י TurnManager כשמגלגלים 7 או משחקים אביר ──

    public void ActivateRobber(int rollingPlayer)
    {
        if (!IsServer) return;

        int totalPlayers = TurnManager.instance != null ? TurnManager.instance.totalPlayers : 1;
        for (int i = 0; i < totalPlayers; i++)
        {
            int total = ResourceManager.instance.GetTotalResourceCount(i);
            if (total > 7) DiscardHalf(i, total / 2);
        }

        // ממתין לשחקן שיבחר אריח
        netRobberActivePlayer.Value  = rollingPlayer;
        netWaitingForPlacement.Value = true;
        PromptPlacementClientRpc(rollingPlayer);
    }

    [ClientRpc]
    private void PromptPlacementClientRpc(int playerIndex)
    {
        if (PlayerManager.LocalPlayerIndex == playerIndex)
            Debug.Log("[Robber] לחץ על אריח להנחת השודד!");
    }

    // ── נקרא ע"י HexTile.OnMouseDown ──

    [ServerRpc(RequireOwnership = false)]
    public void PlaceRobberOnHexServerRpc(Vector3 hexPosition)
    {
        if (!netWaitingForPlacement.Value) return;
        netWaitingForPlacement.Value = false;
        netRobberActivePlayer.Value  = -1;
        MoveRobberClientRpc(hexPosition);
    }

    [ClientRpc]
    private void MoveRobberClientRpc(Vector3 hexPosition)
    {
        PlaceToken(hexPosition);
    }

    // ── Initial placement on desert ──

    public void PlaceOnDesert()
    {
        HexTile desert = null;
        foreach (HexTile tile in FindObjectsOfType<HexTile>())
            if (tile.resourceType == "Desert") { desert = tile; break; }
        if (desert == null) return;
        PlaceToken(desert.transform.position);
    }

    // ── Visual helpers ──

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

    // ── Discard ──

    private void DiscardHalf(int playerIndex, int amount)
    {
        string[] names = ResourceManager.GetResourceNames();
        int removed = 0, guard = 0;
        while (removed < amount && guard++ < 200)
            for (int r = 0; r < names.Length && removed < amount; r++)
                if (ResourceManager.instance.RemoveResource(playerIndex, names[r], 1))
                    removed++;
        NotifyDiscardClientRpc(playerIndex, amount);
    }

    [ClientRpc]
    private void NotifyDiscardClientRpc(int playerIndex, int amount)
    {
        string who = PlayerManager.LocalPlayerIndex == playerIndex ? "אתה" : $"שחקן {playerIndex + 1}";
        Debug.Log($"[Robber] {who} ויתר על {amount} משאבים");
    }
}

using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    public static PlayerManager instance;
    public static int LocalPlayerIndex { get; private set; } = -1;

    private readonly Dictionary<ulong, int> clientIndexMap = new();
    private int nextPlayerIndex = 0;

    // Synced player names and avatar names readable by all clients
    public NetworkList<FixedString64Bytes> playerNames;
    public NetworkList<FixedString64Bytes> playerAvatarNames;

    void Awake()
    {
        instance = this;
        playerNames = new NetworkList<FixedString64Bytes>();
        playerAvatarNames = new NetworkList<FixedString64Bytes>();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        playerNames?.Dispose();
        playerAvatarNames?.Dispose();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            string avatarName = PlayerPrefs.GetString("SavedAvatarName", "");
            RegisterClient(NetworkManager.Singleton.LocalClientId, GameSettings.HostPlayerName, avatarName);
            LocalPlayerIndex = 0;
        }
        else
        {
            string name = PlayerPrefs.GetString("SavedPlayerName", "Player");
            string avatarName = PlayerPrefs.GetString("SavedAvatarName", "");
            RequestPlayerIndexServerRpc(name, avatarName);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerIndexServerRpc(string playerName, string avatarName, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!clientIndexMap.ContainsKey(clientId))
            RegisterClient(clientId, playerName, avatarName);

        int index = clientIndexMap[clientId];
        AssignIndexClientRpc(index, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    private void RegisterClient(ulong clientId, string name, string avatarName)
    {
        clientIndexMap[clientId] = nextPlayerIndex;
        playerNames.Add(new FixedString64Bytes(name));
        playerAvatarNames.Add(new FixedString64Bytes(avatarName));
        nextPlayerIndex++;
        Debug.Log($"[PlayerManager] Registered {name} (avatar: {avatarName}) as player {nextPlayerIndex - 1}");
    }

    [ClientRpc]
    private void AssignIndexClientRpc(int playerIndex, ClientRpcParams _ = default)
    {
        LocalPlayerIndex = playerIndex;
        Debug.Log($"[PlayerManager] I am player {playerIndex}");
    }

    public string GetPlayerName(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerNames.Count)
            return playerNames[playerIndex].ToString();
        return $"Player {playerIndex + 1}";
    }

    public string GetPlayerAvatarName(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerAvatarNames.Count)
            return playerAvatarNames[playerIndex].ToString();
        return "";
    }

    public bool IsPlayerConnected(int playerIndex)
    {
        foreach (var kv in clientIndexMap)
            if (kv.Value == playerIndex)
                return NetworkManager.Singleton.ConnectedClients.ContainsKey(kv.Key);
        return true; // not yet registered = still joining, treat as connected
    }

    public ulong GetClientIdForPlayerIndex(int playerIndex)
    {
        foreach (var kv in clientIndexMap)
            if (kv.Value == playerIndex) return kv.Key;
        return 0;
    }
}

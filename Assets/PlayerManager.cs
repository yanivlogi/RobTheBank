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

    // Synced player names readable by all clients
    public NetworkList<FixedString64Bytes> playerNames;

    void Awake()
    {
        instance = this;
        playerNames = new NetworkList<FixedString64Bytes>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Host is always player 0
            RegisterClient(NetworkManager.Singleton.LocalClientId, GameSettings.HostPlayerName);
            LocalPlayerIndex = 0;
        }
        else
        {
            string name = PlayerPrefs.GetString("SavedPlayerName", "Player");
            RequestPlayerIndexServerRpc(name);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlayerIndexServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!clientIndexMap.ContainsKey(clientId))
            RegisterClient(clientId, playerName);

        int index = clientIndexMap[clientId];
        AssignIndexClientRpc(index, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    private void RegisterClient(ulong clientId, string name)
    {
        clientIndexMap[clientId] = nextPlayerIndex;
        playerNames.Add(new FixedString64Bytes(name));
        nextPlayerIndex++;
        Debug.Log($"[PlayerManager] Registered {name} as player {nextPlayerIndex - 1}");
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
}

using Unity.Netcode;

public class NetworkChatRelay : NetworkBehaviour
{
    public static NetworkChatRelay Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SendChatServerRpc(string playerName, string message)
    {
        BroadcastChatClientRpc(playerName, message);
    }

    [ClientRpc]
    private void BroadcastChatClientRpc(string playerName, string message)
    {
        WaitingRoomChat.Instance?.ReceiveMessage(playerName, message);
    }
}

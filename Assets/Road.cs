using Unity.Netcode;
using UnityEngine;

public class Road : NetworkBehaviour
{
    private NetworkVariable<int> netOwner = new NetworkVariable<int>(-1);
    public int ownerPlayerIndex => netOwner.Value;

    public override void OnNetworkSpawn()
    {
        netOwner.OnValueChanged += (_, v) => ApplyColor(v);
        ApplyColor(netOwner.Value);
    }

    public void SetOwner(int playerIndex)
    {
        if (!IsServer) return;
        netOwner.Value = playerIndex;
    }

    private void ApplyColor(int playerIndex)
    {
        if (playerIndex < 0) return;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Building.GetPlayerColor(playerIndex);
    }
}

using Unity.Netcode;
using UnityEngine;

public class Building : NetworkBehaviour
{
    private NetworkVariable<int> netOwner = new NetworkVariable<int>(-1);
    private NetworkVariable<int> netType  = new NetworkVariable<int>(0);

    public int ownerPlayerIndex => netOwner.Value;
    public BuildingType type    => (BuildingType)netType.Value;
    public string resourceType;

    public enum BuildingType { Settlement, City }

    public override void OnNetworkSpawn()
    {
        netOwner.OnValueChanged += (_, v) => ApplyColor(v);
        ApplyColor(netOwner.Value);
    }

    // קרא רק מהשרת
    public void Initialize(int playerIndex, BuildingType buildingType)
    {
        if (!IsServer) return;
        netOwner.Value = playerIndex;
        netType.Value  = (int)buildingType;
    }

    private void ApplyColor(int playerIndex)
    {
        if (playerIndex < 0) return;
        var r = GetComponent<Renderer>();
        if (r != null) r.material.color = GetPlayerColor(playerIndex);
    }

    public static Color GetPlayerColor(int playerIndex) => playerIndex switch
    {
        0 => Color.red,
        1 => Color.blue,
        2 => Color.green,
        3 => Color.yellow,
        _ => Color.white
    };
}

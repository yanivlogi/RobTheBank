using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ResourceManager : NetworkBehaviour
{
    public static ResourceManager instance;
    private PlayerResources[] playerResources;

    // Synced to all clients: layout is [p0_Wood, p0_Brick, p0_Wheat, p0_Sheep, p0_Ore, p1_Wood, ...]
    private NetworkList<int> netResources;

    private static readonly string[] ResourceOrder = { "Wood", "Brick", "Wheat", "Sheep", "Ore" };

    void Awake()
    {
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        netResources = new NetworkList<int>(
            new List<int>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        netResources?.Dispose();
    }

    public void InitializeForPlayerCount(int count)
    {
        playerResources = new PlayerResources[count];
        for (int i = 0; i < count; i++)
            playerResources[i] = new PlayerResources();

        if (IsServer)
        {
            netResources.Clear();
            for (int i = 0; i < count * ResourceOrder.Length; i++)
                netResources.Add(0);
        }

        Debug.Log($"ResourceManager initialized for {count} players");
    }

    // Pushes server-side state into the synced list so clients can read it
    private void SyncToNet(int playerIndex)
    {
        if (!IsServer || playerResources == null || playerIndex >= playerResources.Length) return;

        // Ensure the list is big enough (defensive init if InitializeForPlayerCount ran before spawn)
        int needed = playerResources.Length * ResourceOrder.Length;
        while (netResources.Count < needed)
            netResources.Add(0);

        var res = playerResources[playerIndex].resources;
        int baseSlot = playerIndex * ResourceOrder.Length;
        for (int r = 0; r < ResourceOrder.Length; r++)
        {
            int slot = baseSlot + r;
            netResources[slot] = res.ContainsKey(ResourceOrder[r]) ? res[ResourceOrder[r]] : 0;
        }
    }

    // Readable by any client — use PlayerManager.LocalPlayerIndex to get own resources
    public int GetNetResource(int playerIndex, int resourceSlot)
    {
        int idx = playerIndex * ResourceOrder.Length + resourceSlot;
        return idx >= 0 && idx < netResources.Count ? netResources[idx] : 0;
    }

    public static string[] GetResourceNames() => ResourceOrder;

    public void DistributeResourcesForRoll(int diceRoll)
    {
        Debug.Log($"=== Distributing for roll {diceRoll} ===");
        HexTile[] tiles = FindObjectsOfType<HexTile>();
        foreach (HexTile tile in tiles)
        {
            if (tile.resourceNumber == diceRoll && tile.resourceType != "Desert")
                DistributeResourcesFromTile(tile);
        }
        DebugPrintResources();
    }

    private void DistributeResourcesFromTile(HexTile tile)
    {
        // השודד חוסם את האריח
        if (RobberManager.instance != null &&
            Vector3.Distance(tile.transform.position, RobberManager.instance.RobberHexPos) <= 0.5f)
        {
            Debug.Log($"[Distribute] Tile '{tile.gameObject.name}' blocked by robber");
            return;
        }
        Debug.Log($"[Distribute] Tile '{tile.gameObject.name}' pos={tile.transform.position} resource={tile.resourceType} number={tile.resourceNumber}");
        CheckBuildPoint(tile.buildTL, tile.resourceType, "buildTL");
        CheckBuildPoint(tile.buildT,  tile.resourceType, "buildT");
        CheckBuildPoint(tile.buildTR, tile.resourceType, "buildTR");
        CheckBuildPoint(tile.buildDL, tile.resourceType, "buildDL");
        CheckBuildPoint(tile.buildD,  tile.resourceType, "buildD");
        CheckBuildPoint(tile.buildDR, tile.resourceType, "buildDR");
    }

    private void CheckBuildPoint(Transform buildPoint, string tileResourceType, string pointName)
    {
        if (buildPoint == null) return;

        Building building = null;
        foreach (Building b in FindObjectsOfType<Building>())
        {
            if (Vector3.Distance(buildPoint.position, b.transform.position) <= 0.5f)
            {
                building = b;
                break;
            }
        }
        if (building == null) return;

        int playerIndex    = building.ownerPlayerIndex;
        int resourceAmount = building.type == Building.BuildingType.City ? 2 : 1;

        if (playerIndex >= 0 && playerIndex < playerResources.Length)
        {
            playerResources[playerIndex].AddResource(tileResourceType, resourceAmount);
            SyncToNet(playerIndex);
            Debug.Log($"Player {playerIndex} received {resourceAmount} {tileResourceType} at {pointName}");
        }
    }

    public bool HasEnoughResources(int playerIndex, Dictionary<string, int> cost)
    {
        // Server reads from authoritative array; clients read from synced NetworkList
        if (IsServer && playerResources != null && playerIndex < playerResources.Length)
            return playerResources[playerIndex].HasEnoughResources(cost);

        foreach (var item in cost)
        {
            int slot = System.Array.IndexOf(ResourceOrder, item.Key);
            if (slot < 0 || GetNetResource(playerIndex, slot) < item.Value) return false;
        }
        return true;
    }

    public void SpendResources(int playerIndex, Dictionary<string, int> cost)
    {
        if (playerIndex >= 0 && playerIndex < playerResources?.Length)
        {
            playerResources[playerIndex].SpendResources(cost);
            SyncToNet(playerIndex);
        }
    }

    private Dictionary<string, int> GetBuildingCost(Building.BuildingType buildingType) => buildingType switch
    {
        Building.BuildingType.Settlement => new Dictionary<string, int> { {"Wood",1}, {"Brick",1}, {"Wheat",1}, {"Sheep",1} },
        Building.BuildingType.City       => new Dictionary<string, int> { {"Wheat",2}, {"Ore",3} },
        _                                => new Dictionary<string, int>()
    };

    public bool CanPlayerBuild(int playerIndex, Building.BuildingType buildingType) =>
        HasEnoughResources(playerIndex, GetBuildingCost(buildingType));

    public bool PurchaseBuilding(int playerIndex, Building.BuildingType buildingType)
    {
        var cost = GetBuildingCost(buildingType);
        if (HasEnoughResources(playerIndex, cost))
        {
            SpendResources(playerIndex, cost);
            Debug.Log($"Player {playerIndex} built a {buildingType}");
            return true;
        }
        Debug.Log($"Player {playerIndex} cannot afford {buildingType}");
        return false;
    }

    public int GetResourceAmountServer(int playerIndex, string resourceType)
    {
        if (!IsServer || playerResources == null || playerIndex >= playerResources.Length) return 0;
        var res = playerResources[playerIndex].resources;
        return res.ContainsKey(resourceType) ? res[resourceType] : 0;
    }

    public void AddResource(int playerIndex, string resourceType, int amount)
    {
        if (!IsServer) return;
        if (playerIndex >= 0 && playerIndex < playerResources?.Length)
        {
            playerResources[playerIndex].AddResource(resourceType, amount);
            SyncToNet(playerIndex);
        }
    }

    public bool RemoveResource(int playerIndex, string resourceType, int amount)
    {
        if (!IsServer) return false;
        if (playerIndex < 0 || playerIndex >= playerResources?.Length) return false;
        var res = playerResources[playerIndex].resources;
        if (!res.ContainsKey(resourceType) || res[resourceType] < amount) return false;
        res[resourceType] -= amount;
        SyncToNet(playerIndex);
        return true;
    }

    public int GetTotalResourceCount(int playerIndex)
    {
        if (IsServer && playerResources != null && playerIndex < playerResources.Length)
        {
            int total = 0;
            foreach (var kv in playerResources[playerIndex].resources) total += kv.Value;
            return total;
        }
        int netTotal = 0;
        for (int r = 0; r < ResourceOrder.Length; r++)
            netTotal += GetNetResource(playerIndex, r);
        return netTotal;
    }

    public void AddTestResources(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerResources?.Length)
        {
            foreach (string res in ResourceOrder)
                playerResources[playerIndex].AddResource(res, 2);
            SyncToNet(playerIndex);
            Debug.Log($"Added test resources to Player {playerIndex}");
        }
    }

    public void DebugPrintResources()
    {
        if (playerResources == null) return;
        Debug.Log("=== Current Resources ===");
        for (int i = 0; i < playerResources.Length; i++)
        {
            string r = "";
            foreach (var kv in playerResources[i].resources)
                r += $"{kv.Key}:{kv.Value} ";
            Debug.Log($"Player {i}: {r}");
        }
    }

    public PlayerResources GetPlayerResources(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerResources?.Length)
            return playerResources[playerIndex];
        return null;
    }

    public string GetPlayerResourcesString(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerResources?.Length)
        {
            string result = "";
            foreach (var kv in playerResources[playerIndex].resources)
                result += $"{kv.Key}: {kv.Value}\n";
            return result;
        }
        return "Invalid player";
    }

    // On server/host: reads from playerResources (always up to date).
    // On client: reads from the network-synced list.
    public string GetNetResourcesString(int playerIndex)
    {
        if (IsServer && playerResources != null && playerIndex < playerResources.Length)
            return GetPlayerResourcesString(playerIndex);

        string result = "";
        for (int r = 0; r < ResourceOrder.Length; r++)
            result += $"{ResourceOrder[r]}: {GetNetResource(playerIndex, r)}\n";
        return result;
    }
}

[System.Serializable]
public class PlayerResources
{
    public Dictionary<string, int> resources = new Dictionary<string, int>
    {
        {"Wood",0}, {"Brick",0}, {"Wheat",0}, {"Sheep",0}, {"Ore",0}
    };

    public void AddResource(string resourceType, int amount)
    {
        if (resources.ContainsKey(resourceType))
            resources[resourceType] += amount;
    }

    public bool HasEnoughResources(Dictionary<string, int> cost)
    {
        foreach (var item in cost)
            if (!resources.ContainsKey(item.Key) || resources[item.Key] < item.Value)
                return false;
        return true;
    }

    public void SpendResources(Dictionary<string, int> cost)
    {
        if (HasEnoughResources(cost))
            foreach (var item in cost)
                resources[item.Key] -= item.Value;
    }
}

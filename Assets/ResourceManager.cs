using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager instance;
    private PlayerResources[] playerResources;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
            
        InitializePlayerResources();
    }

    void InitializePlayerResources()
    {
        TurnManager turnManager = FindObjectOfType<TurnManager>();
        int totalPlayers = turnManager.totalPlayers;
        
        playerResources = new PlayerResources[totalPlayers];
        for (int i = 0; i < totalPlayers; i++)
        {
            playerResources[i] = new PlayerResources();
        }
    }
public void DistributeResourcesForRoll(int diceRoll)
{
    Debug.Log($"=== Starting Resource Distribution for roll {diceRoll} ===");
    
    HexTile[] matchingTiles = FindObjectsOfType<HexTile>();
    Debug.Log($"Found {matchingTiles.Length} total tiles to check");
    
    foreach (HexTile tile in matchingTiles)
    {
        Debug.Log($"Checking tile {tile.name}: Number={tile.resourceNumber}, Resource={tile.resourceType}");
        
        if (tile.resourceNumber == diceRoll && tile.resourceType != "Desert")
        {
            Debug.Log($"Match found! Distributing {tile.resourceType} from tile {tile.name}");
            DistributeResourcesFromTile(tile);
        }
    }
    DebugPrintResources();
}
   
private void DistributeResourcesFromTile(HexTile tile)
{
    Debug.Log($"Checking build points for tile {tile.name}");
    Debug.Log($"buildTL exists: {tile.buildTL != null}");
    Debug.Log($"buildT exists: {tile.buildT != null}");
    Debug.Log($"buildTR exists: {tile.buildTR != null}");
    Debug.Log($"buildDL exists: {tile.buildDL != null}");
    Debug.Log($"buildD exists: {tile.buildD != null}");
    Debug.Log($"buildDR exists: {tile.buildDR != null}");
    
    CheckBuildPoint(tile.buildTL, tile.resourceType, "buildTL");
    CheckBuildPoint(tile.buildT, tile.resourceType, "buildT");
    CheckBuildPoint(tile.buildTR, tile.resourceType, "buildTR");
    CheckBuildPoint(tile.buildDL, tile.resourceType, "buildDL");
    CheckBuildPoint(tile.buildD, tile.resourceType, "buildD");
    CheckBuildPoint(tile.buildDR, tile.resourceType, "buildDR");
}
  private void CheckBuildPoint(Transform buildPoint, string tileResourceType, string pointName)
{
    if (buildPoint == null)
    {
        Debug.Log($"{pointName} is null");
        return;
    }

    // שינוי מ-GetComponentInChildren ל-GetComponent
    Building building = buildPoint.GetComponent<Building>();
    
    // אם אין מבנה בנקודה עצמה, נחפש בסביבה
    if (building == null)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(buildPoint.position, 0.2f);
        foreach (Collider2D collider in colliders)
        {
            building = collider.GetComponent<Building>();
            if (building != null)
            {
                Debug.Log($"Found building near {pointName} owned by Player {building.ownerPlayerIndex}");
                int playerIndex = building.ownerPlayerIndex;
                int resourceAmount = building.type == Building.BuildingType.City ? 2 : 1;
                
                playerResources[playerIndex].AddResource(tileResourceType, resourceAmount);
                string buildingType = building.type == Building.BuildingType.City ? "City" : "Settlement";
                Debug.Log($"Player {playerIndex} received {resourceAmount} {tileResourceType} from their {buildingType} at {pointName}");
                return;
            }
        }
    }
    
    Debug.Log($"No building found at or near {pointName}");
}

    public bool HasEnoughResources(int playerIndex, Dictionary<string, int> cost)
    {
        if (playerIndex >= 0 && playerIndex < playerResources.Length)
        {
            return playerResources[playerIndex].HasEnoughResources(cost);
        }
        return false;
    }

    public void SpendResources(int playerIndex, Dictionary<string, int> cost)
    {
        if (playerIndex >= 0 && playerIndex < playerResources.Length)
        {
            playerResources[playerIndex].SpendResources(cost);
        }
    }

    private Dictionary<string, int> GetBuildingCost(Building.BuildingType buildingType)
    {
        switch (buildingType)
        {
            case Building.BuildingType.Settlement:
                return new Dictionary<string, int>
                {
                    {"Wood", 1},
                    {"Brick", 1},
                    {"Wheat", 1},
                    {"Sheep", 1}
                };
            case Building.BuildingType.City:
                return new Dictionary<string, int>
                {
                    {"Wheat", 2},
                    {"Ore", 3}
                };
            default:
                return new Dictionary<string, int>();
        }
    }

    public bool CanPlayerBuild(int playerIndex, Building.BuildingType buildingType)
    {
        Dictionary<string, int> cost = GetBuildingCost(buildingType);
        return HasEnoughResources(playerIndex, cost);
    }

    public bool PurchaseBuilding(int playerIndex, Building.BuildingType buildingType)
    {
        Dictionary<string, int> cost = GetBuildingCost(buildingType);
        if (HasEnoughResources(playerIndex, cost))
        {
            SpendResources(playerIndex, cost);
            Debug.Log($"Player {playerIndex} built a {buildingType}");
            return true;
        }
        Debug.Log($"Player {playerIndex} cannot afford {buildingType}");
        return false;
    }

    public void AddTestResources(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerResources.Length)
        {
            playerResources[playerIndex].AddResource("Wood", 2);
            playerResources[playerIndex].AddResource("Brick", 2);
            playerResources[playerIndex].AddResource("Wheat", 2);
            playerResources[playerIndex].AddResource("Sheep", 2);
            playerResources[playerIndex].AddResource("Ore", 2);
            Debug.Log($"Added test resources to Player {playerIndex}");
            DebugPrintResources();
        }
    }

    public void DebugPrintResources()
    {
        Debug.Log("=== Current Resources ===");
        for (int i = 0; i < playerResources.Length; i++)
        {
            string resources = "";
            foreach (var resource in playerResources[i].resources)
            {
                resources += $"{resource.Key}: {resource.Value}, ";
            }
            Debug.Log($"Player {i}: {resources}");
        }
    }

    public PlayerResources GetPlayerResources(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerResources.Length)
            return playerResources[playerIndex];
        return null;
    }

    public string GetPlayerResourcesString(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < playerResources.Length)
        {
            string result = "";
            foreach (var resource in playerResources[playerIndex].resources)
            {
                result += $"{resource.Key}: {resource.Value}\n";
            }
            return result;
        }
        return "Invalid player";
    }
}

[System.Serializable]
public class PlayerResources
{
    public Dictionary<string, int> resources = new Dictionary<string, int>()
    {
        {"Wood", 0},
        {"Brick", 0},
        {"Wheat", 0},
        {"Sheep", 0},
        {"Ore", 0}
    };
    
    public void AddResource(string resourceType, int amount)
    {
        if (resources.ContainsKey(resourceType))
        {
             resources[resourceType] += amount;
            Debug.Log($"Player now has {resources[resourceType]} {resourceType}");
        }
    }
    
    public bool HasEnoughResources(Dictionary<string, int> cost)
    {
        foreach (var item in cost)
        {
            if (!resources.ContainsKey(item.Key) || resources[item.Key] < item.Value)
                return false;
        }
        return true;
    }
    
    public void SpendResources(Dictionary<string, int> cost)
    {
        if (HasEnoughResources(cost))
        {
            foreach (var item in cost)
            {
                resources[item.Key] -= item.Value;
            }
        }
    }
}
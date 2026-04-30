using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

public class BuildManager : NetworkBehaviour
{
    [Header("Building Settings")]
    public float hexSize               = 1.0f;
    public float roadConnectionDistance = 1.5f;

    public static BuildManager instance;
    public GameObject settlementPrefab;
    public GameObject cityPrefab;       // prefab נפרד לעיר — גרור ב-Inspector
    public GameObject roadPrefab;

    private bool isBuilding    = false;
    private bool isBuildingRoad = false;
    private Building.BuildingType currentBuildingType;
    private List<BuildingPoint> allBuildPoints;

    // Catan limits
    const int MAX_SETTLEMENTS = 5;
    const int MAX_CITIES      = 4;
    const int MAX_ROADS       = 15;

    // פידבק למשתמש (Subscribe מ-BuildUI)
    public static System.Action<string> onBuildFeedback;

    // דרכים חינם מקלף Road Building (server-side)
    private int[] freeRoadBuilds = new int[8];

    public void AddFreeRoad(int playerIndex, int count = 1)
    {
        if (playerIndex >= 0 && playerIndex < freeRoadBuilds.Length)
            freeRoadBuilds[playerIndex] += count;
    }

    // Readable by Building.cs for OnMouseDown city check
    public Building.BuildingType CurrentBuildingType => currentBuildingType;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start() { } // deferred — HexGridGenerator calls RefreshBuildPoints after grid is ready

    public void RefreshBuildPoints()
    {
        allBuildPoints = FindObjectsOfType<BuildingPoint>().ToList();
        HideAllBuildPoints(); // מוסתר עד שהשחקן לוחץ על כפתור הבנייה
    }

    private void HideAllBuildPoints()
    {
        if (allBuildPoints == null) return;
        foreach (BuildingPoint p in allBuildPoints) p.SetVisibility(false);
    }

    // נקרא במהלך initial placement בין שחקנים
    public void ShowValidBuildPoints()
    {
        if (allBuildPoints == null) return;
        foreach (BuildingPoint p in allBuildPoints)
            p.SetVisibility(IsValidBuildingLocation(p.transform.position));
    }

    // ── Build mode entry ──

    public void StartBuilding(Building.BuildingType buildingType)
    {
        // toggle — אם כבר פתוח באותו מצב, סגור
        if (isBuilding && currentBuildingType == buildingType)
        {
            CancelBuildingClientRpc();
            return;
        }
        isBuilding          = true;
        isBuildingRoad      = false;
        currentBuildingType = buildingType;
        // הסתר נקודות דרך אם היו פתוחות
        foreach (BuildRoad rp in FindObjectsOfType<BuildRoad>()) rp.SetVisibility(false);
        UpdateValidBuildingPoints();
    }

    public void StartBuildingRoad()
    {
        // toggle — אם כבר פתוח, סגור
        if (isBuildingRoad)
        {
            CancelBuildingClientRpc();
            return;
        }
        isBuilding     = false;
        isBuildingRoad = true;
        // הסתר נקודות ישוב אם היו פתוחות
        HideAllBuildPoints();
        UpdateValidRoadPoints(TurnManager.instance.currentPlayer);
    }

    public bool IsBuilding()     => isBuilding;
    public bool IsBuildingRoad() => isBuildingRoad;

    // ── Valid point highlighting ──

    public void UpdateValidBuildingPoints()
    {
        if (allBuildPoints == null) return;

        if (isBuilding && currentBuildingType == Building.BuildingType.City)
        {
            foreach (BuildingPoint p in allBuildPoints) p.SetVisibility(false);
            return;
        }

        int player = TurnManager.instance != null ? TurnManager.instance.currentPlayer : -1;
        bool initialPhase = TurnManager.instance != null && TurnManager.instance.isInitialPlacement;

        foreach (BuildingPoint p in allBuildPoints)
            p.SetVisibility(IsValidBuildingLocation(p.transform.position, player, initialPhase));
    }

    private bool IsValidBuildingLocation(Vector3 position, int playerIndex = -1, bool initialPlacement = true)
    {
        // חייב להיות רחוק מספיק מכל מבנה קיים
        float minDist = hexSize * 2.6f;
        foreach (Building b in FindObjectsOfType<Building>())
            if (CalculateHexGridDistance(position, b.transform.position) < minDist)
                return false;

        // בשלב ההנחה הראשונית אין צורך בחיבור לדרך
        if (initialPlacement || playerIndex < 0) return true;

        // בשלב רגיל — חייב להיות מחובר לדרך של השחקן
        return IsRoadConnectedToSettlement(position, playerIndex);
    }

    private float CalculateHexGridDistance(Vector3 a, Vector3 b)
    {
        float dx = Mathf.Abs(a.x - b.x);
        float dy = Mathf.Abs(a.y - b.y);
        if ((a.y > b.y && a.x > b.x) || (a.y < b.y && a.x < b.x))
            dy *= 1.15f;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    public void OnBuildingPlaced() => CancelBuildingClientRpc();

    [ClientRpc]
    public void CancelBuildingClientRpc()
    {
        isBuilding    = false;
        isBuildingRoad = false;
        HideAllBuildPoints();
        foreach (BuildRoad rp in FindObjectsOfType<BuildRoad>())
            rp.SetVisibility(false);
    }

    // ── Build actions (called from BuildingPoint or Building click) ──

    public void BuildAtCorner(Vector3 position)
    {
        if (!isBuilding) return;
        int player = TurnManager.instance.currentPlayer;

        if (currentBuildingType == Building.BuildingType.City)
        {
            if (!ResourceManager.instance.CanPlayerBuild(player, Building.BuildingType.City))
            { onBuildFeedback?.Invoke("צריך: 2 חיטה + 3 עפרת"); return; }
            BuildAtCornerServerRpc(position, player, (int)Building.BuildingType.City);
        }
        else
        {
            if (!ResourceManager.instance.CanPlayerBuild(player, currentBuildingType))
            { onBuildFeedback?.Invoke("צריך: עץ + לבנה + חיטה + כבשה"); return; }
            BuildAtCornerServerRpc(position, player, (int)currentBuildingType);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void BuildAtCornerServerRpc(Vector3 position, int playerIndex, int buildingType)
    {
        var bt = (Building.BuildingType)buildingType;

        if (bt == Building.BuildingType.City)
        {
            foreach (Building b in FindObjectsOfType<Building>())
            {
                if (b.ownerPlayerIndex == playerIndex &&
                    b.type == Building.BuildingType.Settlement &&
                    Vector3.Distance(b.transform.position, position) <= 0.5f)
                {
                    if (CountBuildings(playerIndex, Building.BuildingType.City) >= MAX_CITIES) return;
                    ResourceManager.instance.PurchaseBuilding(playerIndex, Building.BuildingType.City);

                    Vector3 pos = b.transform.position;
                    b.GetComponent<NetworkObject>().Despawn(true); // הסר ישוב

                    // בנה עיר — אם יש cityPrefab נפרד השתמש בו, אחרת שדרג את אותו prefab
                    var prefab = cityPrefab != null ? cityPrefab : settlementPrefab;
                    var cityGo = Instantiate(prefab, pos, Quaternion.identity);
                    cityGo.GetComponent<NetworkObject>().Spawn(true);
                    cityGo.GetComponent<Building>().Initialize(playerIndex, Building.BuildingType.City);

                    CancelBuildingClientRpc();
                    VictoryManager.instance?.CheckVictory();
                    return;
                }
            }
            return;
        }

        // Settlement
        if (!IsValidBuildingLocation(position, playerIndex, false)) return;
        if (CountBuildings(playerIndex, Building.BuildingType.Settlement) >= MAX_SETTLEMENTS) return;

        var go = Instantiate(settlementPrefab, position, Quaternion.identity);
        go.GetComponent<NetworkObject>().Spawn(true);
        go.GetComponent<Building>().Initialize(playerIndex, Building.BuildingType.Settlement);
        ResourceManager.instance.PurchaseBuilding(playerIndex, Building.BuildingType.Settlement);
        CancelBuildingClientRpc();
        VictoryManager.instance?.CheckVictory();
    }

    private int CountBuildings(int playerIndex, Building.BuildingType type)
    {
        int count = 0;
        foreach (Building b in FindObjectsOfType<Building>())
            if (b.ownerPlayerIndex == playerIndex && b.type == type) count++;
        return count;
    }

    // ── Roads ──

    public void BuildRoad(Vector3 position, float rotation)
    {
        if (!isBuildingRoad) return;
        int player = TurnManager.instance.currentPlayer;
        var cost = new Dictionary<string, int> { {"Wood",1}, {"Brick",1} };
        bool hasFree = player < freeRoadBuilds.Length && freeRoadBuilds[player] > 0;
        if (hasFree || ResourceManager.instance.HasEnoughResources(player, cost))
            BuildRoadServerRpc(position, rotation, player);
        else
            onBuildFeedback?.Invoke("צריך: עץ + לבנה לדרך");
    }

    [ServerRpc(RequireOwnership = false)]
    private void BuildRoadServerRpc(Vector3 position, float rotation, int playerIndex)
    {
        if (CountRoads(playerIndex) >= MAX_ROADS) return;

        bool usedFree = playerIndex < freeRoadBuilds.Length && freeRoadBuilds[playerIndex] > 0;
        if (usedFree)
        {
            freeRoadBuilds[playerIndex]--;
        }
        else
        {
            var cost = new Dictionary<string, int> { {"Wood",1}, {"Brick",1} };
            if (!ResourceManager.instance.HasEnoughResources(playerIndex, cost)) return;
            ResourceManager.instance.SpendResources(playerIndex, cost);
        }

        // בדיקת כפילות
        foreach (Road r in FindObjectsOfType<Road>())
            if (Vector3.Distance(r.transform.position, position) <= 0.25f) return;

        var go = Instantiate(roadPrefab, position, Quaternion.Euler(0, 0, rotation));
        go.GetComponent<NetworkObject>().Spawn(true);
        go.GetComponent<Road>().SetOwner(playerIndex);
        TurnManager.instance?.UpdateLongestRoad();

        // אם נשארו דרכים חינם — הפעל מחדש בניית דרך אצל השחקן הספציפי
        if (usedFree && playerIndex < freeRoadBuilds.Length && freeRoadBuilds[playerIndex] > 0)
        {
            ulong clientId = PlayerManager.instance?.GetClientIdForPlayerIndex(playerIndex) ?? 0;
            ContinueRoadBuildingClientRpc(new ClientRpcParams
                { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
        }
        else
        {
            CancelBuildingClientRpc();
        }
    }

    [ClientRpc]
    private void ContinueRoadBuildingClientRpc(ClientRpcParams rpcParams = default)
    {
        isBuilding     = false;
        isBuildingRoad = true;
        HideAllBuildPoints();
        UpdateValidRoadPoints(TurnManager.instance != null ? TurnManager.instance.currentPlayer : 0);
        onBuildFeedback?.Invoke("הנח דרך חינם נוספת!");
    }

    private int CountRoads(int playerIndex)
    {
        int count = 0;
        foreach (Road r in FindObjectsOfType<Road>())
            if (r.ownerPlayerIndex == playerIndex) count++;
        return count;
    }

    // ── Initial placement ──

    public void BuildInitialSettlement(Vector3 position, int playerIndex)
    {
        BuildInitialSettlementServerRpc(position, playerIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void BuildInitialSettlementServerRpc(Vector3 position, int playerIndex)
    {
        if (!IsValidBuildingLocation(position, -1, true)) return;
        var go = Instantiate(settlementPrefab, position, Quaternion.identity);
        go.GetComponent<NetworkObject>().Spawn(true);
        go.GetComponent<Building>().Initialize(playerIndex, Building.BuildingType.Settlement);
        OnBuildingPlaced();
    }

    public void BuildInitialRoad(Vector3 position, float rotation)
    {
        int player = TurnManager.instance.currentPlayer;
        BuildInitialRoadServerRpc(position, rotation, player);
    }

    public void BuildInitialRoad(Vector3 position, int playerIndex)
    {
        BuildInitialRoadServerRpc(position, 0f, playerIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void BuildInitialRoadServerRpc(Vector3 position, float rotation, int playerIndex)
    {
        var go = Instantiate(roadPrefab, position, Quaternion.Euler(0, 0, rotation));
        go.GetComponent<NetworkObject>().Spawn(true);
        go.GetComponent<Road>().SetOwner(playerIndex);
    }

    // ── Road connection checks ──

    public bool IsRoadConnectedToSettlement(Vector3 roadPosition, int playerIndex)
    {
        foreach (Building b in FindObjectsOfType<Building>())
            if (b.ownerPlayerIndex == playerIndex &&
                Vector3.Distance(roadPosition, b.transform.position) <= roadConnectionDistance)
                return true;

        foreach (Road road in FindObjectsOfType<Road>())
            if (road.ownerPlayerIndex == playerIndex &&
                Vector3.Distance(roadPosition, road.transform.position) <= roadConnectionDistance)
                return true;

        return false;
    }

    public void UpdateValidRoadPoints(int playerIndex)
    {
        foreach (BuildRoad p in FindObjectsOfType<BuildRoad>())
            p.SetVisibility(IsRoadConnectedToSettlement(p.transform.position, playerIndex));
    }

    void OnDrawGizmos()
    {
        if (!isBuilding && !isBuildingRoad) return;
        foreach (Building b in FindObjectsOfType<Building>())
        {
            if (b.ownerPlayerIndex == TurnManager.instance?.currentPlayer)
            {
                Gizmos.color = new Color(0, 1, 0, 0.2f);
                Gizmos.DrawWireSphere(b.transform.position, roadConnectionDistance);
            }
        }
    }
}

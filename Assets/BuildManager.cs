using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

public class BuildManager : NetworkBehaviour
{
    // הוסף למעלה עם שאר המשתנים הציבוריים
   [Header("Building Settings")]
[Tooltip("Size of hexagons in the grid")]
public float hexSize = 1.0f;
[Tooltip("Distance for valid road placement")]
public float roadConnectionDistance = 1.5f;  // הגדלת המרחק האפשרי

    public static BuildManager instance;
    public GameObject settlementPrefab;
    public GameObject roadPrefab;
    private bool isBuilding = false;
    private bool isBuildingRoad = false;
    private Building.BuildingType currentBuildingType;
    private List<BuildingPoint> allBuildPoints;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        allBuildPoints = FindObjectsOfType<BuildingPoint>().ToList();
        UpdateValidBuildingPoints();
    }

    public void StartBuilding(Building.BuildingType buildingType)
    {
        isBuilding = true;
        isBuildingRoad = false;
        currentBuildingType = buildingType;
        UpdateValidBuildingPoints();
    }

    public void StartBuildingRoad()
{
    isBuilding = false;
    isBuildingRoad = true;
    
    TurnManager turnManager = FindObjectOfType<TurnManager>();
    UpdateValidRoadPoints(turnManager.currentPlayer);
}

    public bool IsBuilding()
    {
        return isBuilding;
    }

    public bool IsBuildingRoad()
    {
        return isBuildingRoad;
    }

    public void UpdateValidBuildingPoints()
    {
        if (allBuildPoints == null) return;

        foreach (BuildingPoint point in allBuildPoints)
        {
            bool isValid = IsValidBuildingLocation(point.transform.position);
            point.SetVisibility(isValid);
            Debug.Log($"Setting build point visibility at {point.transform.position}: {isValid}");
        }
    }

private bool IsValidBuildingLocation(Vector3 position)
{
    Building[] existingBuildings = FindObjectsOfType<Building>();
    float minDistanceRequired = hexSize * 2.6f; // מרחק של בערך 2 דרכים

    foreach (Building building in existingBuildings)
    {
        // במקום למצוא מסלול, נבדוק את המרחק בצורה חכמה יותר
        float distance = CalculateHexGridDistance(position, building.transform.position);
        
        if (distance < minDistanceRequired)
        {
            Debug.Log($"Too close to existing building. Distance: {distance}");
            return false;
        }
    }
    return true;
}

private float CalculateHexGridDistance(Vector3 a, Vector3 b)
{
    // מחשב מרחק שמתחשב במבנה המשושי
    float dx = Mathf.Abs(a.x - b.x);
    float dy = Mathf.Abs(a.y - b.y);

    // מתקן את המרחק בהתאם לזווית של הרשת המשושית
    if ((a.y > b.y && a.x > b.x) || (a.y < b.y && a.x < b.x))
    {
        dy *= 1.15f; // מתקן את המרחק האנכי כשהולכים באלכסון
    }

    return Mathf.Sqrt(dx * dx + dy * dy);
}

private int FindShortestPathLength(Vector3 start, Vector3 end, BuildingPoint[] points)
{
    // נמצא את נקודות הבנייה הקרובות לנקודת ההתחלה והסוף
    var adjacencyPoints = new Dictionary<Vector3, List<Vector3>>();
    
    // בונה מפת שכנים עבור כל נקודת בנייה
    foreach (BuildingPoint point in points)
    {
        Vector3 pos = point.transform.position;
        adjacencyPoints[pos] = new List<Vector3>();
        
        // מוצא את כל הנקודות במרחק של דרך אחת
        foreach (BuildingPoint otherPoint in points)
        {
            if (point != otherPoint)
            {
                float distance = Vector3.Distance(pos, otherPoint.transform.position);
                if (distance <= hexSize * 1.2f) // מרווח קטן לסטיות
                {
                    adjacencyPoints[pos].Add(otherPoint.transform.position);
                }
            }
        }
    }

    // מוצא את הנקודות הקרובות ביותר להתחלה ולסוף
    Vector3 startPoint = FindNearestPoint(start, points);
    Vector3 endPoint = FindNearestPoint(end, points);

    // BFS למציאת המסלול הקצר ביותר
    var queue = new Queue<Vector3>();
    var distances = new Dictionary<Vector3, int>();
    
    queue.Enqueue(startPoint);
    distances[startPoint] = 0;

    while (queue.Count > 0)
    {
        Vector3 current = queue.Dequeue();
        
        if (current == endPoint)
            return distances[current];

        foreach (Vector3 neighbor in adjacencyPoints[current])
        {
            if (!distances.ContainsKey(neighbor))
            {
                distances[neighbor] = distances[current] + 1;
                queue.Enqueue(neighbor);
            }
        }
    }

    return int.MaxValue; // אם לא נמצא מסלול
}

private Vector3 FindNearestPoint(Vector3 position, BuildingPoint[] points)
{
    float minDistance = float.MaxValue;
    Vector3 nearest = Vector3.zero;

    foreach (BuildingPoint point in points)
    {
        float distance = Vector3.Distance(position, point.transform.position);
        if (distance < minDistance)
        {
            minDistance = distance;
            nearest = point.transform.position;
        }
    }

    return nearest;
}
    private bool IsPointBetweenBuildings(Vector3 point, Vector3 start, Vector3 end)
    {
        float totalDistance = Vector3.Distance(start, end);
        float distanceFromStart = Vector3.Distance(start, point);
        float distanceFromEnd = Vector3.Distance(point, end);

        // מרווח שגיאה קטן לחישובים
        float tolerance = 0.1f;

        // הנקודה נחשבת "בין" אם סכום המרחקים שלה מנקודת ההתחלה והסוף
        // שווה בערך למרחק הישיר בין נקודת ההתחלה והסוף
        return Mathf.Abs(distanceFromStart + distanceFromEnd - totalDistance) < tolerance;
    }

    public void OnBuildingPlaced()
    {
        UpdateValidBuildingPoints();
    }

    public void BuildAtCorner(Vector3 position)
    {
        if (!isBuilding) return;
        int currentPlayer = TurnManager.instance.currentPlayer;
        if (ResourceManager.instance.CanPlayerBuild(currentPlayer, currentBuildingType))
            BuildAtCornerServerRpc(position, currentPlayer, (int)currentBuildingType);
        else
            Debug.Log("Not enough resources!");
    }

    [ServerRpc(RequireOwnership = false)]
    private void BuildAtCornerServerRpc(Vector3 position, int playerIndex, int buildingType)
    {
        if (!IsValidBuildingLocation(position)) return;
        var go = Instantiate(settlementPrefab, position, Quaternion.identity);
        go.GetComponent<NetworkObject>().Spawn(true);
        go.GetComponent<Building>().Initialize(playerIndex, (Building.BuildingType)buildingType);
        ResourceManager.instance.PurchaseBuilding(playerIndex, (Building.BuildingType)buildingType);
        isBuilding = false;
        OnBuildingPlaced();
    }

    public void BuildRoad(Vector3 position, float rotation)
    {
        if (!isBuildingRoad) return;
        int currentPlayer = TurnManager.instance.currentPlayer;
        var roadCost = new Dictionary<string, int> { {"Wood", 1}, {"Brick", 1} };
        if (ResourceManager.instance.HasEnoughResources(currentPlayer, roadCost))
            BuildRoadServerRpc(position, rotation, currentPlayer);
        else
            Debug.Log("Not enough resources for road!");
    }

    [ServerRpc(RequireOwnership = false)]
    private void BuildRoadServerRpc(Vector3 position, float rotation, int playerIndex)
    {
        var go = Instantiate(roadPrefab, position, Quaternion.Euler(0, 0, rotation));
        go.GetComponent<NetworkObject>().Spawn(true);
        go.GetComponent<Road>().SetOwner(playerIndex);
        var roadCost = new Dictionary<string, int> { {"Wood", 1}, {"Brick", 1} };
        ResourceManager.instance.SpendResources(playerIndex, roadCost);
        isBuildingRoad = false;
    }

    public void BuildInitialRoad(Vector3 position, float rotation)
    {
        int currentPlayer = TurnManager.instance.currentPlayer;
        BuildInitialRoadServerRpc(position, rotation, currentPlayer);
    }
private BuildRoad FindRoadPointAtPosition(Vector3 position)
{
    float threshold = 0.1f; // מרחק סף לזיהוי נקודת הדרך
    BuildRoad[] roadPoints = FindObjectsOfType<BuildRoad>();
    
    foreach (BuildRoad point in roadPoints)
    {
        if (Vector3.Distance(point.transform.position, position) < threshold)
        {
            return point;
        }
    }
    return null;
}

    public void BuildInitialSettlement(Vector3 position, int playerIndex)
    {
        BuildInitialSettlementServerRpc(position, playerIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void BuildInitialSettlementServerRpc(Vector3 position, int playerIndex)
    {
        if (!IsValidBuildingLocation(position)) return;
        var go = Instantiate(settlementPrefab, position, Quaternion.identity);
        go.GetComponent<NetworkObject>().Spawn(true);
        go.GetComponent<Building>().Initialize(playerIndex, Building.BuildingType.Settlement);
        OnBuildingPlaced();
    }

    [ServerRpc(RequireOwnership = false)]
    private void BuildInitialRoadServerRpc(Vector3 position, float rotation, int playerIndex)
    {
        var go = Instantiate(roadPrefab, position, Quaternion.Euler(0, 0, rotation));
        go.GetComponent<NetworkObject>().Spawn(true);
        go.GetComponent<Road>().SetOwner(playerIndex);
    }

    public void BuildInitialRoad(Vector3 position, int playerIndex)
    {
        BuildInitialRoadServerRpc(position, 0f, playerIndex);
    }

 public bool IsRoadConnectedToSettlement(Vector3 roadPosition, int playerIndex)
{
    float maxDistance = roadConnectionDistance;  // שימוש במשתנה החדש

    // בדיקת חיבור למבנים
    Building[] buildings = FindObjectsOfType<Building>();
    foreach (Building building in buildings)
    {
        if (building != null && building.ownerPlayerIndex == playerIndex)
        {
            float distance = Vector3.Distance(roadPosition, building.transform.position);
            Debug.Log($"Checking distance to building: {distance}"); // לוג לדיבאג
            if (distance <= maxDistance)
            {
                return true;
            }
        }
    }

    // בדיקת חיבור לדרכים קיימות
    GameObject[] existingRoads = GameObject.FindGameObjectsWithTag("Road");
    foreach (GameObject road in existingRoads)
    {
        SpriteRenderer roadRenderer = road.GetComponent<SpriteRenderer>();
        if (roadRenderer != null && roadRenderer.color == GetPlayerColor(playerIndex))
        {
            float distance = Vector3.Distance(roadPosition, road.transform.position);
            Debug.Log($"Checking distance to road: {distance}"); // לוג לדיבאג
            if (distance <= maxDistance)
            {
                return true;
            }
        }
    }

    return false;
}

public void UpdateValidRoadPoints(int playerIndex)
{
    BuildRoad[] roadPoints = FindObjectsOfType<BuildRoad>();
    foreach (BuildRoad point in roadPoints)
    {
        bool isValid = IsRoadConnectedToSettlement(point.transform.position, playerIndex);
        point.SetVisibility(isValid);
    }
}
    private Color GetPlayerColor(int playerIndex)
    {
        switch(playerIndex)
        {
            case 0: return Color.red;
            case 1: return Color.blue;
            case 2: return Color.green;
            case 3: return Color.yellow;
            default: return Color.white;
        }
    }
     void OnDrawGizmos()
    {
        if (isBuilding || isBuildingRoad)
        {
            TurnManager turnManager = FindObjectOfType<TurnManager>();
            if (turnManager != null)
            {
                // מציג את טווח החיבור סביב כל מבנה של השחקן הנוכחי
                Building[] buildings = FindObjectsOfType<Building>();
                foreach (Building building in buildings)
                {
                    if (building.ownerPlayerIndex == turnManager.currentPlayer)
                    {
                        Gizmos.color = new Color(0, 1, 0, 0.2f);
                        Gizmos.DrawWireSphere(building.transform.position, roadConnectionDistance);
                    }
                }
            }
        }
    }
}
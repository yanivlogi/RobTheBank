using UnityEngine;
using System.Collections.Generic;

public class HexGridGenerator : MonoBehaviour
{
    [Header("Hex Settings")]
    public GameObject hexPrefab;   
    public int mapRadius = 2;      
    public float hexSize = 1f;     

    [Header("Resources and Numbers")]
    [Tooltip("יער:4 | מרעה:4 | חיטה:4 | לבנה:3 | ברזל:3 | מדבר:1")]
    public string[] resourceTypes = {
        "Wood", "Wood", "Wood", "Wood",
        "Sheep", "Sheep", "Sheep", "Sheep",
        "Wheat", "Wheat", "Wheat", "Wheat",
        "Brick", "Brick", "Brick",
        "Ore", "Ore", "Ore",
        "Desert"
    };

    public int[] possibleNumbers = { 2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12 };
    
    [Header("Resource Sprites")]
    public Sprite woodSprite;
    public Sprite brickSprite;
    public Sprite wheatSprite;
    public Sprite sheepSprite;
    public Sprite oreSprite;
    public Sprite desertSprite;

    [Header("Building Points")]
    public GameObject buildPointPrefab;  
    public GameObject roadPointPrefab;   

    private List<GameObject> hexTiles = new List<GameObject>();
    private List<Vector2Int> hexCoordinates = new List<Vector2Int>();

    void Start()
    {
        GenerateHexGrid();
        AssignResourcesAndNumbers();
        SetupBuildingPoints();
    }

    void GenerateHexGrid()
    {
        hexCoordinates.Clear();
        hexCoordinates.AddRange(GenerateHexCoordinates());

        foreach (Vector2Int coord in hexCoordinates)
        {
            Vector3 hexPosition = HexToWorldPosition(coord);
            GameObject hex = Instantiate(hexPrefab, hexPosition, Quaternion.identity);
            hex.transform.SetParent(transform);
            hex.name = $"Hex_{coord.x}_{coord.y}";
            hexTiles.Add(hex);
        }
    }

    List<Vector2Int> GenerateHexCoordinates()
    {
        List<Vector2Int> coordinates = new List<Vector2Int>();

        for (int q = -mapRadius; q <= mapRadius; q++)
        {
            for (int r = -mapRadius; r <= mapRadius; r++)
            {
                if (Mathf.Abs(q + r) <= mapRadius)
                {
                    coordinates.Add(new Vector2Int(q, r));
                }
            }
        }

        return coordinates;
    }

    Vector3 HexToWorldPosition(Vector2Int hexCoord)
    {
        float x = hexSize * Mathf.Sqrt(3) * (hexCoord.x + hexCoord.y / 2f);
        float y = hexSize * 1.5f * hexCoord.y;
        return new Vector3(x, y, 0);
    }

    void SetupBuildingPoints()
    {
        foreach (GameObject hex in hexTiles)
        {
            HexTile hexTile = hex.GetComponent<HexTile>();
            
            // נקודות בנייה ליישובים/ערים
            hexTile.buildT = CreateBuildPoint(hex.transform, new Vector3(0, hexSize, 0), "BuildPoint_Top");
            hexTile.buildTR = CreateBuildPoint(hex.transform, new Vector3(hexSize * 0.866f, hexSize * 0.5f, 0), "BuildPoint_TopRight");
            hexTile.buildTL = CreateBuildPoint(hex.transform, new Vector3(-hexSize * 0.866f, hexSize * 0.5f, 0), "BuildPoint_TopLeft");
            hexTile.buildD = CreateBuildPoint(hex.transform, new Vector3(0, -hexSize, 0), "BuildPoint_Bottom");
            hexTile.buildDR = CreateBuildPoint(hex.transform, new Vector3(hexSize * 0.866f, -hexSize * 0.5f, 0), "BuildPoint_BottomRight");
            hexTile.buildDL = CreateBuildPoint(hex.transform, new Vector3(-hexSize * 0.866f, -hexSize * 0.5f, 0), "BuildPoint_BottomLeft");

            // נקודות לדרכים
            CreateRoadPoints(hex.transform);
        }
    }

    private Transform CreateBuildPoint(Transform parent, Vector3 localPosition, string pointName)
    {
        GameObject point = Instantiate(buildPointPrefab, parent.position + localPosition, Quaternion.identity);
        point.name = pointName;
        point.transform.SetParent(parent);
        point.AddComponent<BuildingPoint>();
        return point.transform;
    }
private void CreateRoadPoints(Transform parent)
{
    Vector3[] roadPositions = new Vector3[]
    {
        new Vector3(hexSize * 0.433f, hexSize * 0.75f, 0),  // Top-Right
        new Vector3(-hexSize * 0.433f, hexSize * 0.75f, 0), // Top-Left
        new Vector3(hexSize * 0.866f, 0, 0),                // Right
        new Vector3(-hexSize * 0.866f, 0, 0),               // Left
        new Vector3(hexSize * 0.433f, -hexSize * 0.75f, 0), // Bottom-Right
        new Vector3(-hexSize * 0.433f, -hexSize * 0.75f, 0) // Bottom-Left
    };

    float[] roadAngles = new float[]
    {
        150f,   // Top-Right
        210f,   // Top-Left
        90f,   // Right
        90f,   // Left
        30f,    // Bottom-Right
        -30f    // Bottom-Left
    };

    string[] roadNames = new string[]
    {
        "RoadPoint_TopRight",
        "RoadPoint_TopLeft",
        "RoadPoint_Right",
        "RoadPoint_Left",
        "RoadPoint_BottomRight",
        "RoadPoint_BottomLeft"
    };

    for (int i = 0; i < roadPositions.Length; i++)
    {
        GameObject roadPoint = Instantiate(roadPointPrefab, parent.position + roadPositions[i], Quaternion.Euler(0, 0, roadAngles[i]));
        roadPoint.name = roadNames[i];
        roadPoint.transform.SetParent(parent);
        Debug.Log($"Created {roadNames[i]} with rotation {roadAngles[i]}");
    }
}
    List<string> BuildResourceListForHexCount(int hexCount)
    {
        List<string> nonDesert = new List<string>();
        foreach (string r in resourceTypes)
            if (r != "Desert") nonDesert.Add(r);

        List<string> result = new List<string> { "Desert" };
        int idx = 0;
        while (result.Count < hexCount)
        {
            result.Add(nonDesert[idx % nonDesert.Count]);
            idx++;
        }
        return result;
    }

    static readonly Vector2Int[] HexNeighbors = {
        new Vector2Int(1, 0), new Vector2Int(1, -1), new Vector2Int(0, -1),
        new Vector2Int(-1, 0), new Vector2Int(-1, 1), new Vector2Int(0, 1)
    };

    void ScatterResources(List<string> resources)
    {
        Dictionary<Vector2Int, int> coordToIndex = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < hexCoordinates.Count; i++)
            coordToIndex[hexCoordinates[i]] = i;

        int maxPasses = 50;
        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool swapped = false;
            for (int i = 0; i < resources.Count; i++)
            {
                for (int j = i + 1; j < resources.Count; j++)
                {
                    if (resources[i] == resources[j]) continue;
                    int adjBefore = CountAdjacentSame(resources, coordToIndex, i) + CountAdjacentSame(resources, coordToIndex, j);
                    string ri = resources[i], rj = resources[j];
                    resources[i] = rj; resources[j] = ri;
                    int adjAfter = CountAdjacentSame(resources, coordToIndex, i) + CountAdjacentSame(resources, coordToIndex, j);
                    if (adjAfter < adjBefore) swapped = true;
                    else { resources[i] = ri; resources[j] = rj; }
                }
            }
            if (!swapped) break;
        }
    }

    int CountAdjacentSame(List<string> resources, Dictionary<Vector2Int, int> coordToIndex, int idx)
    {
        Vector2Int coord = hexCoordinates[idx];
        string myRes = resources[idx];
        int count = 0;
        foreach (var dir in HexNeighbors)
        {
            var n = coord + dir;
            if (coordToIndex.TryGetValue(n, out int ni) && resources[ni] == myRes) count++;
        }
        return count;
    }

void AssignResourcesAndNumbers()
{
    int hexCount = hexTiles.Count;
    List<string> resourcesToAssign = BuildResourceListForHexCount(hexCount);
    resourcesToAssign.Shuffle();
    ScatterResources(resourcesToAssign);

    List<int> shuffledNumbers = new List<int>(possibleNumbers);
    shuffledNumbers.Shuffle();

    int resourceIndex = 0;
    int numberIndex = 0;

    foreach (GameObject hex in hexTiles)
    {
        HexTile hexTile = hex.GetComponent<HexTile>();
        if (hexTile == null)
        {
            Debug.LogError($"HexTile component is missing on {hex.name}");
            continue;
        }

        SpriteRenderer spriteRenderer = hexTile.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"SpriteRenderer is missing on {hex.name}");
            continue;
        }

        if (resourceIndex >= resourcesToAssign.Count)
        {
            Debug.LogError($"Not enough resources for all hexes. Hexes: {hexCount}, Resources: {resourcesToAssign.Count}");
            break;
        }

        string resource = resourcesToAssign[resourceIndex];
        resourceIndex++;

        if (resource == "Desert")
        {
            hexTile.InitializeTile(0, resource);
            spriteRenderer.sprite = desertSprite;
        }
        else
        {
            if (numberIndex < shuffledNumbers.Count)
            {
                int number = shuffledNumbers[numberIndex];
                numberIndex++;
                hexTile.InitializeTile(number, resource);
            }
            else
            {
                hexTile.InitializeTile(0, resource);
            }
            spriteRenderer.sprite = GetResourceSprite(resource);
        }
    }
}


    private Sprite GetResourceSprite(string resource)
    {
        switch (resource)
        {
            case "Wood": return woodSprite;
            case "Brick": return brickSprite;
            case "Wheat": return wheatSprite;
            case "Sheep": return sheepSprite;
            case "Ore": return oreSprite;
            default: return null;
        }
    }

#if UNITY_EDITOR
    // פונקציית עזר להצגת הנקודות במצב עריכה
    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            List<Vector2Int> coordinates = GenerateHexCoordinates();
            foreach (Vector2Int coord in coordinates)
            {
                Vector3 pos = HexToWorldPosition(coord);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(pos, 0.1f);
            }
        }
    }
#endif
}

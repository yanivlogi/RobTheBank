using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexTile : MonoBehaviour
{
    [Header("Resource Info")]
    public int resourceNumber;
    public string resourceType;
    
    [Header("Building Points")]
    public Transform buildT;
    public Transform buildTL;
    public Transform buildTR;
    public Transform buildD;
    public Transform buildDL; 
    public Transform buildDR;

    [Header("Road Points")]
    // נשנה את נקודות הדרכים כך שכל משושה ייצור רק את הנקודות הימניות והעליונות שלו
    public Transform roadR;      // ימין
    public Transform roadTR;     // ימין-למעלה
    public Transform roadT;      // למעלה

    public void InitializeTile(int number, string type)
    {
        resourceNumber = number;
        resourceType = type;
        InitializeBuildingPoints();
        InitializeRoadPoints();
        Debug.Log($"Tile: {name}, Resource: {resourceType}, Number: {resourceNumber}");
    }

    private void InitializeBuildingPoints()
    {
        if (buildT) AddBuildingPoint(buildT);
        if (buildTL) AddBuildingPoint(buildTL);
        if (buildTR) AddBuildingPoint(buildTR);
        if (buildD) AddBuildingPoint(buildD);
        if (buildDL) AddBuildingPoint(buildDL);
        if (buildDR) AddBuildingPoint(buildDR);
    }

    private void InitializeRoadPoints()
    {
        // רק את נקודות הדרך הימניות והעליונות
        if (roadR) AddRoadPoint(roadR);
        if (roadTR) AddRoadPoint(roadTR);
        if (roadT) AddRoadPoint(roadT);
    }

    private void AddBuildingPoint(Transform point)
    {
        if (!point.GetComponent<BuildingPoint>())
        {
            point.gameObject.AddComponent<BuildingPoint>();
            point.gameObject.AddComponent<BoxCollider2D>();
        }
    }

    private void AddRoadPoint(Transform point)
    {
        if (!point.GetComponent<BuildRoad>())
        {
            point.gameObject.AddComponent<BuildRoad>();
            BoxCollider2D collider = point.gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.3f, 0.3f); // התאמת גודל ה-collider
        }
    }

    private void OnDrawGizmos()
    {
        // הצגת נקודות בנייה
        Gizmos.color = Color.blue;
        DrawGizmoPoint(buildT);
        DrawGizmoPoint(buildTL);
        DrawGizmoPoint(buildTR);
        DrawGizmoPoint(buildD);
        DrawGizmoPoint(buildDL);
        DrawGizmoPoint(buildDR);

        // הצגת נקודות דרכים - רק הנקודות שאנחנו באמת משתמשים בהן
        Gizmos.color = Color.yellow;
        DrawGizmoPoint(roadR);
        DrawGizmoPoint(roadTR);
        DrawGizmoPoint(roadT);
    }

    private void DrawGizmoPoint(Transform point)
    {
        if (point != null)
        {
            Gizmos.DrawWireSphere(point.position, 0.1f);
        }
    }
}
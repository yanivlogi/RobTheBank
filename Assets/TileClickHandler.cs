using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileClickHandler : MonoBehaviour
{
    public Transform[] buildPoints; // נקודות בנייה על האריח

    private void OnMouseDown()
    {
        Debug.Log("Tile clicked!");

        // בדיקה שיש נקודות בנייה זמינות
        if (buildPoints.Length > 0 && BuildManager.instance.IsBuilding())
        {
            // בחירת נקודת הבנייה הראשונה (לדוגמה)
            Vector3 buildPosition = buildPoints[0].position;
            
            TurnManager turnManager = FindObjectOfType<TurnManager>();
            int currentPlayer = turnManager.currentPlayer;

            // בדיקה האם הנקודה פנויה
            if (IsPointAvailable(buildPosition))
            {
                BuildManager.instance.BuildAtCorner(buildPosition);
            }
            else
            {
                Debug.Log("Build point is already occupied!");
            }
        }
    }

    private bool IsPointAvailable(Vector3 position)
    {
        // בדיקה אם יש כבר מבנה במיקום זה
        Collider[] colliders = Physics.OverlapSphere(position, 0.1f);
        foreach (Collider collider in colliders)
        {
            if (collider.GetComponent<Building>() != null)
            {
                return false;
            }
        }
        return true;
    }

    // פונקציה עזר להצגת נקודות הבנייה במצב פיתוח
    void OnDrawGizmos()
    {
        if (buildPoints == null) return;

        foreach (Transform point in buildPoints)
        {
            if (point != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(point.position, 0.1f);
            }
        }
    }
}
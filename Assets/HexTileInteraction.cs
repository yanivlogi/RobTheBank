using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class HexTileInteraction : MonoBehaviour
{
    public Transform[] buildPoints; // נקודות בנייה על האריח

    // private void OnMouseDown()
    // {
    //     Debug.Log("Tile clicked!");

    //     // לדוגמה, לבחור את הנקודה הראשונה
    //     if (buildPoints.Length > 0)
    //     {
    //         Vector3 buildPosition = buildPoints[0].position;
    //         // וודא ש- buildPoints[0].position הוא ה- Vector3 שנשלח
    //         BuildManager.instance.BuildAtCorner(buildPoints[3].position);

    //     }
    // }
}

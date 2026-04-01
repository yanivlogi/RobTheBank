using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildUI : MonoBehaviour
{
    public void OnBuildSettlementClick()
    {
        Debug.Log("Starting Settlement Build Mode");
        BuildManager.instance.StartBuilding(Building.BuildingType.Settlement);
    }

    public void OnBuildRoadClick()
    {
        Debug.Log("Starting Road Build Mode");
        BuildManager.instance.StartBuildingRoad();
    }
}

using UnityEngine;

public class BuildUI : MonoBehaviour
{
    public void OnBuildSettlementClick()
    {
        if (!TurnManager.instance.IsMyTurn()) { Debug.Log("Not your turn!"); return; }
        Debug.Log("Starting Settlement Build Mode");
        BuildManager.instance.StartBuilding(Building.BuildingType.Settlement);
    }

    public void OnBuildRoadClick()
    {
        if (!TurnManager.instance.IsMyTurn()) { Debug.Log("Not your turn!"); return; }
        Debug.Log("Starting Road Build Mode");
        BuildManager.instance.StartBuildingRoad();
    }

    public void OnRollDiceClick()
    {
        TurnManager.instance.RollDice();
    }

    public void OnEndTurnClick()
    {
        TurnManager.instance.NextTurn();
    }

    public void OnStartBuildingPhaseClick()
    {
        TurnManager.instance.StartBuildingPhase();
    }

    public void OnEndTradingPhaseClick()
    {
        TurnManager.instance.EndTradingPhase();
    }
}

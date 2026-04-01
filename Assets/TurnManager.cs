using UnityEngine;
using UnityEngine.Events;

public class TurnManager : MonoBehaviour
{
    public static TurnManager instance;

    public int currentPlayer = 0;
    public int totalPlayers = 4;
    
    public UnityEvent<int> onPlayerTurnChanged;
    public UnityEvent<int> onDiceRolled;
    
    public enum TurnState
    {
        WaitingForSettlement,
        WaitingForDiceRoll,
        ResourceCollection,
        Building,
        Trading,
        TurnEnd
    }
    
    public TurnState currentState;
    public bool isInitialPlacement = true;
    public bool waitingForRoad = false;
    private int initialSettlementsPlaced = 0;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        currentState = TurnState.WaitingForSettlement;
        Debug.Log("Place your first settlement");
    }

    public bool IsInInitialPhase()
    {
        return isInitialPlacement;
    }

    public void PlaceInitialSettlement(Vector3 position, int playerIndex)
    {
        BuildManager.instance.BuildInitialSettlement(position, currentPlayer);
        waitingForRoad = true;
        
        HighlightValidRoadPoints(position);
        
        Debug.Log($"Player {currentPlayer + 1} - Now place a road connected to your settlement");
    }

    private void HighlightValidRoadPoints(Vector3 settlementPosition)
    {
        BuildRoad[] roadPoints = FindObjectsOfType<BuildRoad>();
        float maxDistance = 1.5f;

        foreach (BuildRoad roadPoint in roadPoints)
        {
            float distance = Vector3.Distance(settlementPosition, roadPoint.transform.position);
            roadPoint.SetVisibility(distance <= maxDistance);
        }
    }

    public void HideAllRoadPoints()
    {
        BuildRoad[] roadPoints = FindObjectsOfType<BuildRoad>();
        foreach (BuildRoad roadPoint in roadPoints)
        {
            roadPoint.SetVisibility(false);
        }
    }

   public void PlaceInitialRoad(Vector3 position, float rotation)
{
    if (!waitingForRoad) return;

    if (BuildManager.instance.IsRoadConnectedToSettlement(position, currentPlayer))
    {
        BuildManager.instance.BuildInitialRoad(position, rotation);
        waitingForRoad = false;
            HideAllRoadPoints();
            initialSettlementsPlaced++;

            if (initialSettlementsPlaced >= totalPlayers * 2)
            {
                isInitialPlacement = false;
                currentState = TurnState.WaitingForDiceRoll;
                Debug.Log("Initial placement phase complete. Game starting!");
            }
            else
            {
                currentPlayer = (currentPlayer + 1) % totalPlayers;
                onPlayerTurnChanged?.Invoke(currentPlayer);
                Debug.Log($"Player {currentPlayer + 1}'s turn to place settlement");
            }
        }
        else
        {
            Debug.Log("Road must be connected to your settlement!");
        }
    }

    public void NextTurn()
    {
        if (currentState != TurnState.TurnEnd)
        {
            Debug.LogWarning("Cannot end turn before completing all actions!");
            return;
        }
        
        currentPlayer = (currentPlayer + 1) % totalPlayers;
        currentState = TurnState.WaitingForDiceRoll;
        
        Debug.Log($"Player {currentPlayer + 1}'s turn");
        onPlayerTurnChanged?.Invoke(currentPlayer);
    }

    public void RollDice()
    {
        if (currentState != TurnState.WaitingForDiceRoll)
        {
            Debug.LogWarning("Can only roll dice at the start of your turn!");
            return;
        }

        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        int totalRoll = dice1 + dice2;
        
        Debug.Log($"Rolled: {dice1} + {dice2} = {totalRoll}");
        onDiceRolled?.Invoke(totalRoll);
        
        if (totalRoll == 7)
        {
            HandleRobber();
        }
        else
        {
            DistributeResources(totalRoll);
        }
        
        currentState = TurnState.ResourceCollection;
    }
    
    private void HandleRobber()
    {
        Debug.Log("Robber has been activated!");
    }
    
    private void DistributeResources(int diceRoll)
    {
        Debug.Log($"Distributing resources for number {diceRoll}");
        ResourceManager.instance.DistributeResourcesForRoll(diceRoll);
    }
    
    public void StartBuildingPhase()
    {
        if (currentState != TurnState.ResourceCollection)
        {
            Debug.LogWarning("Must collect resources before building!");
            return;
        }
        
        currentState = TurnState.Building;
        Debug.Log("Starting building phase");
    }
    
    public void StartTradingPhase()
    {
        if (currentState != TurnState.Building)
        {
            Debug.LogWarning("Must complete building phase before trading!");
            return;
        }
        
        currentState = TurnState.Trading;
        Debug.Log("Starting trading phase");
    }
    
    public void EndTradingPhase()
    {
        if (currentState != TurnState.Trading)
        {
            Debug.LogWarning("Not in trading phase!");
            return;
        }
        
        currentState = TurnState.TurnEnd;
        Debug.Log("Trading phase ended - can now end turn");
    }
    
    public string GetCurrentStateInfo()
    {
        return $"Player {currentPlayer + 1}, State: {currentState}";
    }
}
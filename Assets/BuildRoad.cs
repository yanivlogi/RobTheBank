using UnityEngine;
using System.Collections.Generic;

public class BuildRoad : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private bool canBuild = true;
    public static Dictionary<string, int> roadCost = new Dictionary<string, int>
    {
        {"Wood", 1},
        {"Brick", 1}
    };

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        SetVisibility(false);
    }

    public void SetVisibility(bool isVisible)
    {
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = isVisible ? 1f : 0f;
            spriteRenderer.color = color;
        }
    }

    private void OnMouseDown()
    {
        TurnManager turnManager = FindObjectOfType<TurnManager>();
        if (!turnManager.IsMyTurn()) return;

        Debug.Log($"Road point clicked with rotation: {transform.eulerAngles.z}");
        if (!canBuild)
        {
            Debug.Log("Cannot build here - point is disabled");
            return;
        }

        if (turnManager.IsInInitialPhase() && turnManager.waitingForRoad)
        {
            if (BuildManager.instance.IsRoadConnectedToSettlement(transform.position, turnManager.currentPlayer))
            {
                // העבר גם את הרוטציה
                turnManager.PlaceInitialRoad(transform.position, transform.eulerAngles.z);
            }
            else
            {
                Debug.Log("Road must be connected to your settlement!");
            }
            return;
        }

        if (BuildManager.instance.IsBuildingRoad() && canBuild)
        {
            int currentPlayer = turnManager.currentPlayer;
            if (ResourceManager.instance.HasEnoughResources(currentPlayer, roadCost))
            {
                // העבר את הרוטציה גם כאן
                BuildManager.instance.BuildRoad(transform.position, transform.eulerAngles.z);
            }
            else
            {
                Debug.Log("Not enough resources to build road!");
            }
        }
    }
}
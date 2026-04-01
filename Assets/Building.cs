using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour
{
    public int ownerPlayerIndex;
    public string resourceType;
    public BuildingType type;

    public enum BuildingType
    {
        Settlement,
        City
    }

    public void Initialize(int playerIndex, BuildingType buildingType)
    {
        ownerPlayerIndex = playerIndex;
        type = buildingType;
        GetComponent<Renderer>().material.color = GetPlayerColor(playerIndex);
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
}

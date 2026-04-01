using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ResourceUI : MonoBehaviour
{
  public TextMeshProUGUI[] playerResourceTexts;
    
    void Update()
    {
        if (ResourceManager.instance != null)
        {
            for (int i = 0; i < playerResourceTexts.Length; i++)
            {
                playerResourceTexts[i].text = $"Player {i + 1}:\n" + 
                    ResourceManager.instance.GetPlayerResourcesString(i);
            }
        }
    }
}
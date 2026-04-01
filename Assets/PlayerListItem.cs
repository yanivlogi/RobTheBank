using TMPro;
using UnityEngine;

public class PlayerListItem : MonoBehaviour
{
    public TMP_Text playerNameText;

    public void Setup(string playerName)
    {
        if (playerNameText != null)
            playerNameText.text = playerName;
    }
}
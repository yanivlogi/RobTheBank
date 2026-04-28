using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class TradingManager : NetworkBehaviour
{
    public static TradingManager instance;

    [Header("Trade Panel UI — assign in Inspector")]
    public GameObject  tradePanel;
    public TMP_Dropdown giveDropdown;
    public TMP_Dropdown receiveDropdown;
    public TMP_Text    feedbackText;

    private static readonly string[] ResourceNames = { "Wood", "Brick", "Wheat", "Sheep", "Ore" };

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        if (tradePanel != null) tradePanel.SetActive(false);
    }

    // ── Called from BuildUI button ──

    public void OpenTradePanel()
    {
        if (TurnManager.instance == null || !TurnManager.instance.IsMyTurn()) return;
        if (tradePanel != null) tradePanel.SetActive(true);
        if (feedbackText != null) feedbackText.text = "Give 4 of one resource, get 1 of another.";
    }

    public void CloseTradePanel()
    {
        if (tradePanel != null) tradePanel.SetActive(false);
    }

    public void OnConfirmTradeClick()
    {
        if (TurnManager.instance == null || !TurnManager.instance.IsMyTurn()) return;
        int giveIdx    = giveDropdown    != null ? giveDropdown.value    : 0;
        int receiveIdx = receiveDropdown != null ? receiveDropdown.value : 1;
        if (giveIdx == receiveIdx) { SetFeedback("Can't trade the same resource!"); return; }
        ExecuteTradeServerRpc(PlayerManager.LocalPlayerIndex, giveIdx, receiveIdx);
    }

    // ── Networked trade ──

    [ServerRpc(RequireOwnership = false)]
    private void ExecuteTradeServerRpc(int playerIndex, int giveIdx, int receiveIdx)
    {
        if (giveIdx == receiveIdx) return;
        string give = ResourceNames[giveIdx];
        string recv = ResourceNames[receiveIdx];

        var cost = new Dictionary<string, int> { {give, 4} };
        if (!ResourceManager.instance.HasEnoughResources(playerIndex, cost))
        {
            TradeResultClientRpc(playerIndex, false, giveIdx, receiveIdx);
            return;
        }

        ResourceManager.instance.SpendResources(playerIndex, cost);
        ResourceManager.instance.AddResource(playerIndex, recv, 1);
        TradeResultClientRpc(playerIndex, true, giveIdx, receiveIdx);
    }

    [ClientRpc]
    private void TradeResultClientRpc(int playerIndex, bool success, int gaveIdx, int receivedIdx)
    {
        if (PlayerManager.LocalPlayerIndex != playerIndex) return;
        if (success)
        {
            SetFeedback($"4 {ResourceNames[gaveIdx]} → 1 {ResourceNames[receivedIdx]}");
            CloseTradePanel();
        }
        else
        {
            SetFeedback($"Need 4 {ResourceNames[gaveIdx]}!");
        }
    }

    private void SetFeedback(string msg)
    {
        if (feedbackText != null) feedbackText.text = msg;
        Debug.Log("[Trade] " + msg);
    }
}

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildUI : MonoBehaviour
{
    [Header("Feedback — assign a TMP_Text in Inspector")]
    public TMP_Text feedbackText;

    [Header("Dev Card Action Panel (Year of Plenty / Monopoly)")]
    public GameObject    devCardActionPanel;
    public TMP_Text      devCardActionTitle;
    public TMP_Dropdown  devCardResource1Dropdown;
    public TMP_Dropdown  devCardResource2Dropdown; // hidden for Monopoly
    public GameObject    resource2Row;             // parent of second dropdown — hide for Monopoly

    private Coroutine feedbackCoroutine;
    private int       pendingDevCardAction; // 1 = YearOfPlenty, 2 = Monopoly

    // ── Lifecycle ──

    void OnEnable()  { BuildManager.onBuildFeedback += ShowFeedback; }
    void OnDisable() { BuildManager.onBuildFeedback -= ShowFeedback; }
    void Start()
    {
        if (feedbackText          != null) feedbackText.gameObject.SetActive(false);
        if (devCardActionPanel    != null) devCardActionPanel.SetActive(false);
    }

    // ── Build buttons ──

    public void OnBuildSettlementClick()
    {
        if (!TurnManager.instance.IsMyTurn()) { ShowFeedback("לא התור שלך!"); return; }
        BuildManager.instance.StartBuilding(Building.BuildingType.Settlement);
    }

    public void OnBuildCityClick()
    {
        if (!TurnManager.instance.IsMyTurn()) { ShowFeedback("לא התור שלך!"); return; }
        BuildManager.instance.StartBuilding(Building.BuildingType.City);
        ShowFeedback("לחץ על אחד מהישובים שלך לשדרג לעיר");
    }

    public void OnBuildRoadClick()
    {
        if (!TurnManager.instance.IsMyTurn()) { ShowFeedback("לא התור שלך!"); return; }
        BuildManager.instance.StartBuildingRoad();
    }

    // ── Dev card buttons ──

    public void OnBuyDevCardClick()
    {
        if (!TurnManager.instance.IsMyTurn()) { ShowFeedback("לא התור שלך!"); return; }
        DevCardManager.instance?.BuyDevCardServerRpc(PlayerManager.LocalPlayerIndex);
    }

    public void OnPlayKnightClick()
    {
        if (!TurnManager.instance.IsMyTurn()) { ShowFeedback("לא התור שלך!"); return; }
        DevCardManager.instance?.PlayKnightServerRpc(PlayerManager.LocalPlayerIndex);
    }

    public void OnPlayRoadBuildingClick()
    {
        if (!TurnManager.instance.IsMyTurn()) { ShowFeedback("לא התור שלך!"); return; }
        DevCardManager.instance?.PlayRoadBuildingServerRpc(PlayerManager.LocalPlayerIndex);
    }

    public void OnPlayYearOfPlentyClick()
    {
        if (!TurnManager.instance.IsMyTurn()) { ShowFeedback("לא התור שלך!"); return; }
        pendingDevCardAction = 1;
        OpenDevCardPanel("Year of Plenty — בחר 2 משאבים", showSecond: true);
    }

    public void OnPlayMonopolyClick()
    {
        if (!TurnManager.instance.IsMyTurn()) { ShowFeedback("לא התור שלך!"); return; }
        pendingDevCardAction = 2;
        OpenDevCardPanel("Monopoly — בחר משאב לגנוב", showSecond: false);
    }

    public void OnConfirmDevCardActionClick()
    {
        int p = PlayerManager.LocalPlayerIndex;
        int r1 = devCardResource1Dropdown != null ? devCardResource1Dropdown.value : 0;
        int r2 = devCardResource2Dropdown != null ? devCardResource2Dropdown.value : 0;

        if (pendingDevCardAction == 1)
            DevCardManager.instance?.PlayYearOfPlentyServerRpc(p, r1, r2);
        else if (pendingDevCardAction == 2)
            DevCardManager.instance?.PlayMonopolyServerRpc(p, r1);

        if (devCardActionPanel != null) devCardActionPanel.SetActive(false);
    }

    public void OnCancelDevCardActionClick()
    {
        if (devCardActionPanel != null) devCardActionPanel.SetActive(false);
    }

    // ── Trade ──

    public void OnOpenTradeClick()   { TradingManager.instance?.OpenTradePanel(); }

    // ── Turn flow ──

    public void OnRollDiceClick()             { TurnManager.instance.RollDice(); }
    public void OnEndTurnClick()              { TurnManager.instance.NextTurn(); }
    public void OnStartBuildingPhaseClick()   { TurnManager.instance.StartBuildingPhase(); }
    public void OnEndTradingPhaseClick()      { TurnManager.instance.EndTradingPhase(); }

    // ── Helpers ──

    private void OpenDevCardPanel(string title, bool showSecond)
    {
        if (devCardActionPanel == null) return;
        devCardActionPanel.SetActive(true);
        if (devCardActionTitle != null) devCardActionTitle.text = title;
        if (resource2Row != null) resource2Row.SetActive(showSecond);
    }

    private void ShowFeedback(string msg)
    {
        if (feedbackText == null) { Debug.Log("[UI] " + msg); return; }
        if (feedbackCoroutine != null) StopCoroutine(feedbackCoroutine);
        feedbackCoroutine = StartCoroutine(FeedbackRoutine(msg));
    }

    private IEnumerator FeedbackRoutine(string msg)
    {
        feedbackText.text = msg;
        feedbackText.gameObject.SetActive(true);
        yield return new WaitForSeconds(2.5f);
        feedbackText.gameObject.SetActive(false);
        feedbackCoroutine = null;
    }
}

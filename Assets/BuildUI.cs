using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildUI : MonoBehaviour
{
    [Header("Feedback")]
    public TMP_Text feedbackText;

    [Header("Dev Card Action Panel")]
    public GameObject   devCardActionPanel;
    public TMP_Text     devCardActionTitle;
    public TMP_Dropdown devCardResource1Dropdown;
    public TMP_Dropdown devCardResource2Dropdown;
    public GameObject   resource2Row;

    [Header("Buttons — גרור מה-Inspector")]
    public Button buildSettlementButton;
    public Button buildCityButton;
    public Button buildRoadButton;
    public Button buyDevCardButton;
    public Button playKnightButton;
    public Button playRoadBuildingButton;
    public Button playYearOfPlentyButton;
    public Button playMonopolyButton;
    public Button openTradeButton;
    public Button rollDiceButton;
    public Button endTurnButton;

    private Coroutine feedbackCoroutine;
    private int       pendingDevCardAction;

    // ── Lifecycle ──

    void OnEnable()  { BuildManager.onBuildFeedback += ShowFeedback; }
    void OnDisable() { BuildManager.onBuildFeedback -= ShowFeedback; }

    void Start()
    {
        if (feedbackText       != null) feedbackText.gameObject.SetActive(false);
        if (devCardActionPanel != null) devCardActionPanel.SetActive(false);
    }

    void Update() => RefreshButtons();

    // ── Button state refresh ──

    private void RefreshButtons()
    {
        if (TurnManager.instance == null) return;

        bool isMyTurn = TurnManager.instance.IsMyTurn();
        var  state    = TurnManager.instance.currentState;
        bool initial  = TurnManager.instance.isInitialPlacement;
        int  me       = PlayerManager.LocalPlayerIndex;

        bool beforeRoll = isMyTurn && state == TurnManager.TurnState.WaitingForDiceRoll;
        bool afterRoll  = isMyTurn && !initial &&
                          state != TurnManager.TurnState.WaitingForDiceRoll &&
                          state != TurnManager.TurnState.WaitingForSettlement;

        // כפתורי בנייה — גלויים תמיד, מופעלים רק אם יש משאבים
        var rm = ResourceManager.instance;
        SetBtn(buildSettlementButton, afterRoll && rm != null && rm.CanPlayerBuild(me, Building.BuildingType.Settlement));
        SetBtn(buildCityButton,       afterRoll && rm != null && rm.CanPlayerBuild(me, Building.BuildingType.City));
        SetBtn(buildRoadButton,       afterRoll && rm != null && rm.HasEnoughResources(me, roadCost));
        SetBtn(buyDevCardButton,      afterRoll && rm != null && rm.HasEnoughResources(me, devCardCost));
        SetBtn(openTradeButton,       afterRoll);

        // כפתורי קלפי פיתוח — מוסתרים אם אין את הקלף, מופיעים רק כשיש
        bool canPlay = beforeRoll || afterRoll;
        var  dm      = DevCardManager.instance;
        RefreshDevBtn(playKnightButton,       dm?.GetCard(me, DevCardManager.CardType.Knight)       ?? 0, canPlay);
        RefreshDevBtn(playRoadBuildingButton,  dm?.GetCard(me, DevCardManager.CardType.RoadBuilding) ?? 0, canPlay);
        RefreshDevBtn(playYearOfPlentyButton,  dm?.GetCard(me, DevCardManager.CardType.YearOfPlenty) ?? 0, canPlay);
        RefreshDevBtn(playMonopolyButton,      dm?.GetCard(me, DevCardManager.CardType.Monopoly)     ?? 0, canPlay);

        SetBtn(rollDiceButton, beforeRoll);
        SetBtn(endTurnButton,  afterRoll);
    }

    private void RefreshDevBtn(Button btn, int count, bool canPlay)
    {
        if (btn == null) return;
        btn.gameObject.SetActive(count > 0);
        btn.interactable = canPlay && count > 0;
    }

    private void SetBtn(Button btn, bool on)
    {
        if (btn != null) btn.interactable = on;
    }

    private static readonly System.Collections.Generic.Dictionary<string, int> roadCost =
        new System.Collections.Generic.Dictionary<string, int> { {"Wood",1}, {"Brick",1} };
    private static readonly System.Collections.Generic.Dictionary<string, int> devCardCost =
        new System.Collections.Generic.Dictionary<string, int> { {"Wheat",1}, {"Sheep",1}, {"Ore",1} };

    // ── Build buttons ──

    public void OnBuildSettlementClick() => BuildManager.instance.StartBuilding(Building.BuildingType.Settlement);
    public void OnBuildCityClick()
    {
        BuildManager.instance.StartBuilding(Building.BuildingType.City);
        if (BuildManager.instance.IsBuilding())
            ShowFeedback("לחץ על אחד מהישובים שלך לשדרג לעיר");
    }
    public void OnBuildRoadClick() => BuildManager.instance.StartBuildingRoad();

    // ── Dev card buttons ──

    public void OnBuyDevCardClick()
        => DevCardManager.instance?.BuyDevCardServerRpc(PlayerManager.LocalPlayerIndex);

    public void OnPlayKnightClick()
        => DevCardManager.instance?.PlayKnightServerRpc(PlayerManager.LocalPlayerIndex);

    public void OnPlayRoadBuildingClick()
        => DevCardManager.instance?.PlayRoadBuildingServerRpc(PlayerManager.LocalPlayerIndex);

    public void OnPlayYearOfPlentyClick()
    {
        pendingDevCardAction = 1;
        OpenDevCardPanel("Year of Plenty — בחר 2 משאבים", showSecond: true);
    }

    public void OnPlayMonopolyClick()
    {
        pendingDevCardAction = 2;
        OpenDevCardPanel("Monopoly — בחר משאב לגנוב", showSecond: false);
    }

    public void OnConfirmDevCardActionClick()
    {
        int p  = PlayerManager.LocalPlayerIndex;
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

    public void OnOpenTradeClick() => TradingManager.instance?.OpenTradePanel();

    // ── Turn flow ──

    public void OnRollDiceClick()           => TurnManager.instance.RollDice();
    public void OnEndTurnClick()            => TurnManager.instance.NextTurn();
    public void OnStartBuildingPhaseClick() => TurnManager.instance.StartBuildingPhase();
    public void OnEndTradingPhaseClick()    => TurnManager.instance.EndTradingPhase();

    // ── Helpers ──

    private void OpenDevCardPanel(string title, bool showSecond)
    {
        if (devCardActionPanel == null) return;
        devCardActionPanel.SetActive(true);
        if (devCardActionTitle != null) devCardActionTitle.text = title;
        if (resource2Row      != null) resource2Row.SetActive(showSecond);
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
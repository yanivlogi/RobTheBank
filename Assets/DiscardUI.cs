using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiscardUI : MonoBehaviour
{
    public static DiscardUI instance;

    [Header("Panel")]
    public GameObject panel;
    public TMP_Text   titleText;
    public TMP_Text   progressText;

    [Header("Resource Rows — 5 entries: Wood, Brick, Wheat, Sheep, Ore")]
    public TMP_Text[] availableTexts;   // כמה יש לשחקן
    public TMP_Text[] selectedTexts;    // כמה בחר להשליך
    public Button[]   increaseButtons;  // + לכל משאב
    public Button[]   decreaseButtons;  // - לכל כל משאב

    [Header("Confirm")]
    public Button confirmButton;

    private int   playerIndex;
    private int   required;
    private int[] selected  = new int[5];
    private int[] available = new int[5];

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
        if (panel != null) panel.SetActive(false);
    }

    public void Show(int pIndex, int amount)
    {
        playerIndex = pIndex;
        required    = amount;
        selected    = new int[5];

        for (int r = 0; r < 5; r++)
            available[r] = ResourceManager.instance.GetNetResource(pIndex, r);

        if (panel != null) panel.SetActive(true);

        // חיבור כפתורים
        for (int r = 0; r < 5; r++)
        {
            int cap = r;
            if (increaseButtons != null && cap < increaseButtons.Length && increaseButtons[cap] != null)
            {
                increaseButtons[cap].onClick.RemoveAllListeners();
                increaseButtons[cap].onClick.AddListener(() => Adjust(cap, +1));
            }
            if (decreaseButtons != null && cap < decreaseButtons.Length && decreaseButtons[cap] != null)
            {
                decreaseButtons[cap].onClick.RemoveAllListeners();
                decreaseButtons[cap].onClick.AddListener(() => Adjust(cap, -1));
            }
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirm);
        }

        Refresh();
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Adjust(int r, int delta)
    {
        int next = selected[r] + delta;
        if (next < 0 || next > available[r]) return;
        if (delta > 0 && TotalSelected() >= required) return;
        selected[r] = next;
        Refresh();
    }

    private int TotalSelected()
    {
        int t = 0;
        for (int r = 0; r < 5; r++) t += selected[r];
        return t;
    }

    private void Refresh()
    {
        int total = TotalSelected();
        if (titleText    != null) titleText.text    = $"השלך {required} קלפים";
        if (progressText != null) progressText.text = $"נבחר: {total} / {required}";

        for (int r = 0; r < 5; r++)
        {
            if (availableTexts != null && r < availableTexts.Length && availableTexts[r] != null)
                availableTexts[r].text = available[r].ToString();

            if (selectedTexts != null && r < selectedTexts.Length && selectedTexts[r] != null)
                selectedTexts[r].text = selected[r].ToString();

            if (increaseButtons != null && r < increaseButtons.Length && increaseButtons[r] != null)
                increaseButtons[r].interactable = selected[r] < available[r] && total < required;

            if (decreaseButtons != null && r < decreaseButtons.Length && decreaseButtons[r] != null)
                decreaseButtons[r].interactable = selected[r] > 0;
        }

        if (confirmButton != null)
            confirmButton.interactable = total == required;
    }

    private void OnConfirm()
    {
        RobberManager.instance?.SubmitDiscardServerRpc(
            playerIndex,
            selected[0], selected[1], selected[2], selected[3], selected[4]);
        Hide();
    }
}

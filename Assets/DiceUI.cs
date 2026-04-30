using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiceUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject  dicePanel;
    public CanvasGroup canvasGroup;

    [Header("Die 1")]
    public Image   dice1Image;
    public TMP_Text dice1Text;

    [Header("Die 2")]
    public Image   dice2Image;
    public TMP_Text dice2Text;

    [Header("Total")]
    public TMP_Text totalText;

    [Header("Sprites (optional — 6 sprites, index 0 = face 1)")]
    public Sprite[] diceSprites;

    [Header("Timing")]
    public float rollDuration    = 0.6f;
    public float displayDuration = 2.5f;
    public float fadeOutDuration = 0.4f;

    private Coroutine activeRoutine;

    void OnEnable()  => TurnManager.onDiceRolledPair += ShowDice;
    void OnDisable() => TurnManager.onDiceRolledPair -= ShowDice;

    void Start()
    {
        if (dicePanel != null) dicePanel.SetActive(false);
    }

    private void ShowDice(int d1, int d2)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(RollRoutine(d1, d2));
    }

    private IEnumerator RollRoutine(int d1, int d2)
    {
        if (dicePanel    != null) dicePanel.SetActive(true);
        if (canvasGroup  != null) canvasGroup.alpha = 1f;
        if (totalText    != null) totalText.gameObject.SetActive(false);

        // אנימציית גלגול — פנים אקראיות כל 70ms
        float elapsed = 0f;
        while (elapsed < rollDuration)
        {
            SetDiceFace(dice1Image, dice1Text, Random.Range(1, 7));
            SetDiceFace(dice2Image, dice2Text, Random.Range(1, 7));
            elapsed += 0.07f;
            yield return new WaitForSeconds(0.07f);
        }

        // תוצאה סופית
        SetDiceFace(dice1Image, dice1Text, d1);
        SetDiceFace(dice2Image, dice2Text, d2);

        if (totalText != null)
        {
            totalText.gameObject.SetActive(true);
            int total = d1 + d2;
            totalText.text  = $"= {total}";
            totalText.color = (total == 7)
                ? new Color(1f, 0.25f, 0.25f)   // אדום — שודד
                : Color.white;
        }

        yield return new WaitForSeconds(displayDuration);

        // fade out
        if (canvasGroup != null)
        {
            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = 1f - t / fadeOutDuration;
                yield return null;
            }
        }

        if (dicePanel != null) dicePanel.SetActive(false);
        activeRoutine = null;
    }

    private void SetDiceFace(Image img, TMP_Text txt, int value)
    {
        bool useSprites = diceSprites != null && diceSprites.Length >= 6 && diceSprites[value - 1] != null;

        if (useSprites)
        {
            if (img != null) img.sprite = diceSprites[value - 1];
            if (txt != null) txt.gameObject.SetActive(false);
        }
        else
        {
            if (txt != null)
            {
                txt.gameObject.SetActive(true);
                txt.text = value.ToString();
            }
        }
    }
}

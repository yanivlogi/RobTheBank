using UnityEngine;

public class PlayerCardManager : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject playerCardPrefab;

    [Header("Layout")]
    public Transform cardsContainer;
    public float cardSpacing = 8f;

    private PlayerCardUI[] cards;

    void Update()
    {
        if (cards == null && TurnManager.instance != null)
            BuildAllCards();

        if (cards != null)
            foreach (var c in cards)
                c?.Refresh();
    }

    private void BuildAllCards()
    {
        if (playerCardPrefab == null)
        {
            Debug.LogWarning("[PlayerCardManager] playerCardPrefab is not assigned!");
            return;
        }

        Transform parent = cardsContainer != null
            ? cardsContainer
            : FindObjectOfType<Canvas>()?.transform ?? transform;

        // Read card width from prefab RectTransform
        float cardWidth = 160f;
        var prefabRt = playerCardPrefab.GetComponent<RectTransform>();
        if (prefabRt != null) cardWidth = prefabRt.rect.width;
        if (cardWidth <= 0f) cardWidth = 160f;

        int count = TurnManager.instance.totalPlayers;
        cards = new PlayerCardUI[count];

        float totalWidth = count * cardWidth + (count - 1) * cardSpacing;
        float startX = -totalWidth / 2f + cardWidth / 2f;

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(playerCardPrefab, parent);
            go.name = $"PlayerCard_{i}";

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = new Vector2(startX + i * (cardWidth + cardSpacing), 0f);

            cards[i] = go.GetComponent<PlayerCardUI>();
            if (cards[i] == null)
            {
                Debug.LogError($"[PlayerCardManager] Prefab '{playerCardPrefab.name}' has no PlayerCardUI component!");
                continue;
            }
            cards[i].Setup(i);
        }
    }
}

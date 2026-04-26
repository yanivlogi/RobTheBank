using UnityEngine;

public class BuildingPoint : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D buildCollider;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        buildCollider = GetComponent<BoxCollider2D>();
        if (buildCollider == null)
        {
            buildCollider = gameObject.AddComponent<BoxCollider2D>();
            buildCollider.size = new Vector2(0.3f, 0.3f);
        }
    }

    public void SetVisibility(bool isValid)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = isValid;
        }
        if (buildCollider != null)
        {
            buildCollider.enabled = isValid;
        }
    }

    private void OnMouseDown()
    {
        TurnManager turnManager = FindObjectOfType<TurnManager>();
        if (!turnManager.IsMyTurn()) return;

        if (turnManager.IsInInitialPhase())
        {
            if (!turnManager.waitingForRoad)
                turnManager.PlaceInitialSettlement(transform.position, turnManager.currentPlayer);
        }
        else if (BuildManager.instance.IsBuilding())
        {
            BuildManager.instance.BuildAtCorner(transform.position);
        }
    }
}
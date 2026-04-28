using Unity.Netcode;
using UnityEngine;

public class Building : NetworkBehaviour
{
    private NetworkVariable<int> netOwner = new NetworkVariable<int>(-1);
    private NetworkVariable<int> netType  = new NetworkVariable<int>(0);

    public int ownerPlayerIndex => netOwner.Value;
    public BuildingType type    => (BuildingType)netType.Value;
    public string resourceType;

    public enum BuildingType { Settlement, City }

    [Header("Visual Scale")]
    public float visualScale = 1f;

    [Header("Sprites — slot 0=אדום  1=כחול  2=לבן  3=כתום")]
    public Sprite[] settlementSprites = new Sprite[4];
    public Sprite[] citySprites       = new Sprite[4];

    private Vector3 prefabScale;

    void Awake()
    {
        prefabScale = transform.localScale;
        if (GetComponent<Collider2D>() == null)
        {
            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;
        }
    }

    public override void OnNetworkSpawn()
    {
        netOwner.OnValueChanged += (_, v) => ApplyVisual(v, type);
        netType.OnValueChanged  += (_, v) => ApplyVisual(netOwner.Value, (BuildingType)v);
        ApplyVisual(netOwner.Value, type);
    }

    // Server only
    public void Initialize(int playerIndex, BuildingType buildingType)
    {
        if (!IsServer) return;
        netOwner.Value = playerIndex;
        netType.Value  = (int)buildingType;
    }

    // Server only — upgrades this settlement to a city
    public void UpgradeToCity()
    {
        if (!IsServer) return;
        netType.Value = (int)BuildingType.City;
    }

    private void ApplyVisual(int playerIndex, BuildingType buildingType)
    {
        if (playerIndex < 0) return;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            var sprites = buildingType == BuildingType.City ? citySprites : settlementSprites;
            if (sprites != null && playerIndex < sprites.Length && sprites[playerIndex] != null)
            {
                sr.sprite = sprites[playerIndex];
                sr.color  = Color.white; // הספרייט כבר צבוע — לא צריך tint
            }
            else
            {
                sr.color = GetPlayerColor(playerIndex); // fallback אם אין ספרייט
            }
        }

        transform.localScale = prefabScale * visualScale;
    }

    // Allows clicking directly on a placed settlement when BuildManager is in City mode
    void OnMouseDown()
    {
        if (BuildManager.instance == null || !BuildManager.instance.IsBuilding()) return;
        if (BuildManager.instance.CurrentBuildingType != BuildingType.City) return;
        if (type != BuildingType.Settlement) return;
        if (ownerPlayerIndex != TurnManager.instance?.currentPlayer) return;

        BuildManager.instance.BuildAtCorner(transform.position);
    }

    public static Color GetPlayerColor(int playerIndex) => playerIndex switch
    {
        0 => new Color(0.85f, 0.15f, 0.15f), // אדום
        1 => new Color(0.20f, 0.45f, 0.90f), // כחול
        2 => new Color(0.90f, 0.90f, 0.90f), // לבן
        3 => new Color(0.95f, 0.55f, 0.10f), // כתום
        _ => Color.white
    };
}

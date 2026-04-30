using UnityEngine;
using TMPro;

public class ResourceUI : MonoBehaviour
{
    [Header("Resources — local player only")]
    public TMP_Text woodText;
    public TMP_Text brickText;
    public TMP_Text wheatText;
    public TMP_Text sheepText;
    public TMP_Text oreText;

    [Header("Dev Cards in hand — local player only")]
    public TMP_Text knightCardText;
    public TMP_Text vpCardText;
    public TMP_Text roadBuildCardText;
    public TMP_Text yearOfPlentyCardText;
    public TMP_Text monopolyCardText;

    void Update()
    {
        if (ResourceManager.instance == null) return;
        int me = PlayerManager.LocalPlayerIndex;
        if (me < 0) return;

        // משאבים
        SetText(woodText,  ResourceManager.instance.GetNetResource(me, 0));
        SetText(brickText, ResourceManager.instance.GetNetResource(me, 1));
        SetText(wheatText, ResourceManager.instance.GetNetResource(me, 2));
        SetText(sheepText, ResourceManager.instance.GetNetResource(me, 3));
        SetText(oreText,   ResourceManager.instance.GetNetResource(me, 4));

        // קלפי פיתוח
        if (DevCardManager.instance == null) return;
        SetText(knightCardText,       DevCardManager.instance.GetCard(me, DevCardManager.CardType.Knight));
        SetText(vpCardText,           DevCardManager.instance.GetCard(me, DevCardManager.CardType.VP));
        SetText(roadBuildCardText,    DevCardManager.instance.GetCard(me, DevCardManager.CardType.RoadBuilding));
        SetText(yearOfPlentyCardText, DevCardManager.instance.GetCard(me, DevCardManager.CardType.YearOfPlenty));
        SetText(monopolyCardText,     DevCardManager.instance.GetCard(me, DevCardManager.CardType.Monopoly));
    }

    private void SetText(TMP_Text t, int value)
    {
        if (t != null) t.text = value.ToString();
    }
}
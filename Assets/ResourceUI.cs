using UnityEngine;
using TMPro;

public class ResourceUI : MonoBehaviour
{
    [Header("Shows only the local player's resources")]
    public TextMeshProUGUI myResourcesText;

    void Update()
    {
        if (ResourceManager.instance == null || myResourcesText == null) return;
        int myIndex = PlayerManager.LocalPlayerIndex;
        if (myIndex < 0) return;
        myResourcesText.text = ResourceManager.instance.GetNetResourcesString(myIndex);
    }
}

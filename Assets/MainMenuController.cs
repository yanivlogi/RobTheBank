using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    public TextMeshProUGUI nameDisplay; // הטקסט "Player" בשלט העץ
    public Image avatarDisplay;        // תמונת האווטאר בשלט העץ

    void Start()
    {
        RefreshDisplay();
    }

    // פונקציה שניתן לקרוא לה מכל מקום כדי לעדכן את התצוגה
    public void RefreshDisplay()
    {
        // טעינת שם - ברירת מחדל "Player"
        string savedName = PlayerPrefs.GetString("SavedPlayerName", "Player");
        if (nameDisplay != null) nameDisplay.text = savedName;

        // טעינת אווטאר
        if (PlayerPrefs.HasKey("SavedAvatarName"))
        {
            string savedAvatar = PlayerPrefs.GetString("SavedAvatarName");
            Sprite loadedSprite = Resources.Load<Sprite>("Avatars/" + savedAvatar);
            
            if (loadedSprite != null && avatarDisplay != null)
            {
                avatarDisplay.sprite = loadedSprite;
            }
        }
        Debug.Log("התצוגה בדף הראשי עודכנה!");
    }
}

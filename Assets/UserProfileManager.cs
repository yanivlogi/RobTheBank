using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UserProfileManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_InputField nameInputField; // שדה הקלדת השם
    public Image avatarImage;             // תמונת האווטאר בתוך פאנל ההגדרות
    
    [Header("Panels")]
    public GameObject onlinePanel;      // גרור לכאן את הפאנל שצריך להיסגר (User Setting)

    void Start()
    {
        LoadUserData();
    }

    public void SaveUserData()
    {
        // 1. שמירה ל-PlayerPrefs
        string nameToSave = nameInputField.text;
        PlayerPrefs.SetString("SavedPlayerName", nameToSave);

        if (avatarImage.sprite != null)
        {
            PlayerPrefs.SetString("SavedAvatarName", avatarImage.sprite.name);
        }

        PlayerPrefs.Save();
        Debug.Log("הנתונים נשמרו בהצלחה!");

        // 2. עדכון מיידי של הדף הראשי
        MainMenuController mainDisplay = FindFirstObjectByType<MainMenuController>();
        if (mainDisplay != null)
        {
            mainDisplay.RefreshDisplay();
        }

        // 3. סגירת הפאנל (השורה החדשה)
        CloseSettings();
    }

    public void CloseSettings()
    {
        if (onlinePanel != null)
        {
            onlinePanel.SetActive(false);
        }
        else
        {
            // אם לא גררת פאנל, הוא יסגור את האובייקט שהסקריפט עליו
            gameObject.SetActive(false);
        }
    }

    public void LoadUserData()
    {
        if (PlayerPrefs.HasKey("SavedPlayerName"))
        {
            nameInputField.text = PlayerPrefs.GetString("SavedPlayerName");
        }

        if (PlayerPrefs.HasKey("SavedAvatarName"))
        {
            string savedAvatar = PlayerPrefs.GetString("SavedAvatarName");
            Sprite loadedSprite = Resources.Load<Sprite>("Avatars/" + savedAvatar);
            if (loadedSprite != null)
            {
                avatarImage.sprite = loadedSprite;
            }
        }
    }
}

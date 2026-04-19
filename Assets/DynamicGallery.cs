using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DynamicGallery : MonoBehaviour
{
    [Header("UI References")]
    public GameObject buttonPrefab;    // ה-Prefab של הכפתור
    public Transform gridContent;      // ה-Content שבתוך ה-Scroll View
    public Image mainAvatarDisplay;    // האווטאר הראשי שמשתנה (העיגול במסך הראשי)
    public GameObject galleryPanel;    // הפאנל הראשי של הגלריה

    void Start()
    {
        GenerateGallery();
    }

    void GenerateGallery()
    {
        Sprite[] allAvatars = Resources.LoadAll<Sprite>("Avatars");

        if (allAvatars.Length == 0)
        {
            Debug.LogError("שגיאה: לא נמצאו תמונות בתיקייה Resources/Avatars!");
            return;
        }

        foreach (Sprite s in allAvatars)
        {
            GameObject newBtn = Instantiate(buttonPrefab, gridContent);
            
            Image btnImage = newBtn.GetComponentInChildren<Image>();
            if (btnImage != null)
            {
                btnImage.sprite = s;
            }

            Button btn = newBtn.GetComponent<Button>();
            if (btn != null)
            {
                // ניקוי לחיצות קודמות והוספת לחיצה חדשה
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    SelectAvatar(s);
                });
            }
        }

        StartCoroutine(ResetScrollToTop());
    }

    public void SelectAvatar(Sprite selectedSprite)
    {
        Debug.Log("אווטאר נבחר: " + selectedSprite.name);

        // בדיקה אם המשתנה מחובר ב-Inspector
        if (mainAvatarDisplay != null)
        {
            mainAvatarDisplay.sprite = selectedSprite;
        }
        else
        {
            Debug.LogError("שגיאה: לא גררת את ה-Main Avatar Display לסקריפט ב-Inspector!");
        }
        
        // קריאה לפונקציית הסגירה
        CloseGallery(); 
    }

    public void CloseGallery()
    {
        Debug.Log("מנסה לסגור את הפאנל...");

        if (galleryPanel != null)
        {
            galleryPanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Gallery Panel לא מחובר, סוגר את האובייקט הנוכחי.");
            gameObject.SetActive(false);
        }
    }

    IEnumerator ResetScrollToTop()
    {
        yield return new WaitForEndOfFrame();
        ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
        
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }
}

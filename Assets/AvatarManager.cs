using UnityEngine;
using UnityEngine.UI;

public class AvatarManager : MonoBehaviour
{
    public Image currentAvatarDisplay; // התמונה הראשית שרואים ב-User Setting
    public GameObject galleryPanel;    // הפאנל עם כל האווטארים

    // פונקציה לפתיחת/סגירת הגלריה
    public void ToggleGallery()
    {
        galleryPanel.SetActive(!galleryPanel.activeSelf);
    }

    // פונקציה שתופעל כשלוחצים על אווטאר ספציפי בגלריה
    public void SelectAvatar(Image selectedImage)
    {
        currentAvatarDisplay.sprite = selectedImage.sprite; // עדכון האווטאר הנבחר
        galleryPanel.SetActive(false); // סגירת הגלריה
    }
}

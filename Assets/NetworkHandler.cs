using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI; // חובה כדי לגשת לכפתור

public class NetworkHandler : MonoBehaviour
{
    public Button startGameButton; // תגרור את הכפתור לכאן ב-Inspector

   public void StartHost()
{
    Debug.Log("מנסה להפעיל Host...");
    
    if (NetworkManager.Singleton.StartHost())
    {
        Debug.Log("הוסט הופעל בהצלחה!");
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(true); // מציג את האובייקט
            startGameButton.interactable = true;        // הופך אותו ללחיץ
        }
    }
    else
    {
        Debug.LogError("נכשל בהפעלת Host! בדוק את ה-Console לשגיאות נוספות.");
    }
}


    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        // עבור Client, הכפתור יישאר לא לחיץ (אפור)
    }

    public void LoadMainGame()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            // מעביר את כולם יחד לסצנת המשחק
            NetworkManager.Singleton.SceneManager.LoadScene("SampleScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}

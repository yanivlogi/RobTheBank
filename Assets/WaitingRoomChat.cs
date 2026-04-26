using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WaitingRoomChat : MonoBehaviour
{
    public static WaitingRoomChat Instance { get; private set; }

    [Header("Chat UI")]
    public ScrollRect chatScrollRect;
    public TMP_Text chatLogText;
    public TMP_InputField chatInput;
    public Button sendButton;

    private string localPlayerName = "Player";

    void Awake()
    {
        Instance = this;

        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendClicked);
    }

    public void SetPlayerName(string name)
    {
        localPlayerName = string.IsNullOrWhiteSpace(name) ? "Player" : name;
    }

    public void OnSendClicked()
    {
        if (chatInput == null || string.IsNullOrWhiteSpace(chatInput.text)) return;

        string msg = chatInput.text.Trim();
        chatInput.text = "";
        chatInput.ActivateInputField();

        if (NetworkChatRelay.Instance != null)
            NetworkChatRelay.Instance.SendChatServerRpc(localPlayerName, msg);
        else
            ReceiveMessage(localPlayerName, msg);
    }

    public void ReceiveMessage(string playerName, string message)
    {
        if (chatLogText == null) return;

        chatLogText.text += (chatLogText.text.Length > 0 ? "\n" : "") + $"<b>{playerName}:</b> {message}";

        if (chatScrollRect != null && chatScrollRect.content != null)
            StartCoroutine(ScrollToBottom());
    }

    private IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        chatScrollRect.verticalNormalizedPosition = 0f;
    }
}

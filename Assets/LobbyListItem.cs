using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Lobbies.Models;

public class LobbyListItem : MonoBehaviour
{
    public TMP_Text lobbyNameText;
    public TMP_Text playersText;
    public TMP_Text settingsText;
    public Button joinButton;

    private Lobby currentLobby;
    private LobbyController lobbyController;

    public void Setup(Lobby lobby, LobbyController controller)
    {
        currentLobby = lobby;
        lobbyController = controller;

        string roomName = lobby.Name;
        string players = $"Players: {lobby.Players.Count}/{lobby.MaxPlayers}";

        string maxPoints = lobby.Data != null && lobby.Data.ContainsKey("MaxPoints")
            ? lobby.Data["MaxPoints"].Value
            : "-";

        string turnTime = lobby.Data != null && lobby.Data.ContainsKey("TurnTime")
            ? lobby.Data["TurnTime"].Value
            : "-";

        string allowTrade = lobby.Data != null && lobby.Data.ContainsKey("AllowTrade")
            ? lobby.Data["AllowTrade"].Value
            : "-";

        string settings = $"{maxPoints} Points | {turnTime} Sec | Trade: {allowTrade}";

        if (lobbyNameText != null)
            lobbyNameText.text = roomName;

        if (playersText != null)
            playersText.text = players;

        if (settingsText != null)
            settingsText.text = settings;

        Debug.Log($"UI Item -> {roomName} | {players} | {settings}");

        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() =>
            {
                _ = lobbyController.JoinLobbyById(currentLobby.Id);
            });
        }
    }
}
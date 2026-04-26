using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public class LobbyController : MonoBehaviour
{
[Header("Player Settings")]
public TMP_InputField playerNameInput;
public Button saveNameButton;

    [Header("Join UI")]
    public Transform lobbyListContent;
    public GameObject lobbyItemPrefab;

    [Header("Waiting Room")]
    public GameObject waitingRoomPanel;
    public TMP_Text waitingRoomNameText;
    public TMP_Text waitingRoomStatusText;
    public Transform playersListContent;
    public GameObject playerItemPrefab;
    public Button startGameButton;
    public Button leaveRoomButton;

    [Header("Waiting Room - Settings Display")]
    public TMP_Text settingsDisplayText;

    [Header("Waiting Room - Chat")]
    public WaitingRoomChat waitingRoomChat;

    [Header("Panels")]
    public GameObject hostSettingsPanel;
    public GameObject joinPanel;
    public GameObject HostPanel;

    [Header("Error Feedback")]
    public TMP_Text errorText;

    [Header("Settings Fields - Game Rules")]
    public Slider pointsSlider;
    public TMP_Text pointsValueText;
    public Slider turnTimeSlider;
    public TMP_Text turnTimeValueText;
    public Toggle friendlyRobberToggle;
    public Toggle allowTradeToggle;

    [Header("Settings Fields - Lobby Settings")]
    public TMP_InputField roomNameInput;
    public Toggle privateRoomToggle;
    public TMP_InputField roomPasswordInput;
    public Slider maxPlayersSlider;
    public TMP_Text maxPlayersValueText;

    private Lobby joinedLobby;
    private float heartbeatTimer = 0f;
    private float lobbyPollTimer = 0f;
    private List<Lobby> availableLobbies = new List<Lobby>();
    private bool servicesReady = false;

    async void Start()
    {
        HideAllPanelsAtStart();
        if (pointsSlider != null)
        {
            if (pointsValueText != null)
                pointsValueText.text = Mathf.RoundToInt(pointsSlider.value).ToString();
            pointsSlider.onValueChanged.AddListener(v =>
            {
                if (pointsValueText != null)
                    pointsValueText.text = Mathf.RoundToInt(v).ToString();
            });
        }

        if (turnTimeSlider != null)
        {
            float snapped = Mathf.Round(turnTimeSlider.value / 15f) * 15f;
            if (turnTimeValueText != null)
                turnTimeValueText.text = Mathf.RoundToInt(snapped) + "s";
            turnTimeSlider.onValueChanged.AddListener(v =>
            {
                float s = Mathf.Round(v / 15f) * 15f;
                turnTimeSlider.SetValueWithoutNotify(s);
                if (turnTimeValueText != null)
                    turnTimeValueText.text = Mathf.RoundToInt(s) + "s";
            });
        }

        if (maxPlayersSlider != null)
        {
            if (maxPlayersValueText != null)
                maxPlayersValueText.text = Mathf.RoundToInt(maxPlayersSlider.value).ToString();
            maxPlayersSlider.onValueChanged.AddListener(v =>
            {
                int val = Mathf.RoundToInt(v);
                maxPlayersSlider.SetValueWithoutNotify(val);
                if (maxPlayersValueText != null)
                    maxPlayersValueText.text = val.ToString();
            });
        }

        await InitializeUnityServices();
    }

    void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyPolling();
    }

    private void HideAllPanelsAtStart()
    {
        if (hostSettingsPanel != null)
            hostSettingsPanel.SetActive(false);

        if (joinPanel != null)
            joinPanel.SetActive(false);

        if (waitingRoomPanel != null)
            waitingRoomPanel.SetActive(false);
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            Debug.Log("מתחיל חיבור ל-Unity Services...");

            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            servicesReady = true;
            Debug.Log("יוניטי תתחבר בהצלחה");
            Debug.Log("Player ID: " + AuthenticationService.Instance.PlayerId);
        }
        catch (System.Exception e)
        {
            servicesReady = false;
            Debug.LogError("שגיאה בחיבור לשירותים: " + e);
        }
    }

    public void OpenHostSettings()
    {
        if (!servicesReady)
        {
            Debug.LogWarning("השירותים עדיין לא מוכנים.");
            return;
        }

        if (hostSettingsPanel != null)
            hostSettingsPanel.SetActive(true);

        if (joinPanel != null)
            joinPanel.SetActive(false);

        if (waitingRoomPanel != null)
            waitingRoomPanel.SetActive(false);
    }
    private void ShowError(string msg)
    {
        Debug.LogWarning(msg);
        if (errorText != null)
        {
            errorText.text = msg;
            errorText.gameObject.SetActive(true);
        }
    }

    private void ClearError()
    {
        if (errorText != null)
            errorText.gameObject.SetActive(false);
    }

    private string GetPlayerName()
{
    if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
        return playerNameInput.text.Trim();

    string saved = PlayerPrefs.GetString("SavedPlayerName", "");
    if (!string.IsNullOrWhiteSpace(saved))
        return saved;

    return "Player";
}


    public async void OpenJoinPanel()
    {
        if (!servicesReady)
        {
            Debug.LogWarning("השירותים עדיין לא מוכנים.");
            return;
        }

        if (joinPanel != null)
            joinPanel.SetActive(true);

        if (hostSettingsPanel != null)
            hostSettingsPanel.SetActive(false);

        if (waitingRoomPanel != null)
            waitingRoomPanel.SetActive(false);

        Debug.Log("Join panel opened");
        await ListLobbiesAsync();
    }

    public void CloseJoinPanel()
    {
        if (joinPanel != null)
            joinPanel.SetActive(false);
    }
    public void CloseHostPanel()
    {
        if (HostPanel != null)
            HostPanel.SetActive(false);
    }

    public void CloseWaitingRoomPanel()
    {
        LeaveRoom();
    }

   public async void FinalCreateGame()
{
    Debug.Log("=== FinalCreateGame נקרא ===");
    ClearError();

    if (!servicesReady)
    {
        ShowError("Services not ready. Check internet connection.");
        return;
    }

    if (NetworkManager.Singleton != null &&
        (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
    {
        await LeaveRoomAsync();
    }

    try
    {
        string maxPoints = pointsSlider != null
            ? Mathf.RoundToInt(pointsSlider.value).ToString()
            : "10";

        string turnTime = turnTimeSlider != null
            ? (Mathf.RoundToInt(turnTimeSlider.value / 15f) * 15).ToString()
            : "60";

        string allowTrade = allowTradeToggle != null
            ? allowTradeToggle.isOn.ToString()
            : "true";

        string friendlyRobber = friendlyRobberToggle != null
            ? friendlyRobberToggle.isOn.ToString()
            : "false";

        string roomName = (roomNameInput != null && !string.IsNullOrWhiteSpace(roomNameInput.text))
            ? roomNameInput.text.Trim()
            : "Catan Room";

        bool isPrivate = privateRoomToggle != null && privateRoomToggle.isOn;

        string password = (roomPasswordInput != null && !string.IsNullOrWhiteSpace(roomPasswordInput.text))
            ? roomPasswordInput.text.Trim()
            : "";

        int maxPlayers = maxPlayersSlider != null
            ? Mathf.Max(2, Mathf.RoundToInt(maxPlayersSlider.value))
            : 4;

        string playerName = GetPlayerName();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        CreateLobbyOptions options = new CreateLobbyOptions
        {
            IsPrivate = isPrivate,

            Player = new Player
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
                }
            },

            Data = new Dictionary<string, DataObject>
            {
                { "JoinCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) },
                { "MaxPoints", new DataObject(DataObject.VisibilityOptions.Public, maxPoints) },
                { "TurnTime", new DataObject(DataObject.VisibilityOptions.Public, turnTime) },
                { "AllowTrade", new DataObject(DataObject.VisibilityOptions.Public, allowTrade) },
                { "FriendlyRobber", new DataObject(DataObject.VisibilityOptions.Public, friendlyRobber) },
                { "Password", new DataObject(DataObject.VisibilityOptions.Member, password) }
            }
        };

        joinedLobby = await LobbyService.Instance.CreateLobbyAsync(roomName, maxPlayers, options);

        Debug.Log("לובי נוצר בהצלחה!");
        Debug.Log("Lobby ID: " + joinedLobby.Id);
        Debug.Log("Relay Join Code: " + joinCode);
        Debug.Log("Host Player Name: " + playerName);

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton הוא null");
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("UnityTransport לא נמצא על NetworkManager");
            return;
        }

        transport.SetHostRelayData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData
        );

        bool hostStarted = NetworkManager.Singleton.StartHost();
        Debug.Log("StartHost result: " + hostStarted);

        if (!hostStarted)
        {
            Debug.LogError("StartHost נכשל");
            return;
        }

        heartbeatTimer = 15f;
        lobbyPollTimer = 1f;

        if (hostSettingsPanel != null)
            hostSettingsPanel.SetActive(false);

        if (joinPanel != null)
            joinPanel.SetActive(false);

        if (waitingRoomPanel != null)
            waitingRoomPanel.SetActive(true);

        if (waitingRoomNameText != null)
            waitingRoomNameText.text = joinedLobby.Name;

        if (waitingRoomStatusText != null)
            waitingRoomStatusText.text = "Waiting for players...";

        if (startGameButton != null)
            startGameButton.gameObject.SetActive(true);

        UpdateSettingsDisplay();
        WaitingRoomChat.Instance?.SetPlayerName(playerName);
        RefreshPlayers();

        Debug.Log("המשחק באוויר, מחכה לשחקנים...");
    }
    catch (System.Exception e)
    {
        ShowError("Failed to create room: " + e.Message);
        Debug.LogError("שגיאה ביצירת משחק: " + e);
    }
}

    public async void ListLobbies()
    {
        await ListLobbiesAsync();
    }

    private void ClearLobbyListUI()
    {
        if (lobbyListContent == null)
            return;

        for (int i = lobbyListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(lobbyListContent.GetChild(i).gameObject);
        }
    }

    private async Task ListLobbiesAsync()
    {
        if (!servicesReady)
        {
            Debug.LogWarning("השירותים עדיין לא מוכנים. אי אפשר למשוך רשימת חדרים.");
            return;
        }

        if (lobbyListContent == null)
        {
            Debug.LogError("lobbyListContent לא מחובר ב-Inspector");
            return;
        }

        if (lobbyItemPrefab == null)
        {
            Debug.LogError("lobbyItemPrefab לא מחובר ב-Inspector");
            return;
        }

        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 25
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            availableLobbies = response.Results;

            Debug.Log("נמצאו " + availableLobbies.Count + " חדרים.");

            ClearLobbyListUI();

            foreach (Lobby lobby in availableLobbies)
            {
                Debug.Log("יוצר UI עבור חדר: " + lobby.Name);

                GameObject itemObj = Instantiate(lobbyItemPrefab, lobbyListContent);
                itemObj.name = "LobbyItem_" + lobby.Name;

                LobbyListItem item = itemObj.GetComponent<LobbyListItem>();

                if (item == null)
                {
                    Debug.LogError("חסר LobbyListItem על ה-Prefab");
                    continue;
                }

                item.Setup(lobby, this);
                Debug.Log("נוצר item עבור: " + lobby.Name);
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("שגיאה בשליפת לובים: " + e);
        }
        catch (System.Exception e)
        {
            Debug.LogError("שגיאה כללית בשליפת לובים: " + e);
        }
    }

    public async void JoinFirstLobby()
    {
        if (!servicesReady)
        {
            Debug.LogWarning("השירותים עדיין לא מוכנים. אי אפשר להצטרף.");
            return;
        }

        if (availableLobbies == null || availableLobbies.Count == 0)
        {
            await ListLobbiesAsync();
        }

        if (availableLobbies == null || availableLobbies.Count == 0)
        {
            Debug.LogWarning("אין לובים זמינים.");
            return;
        }

        await JoinLobbyById(availableLobbies[0].Id);
    }

   public async Task JoinLobbyById(string lobbyId)
{
    if (!servicesReady)
    {
        Debug.LogWarning("השירותים עדיין לא מוכנים.");
        return;
    }

    if (NetworkManager.Singleton != null &&
        (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost))
    {
        Debug.LogWarning("כבר מחובר לרשת — יוצאים לפני הצטרפות לחדר.");
        await LeaveRoomAsync();
    }

    try
    {
        string playerName = GetPlayerName();

        JoinLobbyByIdOptions joinOptions = new JoinLobbyByIdOptions
        {
            Player = new Player
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
                }
            }
        };

        joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, joinOptions);

        if (joinedLobby == null)
        {
            Debug.LogError("JoinLobbyByIdAsync החזיר null");
            return;
        }

        if (joinedLobby.Data == null || !joinedLobby.Data.ContainsKey("JoinCode"))
        {
            Debug.LogError("לא נמצא JoinCode בלובי");
            return;
        }

        string joinCode = joinedLobby.Data["JoinCode"].Value;
        Debug.Log("Joined with player name: " + playerName);

        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton הוא null");
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("UnityTransport לא נמצא על NetworkManager");
            return;
        }

        transport.SetClientRelayData(
            joinAllocation.RelayServer.IpV4,
            (ushort)joinAllocation.RelayServer.Port,
            joinAllocation.AllocationIdBytes,
            joinAllocation.Key,
            joinAllocation.ConnectionData,
            joinAllocation.HostConnectionData
        );

        bool clientStarted = NetworkManager.Singleton.StartClient();
        Debug.Log("StartClient result: " + clientStarted);

        if (!clientStarted)
        {
            Debug.LogError("StartClient נכשל");
            return;
        }

        lobbyPollTimer = 1f;

        if (hostSettingsPanel != null)
            hostSettingsPanel.SetActive(false);

        if (joinPanel != null)
            joinPanel.SetActive(false);

        if (waitingRoomPanel != null)
            waitingRoomPanel.SetActive(true);

        if (waitingRoomNameText != null)
            waitingRoomNameText.text = joinedLobby.Name;

        if (waitingRoomStatusText != null)
            waitingRoomStatusText.text = "Joined room";

        if (startGameButton != null)
            startGameButton.gameObject.SetActive(false);

        UpdateSettingsDisplay();
        WaitingRoomChat.Instance?.SetPlayerName(playerName);
        RefreshPlayers();
    }
    catch (System.Exception e)
    {
        Debug.LogError("שגיאה בהצטרפות לחדר: " + e);
    }
}

 private void UpdateSettingsDisplay()
{
    if (settingsDisplayText == null || joinedLobby == null) return;

    var d = joinedLobby.Data;
    string Get(string key) => (d != null && d.ContainsKey(key)) ? d[key].Value : "?";

    string friendlyRobber = Get("FriendlyRobber") == "True" ? "On" : "Off";
    string allowTrade     = Get("AllowTrade")     == "True" ? "On" : "Off";

    settingsDisplayText.text =
        $"Points to Win: {Get("MaxPoints")}\n" +
        $"Turn Time: {Get("TurnTime")}s\n" +
        $"Friendly Robber: {friendlyRobber}\n" +
        $"Enable Trade: {allowTrade}";
}

 private void RefreshPlayers()
{
    if (playersListContent == null || playerItemPrefab == null || joinedLobby == null)
        return;

    for (int i = playersListContent.childCount - 1; i >= 0; i--)
    {
        Destroy(playersListContent.GetChild(i).gameObject);
    }

    if (joinedLobby.Players == null)
        return;

    string myPlayerId = AuthenticationService.Instance.PlayerId;

    foreach (var player in joinedLobby.Players)
    {
        GameObject obj = Instantiate(playerItemPrefab, playersListContent);
        PlayerListItem item = obj.GetComponent<PlayerListItem>();

        string playerName = "Player";

        if (player.Data != null &&
            player.Data.ContainsKey("PlayerName") &&
            !string.IsNullOrWhiteSpace(player.Data["PlayerName"].Value))
        {
            playerName = player.Data["PlayerName"].Value;
        }

        if (player.Id == myPlayerId)
        {
            playerName += " (Me)";
        }

        if (item != null)
            item.Setup(playerName);
    }
}

    public async void LeaveRoom()
    {
        await LeaveRoomAsync();
    }

    public async Task LeaveRoomAsync()
    {
        try
        {
            if (joinedLobby != null)
            {
                await LobbyService.Instance.RemovePlayerAsync(
                    joinedLobby.Id,
                    AuthenticationService.Instance.PlayerId
                );
            }

            joinedLobby = null;
            availableLobbies.Clear();

            if (NetworkManager.Singleton != null &&
                (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
            {
                NetworkManager.Singleton.Shutdown();
            }

            if (waitingRoomPanel != null)
                waitingRoomPanel.SetActive(false);

            if (hostSettingsPanel != null)
                hostSettingsPanel.SetActive(false);

            if (joinPanel != null)
                joinPanel.SetActive(false);

            ClearLobbyListUI();

            if (playersListContent != null)
            {
                for (int i = playersListContent.childCount - 1; i >= 0; i--)
                {
                    Destroy(playersListContent.GetChild(i).gameObject);
                }
            }

            Debug.Log("יצאת מהחדר");
        }
        catch (System.Exception e)
        {
            Debug.LogError("שגיאה ביציאה מהחדר: " + e.Message);
        }
    }

    public async void ExitGame()
    {
        Debug.Log("=== ExitGame נקרא ===");
        try
        {
            if (joinedLobby != null)
            {
                string myId = AuthenticationService.Instance.PlayerId;
                if (joinedLobby.HostId == myId)
                    await LobbyService.Instance.DeleteLobbyAsync(joinedLobby.Id);
                else
                    await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, myId);
                joinedLobby = null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Lobby cleanup error: " + e.Message);
        }

        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
            NetworkManager.Singleton.Shutdown();

        GameSettings.Reset();
        hostSettingsPanel?.SetActive(false);
        joinPanel?.SetActive(false);
        waitingRoomPanel?.SetActive(false);
        ClearError();

        if (SceneManager.GetActiveScene().name != "Online Lobby")
            SceneManager.LoadScene("Online Lobby");
    }

    public void StartGameFromWaitingRoom()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("רק Host יכול להתחיל משחק");
            return;
        }

        if (joinedLobby == null)
        {
            Debug.LogWarning("אין לובי פעיל");
            return;
        }

        var d = joinedLobby.Data;
        string Get(string key) => (d != null && d.ContainsKey(key)) ? d[key].Value : "";

        int.TryParse(Get("MaxPoints"), out GameSettings.PointsToWin);
        int.TryParse(Get("TurnTime"),  out GameSettings.TurnTimeSeconds);
        bool.TryParse(Get("FriendlyRobber"), out GameSettings.FriendlyRobber);
        bool.TryParse(Get("AllowTrade"),     out GameSettings.AllowTrade);
        GameSettings.MaxPlayers   = joinedLobby.Players.Count;
        GameSettings.RoomName     = joinedLobby.Name;
        GameSettings.HostPlayerName = GetPlayerName();

        Debug.Log($"Starting game — Points:{GameSettings.PointsToWin} TurnTime:{GameSettings.TurnTimeSeconds}s " +
                  $"FriendlyRobber:{GameSettings.FriendlyRobber} AllowTrade:{GameSettings.AllowTrade}");

        NetworkManager.Singleton.SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
    }

    private async void HandleLobbyPolling()
    {
        if (joinedLobby == null)
            return;

        lobbyPollTimer -= Time.deltaTime;

        if (lobbyPollTimer > 0f)
            return;

        lobbyPollTimer = 3f;

        try
        {
            joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
            RefreshPlayers();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning("שגיאה ברענון הלובי: " + e.Message);
        }
        catch (System.Exception e)
        {
            Debug.LogError("שגיאה כללית ברענון הלובי: " + e.Message);
        }
    }

    private async void HandleLobbyHeartbeat()
    {
        if (joinedLobby == null)
            return;

        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsHost)
            return;

        heartbeatTimer -= Time.deltaTime;

        if (heartbeatTimer > 0f)
            return;

        heartbeatTimer = 15f;

        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
            Debug.Log("Heartbeat נשלח ללובי");
        }
        catch (System.Exception e)
        {
            Debug.LogError("שגיאה בשליחת Heartbeat: " + e.Message);
        }
    }
}
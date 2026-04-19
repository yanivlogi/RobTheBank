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

    [Header("Panels")]
    public GameObject hostSettingsPanel;
    public GameObject joinPanel;
    public GameObject HostPanel;

    [Header("Settings Fields")]
    public TMP_InputField pointsInput;
    public TMP_InputField turnTimeInput;
    public Toggle allowTradeToggle;

    private Lobby joinedLobby;
    private float heartbeatTimer = 0f;
    private float lobbyPollTimer = 0f;
    private List<Lobby> availableLobbies = new List<Lobby>();
    private bool servicesReady = false;

    async void Start()
    {
        HideAllPanelsAtStart();
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
    private string GetPlayerName()
{
    if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
        return playerNameInput.text.Trim();

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
    if (!servicesReady)
    {
        Debug.LogWarning("השירותים עדיין לא מוכנים.");
        return;
    }

    if (NetworkManager.Singleton != null &&
        (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
    {
        Debug.LogWarning("כבר מחובר לרשת — יוצאים מהחדר לפני יצירת חדר חדש.");
        await LeaveRoomAsync();
    }

    try
    {
        string maxPoints = pointsInput != null && !string.IsNullOrWhiteSpace(pointsInput.text)
            ? pointsInput.text
            : "10";

        string turnTime = turnTimeInput != null && !string.IsNullOrWhiteSpace(turnTimeInput.text)
            ? turnTimeInput.text
            : "60";

        string allowTrade = allowTradeToggle != null
            ? allowTradeToggle.isOn.ToString()
            : "true";

        string playerName = GetPlayerName();

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        CreateLobbyOptions options = new CreateLobbyOptions
        {
            IsPrivate = false,

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
                { "AllowTrade", new DataObject(DataObject.VisibilityOptions.Public, allowTrade) }
            }
        };

        joinedLobby = await LobbyService.Instance.CreateLobbyAsync("Catan Room", 4, options);

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

        RefreshPlayers();

        Debug.Log("המשחק באוויר, מחכה לשחקנים...");
    }
    catch (System.Exception e)
    {
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

        RefreshPlayers();
    }
    catch (System.Exception e)
    {
        Debug.LogError("שגיאה בהצטרפות לחדר: " + e);
    }
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

    public void StartGameFromWaitingRoom()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("רק Host יכול להתחיל משחק");
            return;
        }

        Debug.Log("Start Game clicked");
        // בהמשך:
        // NetworkManager.Singleton.SceneManager.LoadScene("Main Game", UnityEngine.SceneManagement.LoadSceneMode.Single);
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
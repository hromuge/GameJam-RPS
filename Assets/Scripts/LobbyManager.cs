using UnityEngine;
using Unity.Services.Core;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    private Lobby _hostLobby;
    private Lobby _joinedLobby;
    private float _heartbeatTimer;
    private float _lobbyUpdateTimer;
    private string _playerName;
    private bool _gameStartedLocal; // Merkt sich, ob wir schon ins GamePanel gewechselt haben

    [Header("Network Settings")]
    [SerializeField] private float heartbeatInterval = 15f;
    [SerializeField] private float lobbyUpdateInterval = 1.5f;

    [Header("UI References")]
    public TMP_InputField lobbyCodeInput;
    public TMP_InputField playerNameInput;
    public TextMeshProUGUI hostPlayerLabel;
    public TextMeshProUGUI clientPlayerLabel;
    public TextMeshProUGUI lobbyCodeLabel;
    public Button startButton;
    public Button copyToClipboardButton;
    
    public GameObject lobbyPanel;
    public GameObject gamePanel;

    private async void Start()
    {
        gamePanel.SetActive(false);
        
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"Sign in anonymously succeeded! PlayerID is: {AuthenticationService.Instance.PlayerId}");
            
            if (PlayerPrefs.HasKey("PlayerName"))
            {
                _playerName = PlayerPrefs.GetString("PlayerName");
                playerNameInput.text = _playerName;
            }
            else
            {
                _playerName = "Player" + UnityEngine.Random.Range(1000, 5000);
                playerNameInput.text = _playerName;
                PlayerPrefs.SetString("PlayerName", _playerName);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyUpdate();
    }

    private async void HandleLobbyHeartbeat()
    {
        if (_hostLobby == null) return;
        
        _heartbeatTimer -= Time.deltaTime;
        if (_heartbeatTimer <= 0)
        {
            _heartbeatTimer = heartbeatInterval;
            await LobbyService.Instance.SendHeartbeatPingAsync(_hostLobby.Id);
        }
    }

    public async void CreateLobby()
    {
        try
        {
            string lobbyName = "My Lobby";
            int maxPlayers = 2;
            string playerName = playerNameInput.text != "" ? playerNameInput.text : PlayerPrefs.GetString("PlayerName");
            
            PlayerPrefs.SetString("PlayerName", playerName);
            hostPlayerLabel.text = playerName + " (You)";

            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = true,
                Player = CreatePlayer(playerName),
                Data = new Dictionary<string, DataObject>
                {
                    {"GameStarted", new DataObject(DataObject.VisibilityOptions.Public, "False")},
                    {"RoundOver", new DataObject(DataObject.VisibilityOptions.Public, "False")},
                    {"Phase", new DataObject(DataObject.VisibilityOptions.Public, "None")} // Dummy Startwert
                }
            };
            
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);
            
            _hostLobby = lobby;
            _joinedLobby = lobby;
            
            Debug.Log($"Lobby created: {lobby.Name} {lobby.LobbyCode}");
            lobbyCodeLabel.text = lobby.LobbyCode;
            copyToClipboardButton.gameObject.SetActive(true);
            lobbyCodeLabel.gameObject.SetActive(true);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogException(e);
        }
    }

    public async void JoinLobby()
    {
        try
        {
            string lobbyCode = Regex.Replace(lobbyCodeInput.text, @"[^\w]", "");
            string playerName = playerNameInput.text != "" ? playerNameInput.text : PlayerPrefs.GetString("PlayerName");
            
            PlayerPrefs.SetString("PlayerName", playerName);
            clientPlayerLabel.text = playerName;
            
            JoinLobbyByCodeOptions joinLobbyByCodeOptions = new JoinLobbyByCodeOptions
            {
                Player = CreatePlayer(playerName)
            };
        
            Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode,  joinLobbyByCodeOptions);
            _joinedLobby = lobby;

            if (lobby != null)
            {
                Debug.Log($"Lobby joined: {lobbyCode}");
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private Player CreatePlayer(string playerName)
    {
        return new Player(
            id: AuthenticationService.Instance.PlayerId,
            data: new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) },
                { "IsWinner", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "False") }
            });
    }

    private async void HandleLobbyUpdate()
    {
        if (_joinedLobby == null) return;

        try
        {
            _lobbyUpdateTimer -= Time.deltaTime;
            if (_lobbyUpdateTimer <= 0f)
            {
                _lobbyUpdateTimer = lobbyUpdateInterval;
                _joinedLobby = await LobbyService.Instance.GetLobbyAsync(_joinedLobby.Id);
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
        
        GameManager.Instance.SetLobby(_joinedLobby);
        UpdateLobbyUI(); // Heißt jetzt UpdateLobbyUI, da es nur noch die Lobby macht
    }

    private void UpdateLobbyUI()
    {
        if (_joinedLobby == null) return;
        
        // Spieler Namen aktualisieren
        foreach (Player player in _joinedLobby.Players)
        {
            string playerName = player.Data["PlayerName"].Value;
            bool isLocal = player.Id == AuthenticationService.Instance.PlayerId;
            string displayName = isLocal ? playerName + " (You)" : playerName;

            if (isLocal) hostPlayerLabel.text = displayName;
            else clientPlayerLabel.text = displayName;
        }

        // Start Button nur für Host
        if (_joinedLobby.Players.Count == 2)
        {
            startButton.interactable = IsHost();
        }

        // --- HIER IST DIE TRENNUNG ---
        // Der LobbyManager schaut nur: Geht das Spiel los? Wenn ja, Panel umschalten.
        if (_joinedLobby.Data != null)
        {
            if (!_gameStartedLocal && _joinedLobby.Data.TryGetValue("GameStarted", out var started) && started.Value == "True")
            {
                _gameStartedLocal = true;
                lobbyPanel.SetActive(false);
                gamePanel.SetActive(true);
            }
        }
    }

    private bool IsHost()
    {
        if (_joinedLobby == null) return false;
        return _joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
    }
}
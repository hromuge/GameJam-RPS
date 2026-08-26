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
    private bool _isLobbyFull;

    [SerializeField]
    private float heartbeatInterval = 15;
    
    [SerializeField]
    private float lobbyUpdateInterval = 1.1f;

    public TMP_InputField lobbyCodeInput;
    public TMP_InputField playerNameInput;
    
    public TextMeshProUGUI hostPlayerLabel;
    public TextMeshProUGUI clientPlayerLabel;
    public TextMeshProUGUI lobbyCodeLabel;

    public Button startButton;
    

    private async void Start()
    {
        if (PlayerPrefs.GetString("PlayerName") != null)
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
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"Sign in anonymously succeeded! PlayerID is: {AuthenticationService.Instance.PlayerId}");
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
            string playerName;

            if (playerNameInput.text != "")
            {
                playerName = playerNameInput.text;
            }
            else
            {
                playerName = PlayerPrefs.GetString("PlayerName");
            }
            PlayerPrefs.SetString("PlayerName", playerName);

            hostPlayerLabel.text = playerName + " (You)";

            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = true,
                Player = CreatePlayer(playerName)
            };
            
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);
            
            _hostLobby = lobby;
            _joinedLobby = lobby;
            
            Debug.Log($"Lobby created: {lobby.Name} {lobby.LobbyCode}");
            
            lobbyCodeLabel.text = "Code: " + lobby.LobbyCode;
            
            PrintPlayers(_hostLobby);
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
            string playerName;

            playerName = playerNameInput.text != "" ? playerNameInput.text : PlayerPrefs.GetString("PlayerName");
            
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
                PrintPlayers(lobby);
                lobbyCodeLabel.text = "Code: " + lobby.LobbyCode;
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
            });
    }

    private void PrintPlayers(Lobby lobby)
    {
        foreach (Player player in lobby.Players)
        {
            Debug.Log(player.Data["PlayerName"].Value);
        }
    }

    private async void HandleLobbyUpdate()
    {
        if (_joinedLobby == null) return;
        
        _lobbyUpdateTimer -= Time.deltaTime;
        if (_lobbyUpdateTimer <= 0f)
        {
            _lobbyUpdateTimer = lobbyUpdateInterval;
            Lobby updatedLobby = await LobbyService.Instance.GetLobbyAsync(_joinedLobby.Id);
        
            _joinedLobby = updatedLobby;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_joinedLobby == null) return;
        
        foreach (Player player in _joinedLobby.Players)
        {
            string playerName = player.Data["PlayerName"].Value;
            bool isLocal = player.Id == AuthenticationService.Instance.PlayerId;
            string displayName = isLocal ? playerName + " (You)" : playerName;

            if (isLocal)
            {
                hostPlayerLabel.text = displayName;
            }
            else
            {
                clientPlayerLabel.text = displayName;
            }
            
        }

        if (_joinedLobby.Players.Count == 2)
        {
            startButton.interactable = true;
            startButton.gameObject.SetActive(IsHost()); 
        }
    }

    private bool IsHost()
    {
        if (_joinedLobby == null) return false;

        return _joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

}
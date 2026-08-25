using UnityEngine;
using Unity.Services.Core;
using System;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine.Serialization;

public class LobbyManager : MonoBehaviour
{

    private Lobby _hostLobby;
    private float _heartbeatTimer;
    [SerializeField]
    private float heartbeatInterval = 15;

    public TextMeshProUGUI lobbyCodeInput;
    
    private async void Start()
    {
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
            
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers);
            
            _hostLobby = lobby;
            
            Debug.Log($"Lobby created: {lobby.Name} {lobby.LobbyCode}");
            
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
            string lobbyCode = lobbyCodeInput.text;
        
            await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
        
            Debug.Log($"Lobby joined: {lobbyCode}");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            throw;
        }
    }

}
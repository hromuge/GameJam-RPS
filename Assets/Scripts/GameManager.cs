using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    
    public static GameManager Instance { get; private set; }
    private Lobby _currentLobby;
    private int _currentPhase;
    private bool _gameStarted;
    private bool _roundOver;

    [SerializeField]
    private float animationSpeed = 1.0f;
    
    
    
    [SerializeField] private Sprite cannonSprite;
    [SerializeField] private Sprite cutlassSprite;
    [SerializeField] private Sprite sailSprite;

    [SerializeField] private UnityEngine.UI.Image playerChoiceDisplayImage;
    [SerializeField] private UnityEngine.UI.Image enemyChoiceDisplayImage;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Update()
    {
        switch (_currentPhase)
        {
            case 0:
                if (_gameStarted)
                {
                    CheckForPlayerChoices();
                }
                break;
                
            default:
                break;
        }
    }

    public void SetLobby(Lobby lobby)
    {
        _currentLobby = lobby;
        if (!_gameStarted && _currentLobby.Data.ContainsKey("GameStarted") && _currentLobby.Data["GameStarted"].Value == "True")
        {
            _gameStarted = true;
        }
    }

    public async void StartGame()
    {
        UpdateLobbyOptions updateOptions = new UpdateLobbyOptions
        {
            Data = new Dictionary<string, DataObject>
            {
                { "GameStarted", new DataObject(DataObject.VisibilityOptions.Public, "True") },
                { "RoundOver", new DataObject(DataObject.VisibilityOptions.Public, "False") }
            }
        };
        await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, updateOptions);
    }
    

    private void ShowStampsAnimation()
    {
        
    }

    public void OnChoiceSelected(string choice)
    {
        string localPlayerId = AuthenticationService.Instance.PlayerId;
        UpdatePlayerChoice(localPlayerId, choice);
    }

    private async void UpdatePlayerChoice(string playerId, string choice)
    {
        try
        {
            await LobbyService.Instance.UpdatePlayerAsync(_currentLobby.Id, playerId, new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    {"Choice", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, choice) }
                }
            });

            Debug.Log($"Player {playerId} has choosen {choice}");
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    private void CheckForPlayerChoices()
    {
        Dictionary<string, string> choices = new Dictionary<string, string>();

        foreach (Player player in _currentLobby.Players)
        {
            if (player.Data.TryGetValue("Choice", out var choice))
            {
                choices[player.Id] = choice.Value;
            }
        }

        if (choices.Count == 2)
        {
            ShowChoices();
            EvaluateWinner(_currentLobby.Players);
            _currentPhase++;
            choices.Clear();
        }
    }

    private async void EvaluateWinner(List<Player> players)
    {
        string player1Name = players[0].Data["PlayerName"].Value;
        string player2Name = players[1].Data["PlayerName"].Value;
        string player1Choice = players[0].Data["Choice"].Value;
        string player2Choice = players[1].Data["Choice"].Value;
        bool isDraw = false;
        
        string currentPlayer = AuthenticationService.Instance.PlayerId;


        if (player1Choice == player2Choice)
        {
            isDraw = true;
        }

        bool player1Won = false;
        
        if (player1Choice == "Cannon" && player2Choice == "Cutlass") player1Won = true;
        else if (player1Choice == "Cutlass" && player2Choice == "Sail") player1Won = true;
        else if (player1Choice == "Sail" && player2Choice == "Cannon") player1Won = true;
        
        
        try
        {
            string winnerPlayerId = "None";
            string winnerPlayerName = "Nobody";
            
            if (player1Won)
            {
                winnerPlayerId = players[0].Id;
                winnerPlayerName = player1Name;
            } else if (!isDraw)
            {
                winnerPlayerId = players[1].Id;
                winnerPlayerName = player2Name;
            }
            
            
            UpdateLobbyOptions updateLobbyOptions = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RoundOver", new DataObject(DataObject.VisibilityOptions.Public, "True") }
                }
            };

            if (winnerPlayerId == "None" && currentPlayer == _currentLobby.HostId)
            {
                updateLobbyOptions.Data.Add("IsDraw", new DataObject(DataObject.VisibilityOptions.Public, "True"));
                await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, updateLobbyOptions);
            }
            else if (winnerPlayerId == currentPlayer)
            {
                UpdatePlayerOptions updatePlayerOptions = new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "IsWinner", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "True") }
                    }
                };
                
                await LobbyService.Instance.UpdatePlayerAsync(_currentLobby.Id, winnerPlayerId, updatePlayerOptions);
            }
            
            _roundOver = true;
            Debug.Log("Round Over: " + winnerPlayerName + " won this round.");
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    private void ShowChoices()
    {
        foreach (Player player in _currentLobby.Players)
        {
            bool isLocal = player.Id == AuthenticationService.Instance.PlayerId;
            Sprite sprite = null;
            string playerChoice = player.Data.TryGetValue("Choice", out var choice) ? choice.Value : "";
            
            switch (playerChoice)
            {
                case "Cannon":
                    sprite = cannonSprite;
                    break;
                case "Cutlass":
                    sprite = cutlassSprite;
                    break;
                case "Sail":
                    sprite = sailSprite;
                    break;
            }
            
            if (isLocal)
            {
                playerChoiceDisplayImage.sprite = sprite;
            }
            else
            {
                enemyChoiceDisplayImage.sprite = sprite;
            }
        }
    }

}

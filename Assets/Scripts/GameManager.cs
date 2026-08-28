using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using DG.Tweening;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    private Lobby _currentLobby;
    private bool _isEvaluating;
    private string _lastPhaseName = "";
    private enum SubSelectionMode { None, Swap, Bribe }
    private SubSelectionMode _currentSubMode = SubSelectionMode.None;

    [Header("Tray Animation (DOTween)")]
    [SerializeField] private RectTransform selfTrayRect;
    [SerializeField] private Vector2 selfTrayHiddenPos = new Vector2(0, -500); // Startet unten aus dem Bild
    [SerializeField] private Vector2 selfTrayVisiblePos = new Vector2(0, 0);   // Endposition im Bild

    [SerializeField] private RectTransform enemyTrayRect;
    [SerializeField] private Vector2 enemyTrayHiddenPos = new Vector2(0, 500); // Startet oben aus dem Bild
    [SerializeField] private Vector2 enemyTrayVisiblePos = new Vector2(0, 0);

    [SerializeField] private float slideDuration = 1f; // Wie lange die Animation dauert
    
    
    [Header("In-Game UI Groups")]
    [SerializeField] private GameObject stampsGroup;
    [SerializeField] private GameObject selectionGroup;
    [SerializeField] private GameObject buttonsGroup;
    [SerializeField] private TextMeshProUGUI phaseTitleText;
    [SerializeField] private TextMeshProUGUI phaseInfoText;

    [Header("Result Sprites")]
    [SerializeField] private Sprite cannonSprite;
    [SerializeField] private Sprite cutlassSprite;
    [SerializeField] private Sprite sailSprite;
    [SerializeField] private UnityEngine.UI.Image playerChoiceDisplayImage;
    [SerializeField] private UnityEngine.UI.Image enemyChoiceDisplayImage;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    void Update()
    {
        if (_currentLobby == null || _currentLobby.Data == null) return;
        if (_currentLobby.Data.TryGetValue("GameStarted", out var gameStarted) && gameStarted.Value == "False") return;
        
        if (_currentLobby.Data.TryGetValue("Phase", out var currentPhase) && 
            _currentLobby.Data.TryGetValue("RoundOver", out var roundOver))
        {
            string phaseValue = currentPhase.Value;
            string roundOverValue = roundOver.Value;
            
            // Kombinierter Status, um genau zu wissen, wo wir stehen (z.B. "Deception_False")
            string stateKey = phaseValue + "_" + roundOverValue;

            // 1. Phasen- oder Runden-Wechsel erkennen
            if (_lastPhaseName != stateKey)
            {
                _lastPhaseName = stateKey;
                _isEvaluating = false; 
                _currentSubMode = SubSelectionMode.None;
                
                // UI für die neue Phase exakt 1x aufsetzen!
                TriggerPhaseSetup(phaseValue, roundOverValue);
            }

            // 2. Spiellogik prüfen (Nur während die Runde noch läuft)
            if (roundOverValue == "False")
            {
                switch (phaseValue)
                {
                    case "Command":
                        CheckForPlayerChoices();
                        break;
                    case "Deception":
                        CheckForDeceptionChoice();
                        break;
                }
            }
        }
    }
    
    private void TriggerPhaseSetup(string currentPhase, string isRoundOver)
    {
        Debug.Log("Setting up phase:" + currentPhase);
        // 1. EVALUATION PHASE (Runde ist vorbei)
        if (isRoundOver == "True")
        {
            stampsGroup.SetActive(false);
            buttonsGroup.SetActive(false);
            selectionGroup.SetActive(true);
            return;
        }

        // 2. COMMAND PHASE
        if (currentPhase == "Command")
        {
            stampsGroup.SetActive(true);
            selectionGroup.SetActive(false);
            buttonsGroup.SetActive(false);
            
            DisplayInfo(currentPhase, "Choose a command using the buttons\n\nEnemy command will be revealed at the end of this phase\n\n\n\nFlee counters fire\nEnter counters flee\nFire counters enter ");

            if (selfTrayRect != null)
            {
                selfTrayRect.anchoredPosition = selfTrayHiddenPos;
                selfTrayRect.DOAnchorPos(selfTrayVisiblePos, slideDuration).SetEase(Ease.Flash);
            }

            if (enemyTrayRect != null)
            {
                enemyTrayRect.anchoredPosition = enemyTrayHiddenPos;
                enemyTrayRect.DOAnchorPos(enemyTrayVisiblePos, slideDuration).SetEase(Ease.Flash);
            }
        }
        // 3. DECEPTION PHASE
        else if (currentPhase == "Deception")
        {
            stampsGroup.SetActive(false);
            DisplayInfo("Decieve", "Decieve the enemy with one of these actions:\n\nBribe:\nBribe the enemies crew to listen to your command instead\n\nBluff:\nChange your own command\n\nBrace:\nStick with your command and brace for the fight");
            selectionGroup.SetActive(true);
            ShowChoices("Choice");
            buttonsGroup.SetActive(true);
            
        }
    }

    private void DisplayInfo(string phase, string info)
    {
        phaseTitleText.text = phase;
        phaseInfoText.text = info;
        phaseTitleText.gameObject.SetActive(true);
        phaseInfoText.gameObject.SetActive(true);

        phaseTitleText.DOKill();
        phaseInfoText.DOKill();
        
        phaseTitleText.DOFade(1f, 0.2f);
        phaseInfoText.DOFade(1f, 0.2f);
    }

    public void SetLobby(Lobby lobby)
    {
        _currentLobby = lobby;
    }

    public async void StartGame()
    {
        UpdateLobbyOptions updateOptions = new UpdateLobbyOptions
        {
            Data = new Dictionary<string, DataObject>
            {
                { "GameStarted", new DataObject(DataObject.VisibilityOptions.Public, "True") },
                { "RoundOver", new DataObject(DataObject.VisibilityOptions.Public, "False") },
                { "Phase", new DataObject(DataObject.VisibilityOptions.Public, "Command") }
            }
        };
        await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, updateOptions);
    }
    

    // --- SPIELER AKTIONEN ---
    public async void OnStampClicked(string stampChoice)
    {
        if (_currentLobby == null || !_currentLobby.Data.ContainsKey("Phase")) return;
        string currentPhase = _currentLobby.Data["Phase"].Value;

        if (selfTrayRect != null && selfTrayRect.gameObject.activeSelf)
            selfTrayRect.DOAnchorPos(selfTrayHiddenPos, slideDuration).SetEase(Ease.Flash);

        if (enemyTrayRect != null && enemyTrayRect.gameObject.activeSelf)
            enemyTrayRect.DOAnchorPos(enemyTrayHiddenPos, slideDuration).SetEase(Ease.Flash);

        // 2. Warte exakt so lange, wie die Animation dauert
        await System.Threading.Tasks.Task.Delay((int)(slideDuration * 1000));

        if (currentPhase == "Command")
        {
            stampsGroup.SetActive(false); // Tray sofort ausblenden, damit die Eingabe blockiert ist
            UpdatePlayerChoice(AuthenticationService.Instance.PlayerId, stampChoice, "Choice");
        }
        else if (currentPhase == "Deception")
        {
            if (_currentSubMode == SubSelectionMode.Swap)
            {
                stampsGroup.SetActive(false);
                SendDeceptionData("Swap", stampChoice);
            }
            else if (_currentSubMode == SubSelectionMode.Bribe)
            {
                if (enemyTrayRect != null) enemyTrayRect.gameObject.SetActive(false);
                else stampsGroup.SetActive(false);
            
                SendDeceptionData("Bribe", stampChoice);
            }
            _currentSubMode = SubSelectionMode.None;
        }
    }
    
    public void OnStaySelected()
    {
        buttonsGroup.SetActive(false);
        SendDeceptionData("Stay", "None");
    }

    public void OnSwapSelected()
    {
        buttonsGroup.SetActive(false);
        _currentSubMode = SubSelectionMode.Swap;
        stampsGroup.SetActive(true); // Öffnet dein Tray für die Neuauswahl
        if (selfTrayRect != null)
        {
            selfTrayRect.gameObject.SetActive(true);
            if (enemyTrayRect != null) enemyTrayRect.gameObject.SetActive(false);
            
            selfTrayRect.anchoredPosition = selfTrayHiddenPos;
            selfTrayRect.DOAnchorPos(selfTrayVisiblePos, slideDuration).SetEase(Ease.Flash);
        }
    }

    public void OnBribeSelected()
    {
        buttonsGroup.SetActive(false);
        _currentSubMode = SubSelectionMode.Bribe;
        stampsGroup.SetActive(true);
        if (enemyTrayRect != null)
        {
            if (selfTrayRect != null) selfTrayRect.gameObject.SetActive(false);
            enemyTrayRect.gameObject.SetActive(true);
            
            // DOTween Animation rein
            enemyTrayRect.anchoredPosition = enemyTrayHiddenPos;
            enemyTrayRect.DOAnchorPos(enemyTrayVisiblePos, slideDuration).SetEase(Ease.Flash);
        }
        else if (selfTrayRect != null)
        {
            // Fallback auf Self-Tray
            selfTrayRect.gameObject.SetActive(true);
            selfTrayRect.anchoredPosition = selfTrayHiddenPos;
            selfTrayRect.DOAnchorPos(selfTrayVisiblePos, slideDuration).SetEase(Ease.Flash);
        }
    }

    // (Zusammengefasst, da Choice und Deception im Backend fast identisch sind)
    private async void UpdatePlayerChoice(string playerId, string choiceValue, string dataKey)
    {
        try
        {
            await LobbyService.Instance.UpdatePlayerAsync(_currentLobby.Id, playerId, new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { dataKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, choiceValue) }
                }
            });
            Debug.Log($"Player {playerId} has choosen {choiceValue} for {dataKey}");
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }

    // --- SPIELLOGIK & AUSWERTUNG ---
    private void CheckForPlayerChoices()
    {
        Dictionary<string, string> choices = new Dictionary<string, string>();

        foreach (Player player in _currentLobby.Players)
        {
            if (player.Data.TryGetValue("Choice", out var choice) && !string.IsNullOrEmpty(choice.Value))
            {
                choices[player.Id] = choice.Value;
            }
        }

        if (choices.Count == 2 && !_isEvaluating)
        {
            _isEvaluating = true; 
            
            // Der Host stellt die Lobby sofort auf Deception um.
            if (AuthenticationService.Instance.PlayerId == _currentLobby.HostId)
            {
                TransitionToDeceptionPhase();
            }
        }
    }
    
    private async void TransitionToDeceptionPhase()
    {
        try
        {
            UpdateLobbyOptions options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "Phase", new DataObject(DataObject.VisibilityOptions.Public, "Deception") }
                }
            };
            
            await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, options);
        }
        catch (Exception e) { Debug.Log(e); }
    }

    private void CheckForDeceptionChoice()
    {
        int readyCount = 0;
        foreach (Player player in _currentLobby.Players)
        {
            if (player.Data.ContainsKey("DeceptionAction") && !string.IsNullOrEmpty(player.Data["DeceptionAction"].Value))
            {
                readyCount++;
            }
        }
        
        // Beide Spieler haben Stay, Swap oder Bribe fertig konfiguriert
        if (readyCount == 2 && !_isEvaluating)
        {
            _isEvaluating = true; 
            ResolveDeceptionsAndEvaluate();
        }
    }

    private void ResolveDeceptionsAndEvaluate()
    {
        Player p1 = _currentLobby.Players[0];
        Player p2 = _currentLobby.Players[1];

        string p1Initial = p1.Data["Choice"].Value;
        string p2Initial = p2.Data["Choice"].Value;

        string p1Action = p1.Data["DeceptionAction"].Value;
        string p1Target = p1.Data["DeceptionTarget"].Value;

        string p2Action = p2.Data["DeceptionAction"].Value;
        string p2Target = p2.Data["DeceptionTarget"].Value;

        string p1Final = p1Initial;
        string p2Final = p2Initial;

        // 1. Swap hat absolute Priorität
        if (p1Action == "Swap") p1Final = p1Target;
        if (p2Action == "Swap") p2Final = p2Target;

        // 2. Bribe wirkt nur, wenn das Opfer NICHT geswappt hat
        if (p1Action == "Bribe" && p2Action != "Swap") p2Final = p1Target; 
        if (p2Action == "Bribe" && p1Action != "Swap") p1Final = p2Target; 
        
        UpdateDisplaySprites(p1Final, p2Final);
        
        string localId = AuthenticationService.Instance.PlayerId;
        string myFinal = (localId == p1.Id) ? p1Final : p2Final;
        UpdatePlayerChoice(localId, myFinal, "Choice");

        // Die finalen Resultate lokal überschreiben, damit EvaluateWinner 
        // und ShowChoices() direkt mit den getäuschten Stamps arbeiten können!
        p1.Data["Choice"].Value = p1Final;
        p2.Data["Choice"].Value = p2Final;

        EvaluateWinner(_currentLobby.Players, "Choice");
    }

    private void UpdateDisplaySprites(string p1Choice, string p2Choice)
    {
        Player p1 = _currentLobby.Players[0];
        bool isP1Local = (p1.Id == AuthenticationService.Instance.PlayerId);

        string localChoice = isP1Local ? p1Choice : p2Choice;
        string enemyChoice = isP1Local ? p2Choice : p1Choice;

        if (playerChoiceDisplayImage != null) playerChoiceDisplayImage.sprite = GetSpriteForChoice(localChoice);
        if (enemyChoiceDisplayImage != null) enemyChoiceDisplayImage.sprite = GetSpriteForChoice(enemyChoice);
    }

    private Sprite GetSpriteForChoice(string choice)
    {
        switch (choice)
        {
            case "Cannon": return cannonSprite;
            case "Cutlass": return cutlassSprite;
            case "Sail": return sailSprite;
            default: return null;
        }
    }

    private async void EvaluateWinner(List<Player> players, string dataKeyToCompare)
    {
        string player1Name = players[0].Data["PlayerName"].Value;
        string player2Name = players[1].Data["PlayerName"].Value;
        string player1Choice = players[0].Data[dataKeyToCompare].Value;
        string player2Choice = players[1].Data[dataKeyToCompare].Value;
        
        bool isDraw = (player1Choice == player2Choice);
        bool player1Won = false;
        
        if (player1Choice == "Cannon" && player2Choice == "Cutlass") player1Won = true;
        else if (player1Choice == "Cutlass" && player2Choice == "Sail") player1Won = true;
        else if (player1Choice == "Sail" && player2Choice == "Cannon") player1Won = true;
        
        string currentPlayer = AuthenticationService.Instance.PlayerId;

        try
        {
            string winnerPlayerId = "None";
            string winnerPlayerName = "Nobody (Draw)";
            
            if (player1Won)
            {
                winnerPlayerId = players[0].Id;
                winnerPlayerName = player1Name;
            } 
            else if (!isDraw)
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

            if (winnerPlayerId == "None")
            {
                updateLobbyOptions.Data.Add("IsDraw", new DataObject(DataObject.VisibilityOptions.Public, "True"));
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

            // Nur der Host darf die Lobby updaten
            if (currentPlayer == _currentLobby.HostId)
            {
                await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, updateLobbyOptions);
            }
            
            // HIER IST DIE VERZÖGERUNG FÜR DAS LOGGEN DES GEWINNERS
            await System.Threading.Tasks.Task.Delay(2000);
            Debug.Log($"--- RUNDE BEENDET --- Gewinner: {winnerPlayerName}");
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }

    private void ShowChoices(string dataKeyToRead)
    {
        foreach (Player player in _currentLobby.Players)
        {
            bool isLocal = player.Id == AuthenticationService.Instance.PlayerId;
            Sprite sprite = null;
            string playerChoice = player.Data.TryGetValue(dataKeyToRead, out var choice) ? choice.Value : "";
            
            switch (playerChoice)
            {
                case "Cannon": sprite = cannonSprite; break;
                case "Cutlass": sprite = cutlassSprite; break;
                case "Sail": sprite = sailSprite; break;
            }
            
            if (isLocal && playerChoiceDisplayImage != null) playerChoiceDisplayImage.sprite = sprite;
            else if (!isLocal && enemyChoiceDisplayImage != null) enemyChoiceDisplayImage.sprite = sprite;
        }
    }
    
    private async void SendDeceptionData(string action, string targetChoice)
    {
        try
        {
            string localPlayerId = AuthenticationService.Instance.PlayerId;
            await LobbyService.Instance.UpdatePlayerAsync(_currentLobby.Id, localPlayerId, new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "DeceptionAction", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, action) },
                    { "DeceptionTarget", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, targetChoice) }
                }
            });
            Debug.Log($"Gesendet: {action} mit Ziel: {targetChoice}");
        }
        catch (Exception e) { Debug.Log(e); }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Firebase.Auth;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BackendDev
{
    public class UiManager : MonoBehaviour
    {
        // Screens
        [Header( "Screens" )]
        [SerializeField] private GameObject _loginScreen;
        [SerializeField] private GameObject _gameScreen;
        [SerializeField] private GameObject _gamePlayScreen;
        [SerializeField] private GameObject _createARoomScreen;
        [SerializeField] private GameObject _joinRoomScreen;
        [SerializeField] private GameObject _roomScreenChoices;
        
        //Button
        [Header( "Buttons" )]
        [SerializeField] private Button _submitRoomIdButton;
        [SerializeField] private Button _joinRoomButton;
        [SerializeField] private Button _createRoomScreenButton;
        [SerializeField] private Button _joinRoomScreenButton;
        [SerializeField] private Button _leaveRoomButton;
        
        //TextFields
        [Header( "TextFields" )]
        [SerializeField] private TMP_InputField _roomIdInputField;
        
        //References
        [Header( "References" )]
        [SerializeField] private MessageSender _messageSender;
        [SerializeField] private SignInManger _signInManger;
        
        
        //PlayerDetails
        [Header( "PlayerDetails" )]
        [SerializeField] private TMP_Text _player1Email;
        [SerializeField] private TMP_Text _player1Name;
        [SerializeField] private TMP_Text _player1ScoreText; // ADD THIS
        [SerializeField] private TMP_Text _player2Email;
        [SerializeField] private TMP_Text _player2Name;
        [SerializeField] private TMP_Text _player2ScoreText; // ADD THIS

        
        //Room
        [Header( "Room Details" )]
        [SerializeField] private TMP_Text _roomID;
        [SerializeField] private TMP_Text _roomTimer;
        
        [Header( "Board Details" )]
        [SerializeField] private Button[] gameButtons;
        [SerializeField] private Color defaultColor = Color.white;
        [SerializeField] private Color clickedColor = Color.green;
        [SerializeField] private Button _increaseScoreBtn;
        [SerializeField] private Button _decreaseScoreBtn;
        
        //Private Fields
        private GameData _gameData;
        private RoomData _currentRoomData = new RoomData();
        private string roomId;
        private int[] localStates;
        private float roomTimer;          // countdown in seconds
        // private bool isTimerRunning = false;
        
        
        private float remainingTime;
        private bool isTimerRunning = false;
        private string activeRoomId;



        private void Start()
        {
            SetUpButtonListeners();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleBackButton();
            }
            if (!isTimerRunning) return;

            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0)
            {
                remainingTime = 0;
                isTimerRunning = false;
                _roomTimer.text = "Expired";
                FirestoreManager.Instance.CloseRoom(activeRoomId);
                return;
            }

            TimeSpan ts = TimeSpan.FromSeconds(remainingTime);
            _roomTimer.text = string.Format("{0:D2}:{1:D2}:{2:D2}", ts.Hours, ts.Minutes, ts.Seconds);

            // ✅ Update Firestore every 60s (to reduce writes)
            if (Mathf.FloorToInt(remainingTime) % 60 == 0)
            {
                FirestoreManager.Instance.UpdateRemainingTime(activeRoomId, remainingTime);
            }
        }

        private void OnEnable()
        {
            ActionHandler.OnLoginSuccess += UpdateUiAfterLogin;
            ActionHandler.OnRoomCreated += OnRoomCreated;
            ActionHandler.OnRoomJoined += OnRoomJoined;
        }

        private void OnDisable()
        {
            ActionHandler.OnLoginSuccess -= UpdateUiAfterLogin;
            ActionHandler.OnRoomCreated -= OnRoomCreated;
            ActionHandler.OnRoomJoined -= OnRoomJoined;
            
        }

        #region Button Listeners
        private void SetUpButtonListeners()
        {
            // _signInButton.onClick.AddListener(OnSignInButtonClick); 
            _submitRoomIdButton.onClick.AddListener(OnSubmitRoonIdButtonClick);
            _joinRoomButton.onClick.AddListener(OnJoinRoomButtonClick);
            _createRoomScreenButton.onClick.AddListener(OnCreateRoomScreenButtonClick);
            _joinRoomScreenButton.onClick.AddListener(OnJoinRoomScreenButtonClick);
            _leaveRoomButton.onClick.AddListener(OnLeaveButtonClick);
            _increaseScoreBtn.onClick.AddListener(OnIncreaseScoreClicked);
            _decreaseScoreBtn.onClick.AddListener(OnDecreaseScoreClicked);
        }
        
        private void OnSubmitRoonIdButtonClick()
        {
            RoomData roomData = BuildRoomData();
            // PlayerData playerData = BuildPlayerData();
            GameData gameData = new GameData
            {
                roomData = roomData,
            };
            Debug.Log($"[UIManager] Submitting RoomID: {roomData.roomID}");
            _messageSender.SendCreateOrJoinRoom(gameData);
        }


        private void OnJoinRoomButtonClick()
        {
            _messageSender.JoinRoom();
        }

        private void OnCreateRoomScreenButtonClick()
        {
            _createARoomScreen.SetActive(true);
            _roomScreenChoices.SetActive(false);
        }

        private void OnJoinRoomScreenButtonClick()
        {
            _joinRoomScreen.SetActive(true);
            _roomScreenChoices.SetActive(false);
        }

        private void OnLeaveButtonClick()
        {
            _messageSender.LeaveRoom();
        }

        private void OnIncreaseScoreClicked()
        {
            if (string.IsNullOrEmpty(roomId) || _currentRoomData == null) return;
    
            // --- Background Update ---
            // Send the update request to Firestore. We don't wait for it to complete.
            FirestoreManager.Instance.UpdatePlayerScoreAsync(roomId, 1);
    
            // --- Instant UI Update ---
            // 1. Find out who the local player is
            FirebaseUser currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
            int localPlayerIndex = System.Array.IndexOf(_currentRoomData.playerIDs, currentUser.UserId);
    
            // 2. Modify the local data cache
            if(localPlayerIndex == 0) _currentRoomData.playerScore.player1Score++;
            else if(localPlayerIndex == 1) _currentRoomData.playerScore.player2Score++;

            // 3. Immediately update the UI with the modified local data
            UpdateScoreUI(_currentRoomData);
        }

        private void OnDecreaseScoreClicked()
        {
            if (string.IsNullOrEmpty(roomId) || _currentRoomData == null) return;

            // --- Background Update ---
            FirestoreManager.Instance.UpdatePlayerScoreAsync(roomId, -1);
    
            // --- Instant UI Update ---
            FirebaseUser currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
            int localPlayerIndex = System.Array.IndexOf(_currentRoomData.playerIDs, currentUser.UserId);
    
            if(localPlayerIndex == 0) _currentRoomData.playerScore.player1Score--;
            else if(localPlayerIndex == 1) _currentRoomData.playerScore.player2Score--;

            UpdateScoreUI(_currentRoomData);
        }
        #endregion
        
        private void UpdateScoreUI(RoomData roomData)
        {
            if (roomData?.playerScore == null || roomData.playerIDs == null || roomData.playerIDs.Length == 0)
            {
                // Not enough data to update scores, maybe clear them
                _player1ScoreText.text = "Score: 0";
                _player2ScoreText.text = "Score: 0";
                return;
            }
            
            // --- ADD THIS LOGGING ---
            Debug.Log($"[UpdateScoreUI] Updating UI with scores: P1 Score = {roomData.playerScore.player1Score}, P2 Score = {roomData.playerScore.player2Score}");
            // --- END LOGGING ---
    
            FirebaseUser currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
            if (currentUser == null) return;

            // Determine which score belongs to the local player (who is always P1 in the UI)
            int localPlayerIndex = System.Array.IndexOf(roomData.playerIDs, currentUser.UserId);

            if (localPlayerIndex == 0) // Local player is P1 in Firestore
            {
                _player1ScoreText.text = $"Score: {roomData.playerScore.player1Score}";
                _player2ScoreText.text = $"Score: {roomData.playerScore.player2Score}";
            }
            else if (localPlayerIndex == 1) // Local player is P2 in Firestore
            {
                // Display P2's score in the P1 slot and vice-versa
                _player1ScoreText.text = $"Score: {roomData.playerScore.player2Score}";
                _player2ScoreText.text = $"Score: {roomData.playerScore.player1Score}";
            }
        }
        
        
        #region OnEnable/OnDisable
        public void UpdateUiAfterLogin(FirebaseUser user)
        {
            _loginScreen.SetActive(false); // Hide login screen
            _gameScreen.SetActive(true); // Show the parent game screen
            _roomScreenChoices.SetActive(true); // Explicitly show the choice screen
            _gamePlayScreen.SetActive(false); // Ensure gameplay screen is hidden
        }

        public void OnRoomCreated()
        {
            _createARoomScreen.SetActive(false);
            _gamePlayScreen.SetActive(true);
        }

        public void OnRoomJoined()
        {
            _joinRoomScreen.SetActive(false);
            _gamePlayScreen.SetActive(true);
        }
        #endregion
        
        
        private RoomData BuildRoomData()
        {
            return new RoomData
            {
                roomProperties = new RoomProperties
                {
                   roomStatus = true
                } ,
                roomID = _roomIdInputField.text,
                buildVersion = Application.version,

                // You can extend this later if you want board layout or initial tiles
                boardData = new BoardData(),

                // Optional: initialize with just the current player
                playerIDs = new string[] { Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser?.UserId },

                timerData = new TimerData
                {
                    // expirationTime = Timestamp.FromDateTime(DateTime.UtcNow.AddHours(24)),   // Unity’s current time
                    expirationTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)),   // Unity’s current time
                    creationTime = Timestamp.FromDateTime(DateTime.UtcNow),
                    roomsTime = 120f,           // Example: 2 minutes per room
                    player1Timer = 0f,
                    player2Timer = 0f
                },

                playerScore = new PlayersScore
                {
                    player1Score = 0,
                    player2Score = 0
                },

                playerHandData = new PlayersHandData
                {
                    player1HandData = 0,
                    player2HandData = 0
                }
            };
        }
        
        public void InitializeGameUI(string id)
        {
            roomId = id;
            activeRoomId = id;

            // Add listeners
            for (int i = 0; i < gameButtons.Length; i++)
            {
                int index = i;
                gameButtons[i].onClick.AddListener(() => OnButtonClicked(index));
            }

            // Start listening to Firestore
            _roomID.text = roomId;
            
            FirestoreManager.Instance.ListenToBoard(roomId, UpdateButtonColors);
            
            // ✅ Listen to the room itself for timer changes
            FirestoreManager.Instance.ListenToRoomData(roomId, OnRoomDataUpdated);
        }
        
        private void OnButtonClicked(int index)
        {
            // Toggle example: 0 -> 1, 1 -> 0
            int newState = gameButtons[index].image.color == defaultColor ? 1 : 0;
            // ✅ Send update to Firestore
            FirestoreManager.Instance.UpdateButtonState(roomId, index, newState);
        }

        private void UpdateButtonColors(List<int> states)
        {
            for (int i = 0; i < gameButtons.Length; i++)
            {
                gameButtons[i].image.color = states[i] == 0 ? defaultColor : clickedColor;
            }
        }
        
        public void ResumeGameSession(GameData gameData)
        {
            Debug.Log($"Resuming UI for room: {gameData.roomData.roomID}");

            // Hide all initial screens
            _loginScreen.SetActive(false);
            _gameScreen.SetActive(true); // This likely holds the choice buttons
            _createARoomScreen.SetActive(false);
            _joinRoomScreen.SetActive(false);
            _roomScreenChoices.SetActive(false);

            // Show the main gameplay screen
            _gamePlayScreen.SetActive(true);

            // Initialize the game UI with the resumed room's data.
            // This is crucial as it re-attaches all the Firestore listeners.
            InitializeGameUI(gameData.roomData.roomID);
    
            // You might also need to re-populate the player list immediately
            // since the listener might take a moment.
            // This part is optional but improves user experience.
            FirestoreManager.Instance.ListenToRoomPlayers(gameData.roomData.roomID, players =>
            {
                UpdateRoomPlayerList(players);
            });
        }
        
        public void UpdateRoomPlayerList(List<Dictionary<string, object>> players)
        {
            FirebaseUser currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
            if (currentUser == null)
            {
                Debug.LogError("No logged in user while updating player list");
                return;
            }

            string localUserId = currentUser.UserId;
            
            Dictionary<string, object> self = players.FirstOrDefault(p => p.ContainsKey("userId") && p["userId"].ToString() == localUserId);
            Dictionary<string, object> other = players.FirstOrDefault(p => !p.ContainsKey("userId") || p["userId"].ToString() != localUserId);
            

            // ✅ Always show self as Player 1
            if (self != null)
            {
                _player1Name.text = self.ContainsKey("name") ? self["name"].ToString() : "NoName";
                _player1Email.text = self.ContainsKey("email") ? self["email"].ToString() : "NoEmail";
            }

            // ✅ Show the other as Player 2
            if (other != null)
            {
                _player2Name.text = other.ContainsKey("name") ? other["name"].ToString() : "NoName";
                _player2Email.text = other.ContainsKey("email") ? other["email"].ToString() : "NoEmail";
            }
        }

        public void OnRoomClosed()
        {
            _gamePlayScreen.SetActive(false);
            _createARoomScreen.SetActive(false);
            _joinRoomScreen.SetActive(false);
            _gameScreen.SetActive(true);
            _roomScreenChoices.SetActive(true);
        }
        
        
        private void OnRoomDataUpdated(RoomData incomingRoomData)
        {
            // If the room is no longer open, close the UI and stop processing.
            if (!incomingRoomData.roomProperties.roomStatus)
            {
                Debug.Log("Room has been closed. Returning to menu.");
                OnRoomClosed();
                isTimerRunning = false; // Stop the timer as well
                return;
            }
    
            // ✅ THE FIX: Check if the server data is stale compared to our optimistic UI
            if (_currentRoomData != null && _currentRoomData.playerScore != null && incomingRoomData.playerScore != null)
            {
                FirebaseUser currentUser = FirebaseAuth.DefaultInstance.CurrentUser;
                if (currentUser != null)
                {
                    int localPlayerIndex = System.Array.IndexOf(_currentRoomData.playerIDs, currentUser.UserId);
                    if (localPlayerIndex == 0)
                    {
                        // If the incoming score is less than our current optimistic score, ignore this update.
                        if (incomingRoomData.playerScore.player1Score < _currentRoomData.playerScore.player1Score)
                        {
                            return; // EXIT EARLY
                        }
                    }
                    else if (localPlayerIndex == 1)
                    {
                        if (incomingRoomData.playerScore.player2Score < _currentRoomData.playerScore.player2Score)
                        {
                            return; // EXIT EARLY
                        }
                    }
                }
            }
    
            // 1. ALWAYS update our local cache with the latest valid truth from the server.
            _currentRoomData = incomingRoomData;

            // 2. Refresh the entire UI based on this new data.
            UpdateScoreUI(_currentRoomData);

            if (_currentRoomData.timerData?.expirationTime != null)
            {
                DateTime expiration = _currentRoomData.timerData.expirationTime.ToDateTime();
                remainingTime = (float)(expiration - DateTime.UtcNow).TotalSeconds;

                isTimerRunning = remainingTime > 0;
        
                if (!isTimerRunning)
                {
                    _roomTimer.text = "Expired";
                    FirestoreManager.Instance.CloseRoom(roomId);
                }
            }
        }

        private void HandleBackButton()
        {
            // Determine which screen is active and handle accordingly
            if (_gamePlayScreen.activeSelf)
            {
                // If in gameplay, leave the room
                OnLeaveButtonClick();
            }
            else if (_createARoomScreen.activeSelf || _joinRoomScreen.activeSelf)
            {
                // If in create/join room screens, go back to choices
                _createARoomScreen.SetActive(false);
                _joinRoomScreen.SetActive(false);
                _roomScreenChoices.SetActive(true);
            }
            else if (_roomScreenChoices.activeSelf)
            {
                // If in room choices, go back to game screen or exit
                _roomScreenChoices.SetActive(false);
                _gameScreen.SetActive(true);
            }
            else if (_gameScreen.activeSelf)
            {
                // If in game screen, exit the application
                Application.Quit();
            }
            else if (_loginScreen.activeSelf)
            {
                // If in login screen, exit the application
                Application.Quit();
            }
        }



    }
}

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
        
        //TextFields
        [Header( "TextFields" )]
        [SerializeField] private TMP_InputField _roomIdInputField;
        
        //References
        [Header( "References" )]
        [SerializeField] private MessageSender _messageSender;
        
        
        //PlayerDetails
        [Header( "PlayerDetails" )]
        [SerializeField] private TMP_Text _player1Email;
        [SerializeField] private TMP_Text _player1Name;
        [SerializeField] private TMP_Text _player2Email;
        [SerializeField] private TMP_Text _player2Name;
        
        //Room
        [Header( "Room Details" )]
        [SerializeField] private TMP_Text _roomID;
        [SerializeField] private TMP_Text _roomTimer;
        
        [Header( "Board Details" )]
        [SerializeField] private Button[] gameButtons;
        [SerializeField] private Color defaultColor = Color.white;
        [SerializeField] private Color clickedColor = Color.green;
        
        //Private Fields
        private GameData _gameData;
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
        }
        
        private void OnSubmitRoonIdButtonClick()
        {
            RoomData roomData = BuildRoomData();
            GameData gameData = new GameData
            {
                roomData = roomData
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
        #endregion
        
        #region OnEnable/OnDisable
        public void UpdateUiAfterLogin(FirebaseUser user)
        {
            _gameScreen.SetActive(true);
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
                isOpen = true,
                roomID = _roomIdInputField.text,
                buildVersion = Application.version,

                // You can extend this later if you want board layout or initial tiles
                boardData = new BoardData(),

                // Optional: initialize with just the current player
                playerIDs = new string[] { Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser?.UserId },

                timerData = new TimerData
                {
                    // expirationTime = Timestamp.FromDateTime(DateTime.UtcNow.AddHours(24)),   // Unity’s current time
                    expirationTime = Timestamp.FromDateTime(DateTime.UtcNow.AddHours(24)),   // Unity’s current time
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
            
        }
        
        
        private void OnRoomDataUpdated(RoomData roomData)
        {
            if (roomData.timerData != null && roomData.timerData.expirationTime != null)
            {
                DateTime expiration = roomData.timerData.expirationTime.ToDateTime();
                remainingTime = (float)(expiration - DateTime.UtcNow).TotalSeconds;

                if (remainingTime > 0)
                {
                    isTimerRunning = true;
                }
                else
                {
                    isTimerRunning = false;
                    _roomTimer.text = "Expired";
                    FirestoreManager.Instance.CloseRoom(roomId);
                }
            }
        }



    }
}

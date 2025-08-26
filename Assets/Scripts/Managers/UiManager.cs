using System;
using System.Collections.Generic;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BackendDev
{
    public class UiManager : MonoBehaviour
    {
        // Screens
        [SerializeField] private GameObject _loginScreen;
        [SerializeField] private GameObject _gameScreen;
        [SerializeField] private GameObject _gamePlayScreen;
        [SerializeField] private GameObject _createARoomScreen;
        [SerializeField] private GameObject _joinRoomScreen;
        
        //Button
        [SerializeField] private Button _signInButton;
        [SerializeField] private Button _submitRoomIdButton;
        [SerializeField] private Button _joinRoomButton;
        
        //TextFields
        [SerializeField] private TMP_InputField _roomIdInputField;
        
        //References
        private GameData _gameData;
        
        //PlayerDetails
        [SerializeField] private TMP_Text _player1Email;
        [SerializeField] private TMP_Text _player1Name;
        [SerializeField] private TMP_Text _player2Email;
        [SerializeField] private TMP_Text _player2Name;
        
        //Room
        [SerializeField] private TMP_Text _roomID;


        private void Start()
        {
            SetUpButtonListeners();
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
        }

        private void OnSignInButtonClick()
        {
            
        }
        private void OnSubmitRoonIdButtonClick()
        {
            GameData data = new GameData
            {
                roomData = new RoomData
                {
                    roomID = _roomIdInputField.text
                }
            };
            Debug.Log(data.roomData.roomID);
            FirestoreManager.Instance.CreateOrJoinRoom(data.roomData.roomID);
        }

        private void OnJoinRoomButtonClick()
        {
            FirestoreManager.Instance.FindAndJoinOpenRoom();
        }
        #endregion
        
        #region OnEnable/OnDisable
        public void UpdateUiAfterLogin(FirebaseUser user)
        {
            // _loginScreen.SetActive(false);
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
        
        public void UpdateRoomPlayerList(List<Dictionary<string, object>> players)
        {
            int i = 0;
            _roomID.text = _gameData.roomData.roomID;
            // Clear existing UI list
            // Instantiate prefab for each player
            foreach (var player in players)
            {
                string name = player.ContainsKey("name") ? player["name"].ToString() : "NoName";
                string email = player.ContainsKey("email") ? player["email"].ToString() : "NoEmail";
                if (i == 0)
                {
                    _player1Email.text = email;
                    _player1Name.text = name;
                }
                else if (i == 1)
                {
                    _player2Email.text = email;
                    _player2Name.text = name;
                }
                i++;
            }
        }

    }
}

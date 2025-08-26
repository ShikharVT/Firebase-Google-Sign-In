using System;
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
    }
}

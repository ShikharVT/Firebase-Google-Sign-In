using BackendDev;
using UnityEngine;

namespace BackendDev
{
    [DefaultExecutionOrder(1)]
    public class MessageSender : MonoBehaviour
    {
        [SerializeField] private FirestoreManager _firestoreManager;
        [SerializeField] private SignInManger _signInManger;
        
        public async void SendCreateOrJoinRoom(GameData gameData)
        {
            Debug.Log($"[MessageSender] Sending room data for RoomID: {gameData.roomData.roomID}");
            await _firestoreManager.CreateOrJoinRoom(gameData);
        }

        public async void JoinRoom()
        {
            await _firestoreManager.FindAndJoinOpenRoom();
        }

        // REPLACE the existing LeaveRoom method with this one.
        public async void LeaveRoom()
        {
            Debug.Log("[MessageSender] Sending leave room request.");
            await _firestoreManager.LeaveRoomAsync();
        }
    }

}

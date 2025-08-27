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
            // Debug.Log($"[MessageSender] Sending room data for RoomID: {roomData.roomID}");
            await _firestoreManager.FindAndJoinOpenRoom();
        }
    }

}

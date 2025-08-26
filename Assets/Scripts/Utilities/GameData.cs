using UnityEngine;

namespace BackendDev
{
    [System.Serializable]
    public class GameData
    {
        public RoomData roomData;
    }

    [System.Serializable]
    public class RoomData
    {
        public string roomID;
    }
}

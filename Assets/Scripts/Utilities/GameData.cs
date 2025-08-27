using System.Collections.Generic;
using Firebase.Firestore;

namespace BackendDev
{
    [FirestoreData]
    public class GameData
    {
        [FirestoreProperty]
        public RoomData roomData { get; set; }
    }

    [FirestoreData]
    public class RoomData
    {
        [FirestoreProperty]
        public bool isOpen { get; set; }
        
        [FirestoreProperty]
        public string roomID { get; set; }

        [FirestoreProperty]
        public string buildVersion { get; set; }

        [FirestoreProperty]
        public BoardData boardData { get; set; }

        [FirestoreProperty]
        public string[] playerIDs { get; set; }

        [FirestoreProperty]
        public TimerData timerData { get; set; }

        [FirestoreProperty]
        public PlayersScore playerScore { get; set; }

        [FirestoreProperty]
        public PlayersHandData playerHandData { get; set; }
    }

    [FirestoreData]
    public class BoardData
    {
        [FirestoreProperty]
        public List<int> buttonStates { get; set; } = new List<int> {0,0,0,0};
    }

    [FirestoreData]
    public class TimerData
    {
        [FirestoreProperty]
        public Timestamp expirationTime { get; set; }

        [FirestoreProperty]
        public float roomsTime { get; set; }

        [FirestoreProperty]
        public float player1Timer { get; set; }

        [FirestoreProperty]
        public float player2Timer { get; set; }
    }

    [FirestoreData]
    public class PlayersScore
    {
        [FirestoreProperty]
        public int player1Score { get; set; }

        [FirestoreProperty]
        public int player2Score { get; set; }
    }

    [FirestoreData]
    public class PlayersHandData
    {
        [FirestoreProperty]
        public int player1HandData { get; set; }

        [FirestoreProperty]
        public int player2HandData { get; set; }
    }


    [FirestoreData]
    public class PlayerData
    {
        [FirestoreProperty]
        public string playerName { get; set; }
        
        [FirestoreProperty]
        public string profilePic { get; set; }
        
        [FirestoreProperty]
        public string currentBuildVersion { get; set; }
        
        [FirestoreProperty]
        public string lastBuildVersion { get; set; }
        
        [FirestoreProperty]
        public string email { get; set; }
        
        [FirestoreProperty]
        public string playerId { get; set; }
        
        [FirestoreProperty]
        public Timestamp lastLoginTime { get; set; }
        
        [FirestoreProperty]
        public Scores scores { get; set; }
        
        [FirestoreProperty]
        public Matches matchStats { get; set; }
    }

    [FirestoreData]
    public class Matches
    {
        [FirestoreProperty]
        public int totalMatches { get; set; }
        
        [FirestoreProperty]
        public int totalWines { get; set; }
    }
    
    [FirestoreData]
    public class Scores
    {
        [FirestoreProperty]
        public int score { get; set; }
        
    }
}
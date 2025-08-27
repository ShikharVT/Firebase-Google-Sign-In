using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;
using System.Linq;
using BackendDev;

public class FirestoreManager : MonoBehaviour
{
    [SerializeField] private SignInManger _signInManger;
    [SerializeField] private UiManager _uiManager;
    private FirebaseFirestore db;
    private bool isFirestoreInitialized = false;

    public static FirestoreManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    void Start()
    {
        InitializeFirestore();
    }

    private void InitializeFirestore()
    {
        if (_signInManger != null && _signInManger.db != null)
        {
            db = _signInManger.db;
            isFirestoreInitialized = true;
            Debug.Log("Firestore initialized in FirestoreManager");
        }
        else
        {
            Debug.LogWarning("SignInManager or Firestore not ready, will retry...");
            // Retry initialization after a delay
            Invoke("InitializeFirestore", 1f);
        }
    }
    public void InitializeWithFirestore(FirebaseFirestore firestore)
    {
        db = firestore;
        isFirestoreInitialized = true;
        Debug.Log("Firestore initialized via SignInManager");
    }

   // ---------------- CREATE OR JOIN ROOM ----------------
   public async Task CreateOrJoinRoom(GameData gameData)
   {
       if (!isFirestoreInitialized)
       {
           Debug.LogError("Firestore not initialized");
           return;
       }

       FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
       if (user == null) { Debug.LogError("No user logged in"); return; }

       string roomId = gameData.roomData.roomID;
       var roomRef = db.Collection("rooms").Document(roomId);

       try
       {
           var snapshot = await roomRef.GetSnapshotAsync();

           // RoomData finalRoomData;
           GameData finalGameData;

           if (!snapshot.Exists)
           {
               // ✅ Create new room by writing the object directly
               await roomRef.SetAsync(gameData);
               Debug.Log($"Room {roomId} created.");
               ActionHandler.OnRoomCreated?.Invoke();
               
               finalGameData = gameData;
           }
           else
           {
               // ✅ Room exists → hydrate back into C# class
               finalGameData = snapshot.ConvertTo<GameData>();
               Debug.Log($"Joining existing room {roomId}");
           }
           _uiManager.InitializeGameUI(roomId);

           // Add player to subcollection "players"
           DocumentReference playerRef = roomRef.Collection("players").Document(user.UserId);
           Dictionary<string, object> playerData = new Dictionary<string, object>
           {
               { "userId", user.UserId },
               { "name", user.DisplayName ?? "NoName" },
               { "email", user.Email ?? "NoEmail" }
           };

           await playerRef.SetAsync(playerData, SetOptions.MergeAll);
           Debug.Log($"Player {user.UserId} added to room {roomId}");
           
           ActionHandler.OnRoomJoined?.Invoke();

           // Listen for player changes
           ListenToRoomPlayers(roomId, players =>
           {
               Debug.Log($"Players changed in {roomId}, total: {players.Count}");
               _uiManager.UpdateRoomPlayerList(players);
           });
       }
       catch (System.Exception e)
       {
           Debug.LogError("Error accessing Firestore: " + e.Message);
       }
   }





    // ---------------- FIND OPEN ROOM ----------------
    public async Task FindAndJoinOpenRoom()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null) 
        { 
            Debug.LogError("No user logged in"); 
            return; 
        }

        var openRoomsQuery = db.Collection("rooms").WhereEqualTo("roomData.isOpen", true);
        Debug.Log("Open rooms query: " + openRoomsQuery.ToString());

        var querySnapshot = await openRoomsQuery.GetSnapshotAsync();
        Debug.Log("Query snapshot: " + querySnapshot.Count);

        if (querySnapshot.Count == 0)
        {
            Debug.Log("No open rooms available.");
            return;
        }

        // Pick first open room
        DocumentSnapshot roomSnapshot = querySnapshot.Documents.ToList()[0];
        string roomId = roomSnapshot.Id;
        Debug.Log($"Found open room: {roomId}");

        // Convert Firestore document into RoomData
        GameData existingGameData = roomSnapshot.ConvertTo<GameData>();
        existingGameData.roomData.roomID = roomId; // Ensure the doc ID is stored in your RoomData

        // Join that room
        await CreateOrJoinRoom(existingGameData);
    }


    // ---------------- LISTEN TO ROOM PLAYERS ----------------
    public ListenerRegistration ListenToRoomPlayers(string roomId, System.Action<List<Dictionary<string, object>>> onPlayersChanged)
{
    CollectionReference playersRef = db.Collection("rooms").Document(roomId).Collection("players");
    DocumentReference roomRef = db.Collection("rooms").Document(roomId);

    return playersRef.Listen(async snapshot =>
    {
        List<Dictionary<string, object>> players = new List<Dictionary<string, object>>();

        foreach (var doc in snapshot.Documents)
        {
            players.Add(doc.ToDictionary());
        }

        // Extract player IDs from the players collection
        List<string> playerIDs = snapshot.Documents.Select(d => d.Id).ToList();

        // Update roomData.playerIDs in the parent room doc
        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "roomData.playerIDs", playerIDs }
        };

        try
        {
            await roomRef.UpdateAsync(updates);
            Debug.Log($"Updated room {roomId} with {playerIDs.Count} players.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to update room playerIDs: " + e.Message);
        }

        // Notify the caller with current player data
        onPlayersChanged?.Invoke(players);
    });
}

    
    
    
    // ---------------- UPDATE BUTTON STATE ----------------
    public async void UpdateButtonState(string roomId, int buttonIndex, int newState)
    {
        var roomRef = db.Collection("rooms").Document(roomId);

        await db.RunTransactionAsync(async transaction =>
        {
            DocumentSnapshot snapshot = await transaction.GetSnapshotAsync(roomRef);
            if (!snapshot.Exists) return;

            GameData gameData = snapshot.ConvertTo<GameData>();
            if (gameData.roomData.boardData?.buttonStates == null || gameData.roomData.boardData.buttonStates.Count < 4)
            {
                gameData.roomData.boardData = new BoardData { buttonStates = new List<int> {0,0,0,0} };
            }

            gameData.roomData.boardData.buttonStates[buttonIndex] = newState;
            transaction.Update(roomRef, "roomData.boardData.buttonStates", gameData.roomData.boardData.buttonStates);
        });
    }

// ---------------- LISTEN TO BOARD ----------------
    public ListenerRegistration ListenToBoard(string roomId, System.Action<List<int>> onBoardChanged)
    {
        DocumentReference roomRef = db.Collection("rooms").Document(roomId);

        return roomRef.Listen(snapshot =>
        {
            if (!snapshot.Exists) return;

            GameData gameData = snapshot.ConvertTo<GameData>();
            var states = gameData.roomData.boardData?.buttonStates ?? new List<int> {0,0,0,0};
            onBoardChanged?.Invoke(states);
        });
    }
    

    
    // ---------------- CLOSE ROOM ----------------
    public async void CloseRoom(string roomId)
    {
        if (!isFirestoreInitialized)
        {
            Debug.LogError("Firestore not initialized");
            return;
        }

        DocumentReference roomRef = db.Collection("rooms").Document(roomId);

        try
        {
            await roomRef.UpdateAsync(new Dictionary<string, object>
            {
                { "roomData.isOpen", false }
            });

            Debug.Log($"Room {roomId} expired and closed.");
            _uiManager.OnRoomClosed();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to close room: " + e.Message);
        }
    }
    
    
    public async void UpdateRemainingTime(string roomId, float remainingSeconds)
    {
        DocumentReference roomRef = db.Collection("rooms").Document(roomId);

        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "roomData.timerData.roomsTime", remainingSeconds }
        };

        try
        {
            await roomRef.UpdateAsync(updates);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to update remaining time: " + e.Message);
        }
    }
    
    
    public ListenerRegistration ListenToRoomData(string roomId, System.Action<RoomData> onRoomDataChanged)
    {
        DocumentReference roomRef = db.Collection("rooms").Document(roomId);

        return roomRef.Listen(snapshot =>
        {
            if (!snapshot.Exists) return;
            GameData data = snapshot.ConvertTo<GameData>();
            onRoomDataChanged?.Invoke(data.roomData);
        });
    }
}

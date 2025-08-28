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
    [SerializeField] private BuildConfiguration _buildConfiguration;
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
    
    public CollectionReference GetCollection(string collectionName)
    {
        string root = _buildConfiguration.GetRootCollection();
        return db.Collection(root).Document("data").Collection(collectionName);
    }
    public CollectionReference GetSubCollection(string parentCollection, string parentDocId, string subCollection)
    {
        string root = _buildConfiguration.GetRootCollection();
        return db.Collection(root)
            .Document("data")
            .Collection(parentCollection)
            .Document(parentDocId)
            .Collection(subCollection);
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
    // 1. Pre-condition checks
    if (!IsReady(out FirebaseUser user)) return;

    string roomId = gameData.roomData.roomID;
    var roomRef = GetCollection("rooms").Document(roomId);

    try
    {
        // 2. Handle the core logic of creating or fetching room data
        GameData finalGameData = await HandleRoomCreationOrJoiningAsync(roomRef, gameData);

        // 3. Update UI and save local data
        _uiManager.InitializeGameUI(roomId);
        await SaveRoomData(finalGameData.roomData, user.UserId);

        // 4. Add the current player to the room's subcollection
        await AddPlayerToRoomSubcollectionAsync(roomRef, user);

        // 5. Update the player's global profile
        await UpdateGlobalPlayerProfileAsync(user, finalGameData);

        // 6. Start listening for real-time updates on players in the room
        StartListeningForPlayerChanges(roomId);
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error in CreateOrJoinRoom process: {e.Message}");
    }
}

// =====================================================================
// HELPER FUNCTIONS
// =====================================================================

/// <summary>
/// Performs initial checks for Firestore initialization and user authentication.
/// </summary>
/// <param name="user">The authenticated Firebase user.</param>
/// <returns>True if all checks pass, otherwise false.</returns>
private bool IsReady(out FirebaseUser user)
{
    user = FirebaseAuth.DefaultInstance.CurrentUser;

    if (!isFirestoreInitialized)
    {
        Debug.LogError("Firestore not initialized");
        return false;
    }

    if (user == null)
    {
        Debug.LogError("No user logged in");
        return false;
    }
    return true;
}

/// <summary>
/// Checks if a room exists. If not, it creates it. If it does, it fetches the data.
/// </summary>
/// <param name="roomRef">The Firestore reference to the room document.</param>
/// <param name="initialGameData">The game data to use if creating a new room.</param>
/// <returns>The final GameData, either newly created or fetched from Firestore.</returns>
private async Task<GameData> HandleRoomCreationOrJoiningAsync(DocumentReference roomRef, GameData initialGameData)
{
    var snapshot = await roomRef.GetSnapshotAsync();

    if (!snapshot.Exists)
    {
        // Room does not exist, create it
        await roomRef.SetAsync(initialGameData);
        Debug.Log($"[{_buildConfiguration.buildType}] Room {roomRef.Id} created.");
        ActionHandler.OnRoomCreated?.Invoke();
        return initialGameData;
    }
    else
    {
        // Room exists, convert its data
        Debug.Log($"[{_buildConfiguration.buildType}] Joining existing room {roomRef.Id}");
        return snapshot.ConvertTo<GameData>();
    }
}

/// <summary>
/// Adds or updates the current user's data in the room's "players" subcollection.
/// </summary>
/// <param name="roomRef">The Firestore reference to the room document.</param>
/// <param name="user">The current Firebase user.</param>
private async Task AddPlayerToRoomSubcollectionAsync(DocumentReference roomRef, FirebaseUser user)
{
    DocumentReference playerRef = roomRef.Collection("players").Document(user.UserId);
    Dictionary<string, object> playerData = new Dictionary<string, object>
    {
        { "userId", user.UserId },
        { "name", user.DisplayName ?? "NoName" },
        { "email", user.Email ?? "NoEmail" }
    };

    await playerRef.SetAsync(playerData, SetOptions.MergeAll);
    Debug.Log($"Player {user.UserId} added to room {roomRef.Id}");
    ActionHandler.OnRoomJoined?.Invoke();
}

/// <summary>
/// Updates the player's profile in the global "players" collection.
/// </summary>
/// <param name="user">The current Firebase user.</param>
/// <param name="gameData">The game data containing room status.</param>
private async Task UpdateGlobalPlayerProfileAsync(FirebaseUser user, GameData gameData)
{
    DocumentReference globalPlayerRef = GetCollection("players").Document(user.UserId);
    PlayerData playerProfile = new PlayerData
    {
        playerId = user.UserId,
        playerName = user.DisplayName ?? "NoName",
        email = user.Email ?? "NoEmail",
        profilePic = user.PhotoUrl?.ToString(),
        roomDetails = new RoomDetails
        {
            isOpen = gameData.roomData.isOpen ? true : false,
            roomCreationTime = gameData.roomData.timerData.creationTime,
            loginSource = _signInManger.GetLoginSource(),
            roomID = gameData.roomData.roomID,
        },
        lastLoginTime = Timestamp.GetCurrentTimestamp(),
        currentBuildVersion = Application.version,
        lastBuildVersion = Application.version
    };

    await globalPlayerRef.SetAsync(playerProfile, SetOptions.MergeAll);
    Debug.Log($"Global player profile updated for {user.UserId}");
}

/// <summary>
/// Attaches a listener to the room's players subcollection to detect changes.
/// </summary>
/// <param name="roomId">The ID of the room to listen to.</param>
private void StartListeningForPlayerChanges(string roomId)
{
    ListenToRoomPlayers(roomId, players =>
    {
        Debug.Log($"Player list updated in {roomId}. Total players: {players.Count}");
        _uiManager.UpdateRoomPlayerList(players);
    });
}

   public async Task SaveRoomData(RoomData roomData, string playerId)
   {
       if (!isFirestoreInitialized) return;

       var roomsRef = GetCollection("rooms");
       var playersRef = GetCollection("players");

       // ✅ Wrap RoomData back into GameData for consistency
       GameData gameData = new GameData
       {
           roomData = roomData,
       };

       await roomsRef.Document(roomData.roomID).SetAsync(gameData, SetOptions.MergeAll);

       // Update player’s RoomID info
       var playerDoc = playersRef.Document(playerId);

       await db.RunTransactionAsync(async transaction =>
       {
           var snapshot = await transaction.GetSnapshotAsync(playerDoc);
           if (!snapshot.Exists) return;

           PlayerData player = snapshot.ConvertTo<PlayerData>();

           if (player.roomDetails == null)
               player.roomDetails = new RoomDetails();

           player.roomDetails.isOpen = roomData.isOpen ? true : false;
           player.roomDetails.loginSource = _signInManger.GetLoginSource();
           player.roomDetails.roomID = roomData.roomID;
           player.lastLoginTime = Timestamp.GetCurrentTimestamp();

           transaction.Set(playerDoc, player);
       });
   }
   
   // ---------------- CHECK AND RESUME SESSION ----------------
/// <summary>
/// Checks if the current player was in an active room and attempts to resume the session.
/// This should be called immediately after a user signs in.
/// </summary>
/// <returns>The GameData of the resumed room if successful, otherwise null.</returns>
public async Task<GameData> CheckAndResumePlayerSessionAsync()
{
    // 1. Pre-condition checks
    if (!IsReady(out FirebaseUser user)) return null;

    try
    {
        // 2. Get the player's last known roomID from their profile
        DocumentReference playerRef = GetCollection("players").Document(user.UserId);
        DocumentSnapshot playerSnapshot = await playerRef.GetSnapshotAsync();

        if (!playerSnapshot.Exists)
        {
            Debug.Log("Player profile not found. Cannot resume session.");
            return null;
        }

        PlayerData playerData = playerSnapshot.ConvertTo<PlayerData>();
        string lastRoomId = playerData.roomDetails?.roomID;

        if (string.IsNullOrEmpty(lastRoomId))
        {
            Debug.Log("Player was not in a room. No session to resume.");
            return null;
        }

        // 3. Fetch the room document using the retrieved roomID
        Debug.Log($"Found last room ID: {lastRoomId}. Checking its status...");
        DocumentReference roomRef = GetCollection("rooms").Document(lastRoomId);
        DocumentSnapshot roomSnapshot = await roomRef.GetSnapshotAsync();

        if (!roomSnapshot.Exists)
        {
            Debug.LogWarning($"Room {lastRoomId} no longer exists. Clearing from player profile.");
            // Optional: Clean up the player's stale roomID here
            await playerRef.UpdateAsync("roomDetails.roomID", FieldValue.Delete);
            return null;
        }

        GameData gameData = roomSnapshot.ConvertTo<GameData>();

        // 4. Check if the room is still marked as open
        if (!gameData.roomData.isOpen)
        {
            Debug.Log($"Room {lastRoomId} is already closed. Cannot resume.");
            return null;
        }

        // 5. CRITICAL: Check the server-side expiration time against the current time
        Timestamp expirationTime = gameData.roomData.timerData.expirationTime;
        if (Timestamp.GetCurrentTimestamp().ToDateTime() > expirationTime.ToDateTime())
        {
            Debug.Log($"Room {lastRoomId} has expired. Closing it now.");
            // The room should be closed. Call your existing CloseRoom method.
            CloseRoom(lastRoomId); // This is an async void method, but it's fine here.
            return null; // Return null because the session is invalid.
        }

        // 6. If all checks pass, the room is valid and unexpired.
        Debug.Log($"Resuming session in valid room: {lastRoomId}");
        return gameData; // Success! Return the game data to be used for re-initialization.
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error during session resumption check: {e.Message}");
        return null;
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

        var openRoomsQuery = GetCollection("rooms").WhereEqualTo("roomData.isOpen", true);
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
    CollectionReference playersRef = GetCollection("rooms").Document(roomId).Collection("players");
    DocumentReference roomRef = GetCollection("rooms").Document(roomId);

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
        var roomRef = GetCollection("rooms").Document(roomId);

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
        DocumentReference roomRef = GetCollection("rooms").Document(roomId);

        return roomRef.Listen(snapshot =>
        {
            if (!snapshot.Exists) return;

            GameData gameData = snapshot.ConvertTo<GameData>();
            var states = gameData.roomData.boardData?.buttonStates ?? new List<int> {0,0,0,0};
            onBoardChanged?.Invoke(states);
        });
    }
    

    
    // In FirestoreManager.cs

// ---------------- CLOSE ROOM ----------------
public async Task CloseRoom(string roomId)
{
    if (!isFirestoreInitialized)
    {
        Debug.LogError("Firestore not initialized");
        return;
    }

    DocumentReference roomRef = GetCollection("rooms").Document(roomId);

    try
    {
        // Step 1: Get the current room data to find the players
        DocumentSnapshot roomSnapshot = await roomRef.GetSnapshotAsync();
        if (!roomSnapshot.Exists)
        {
            Debug.LogWarning($"Tried to close a room that doesn't exist: {roomId}");
            return;
        }

        GameData gameData = roomSnapshot.ConvertTo<GameData>();
        string[] playerIDs = gameData.roomData.playerIDs;

        // Step 2: Update the room's status to closed
        await roomRef.UpdateAsync(new Dictionary<string, object>
        {
            { "roomData.isOpen", false }
        });
        Debug.Log($"Room {roomId} closed successfully.");
        
        // This is called on the main thread, so it's safe to update UI
        _uiManager.OnRoomClosed();

        // Step 3: Update the profile of each player in that room
        if (playerIDs != null && playerIDs.Length > 0)
        {
            foreach (string playerId in playerIDs)
            {
                if (string.IsNullOrEmpty(playerId)) continue;
                
                DocumentReference playerRef = GetCollection("players").Document(playerId);
                Dictionary<string, object> playerUpdates = new Dictionary<string, object>
                {
                    // Use dot notation to update a field in a map
                    { "roomDetails.isOpen", false },
                    // // It's also good practice to clear their current room ID
                    // { "roomDetails.roomID", FieldValue.Delete } 
                };

                await playerRef.UpdateAsync(playerUpdates);
                Debug.Log($"Updated player {playerId} profile: room status set to closed.");
            }
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Failed to close room and update players: {e.Message}");
    }
}
    
    
    public async void UpdateRemainingTime(string roomId, float remainingSeconds)
    {
        DocumentReference roomRef = GetCollection("rooms").Document(roomId);

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
        DocumentReference roomRef = GetCollection("rooms").Document(roomId);

        return roomRef.Listen(snapshot =>
        {
            if (!snapshot.Exists) return;
            GameData data = snapshot.ConvertTo<GameData>();
            onRoomDataChanged?.Invoke(data.roomData);
        });
    }
}

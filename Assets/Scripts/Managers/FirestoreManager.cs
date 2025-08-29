using System;
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

    public async Task AddPlayerToGlobalPlayerCollection(FirebaseUser user)
    {
        if (!isFirestoreInitialized) return;
        
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
   
   
   public async void LeaveRoom()
   {
       
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
// In FirestoreManager.cs
private async Task UpdateGlobalPlayerProfileAsync(FirebaseUser user, GameData gameData)
{
    DocumentReference globalPlayerRef = GetCollection("players").Document(user.UserId);

    // ✅ Build a dictionary of specific fields to update safely.
    var updates = new Dictionary<string, object>
    {
        { "playerName", user.DisplayName ?? "NoName" },
        { "email", user.Email ?? "NoEmail" },
        { "profilePic", user.PhotoUrl?.ToString() },
        { "roomID", gameData.roomData.roomID },
        { "loginSource", _signInManger.GetLoginSource() },
        { "lastLoginTime", Timestamp.GetCurrentTimestamp() },
        { "currentBuildVersion", Application.version }
        // We intentionally do NOT touch matchStats or other sensitive data here.
    };

    // Use the safe UpdateAsync command.
    await globalPlayerRef.UpdateAsync(updates);

    Debug.Log($"Global player profile safely updated for {user.UserId}");
}


// In FirestoreManager.cs, add this new private method.

/// <summary>
/// Updates the match statistics for both the winner and the loser of a game.
/// </summary>
/// <param name="winnerId">The ID of the winning player.</param>
/// <param name="loserId">The ID of the losing player.</param>
/// <param name="isDraw">Set to true if the match was a draw.</param>
private async Task UpdatePlayerStatsAsync(string winnerId, string loserId, bool isDraw = false)
{
    if (!isFirestoreInitialized) return;

    var playersCollection = GetCollection("players");
    var batch = db.StartBatch();

    // --- Update Winner ---
    var winnerRef = playersCollection.Document(winnerId);
    var winnerUpdate = new Dictionary<string, object>
    {
        // Use dot notation to target the specific field inside the 'matchStats' map
        { "matchStats.totalMatchesOnline", FieldValue.Increment(1) },
        
        { "matchStats.totalWinsOnline", isDraw ? FieldValue.Increment(0) : FieldValue.Increment(1) }
    };
    batch.Update(winnerRef, winnerUpdate);

    // --- Update Loser ---
    var loserRef = playersCollection.Document(loserId);
    var loserUpdate = new Dictionary<string, object>
    {
        // This only increments the match count, leaving the win count untouched
        { "matchStats.totalMatchesOnline", FieldValue.Increment(1) }
    };
    batch.Update(loserRef, loserUpdate);

    // Commit both updates together
    Debug.Log($"Committing stats update. Winner: {winnerId}, Loser: {loserId}");
    await batch.CommitAsync();
}

/// <summary>
/// Creates a player profile if one doesn't exist, or updates key details upon login.
/// This is called immediately after any successful authentication.
/// </summary>
/// <param name="user">The authenticated Firebase user.</param>
// In FirestoreManager.cs
public async Task CreateOrUpdatePlayerProfileOnLoginAsync(FirebaseUser user)
{
    if (!isFirestoreInitialized || user == null)
    {
        Debug.LogError("Firestore not ready or user is null. Cannot update profile.");
        return;
    }

    DocumentReference globalPlayerRef = GetCollection("players").Document(user.UserId);
    DocumentSnapshot snapshot = await globalPlayerRef.GetSnapshotAsync();

    if (!snapshot.Exists)
    {
        // THIS IS A NEW USER - SetAsync is correct here because we create the whole document.
        var newProfile = new PlayerData
        {
            playerId = user.UserId,
            playerName = user.DisplayName ?? "NoName",
            email = user.Email ?? "NoEmail",
            profilePic = user.PhotoUrl?.ToString(),
            lastLoginTime = Timestamp.GetCurrentTimestamp(),
            currentBuildVersion = Application.version,
            lastBuildVersion = Application.version,
            loginSource = _signInManger.GetLoginSource(),
            roomID = string.Empty,
            scores = new Scores { score = 0 },
            matchStats = new Matches
            {
                totalMatchesOnline = 0,
                totalWinsOnline = 0,
                totalMatchesAI = 0,
                totalWinesAI = 0
            },
            // Corrected property name from previous discussions
            xpLevel = new XPLevel { XPPoints = 0, XPTag = "Rookie" }
        };

        await globalPlayerRef.SetAsync(newProfile);
        Debug.Log($"NEW player profile created for {user.UserId}");
    }
    else
    {
        // --- ✅ THIS IS THE FIX FOR EXISTING USERS ---
        // We now build a dictionary and use UpdateAsync for a safe, targeted update.
        var updates = new Dictionary<string, object>
        {
            { "playerName", user.DisplayName ?? "NoName" },
            { "profilePic", user.PhotoUrl?.ToString() },
            { "lastLoginTime", Timestamp.GetCurrentTimestamp() },
            { "currentBuildVersion", Application.version },
        };

        // Also check if matchStats needs to be initialized
        PlayerData existingProfile = snapshot.ConvertTo<PlayerData>();
        if (existingProfile.matchStats == null)
        {
            Debug.Log($"Player {user.UserId} is missing matchStats. Initializing now.");
            updates["matchStats"] = new Matches
            {
                totalMatchesOnline = 0,
                totalWinsOnline = 0,
                totalMatchesAI = 0,
                totalWinesAI = 0
            };
        }

        await globalPlayerRef.UpdateAsync(updates);
        Debug.Log($"EXISTING player profile safely updated for {user.UserId}");
    }
}

/// <summary>
/// Atomically updates a player's score within a room using a transaction.
/// </summary>
/// <param name="roomId">The ID of the room.</param>
/// <param name="scoreDelta">The amount to add to the score (e.g., 1 or -1).</param>
public async Task UpdatePlayerScoreAsync(string roomId, int scoreDelta)
{
    if (!IsReady(out FirebaseUser user)) return;
    
    try
    {
        DocumentReference roomRef = GetCollection("rooms").Document(roomId);
        DocumentSnapshot snapshot = await roomRef.GetSnapshotAsync();
        
        if (!snapshot.Exists) return;
        
        GameData gameData = snapshot.ConvertTo<GameData>();
        int playerIndex = Array.IndexOf(gameData.roomData.playerIDs, user.UserId);
        
        string scorePath = playerIndex == 0 ? 
            "roomData.playerScore.player1Score" : 
            "roomData.playerScore.player2Score";
            
        await roomRef.UpdateAsync(scorePath, FieldValue.Increment(scoreDelta));
    }
    catch (Exception e)
    {
        Debug.LogError($"Score update error: {e.Message}");
    }
}

/// <summary>
/// Clears the stale room ID from the current player's profile.
/// </summary>
/// <param name="playerRef">The DocumentReference for the player to update.</param>
private async Task ClearStaleRoomDataFromPlayerProfile(DocumentReference playerRef)
{
    var updates = new Dictionary<string, object>
    {
        // Set the roomID field back to an empty string.
        // This is safer than deleting the field if your PlayerData class expects it.
        { "roomID", string.Empty } 
    };
    await playerRef.UpdateAsync(updates);
    Debug.Log($"Cleared stale room data for player {playerRef.Id}.");
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

           // if (player.roomDetails == null)
           //     player.roomDetails = new RoomDetails();
           if(player.roomID == null)
                player.roomID = roomData.roomID;

           // player.roomDetails.isOpen = roomData.isOpen ? true : false;
           // player.roomDetails.loginSource = _signInManger.GetLoginSource();
           // player.roomDetails.roomID = roomData.roomID;
           player.roomID = roomData.roomID;
           player.loginSource = _signInManger.GetLoginSource();
           player.lastLoginTime = Timestamp.GetCurrentTimestamp();

           transaction.Set(playerDoc, player);
       });
   }
   
// ---------------- CHECK AND RESUME SESSION ----------------
/// <summary>
/// Checks if the current player was in an active room and attempts to resume the session.
/// This also handles cleanup for expired or closed rooms.
/// This should be called immediately after a user signs in.
/// </summary>
/// <returns>The GameData of the resumed room if successful, otherwise null.</returns>
public async Task<GameData> CheckAndResumePlayerSessionAsync()
{
    // 1. Pre-condition checks
    if (!IsReady(out FirebaseUser user)) return null;

    DocumentReference playerRef = GetCollection("players").Document(user.UserId);

    try
    {
        // 2. Get the player's profile
        DocumentSnapshot playerSnapshot = await playerRef.GetSnapshotAsync();
        if (!playerSnapshot.Exists)
        {
            Debug.Log("Player profile not found. Cannot resume session.");
            return null;
        }

        PlayerData playerData = playerSnapshot.ConvertTo<PlayerData>();
        string lastRoomId = playerData.roomID;

        // If the player has no roomID, they are in a clean state. Nothing to do.
        if (string.IsNullOrEmpty(lastRoomId))
        {
            Debug.Log("Player was not in a room. No session to resume.");
            return null;
        }

        // 3. Player has a roomID, so we must investigate the room's status.
        Debug.Log($"Player profile indicates they were in room: {lastRoomId}. Checking its status...");
        DocumentReference roomRef = GetCollection("rooms").Document(lastRoomId);
        DocumentSnapshot roomSnapshot = await roomRef.GetSnapshotAsync();

        // --- Scenario 1: The room document was deleted or never existed. ---
        // The player has a stale roomID. Clean it up.
        if (!roomSnapshot.Exists)
        {
            Debug.LogWarning($"Room {lastRoomId} no longer exists. Cleaning up player profile.");
            await ClearStaleRoomDataFromPlayerProfile(playerRef);
            return null;
        }

        GameData gameData = roomSnapshot.ConvertTo<GameData>();

        // --- Scenario 2: The room has EXPIRED. (This is Player 1's case) ---
        // This player is the first to return to an expired room. They are responsible for closing it.
        Timestamp expirationTime = gameData.roomData.timerData.expirationTime;
        if (expirationTime != null && Timestamp.GetCurrentTimestamp().ToDateTime() > expirationTime.ToDateTime())
        {
            Debug.Log($"Room {lastRoomId} has expired. This player will mark it as closed.");
            // Action: Close the room
            await roomRef.UpdateAsync("roomData.roomProperties.roomStatus", false);
            // Action: Clean up this player's profile
            await ClearStaleRoomDataFromPlayerProfile(playerRef);
            return null; // Session is invalid.
        }
        
        // --- Scenario 3: The room is ALREADY CLOSED. (This is Player 2's case) ---
        // Another player (or the server) has already marked this room as closed.
        if (!gameData.roomData.roomProperties.roomStatus)
        {
            Debug.Log($"Room {lastRoomId} is already closed. Cleaning up this player's profile.");
            // Action: Just clean up this player's stale data
            await ClearStaleRoomDataFromPlayerProfile(playerRef); 
            return null;
        }

        // --- SUCCESS ---
        // If all checks pass, the room is valid, unexpired, and open.
        Debug.Log($"SUCCESS: Resuming session in valid room: {lastRoomId}");
        return gameData;
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error during session resumption check: {e.Message}");
        // As a safety net, try to clean up the player's state if an error occurs
        await ClearStaleRoomDataFromPlayerProfile(playerRef);
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

        var openRoomsQuery = GetCollection("rooms").WhereEqualTo("roomData.roomProperties.roomStatus", true);
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
    
    

    
    /// <summary>
    /// Handles the process for a player leaving a room.
    /// This involves removing them from the room's player list,
    /// closing the room, and cleaning up profiles.
    /// </summary>
    public async Task LeaveRoomAsync()
{
    if (!IsReady(out FirebaseUser leavingUser)) return;

    DocumentReference playerProfileRef = GetCollection("players").Document(leavingUser.UserId);
    string roomId = null;

    try
    {
        // Get the player's profile to find their current room ID
        DocumentSnapshot playerSnapshot = await playerProfileRef.GetSnapshotAsync();
        if (playerSnapshot.Exists)
        {
            roomId = playerSnapshot.ConvertTo<PlayerData>().roomID;
        }

        if (string.IsNullOrEmpty(roomId))
        {
            Debug.LogWarning($"Player {leavingUser.UserId} is not in a room, cannot leave.");
            return;
        }

        Debug.Log($"Player {leavingUser.UserId} is leaving room {roomId}.");
        DocumentReference roomRef = GetCollection("rooms").Document(roomId);

        // --- NEW LOGIC: FIND REMAINING PLAYER AND AWARD WIN ---
        DocumentSnapshot roomSnapshot = await roomRef.GetSnapshotAsync();
        if (roomSnapshot.Exists)
        {
            GameData gameData = roomSnapshot.ConvertTo<GameData>();
            if (gameData.roomData.playerIDs != null && gameData.roomData.playerIDs.Length == 2)
            {
                // Find the ID of the player who is NOT leaving
                string remainingPlayerId = gameData.roomData.playerIDs.FirstOrDefault(id => id != leavingUser.UserId);

                if (!string.IsNullOrEmpty(remainingPlayerId))
                {
                    // The remaining player is the winner, the leaver is the loser.
                    await UpdatePlayerStatsAsync(winnerId: remainingPlayerId, loserId: leavingUser.UserId);
                }
            }
        }
        // --- END OF NEW LOGIC ---

        // Now, close the room for everyone. Our modified CloseRoom will handle cleanup.
        // We call CloseRoom instead of duplicating the cleanup logic here.
        await CloseRoom(roomId, processWinLoss: false); 
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error during LeaveRoom process: {e.Message}");
        // As a failsafe, still try to clear the player's room data
        await ClearStaleRoomDataFromPlayerProfile(playerProfileRef);
    }
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
public async Task CloseRoom(string roomId, bool processWinLoss = true)
{
    if (!isFirestoreInitialized)
    {
        Debug.LogError("Firestore not initialized");
        return;
    }

    DocumentReference roomRef = GetCollection("rooms").Document(roomId);

    try
    {
        DocumentSnapshot roomSnapshot = await roomRef.GetSnapshotAsync();
        if (!roomSnapshot.Exists)
        {
            Debug.LogWarning($"Tried to close a room that doesn't exist: {roomId}");
            return;
        }

        GameData gameData = roomSnapshot.ConvertTo<GameData>();
        
        // ✅ --- NEW WRAPPER --- ✅
        // Only process winner stats if instructed to.
        if (processWinLoss)
        {
            Debug.Log("Processing win/loss stats based on score...");
            if (gameData.roomData.playerIDs != null && gameData.roomData.playerIDs.Length == 2)
            {
                string player1Id = gameData.roomData.playerIDs[0];
                string player2Id = gameData.roomData.playerIDs[1];
                int p1Score = gameData.roomData.playerScore.player1Score;
                int p2Score = gameData.roomData.playerScore.player2Score;

                if (p1Score > p2Score)
                {
                    await UpdatePlayerStatsAsync(winnerId: player1Id, loserId: player2Id);
                }
                else if (p2Score > p1Score)
                {
                    await UpdatePlayerStatsAsync(winnerId: player2Id, loserId: player1Id);
                }
                else // It's a draw
                {
                    await UpdatePlayerStatsAsync(winnerId: player1Id, loserId: player2Id, isDraw: true);
                }
            }
        }
        // ✅ --- END OF WRAPPER --- ✅

        // The rest of the cleanup logic runs every time.
        await roomRef.UpdateAsync("roomData.roomProperties.roomStatus", false);
        Debug.Log($"Room {roomId} marked as closed.");
        _uiManager.OnRoomClosed();

        List<string> playerIDsInRoom = gameData.roomData.playerIDs?.ToList() ?? new List<string>();
        if (playerIDsInRoom.Count > 0)
        {
            foreach (string playerId in playerIDsInRoom)
            {
                if (string.IsNullOrEmpty(playerId)) continue;
                DocumentReference playerRef = GetCollection("players").Document(playerId);
                await ClearStaleRoomDataFromPlayerProfile(playerRef);
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

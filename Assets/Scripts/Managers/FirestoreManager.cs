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
    public async Task CreateOrJoinRoom(string roomId)
    {
        // Check if Firestore is initialized
        if (!isFirestoreInitialized)
        {
            Debug.LogError("Firestore not initialized");
            return;
        }
        Debug.Log("Inside CreateOrJoinRoom");
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        Debug.Log("User: " + user.DisplayName);


        try
        {
            var roomRef = db.Collection("rooms").Document(roomId);
            Debug.Log("RoomRef: " + roomRef.Path);
            var snapshot = await roomRef.GetSnapshotAsync();
            Debug.Log("Snapshot: " + snapshot.Exists);

            if (!snapshot.Exists)
            {
                Debug.Log("Inside snapshot.Exist");
                // Create room if it doesn't exist
                Dictionary<string, object> roomData = new Dictionary<string, object>
                {
                    { "isOpen", true },
                    { "createdAt", Timestamp.GetCurrentTimestamp() }
                };
                await roomRef.SetAsync(roomData);
                Debug.Log($"Room {roomId} created.");
                ActionHandler.OnRoomCreated?.Invoke();
            }
            else
            {
                Debug.Log($"Joining existing room {roomId}");
            }

            // Add player to subcollection "players"
            DocumentReference playerRef = roomRef.Collection("players").Document(user.UserId);
            Dictionary<string, object> playerData = new Dictionary<string, object>
            {
                { "name", user.DisplayName ?? "NoName" },
                { "email", user.Email ?? "NoEmail" }
            };

            await playerRef.SetAsync(playerData, SetOptions.MergeAll);
            Debug.Log($"Player {user.UserId} added to room {roomId}");
            ActionHandler.OnRoomJoined?.Invoke();
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
        if (user == null) { Debug.LogError("No user logged in"); return; }

        var openRoomsQuery = db.Collection("rooms").WhereEqualTo("isOpen", true);
        Debug.Log("Open rooms query:" + openRoomsQuery.ToString());
        var querySnapshot = await openRoomsQuery.GetSnapshotAsync();
        Debug.Log("Query snapshot" + querySnapshot.ToString());

        if (querySnapshot.Count == 0)
        {
            Debug.Log("No open rooms available.");
            return;
        }

        DocumentSnapshot roomSnapshot = querySnapshot.Documents.ToList()[0]; // Pick first open room
        string roomId = roomSnapshot.Id;

        Debug.Log($"Found open room: {roomId}");
        await CreateOrJoinRoom(roomId);
    }

    // ---------------- LISTEN TO ROOM PLAYERS ----------------
    public ListenerRegistration ListenToRoomPlayers(string roomId, System.Action<List<Dictionary<string, object>>> onPlayersChanged)
    {
        CollectionReference playersRef = db.Collection("rooms").Document(roomId).Collection("players");

        return playersRef.Listen(snapshot =>
        {
            List<Dictionary<string, object>> players = new List<Dictionary<string, object>>();
            foreach (var doc in snapshot.Documents)
            {
                players.Add(doc.ToDictionary());
            }

            onPlayersChanged?.Invoke(players);
        });
    }
}

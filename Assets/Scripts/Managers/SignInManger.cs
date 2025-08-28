using Firebase;
using Firebase.Auth;
using Google;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BackendDev;
using Firebase.Extensions;
using System.Security.Cryptography;
using System.Text;
using Firebase.Firestore;

#if UNITY_IOS
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Native;
#endif

[DefaultExecutionOrder(-1)]
public class SignInManger : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button googleSignInButton;
    [SerializeField] private Button appleSignInButton; // Keep assigned in the scene; we’ll hide/show it at runtime
    [SerializeField] private TMP_Text emailText;        // Shows name/email
    [SerializeField] private TMP_Text userIdText;       // Shows UID
    
    [SerializeField] private UiManager _uiManager;
    [SerializeField] private FirestoreManager firestoreManager;

    private FirebaseAuth auth;
    public FirebaseFirestore db;
    private GoogleSignInConfiguration googleConfig;
    private bool firebaseReady;
    private bool isSilentLoginInProgress = false;


#if UNITY_IOS
    private IAppleAuthManager appleAuthManager;
#endif

    void Awake()
    {
        // Runtime platform toggle
        bool isAndroid = Application.platform == RuntimePlatform.Android;
        bool isiOS     = Application.platform == RuntimePlatform.IPhonePlayer;

        if (googleSignInButton != null) googleSignInButton.gameObject.SetActive(true);
        if (appleSignInButton != null) appleSignInButton.gameObject.SetActive(isiOS);
    }

    void Start()
    {
        // Initialize Firebase
        InitializeFirebase();
    }
    
    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                db = FirebaseFirestore.DefaultInstance;
                firebaseReady = true;
                Debug.Log("Firebase ready : " + auth);
                Debug.Log("Firestore ready : " + db);

                // Try silent login first
                AttemptSilentLogin();
            }
            else
            {
                Debug.LogError("Could not resolve Firebase dependencies: " + task.Result);
            }
        });

        // Configure Google Sign-In
        googleConfig = new GoogleSignInConfiguration
        {
            WebClientId   = "880483783716-du3uefpp83id86u9943t96a38uikhlp4.apps.googleusercontent.com",
            RequestIdToken = true,
            RequestEmail  = true,
            RequestProfile = true,
            AccountName = null
        };

        // Hook buttons
        if (googleSignInButton != null)
            googleSignInButton.onClick.AddListener(SignInWithGoogle);

#if UNITY_IOS
        // Apple: only set up on iOS build
        if (AppleAuthManager.IsCurrentPlatformSupported)
        {
            appleAuthManager = new AppleAuthManager(new PayloadDeserializer());
            if (appleSignInButton != null)
                appleSignInButton.onClick.AddListener(SignInWithApple);
        }
        else
        {
            if (appleSignInButton != null) appleSignInButton.gameObject.SetActive(false);
        }
#else
        // Not iOS → ensure Apple button hidden
        if (appleSignInButton != null) appleSignInButton.gameObject.SetActive(false);
#endif
    }

    // ---------------- SILENT LOGIN ----------------
    private void AttemptSilentLogin()
    {
        if (!firebaseReady) 
        {
            Debug.LogError("Firebase not ready for silent login");
            return;
        }

        isSilentLoginInProgress = true;

        FirebaseUser currentUser = auth.CurrentUser;

        if (currentUser != null)
        {
            Debug.Log("Silent login found FirebaseAuth user: " + currentUser.UserId);

            // 🔎 Verify Firestore profile exists
            db.Collection("users").Document(currentUser.UserId).GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError("Firestore check failed: " + task.Exception);
                    ShowSignInUI();
                    isSilentLoginInProgress = false;
                    return;
                }

                DocumentSnapshot snapshot = task.Result;
                if (snapshot.Exists)
                {
                    Debug.Log("Firestore profile found, proceeding with login.");
                    OnSignedIn(currentUser);
                }
                else
                {
                    Debug.LogWarning("⚠ No Firestore profile for this user. Forcing re-sign-in.");
                    auth.SignOut();
                    ShowSignInUI();
                }

                isSilentLoginInProgress = false;
            });
        }
        else
        {
            Debug.Log("No previously signed-in FirebaseAuth user.");
#if UNITY_EDITOR
            SignInWithTestAccountInEditor();
#else
        ShowSignInUI();
#endif
            isSilentLoginInProgress = false;
        }
    }

    
    private void ShowSignInUI()
    {
        // Show your sign-in buttons or UI here
        if (googleSignInButton != null) googleSignInButton.gameObject.SetActive(true);
#if UNITY_IOS
        if (appleSignInButton != null) appleSignInButton.gameObject.SetActive(true);
#endif
        
        Debug.Log("Please sign in manually");
    }
    
    // ---------------- EDITOR TEST AUTHENTICATION ----------------
#if UNITY_EDITOR
    private void SignInWithTestAccountInEditor()
    {
        if (!firebaseReady)
        {
            Debug.LogError("Firebase not ready for editor sign-in");
            return;
        }

        // Use a test email and password (you might want to create this user in your Firebase project)
        string testEmail = "test@example.com";
        string testPassword = "test12356";
        
        auth.SignInWithEmailAndPasswordAsync(testEmail, testPassword).ContinueWithOnMainThread(authTask =>
        {
            if (authTask.IsFaulted || authTask.IsCanceled)
            {
                // If the user doesn't exist, create it
                auth.CreateUserWithEmailAndPasswordAsync(testEmail, testPassword).ContinueWithOnMainThread(createTask =>
                {
                    if (createTask.IsFaulted || createTask.IsCanceled)
                    {
                        Debug.LogError("Editor test account creation failed: " + createTask.Exception);
                        ShowSignInUI();
                        return;
                    }
                    
                    FirebaseUser user = createTask.Result.User;
                    Debug.Log("Editor test account created and signed in: " + user.UserId);
                    OnSignedIn(user);
                });
                return;
            }

            FirebaseUser user = authTask.Result.User;
            Debug.Log("Editor test account sign-in successful: " + user.UserId);
            OnSignedIn(user);
        });
    }
#endif

    void Update()
    {
#if UNITY_IOS
        // Required per Apple plugin
        appleAuthManager?.Update();
#endif
    }
    // ---------------- GOOGLE ----------------
    public void SignInWithGoogle()
    {
        if (!firebaseReady) { Debug.LogError("Firebase not ready"); return; }

        GoogleSignIn.Configuration = googleConfig;
        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnGoogleAuthFinished);
    }

    private void OnGoogleAuthFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted || task.IsCanceled)
        {
            Debug.LogError("Google Sign-In failed: " + task.Exception);
            return;
        }

        Credential cred = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
        SignInWithFirebase(cred);
    }

    // ---------------- APPLE (iOS only) ----------------
#if UNITY_IOS
    private void SignInWithApple()
    {
        if (!firebaseReady) { Debug.LogError("Firebase not ready"); return; }

        // 1. Generate a secure random nonce
        string rawNonce = GenerateRandomNonce(32);
        string hashedNonce = Sha256(rawNonce);

        // 2. Request Apple sign-in with the hashed nonce
        var loginArgs = new AppleAuthLoginArgs(
            LoginOptions.IncludeEmail | LoginOptions.IncludeFullName,
            hashedNonce
        );

        appleAuthManager.LoginWithAppleId(
            loginArgs,
            credential =>
            {
                var appleIdCredential = credential as IAppleIDCredential;
                if (appleIdCredential == null)
                {
                    Debug.LogError("Apple credential cast failed");
                    return;
                }

                // 3. Convert token bytes → string
                string idToken = Encoding.UTF8.GetString(appleIdCredential.IdentityToken);

                // 4. Build Firebase OAuth credential using the *raw nonce*
                Credential firebaseCred =
                    OAuthProvider.GetCredential("apple.com", idToken, rawNonce, null);

                SignInWithFirebase(firebaseCred);
            },
            error =>
            {
                Debug.LogError("Apple Sign-In failed: " + error);
            });
    }

    private string GenerateRandomNonce(int length)
    {
        const string charset = "0123456789ABCDEFGHIJKLMNOPQRSTUVXYZabcdefghijklmnopqrstuvwxyz-._";
        var bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = charset[bytes[i] % charset.Length];
        }
        return new string(chars);
    }

    private string Sha256(string input)
    {
        using (var sha = SHA256.Create())
        {
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }



#endif

    // ---------------- FIREBASE (common) ----------------
    private void SignInWithFirebase(Credential credential)
    {
        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask =>
        {
            if (authTask.IsFaulted || authTask.IsCanceled)
            {
                Debug.LogError("Firebase sign-in failed: " + authTask.Exception);
                return;
            }

            FirebaseUser user = authTask.Result;
            OnSignedIn(user);
        });
    }

    private void OnSignedIn(FirebaseUser user)
    {
        Debug.Log($"Signed in: {user.DisplayName} | {user.Email} | UID: {user.UserId}");

        if (_uiManager == null)
        {
            Debug.LogError("❌ _uiManager is NULL, check inspector assignment!");
        }
        else
        {
            try
            {
                _uiManager.UpdateUiAfterLogin(user);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Crash in UpdateUiAfterLogin: " + e);
            }
        }

        if (firestoreManager == null)
        {
            Debug.LogError("❌ firestoreManager is NULL, check inspector assignment!");
        }
        else
        {
            try
            {
                firestoreManager.InitializeWithFirestore(db);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Crash in FirestoreManager init: " + e);
            }
        }
    }

}

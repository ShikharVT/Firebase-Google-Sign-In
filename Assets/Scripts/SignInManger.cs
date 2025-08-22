using Firebase;
using Firebase.Auth;
using Google;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_IOS
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Interfaces;
using AppleAuth.Native;
#endif

public class SignInManger : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button googleSignInButton;
    [SerializeField] private Button appleSignInButton; // Keep assigned in the scene; we’ll hide/show it at runtime
    [SerializeField] private TMP_Text emailText;        // Shows name/email
    [SerializeField] private TMP_Text userIdText;       // Shows UID

    private FirebaseAuth auth;
    private GoogleSignInConfiguration googleConfig;
    private bool firebaseReady;

#if UNITY_IOS
    private IAppleAuthManager appleAuthManager;
#endif

    void Awake()
    {
        // Runtime platform toggle (works on device builds)
        bool isAndroid = Application.platform == RuntimePlatform.Android;
        bool isiOS     = Application.platform == RuntimePlatform.IPhonePlayer;

        // Google button is used on both
        if (googleSignInButton != null) googleSignInButton.gameObject.SetActive(true);

        // Apple button only on iOS
        if (appleSignInButton != null) appleSignInButton.gameObject.SetActive(isiOS);
    }

    void Start()
    {
        // 1) Firebase init
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                firebaseReady = true;
                Debug.Log("Firebase ready");
            }
            else
            {
                Debug.LogError("Could not resolve Firebase dependencies: " + task.Result);
            }
        });

        // 2) Google config
        googleConfig = new GoogleSignInConfiguration
        {
            WebClientId   = "880483783716-du3uefpp83id86u9943t96a38uikhlp4.apps.googleusercontent.com",
            RequestIdToken = true
        };

        // 3) Hook buttons
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

        var loginArgs = new AppleAuthLoginArgs(LoginOptions.IncludeEmail | LoginOptions.IncludeFullName);

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

                // Convert token bytes → string
                string idToken = System.Text.Encoding.UTF8.GetString(appleIdCredential.IdentityToken);

                // (Best practice: include a cryptographic nonce; omitted here for brevity.)
                Credential firebaseCred = OAuthProvider.GetCredential("apple.com", idToken, null, null);
                SignInWithFirebase(firebaseCred);
            },
            error =>
            {
                Debug.LogError("Apple Sign-In failed: " + error);
            });
    }
#endif

    // ---------------- FIREBASE (common) ----------------
    private void SignInWithFirebase(Credential credential)
    {
        auth.SignInWithCredentialAsync(credential).ContinueWith(authTask =>
        {
            if (authTask.IsFaulted || authTask.IsCanceled)
            {
                Debug.LogError("Firebase sign-in failed: " + authTask.Exception);
                return;
            }

            FirebaseUser user = authTask.Result;
            Debug.Log($"Signed in: {user.DisplayName} | {user.Email} | UID: {user.UserId}");
            UpdateUI(user);
        });
    }

    private void UpdateUI(FirebaseUser user)
    {
        emailText.text = $"Name: {user.DisplayName}\nEmail: {user.Email}";
        userIdText.text = $"UID: {user.UserId}";
    }
}

using Firebase;
using Firebase.Auth;
using Google;
using System.Threading.Tasks;
using PimDeWitte.UnityMainThreadDispatcher;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoogleAuthManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] Button signInButton;
    [SerializeField] TMP_Text emailText;   // To show email
    [SerializeField] TMP_Text userIdText;  // To show UID

    private FirebaseAuth auth;
    private GoogleSignInConfiguration config;

    void Start()
    {
        // Firebase init
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            if (task.Result == DependencyStatus.Available)
                auth = FirebaseAuth.DefaultInstance;
        });

        // Google Sign-In config
        config = new GoogleSignInConfiguration
        {
            WebClientId = "880483783716-du3uefpp83id86u9943t96a38uikhlp4.apps.googleusercontent.com",
            RequestIdToken = true
        };

        // Hook button
        signInButton.onClick.AddListener(SignInWithGoogle);
    }

    public void SignInWithGoogle()
    {
        GoogleSignIn.Configuration = config;
        GoogleSignIn.DefaultInstance.SignIn().ContinueWith(OnGoogleAuthFinished);
    }

    private void OnGoogleAuthFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted || task.IsCanceled)
        {
            Debug.LogError("Google Sign-In failed: " + task.Exception);
            return;
        }

        // Get credential and login with Firebase
        Credential credential = GoogleAuthProvider.GetCredential(task.Result.IdToken, null);
        auth.SignInWithCredentialAsync(credential).ContinueWith(authTask =>
        {
            if (authTask.IsFaulted || authTask.IsCanceled)
            {
                Debug.LogError("Firebase sign-in failed: " + authTask.Exception);
                return;
            }

            FirebaseUser user = authTask.Result;
            Debug.Log($"Signed in as: {user.DisplayName}, {user.Email}, UID: {user.UserId}");

            // Update UI
            UpdateUI(user);
        });
    }

    private void UpdateUI(FirebaseUser user)
    {
        
            Debug.Log($"Inside Update UI at start");
            emailText.text = "Name " + user.DisplayName;
            userIdText.text = "UID: " + user.UserId;
            Debug.Log($"Inside Update UI at end");
        
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using Firebase.Extensions;
using Google;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase.Auth;

public class LoginWithGoogle : MonoBehaviour
{
    public string GoogleAPI = "242650185588-l2mv2ak7m1kf8e2c9ll9uhf37c1mplp3.apps.googleusercontent.com";
    private GoogleSignInConfiguration configuration;
    private Firebase.Auth.FirebaseAuth auth;
    private bool isGoogleSignInInitialized = false;
    private bool isFirstLaunch = true;
    public GameObject loadingPanel; // Optional: Assign a loading spinner/panel in the inspector. Can be left null if initialization is fast enough.
    public UnityEngine.UI.Button loginButton; // Assign your sign-in button in the inspector
    private bool isInitialized = false;

    private void Awake()
    {
        // Check if this is the first launch
        isFirstLaunch = !PlayerPrefs.HasKey("HasLaunchedBefore");
        if (isFirstLaunch)
        {
            // We'll sign out AFTER initialization is complete, not here
            PlayerPrefs.SetInt("HasLaunchedBefore", 1);
            PlayerPrefs.Save();
        }
    }

    private IEnumerator Start()
    {
        // Disable sign-in button and show loading panel
        if (loginButton != null) loginButton.interactable = false;
        if (loadingPanel != null) loadingPanel.SetActive(true);
        
        Debug.Log("[LOGIN] Starting initialization process...");
        isInitialized = false;

        // Step 1: Initialize Firebase properly with dependency check
        Debug.Log("[LOGIN] Starting Firebase initialization...");
        var dependencyTask = Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => dependencyTask.IsCompleted);

        if (dependencyTask.Result != Firebase.DependencyStatus.Available)
        {
            Debug.LogError($"[LOGIN] Firebase dependencies not available: {dependencyTask.Result}");
            ShowLoginError("Could not initialize Firebase. Please restart the app.");
            yield break;
        }

        Debug.Log("[LOGIN] Firebase dependencies are available.");
        
        // Step 2: Initialize Firebase Auth
        try
        {
            auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            Debug.Log("[LOGIN] Firebase Auth initialized successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LOGIN] Firebase Auth initialization error: {ex.Message}");
            ShowLoginError("Firebase initialization error. Please restart the app.");
            yield break;
        }
        
        // Step 3: Initialize Google Sign-In
        try
        {
            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                RequestIdToken = true,
                WebClientId = GoogleAPI
            };
            isGoogleSignInInitialized = true;
            Debug.Log("[LOGIN] Google Sign-In configured successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LOGIN] Google Sign-In configuration error: {ex.Message}");
            ShowLoginError("Google Sign-In initialization error. Please restart the app.");
            yield break;
        }
        
        // Step 4: NOW it's safe to sign out if first launch (after SDKs are initialized)
        if (isFirstLaunch)
        {
            Debug.Log("[LOGIN] First launch detected, signing out from previous sessions...");
            try
            {
                if (auth != null) auth.SignOut();
                GoogleSignIn.DefaultInstance.SignOut();
                Debug.Log("[LOGIN] Signed out successfully on first launch.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LOGIN] Error during first launch sign-out: {ex.Message}");
                // Continue anyway, this is not critical
            }
        }
        
        // Step 5: All SDKs are initialized and ready!
        isInitialized = true;
        Debug.Log("[LOGIN] Initialization complete, sign-in enabled!");
        
        // Enable login button and hide loading panel
        if (loginButton != null) loginButton.interactable = true;
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    // Old InitFirebase is no longer needed as we do it in Start() with proper dependency checking
    void InitFirebase() 
    {
        // This is now handled in Start() with proper dependency checking
    }

    public void Login()
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[LOGIN] Tried to sign in before initialization complete.");
            return;
        }
        // Always reinitialize Google Sign-In configuration
        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            RequestIdToken = true,
            WebClientId = GoogleAPI
        };

        isGoogleSignInInitialized = true;

        // Only force sign out on first launch
        if (isFirstLaunch)
        {
            GoogleSignIn.DefaultInstance.SignOut();
        }

        // Disable login button if you have one (optional)
        // Example: loginButton.interactable = false;

        try
        {
            Task<GoogleSignInUser> signIn = GoogleSignIn.DefaultInstance.SignIn();
            TaskCompletionSource<FirebaseUser> signInCompleted = new TaskCompletionSource<FirebaseUser>();
            signIn.ContinueWith(task =>
            {
                if (task.IsCanceled)
                {
                    signInCompleted.SetCanceled();
                    Debug.LogError("[LOGIN] Google sign-in cancelled");
                    ShowLoginError("Google sign-in was cancelled. Please try again.");
                    // Optionally re-enable login button
                    return;
                }
                else if (task.IsFaulted)
                {
                    signInCompleted.SetException(task.Exception);
                    Debug.LogError($"[LOGIN] Google sign-in failed: {task.Exception}");
                    ShowLoginError("Google sign-in failed. Please check your connection and try again.");
                    // Optionally re-enable login button
                    return;
                }
                else
                {
                    try
                    {
                        Credential credential = Firebase.Auth.GoogleAuthProvider.GetCredential(((Task<GoogleSignInUser>)task).Result.IdToken, null);
                        auth.SignInWithCredentialAsync(credential).ContinueWith(authTask =>
                        {
                            if (authTask.IsCanceled)
                            {
                                signInCompleted.SetCanceled();
                                Debug.LogError("[LOGIN] Firebase auth cancelled");
                                ShowLoginError("Firebase authentication was cancelled. Please try again.");
                                // Optionally re-enable login button
                                return;
                            }
                            else if (authTask.IsFaulted)
                            {
                                signInCompleted.SetException(authTask.Exception);
                                Debug.LogError($"[LOGIN] Firebase auth failed: {authTask.Exception}");
                                ShowLoginError("Firebase authentication failed. Please try again.");
                                // Optionally re-enable login button
                                return;
                            }
                            else
                            {
                                signInCompleted.SetResult(((Task<FirebaseUser>)authTask).Result);
                                Debug.Log("[LOGIN] Sign in successful");

                                // Reset to default state (BGSNL) before loading scene
                                PlayerPrefs.SetString("SelectedCityId", "bgsnl");
                                PlayerPrefs.SetInt("ForceDefaultCity", 1);
                                PlayerPrefs.Save();

                                // Start coroutine to load scene after data is ready
                                StartCoroutine(LoadSceneAfterDataReady());
                            }
                        });
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LOGIN] Exception during Firebase credential sign-in: {ex}");
                        ShowLoginError("An error occurred during authentication. Please try again.");
                        // Optionally re-enable login button
                        return;
                    }
                }
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LOGIN] Exception during Google sign-in: {ex}");
            ShowLoginError("An error occurred during Google sign-in. Please try again.");
            // Optionally re-enable login button
        }
    }

    // Show a user-friendly error message (implement as needed)
    private void ShowLoginError(string message)
    {
        Debug.LogError($"[LOGIN ERROR] {message}");
        // TODO: Show a UI popup or message to the user
        // Example: errorText.text = message; errorPanel.SetActive(true);
    }

    private IEnumerator LoadSceneAfterDataReady()
    {
        Debug.Log("[LOGIN] Starting to wait for data to be ready...");
        
        // Find GoogleSheetsService
        var sheetsService = FindObjectOfType<GoogleSheetsService>();
        if (sheetsService != null)
        {
            try
            {
                // Force a refresh of data
                sheetsService.ForceRefresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LOGIN] Error during data refresh: {ex.Message}");
                // Continue anyway, we'll try to load with available data
            }

            // Wait until data is loaded (max 10 seconds to avoid infinite loop)
            float timeout = 10f;
            float timer = 0f;
            var dataModel = FindObjectOfType<DataModelClasses>();
            while (timer < timeout)
            {
                if (dataModel != null &&
                    dataModel.SocialMediaMetrics.Count > 0 &&
                    dataModel.EventMetrics.Count > 0)
                {
                    Debug.Log("[LOGIN] Data is ready, proceeding to load scene.");
                    break;
                }
                yield return new WaitForSeconds(0.2f);
                timer += 0.2f;
            }

            if (timer >= timeout)
            {
                Debug.LogWarning("[LOGIN] Data loading timed out, but proceeding anyway.");
            }
        }
        else
        {
            Debug.LogWarning("[LOGIN] GoogleSheetsService not found, proceeding without data refresh.");
            yield return new WaitForSeconds(1f); // Brief delay to allow time for scene to settle
        }
        
        try
        {
            // Load scene 1 after data is ready
            Debug.Log("[LOGIN] Loading scene 1...");
            SceneManager.LoadScene(1);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LOGIN] Error during scene loading: {ex.Message}");
            ShowLoginError("Error during data loading. Please try again.");
        }
    }

    public void LogOut()
    {
        // Sign out from Firebase
        if (auth != null)
        {
            auth.SignOut();
            Debug.Log("Firebase user signed out");
        }

        // Sign out from Google Sign-In
        GoogleSignIn.DefaultInstance.SignOut();
        Debug.Log("Google Sign-In user signed out");

        // Reset the initialization flag
        isGoogleSignInInitialized = false;

        // Reset first launch flag to ensure proper sign-in next time
        PlayerPrefs.DeleteKey("HasLaunchedBefore");
        PlayerPrefs.Save();

        // Load scene 0 after signing out
        SceneManager.LoadScene(0);
    }

    private void OnApplicationQuit()
    {
        // Don't sign out on quit, only clear the first launch flag if it was set
        if (isFirstLaunch)
        {
            PlayerPrefs.DeleteKey("HasLaunchedBefore");
            PlayerPrefs.Save();
        }
    }
}
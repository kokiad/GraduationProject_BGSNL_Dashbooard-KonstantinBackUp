using System;
using System.Collections;
using System.Collections.Generic;
using Firebase.Extensions;
using Google;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase.Auth;
using Firebase.Database;
using TMPro;

[System.Serializable]
public class RoleSceneMapping
{
    [Header("Role Configuration")]
    public string roleName; // e.g., "bgsnl", "bgsg", etc.
    public int sceneIndex; // Scene build index for this role
    
    [Header("Optional")]
    public string description; // Optional description for inspector clarity
}

public class LoginWithGoogle : MonoBehaviour
{
    [Header("Firebase Configuration")]
    public string GoogleAPI = "242650185588-l2mv2ak7m1kf8e2c9ll9uhf37c1mplp3.apps.googleusercontent.com";
    public string firebaseDatabaseURL = "https://bgsnl-dashboard--signin-default-rtdb.europe-west1.firebasedatabase.app/";
    
    [Header("Debug UI (Optional)")]
    public TextMeshProUGUI debugText; // Assign a UI Text element to show debug info
    
    [Header("Scene Configuration")]
    [SerializeField] private List<RoleSceneMapping> roleSceneMappings = new List<RoleSceneMapping>();
    [SerializeField] private int defaultSceneIndex = 1; // Fallback scene if role not found
    
    private GoogleSignInConfiguration configuration;
    private Firebase.Auth.FirebaseAuth auth;
    private DatabaseReference databaseReference;
    private bool isGoogleSignInInitialized = false;
    private bool isFirstLaunch = true;
    public GameObject loadingPanel; // Optional: Assign a loading spinner/panel in the inspector. Can be left null if initialization is fast enough.
    public UnityEngine.UI.Button loginButton; // Assign your sign-in button in the inspector
    private bool isInitialized = false;
    private bool isDatabaseReady = false;
    
    // Fallback whitelist for testing when database fails
    private Dictionary<string, string> fallbackWhitelist = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"k.sonev1@gmail.com", "admin"},
        {"burronixsub@gmail.com", "bgsnl"},
        {"kokiadtheryzegod@gmail.com", "bgse"},
        {"sledrabota@gmail.com", "bgsg"}
    };

    private string debugLog = "";
    private int maxDebugLines = 20;

    private void UpdateDebugText(string message)
    {
        Debug.Log($"[LOGIN] {message}");
        
        if (debugText != null)
        {
            // Add timestamp and new message
            debugLog += $"[{System.DateTime.Now:HH:mm:ss}] {message}\n";
            
            // Keep only the last maxDebugLines
            string[] lines = debugLog.Split('\n');
            if (lines.Length > maxDebugLines)
            {
                debugLog = string.Join("\n", lines, lines.Length - maxDebugLines, maxDebugLines);
    }

            debugText.text = debugLog;
        }
    }

    private void ClearDebugText()
    {
        debugLog = "";
        if (debugText != null)
        {
            debugText.text = "";
        }
    }

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
        
        UpdateDebugText("Starting initialization...");
    }

    private IEnumerator Start()
    {
        // Disable sign-in button and show loading panel
        if (loginButton != null) loginButton.interactable = false;
        if (loadingPanel != null) loadingPanel.SetActive(true);
        
        UpdateDebugText("Checking Firebase dependencies...");
        isInitialized = false;

        // Step 1: Initialize Firebase properly with dependency check
        var dependencyTask = Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => dependencyTask.IsCompleted);

        if (dependencyTask.Result != Firebase.DependencyStatus.Available)
        {
            UpdateDebugText($"Firebase dependencies failed: {dependencyTask.Result}");
            ShowLoginError("Could not initialize Firebase. Please restart the app.");
            yield break;
        }

        UpdateDebugText("Firebase dependencies OK. Initializing Auth...");
        
        // Step 2: Initialize Firebase Auth (always needed)
        try
        {
            auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            UpdateDebugText("Firebase Auth initialized successfully.");
        }
        catch (Exception ex)
        {
            UpdateDebugText($"Firebase Auth failed: {ex.Message}");
            ShowLoginError("Firebase Auth initialization error. Please restart the app.");
            yield break;
        }
        
        // Step 3: Try to initialize Firebase Database (but don't test connection yet)
        try
        {
            UpdateDebugText("Setting up Firebase Database...");
            
            if (!string.IsNullOrEmpty(firebaseDatabaseURL))
            {
                databaseReference = FirebaseDatabase.GetInstance(firebaseDatabaseURL).RootReference;
                UpdateDebugText("Firebase Database URL configured.");
            }
            else
            {
                databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
                UpdateDebugText("Default Firebase Database configured.");
            }
            
            isDatabaseReady = true;
            UpdateDebugText("Database ready (will test after auth).");
            
        }
        catch (Exception ex)
        {
            UpdateDebugText($"Database setup failed: {ex.Message}");
            UpdateDebugText("Will use fallback whitelist");
            isDatabaseReady = false;
        }
        
        // Step 4: Initialize Google Sign-In
        try
        {
            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                RequestIdToken = true,
                WebClientId = GoogleAPI,
                RequestEmail = true
            };
            isGoogleSignInInitialized = true;
            UpdateDebugText("Google Sign-In configured successfully.");
        }
        catch (Exception ex)
        {
            UpdateDebugText($"Google Sign-In config failed: {ex.Message}");
            ShowLoginError("Google Sign-In initialization error. Please restart the app.");
            yield break;
        }
        
        // Step 5: NOW it's safe to sign out if first launch (after SDKs are initialized)
        if (isFirstLaunch)
        {
            UpdateDebugText("First launch - clearing previous sessions...");
            try
            {
                if (auth != null) auth.SignOut();
                GoogleSignIn.DefaultInstance.SignOut();
                UpdateDebugText("Previous sessions cleared.");
            }
            catch (Exception ex)
            {
                UpdateDebugText($"Error clearing sessions: {ex.Message}");
                // Continue anyway, this is not critical
            }
        }
        
        // Step 6: All SDKs are initialized and ready!
        isInitialized = true;
        UpdateDebugText($"Ready! Database: {(isDatabaseReady ? "Connected" : "Fallback mode")}");
        
        // Enable login button and hide loading panel
        if (loginButton != null) loginButton.interactable = true;
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    // Helper method to encode email for Firebase key
    private string EncodeEmailForFirebaseKey(string email)
    {
        return email.Replace(".", ",").Replace("@", "_AT_");
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
            UpdateDebugText("Sign-in blocked: initialization not complete");
            return;
        }
        
        UpdateDebugText("Starting Google Sign-In...");
        
        // Always reinitialize Google Sign-In configuration
        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            RequestIdToken = true,
            WebClientId = GoogleAPI,
            RequestEmail = true
        };

        isGoogleSignInInitialized = true;

        // Only force sign out on first launch
        if (isFirstLaunch)
        {
            GoogleSignIn.DefaultInstance.SignOut();
        }

        // Disable login button if you have one (optional)
        if (loginButton != null) loginButton.interactable = false;
        if (loadingPanel != null) loadingPanel.SetActive(true);

        try
        {
        Task<GoogleSignInUser> signIn = GoogleSignIn.DefaultInstance.SignIn();
        TaskCompletionSource<FirebaseUser> signInCompleted = new TaskCompletionSource<FirebaseUser>();
        signIn.ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                signInCompleted.SetCanceled();
                    UpdateDebugText("Google sign-in was cancelled");
                    ShowLoginError("Google sign-in was cancelled. Please try again.");
                    ResetLoginUI();
                    return;
            }
            else if (task.IsFaulted)
            {
                signInCompleted.SetException(task.Exception);
                    UpdateDebugText($"Google sign-in failed: {task.Exception?.Message}");
                    ShowLoginError("Google sign-in failed. Please check your connection and try again.");
                    ResetLoginUI();
                    return;
                }
                else
                {
                    try
                    {
                        UpdateDebugText("Processing Google Sign-In result...");
                        
                        GoogleSignInUser googleUser = ((Task<GoogleSignInUser>)task).Result;
                        
                        UpdateDebugText("GoogleSignInUser obtained");
                        
                        // Debug the user object
                        if (googleUser == null)
                        {
                            UpdateDebugText("ERROR: GoogleSignInUser is null!");
                            ShowLoginError("Google Sign-In failed - no user data received.");
                            ResetLoginUI();
                            return;
                        }
                        
                        UpdateDebugText($"GoogleSignInUser not null");
                        UpdateDebugText($"UserId: {googleUser.UserId ?? "NULL"}");
                        UpdateDebugText($"DisplayName: {googleUser.DisplayName ?? "NULL"}");
                        UpdateDebugText($"Email property: {googleUser.Email ?? "NULL"}");
                        
                        string userEmail = googleUser.Email;
                        
                        if (string.IsNullOrEmpty(userEmail))
                        {
                            UpdateDebugText("Google user email empty - will try Firebase user email after auth");
                            userEmail = "temp_for_auth"; // Temporary placeholder
                        }
                        else
                        {
                            userEmail = userEmail.ToLower().Trim();
                            UpdateDebugText($"Google sign-in successful: {userEmail}");
                        }
                        
                        // Always proceed to Firebase auth - we'll get email there if needed
                        ProceedWithFirebaseAuth(googleUser, userEmail);
                    }
                    catch (System.Exception ex)
                    {
                        UpdateDebugText($"Error processing sign-in: {ex.Message}");
                        UpdateDebugText($"Exception type: {ex.GetType().Name}");
                        ShowLoginError("An error occurred during authentication. Please try again.");
                        ResetLoginUI();
                        return;
                    }
                }
            });
        }
        catch (System.Exception ex)
        {
            UpdateDebugText($"Exception during Google sign-in: {ex.Message}");
            ShowLoginError("An error occurred during Google sign-in. Please try again.");
            ResetLoginUI();
        }
    }

    private void CheckEmailWhitelist(string userEmail, GoogleSignInUser googleUser)
    {
        UpdateDebugText($"Checking authorization for: {userEmail}");
        
        // Use Firebase Database if available, otherwise use fallback
        if (isDatabaseReady && databaseReference != null)
        {
            UpdateDebugText("Using Firebase Database whitelist");
            CheckEmailInFirebaseDatabase(userEmail, googleUser);
        }
        else
        {
            UpdateDebugText("Using fallback whitelist");
            CheckEmailInFallbackWhitelist(userEmail, googleUser);
        }
    }
    
    private void CheckEmailInFirebaseDatabase(string userEmail, GoogleSignInUser googleUser)
    {
        // Encode the email for Firebase key lookup
        string encodedEmail = EncodeEmailForFirebaseKey(userEmail);
        UpdateDebugText($"Looking up encoded email: {encodedEmail}");
        
        // Query the whitelist in Firebase Realtime Database
        databaseReference.Child("whitelist").Child(encodedEmail).GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                UpdateDebugText($"Database query failed: {task.Exception?.Message}");
                UpdateDebugText("Falling back to local whitelist");
                CheckEmailInFallbackWhitelist(userEmail, googleUser);
                return;
            }

            DataSnapshot snapshot = task.Result;
            if (!snapshot.Exists)
            {
                // Email not in whitelist
                UpdateDebugText($"Email {userEmail} not in Firebase whitelist");
                auth.SignOut();
                GoogleSignIn.DefaultInstance.SignOut();
                ShowLoginError("Your email is not authorized to use this application. Please contact your administrator.");
                ResetLoginUI();
                return;
            }

            // Email is whitelisted - get the role
            string role = "bgsnl"; // Default role
            if (snapshot.Child("role").Exists)
            {
                role = snapshot.Child("role").Value.ToString().ToLower();
            }

            UpdateDebugText($"Firebase authorization successful! Role: {role}");
            
            // Set the user's role as their city ID
            PlayerPrefs.SetString("SelectedCityId", role);
            PlayerPrefs.SetInt("ForceDefaultCity", 0);
            PlayerPrefs.Save();

            UpdateDebugText($"Loading dashboard with role: {role}");

            // Load appropriate scene based on role
            int sceneToLoad = DetermineSceneForRole(role);
            
            // Start coroutine to load scene after data is ready
            StartCoroutine(LoadSceneAfterDataReady(sceneToLoad));
        });
    }
    
    private void CheckEmailInFallbackWhitelist(string userEmail, GoogleSignInUser googleUser)
    {
        UpdateDebugText("=== FALLBACK WHITELIST CHECK ===");
        UpdateDebugText($"Email to check: '{userEmail}'");
        
        // Check if it's one of our known emails
        if (fallbackWhitelist.ContainsKey(userEmail))
        {
            string role = fallbackWhitelist[userEmail];
            UpdateDebugText($"Found in fallback - role: {role}");
            UpdateDebugText($"AUTHORIZATION GRANTED - Role: {role}");
            
            // Set the user's role as their city ID
            PlayerPrefs.SetString("SelectedCityId", role);
            PlayerPrefs.SetInt("ForceDefaultCity", 0);
            PlayerPrefs.Save();

            UpdateDebugText($"Loading dashboard with role: {role}");

            // Load appropriate scene based on role
            int sceneToLoad = DetermineSceneForRole(role);
            
            // Start coroutine to load scene after data is ready
            StartCoroutine(LoadSceneAfterDataReady(sceneToLoad));
        }
        else
        {
            UpdateDebugText($"Email '{userEmail}' not in fallback whitelist either");
            auth.SignOut();
            GoogleSignIn.DefaultInstance.SignOut();
            ShowLoginError("Your email is not authorized to use this application. Please contact your administrator.");
            ResetLoginUI();
        }
    }

    private void ProceedWithFirebaseAuth(GoogleSignInUser googleUser, string userEmail)
    {
        UpdateDebugText("Proceeding with Firebase authentication...");
        
        try
        {
            Credential credential = Firebase.Auth.GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
                auth.SignInWithCredentialAsync(credential).ContinueWith(authTask =>
                {
                    if (authTask.IsCanceled)
                    {
                    UpdateDebugText("Firebase auth was cancelled");
                    ShowLoginError("Firebase authentication was cancelled. Please try again.");
                    ResetLoginUI();
                    return;
                    }
                    else if (authTask.IsFaulted)
                    {
                    UpdateDebugText($"Firebase auth failed: {authTask.Exception?.Message}");
                    ShowLoginError("Firebase authentication failed. Please try again.");
                    ResetLoginUI();
                    return;
                    }
                    else
                    {
                    UpdateDebugText("Firebase authentication successful!");
                    
                    // Get Firebase user
                    Firebase.Auth.FirebaseUser firebaseUser = auth.CurrentUser;
                    
                    // Get email from Firebase user if we didn't get it from Google
                    if (userEmail == "temp_for_auth" && firebaseUser != null && !string.IsNullOrEmpty(firebaseUser.Email))
                    {
                        userEmail = firebaseUser.Email.ToLower().Trim();
                        UpdateDebugText($"Got email from Firebase user: {userEmail}");
                    }
                    
                    // Validate we have an email
                    if (string.IsNullOrEmpty(userEmail) || userEmail == "temp_for_auth")
                    {
                        UpdateDebugText("ERROR: No email available from Google or Firebase!");
                        ShowLoginError("Authentication failed - no email received.");
                        ResetLoginUI();
                        return;
                    }
                    
                    UpdateDebugText($"Final email to check: {userEmail}");
                    
                    // Now check the whitelist with the final email
                    CheckEmailWhitelist(userEmail, googleUser);
                }
            });
        }
        catch (System.Exception ex)
        {
            UpdateDebugText($"Exception during Firebase auth: {ex.Message}");
            ShowLoginError("An error occurred during authentication. Please try again.");
            ResetLoginUI();
            }
    }

    private int DetermineSceneForRole(string role)
    {
        // Normalize role to lowercase for comparison
        string normalizedRole = role.ToLower().Trim();
        
        UpdateDebugText($"Looking up scene for role: '{normalizedRole}'");
        
        // Search through the configured mappings
        foreach (var mapping in roleSceneMappings)
    {
            if (!string.IsNullOrEmpty(mapping.roleName) && 
                mapping.roleName.ToLower().Trim() == normalizedRole)
        {
                UpdateDebugText($"Found mapping: {normalizedRole} -> Scene {mapping.sceneIndex}");
                return mapping.sceneIndex;
    }
        }
        
        // If no mapping found, use default scene
        UpdateDebugText($"No mapping found for role '{normalizedRole}', using default scene {defaultSceneIndex}");
        return defaultSceneIndex;
    }

    // Helper method to validate scene mappings (can be called from inspector button if needed)
    [ContextMenu("Validate Scene Mappings")]
    private void ValidateSceneMappings()
    {
        Debug.Log("=== SCENE MAPPING VALIDATION ===");
        Debug.Log($"Default Scene Index: {defaultSceneIndex}");
        Debug.Log($"Configured Mappings: {roleSceneMappings.Count}");
        
        foreach (var mapping in roleSceneMappings)
        {
            string status = "✓ Valid";
            if (string.IsNullOrEmpty(mapping.roleName))
            {
                status = "✗ Missing role name";
            }
            else if (mapping.sceneIndex < 0)
            {
                status = "✗ Invalid scene index";
            }
            
            Debug.Log($"  {mapping.roleName} -> Scene {mapping.sceneIndex} ({status})");
            if (!string.IsNullOrEmpty(mapping.description))
            {
                Debug.Log($"    Description: {mapping.description}");
            }
        }
        
        // List all roles that should be configured
        string[] expectedRoles = { "bgsnl", "bgsg", "bgsr", "bgsl", "bgsa", "bgsb", "bgsm", "bgse" };
        Debug.Log("Expected roles that should be configured:");
        foreach (string expectedRole in expectedRoles)
        {
            bool found = false;
            foreach (var mapping in roleSceneMappings)
            {
                if (!string.IsNullOrEmpty(mapping.roleName) && 
                    mapping.roleName.ToLower() == expectedRole.ToLower())
                {
                    found = true;
                    break;
                }
            }
            
            string foundStatus = found ? "✓ Configured" : "⚠ Missing";
            Debug.Log($"  {expectedRole}: {foundStatus}");
        }
        
        Debug.Log("=== END VALIDATION ===");
    }

    // Helper method to set up default role mappings (can be called from inspector button)
    [ContextMenu("Setup Default Role Mappings")]
    private void SetupDefaultRoleMappings()
        {
        roleSceneMappings.Clear();
        
        // Add all expected roles with placeholder scene indices
        // You can modify these scene indices later when you create the scenes
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsnl", sceneIndex = 1, description = "BGSNL Main Dashboard (with cities dropdown)" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsg", sceneIndex = 2, description = "Groningen Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsr", sceneIndex = 3, description = "Rotterdam Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsl", sceneIndex = 4, description = "Leeuwarden Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsa", sceneIndex = 5, description = "Amsterdam Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsb", sceneIndex = 6, description = "Breda Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsm", sceneIndex = 7, description = "Maastricht Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgse", sceneIndex = 8, description = "Eindhoven Dashboard" });
        
        // Special case for admin - can go to main BGSNL scene
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "admin", sceneIndex = 1, description = "Admin access (same as BGSNL)" });
        
        Debug.Log("Default role mappings have been set up! You can now modify the scene indices as needed.");
        Debug.Log("Current mappings:");
        ValidateSceneMappings();
    }

    private void ResetLoginUI()
    {
        if (loginButton != null) loginButton.interactable = true;
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    // Show a user-friendly error message (implement as needed)
    private void ShowLoginError(string message)
    {
        UpdateDebugText($"ERROR: {message}");
        // TODO: Show a UI popup or message to the user
        // Example: errorText.text = message; errorPanel.SetActive(true);
    }

    private IEnumerator LoadSceneAfterDataReady(int sceneIndex = 1)
    {
        UpdateDebugText("Preparing to load dashboard...");
        
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
                UpdateDebugText($"Data refresh error: {ex.Message}");
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
                    UpdateDebugText("Data is ready!");
                    break;
                }
                yield return new WaitForSeconds(0.2f);
                timer += 0.2f;
            }

            if (timer >= timeout)
            {
                UpdateDebugText("Data loading timed out, continuing anyway");
            }
        }
        else
        {
            UpdateDebugText("No GoogleSheetsService found");
            yield return new WaitForSeconds(1f); // Brief delay to allow time for scene to settle
        }
        
        try
        {
            // Load the specified scene
            UpdateDebugText($"Loading scene {sceneIndex}...");
            SceneManager.LoadScene(sceneIndex);
        }
        catch (Exception ex)
        {
            UpdateDebugText($"Scene loading error: {ex.Message}");
            ShowLoginError("Error during data loading. Please try again.");
            ResetLoginUI();
        }
    }

    public void LogOut()
    {
        // Sign out from Firebase
        if (auth != null)
        {
            auth.SignOut();
            UpdateDebugText("Firebase user signed out");
        }

        // Sign out from Google Sign-In
        GoogleSignIn.DefaultInstance.SignOut();
        UpdateDebugText("Google Sign-In user signed out");

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

    public void ClearDebugLog()
    {
        ClearDebugText();
        UpdateDebugText("Debug log cleared - ready for testing");
    }
}
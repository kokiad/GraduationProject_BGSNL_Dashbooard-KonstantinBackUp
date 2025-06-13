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
    
    // Auto-login PlayerPrefs keys
    private const string PREF_MANUAL_LOGIN_SUCCESS = "ManualLoginSuccess";
    private const string PREF_SAVED_USER_EMAIL = "SavedUserEmail";
    private const string PREF_SAVED_USER_ROLE = "SavedUserRole";
    private const string PREF_USER_LOGGED_OUT = "UserLoggedOut";
    
    // NEW: Track how user got to dashboard
    private const string PREF_LAST_LOGIN_TYPE = "LastLoginType"; // "auto" or "manual"
    
    private GoogleSignInConfiguration configuration;
    private Firebase.Auth.FirebaseAuth auth;
    private DatabaseReference databaseReference;
    private bool isGoogleSignInInitialized = false;
    private bool isFirstLaunch = true;
    public GameObject loadingPanel; // Optional: Assign a loading spinner/panel in the inspector. Can be left null if initialization is fast enough.
    public UnityEngine.UI.Button loginButton; // Assign your sign-in button in the inspector
    private bool isInitialized = false;
    private bool isDatabaseReady = false;
    
    // Track if we need aggressive cache clearing
    private static bool needsAggressiveCacheClear = false;
    
    // Fallback whitelist for testing when database fails
    private Dictionary<string, string> fallbackWhitelist = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        {"k.sonev1@gmail.com", "admin"},
        {"burronixsub@gmail.com", "bgsnl"},
        {"kokiadtheryzegod@gmail.com", "bgse"},
        {"sledrabota@gmail.com", "bgsg"}
    };

    private void UpdateDebugText(string message)
    {
        Debug.Log($"[LOGIN] {message}");
    }

    private void ClearDebugText()
    {
        // Removed debug text clearing
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
        
        // CRITICAL: Check if we need aggressive cache clearing
        CheckIfAggressiveCacheClearNeeded();
    }

    private void CheckIfAggressiveCacheClearNeeded()
    {
        string lastLoginType = PlayerPrefs.GetString(PREF_LAST_LOGIN_TYPE, "");
        bool userLoggedOut = PlayerPrefs.HasKey(PREF_USER_LOGGED_OUT);
        
        UpdateDebugText($"Last login type: '{lastLoginType}', User logged out: {userLoggedOut}");
        
        // If user logged out after auto-login, we need aggressive clearing
        if (lastLoginType == "auto" && userLoggedOut)
        {
            needsAggressiveCacheClear = true;
            UpdateDebugText("DETECTED: User logged out after auto-login - will do aggressive cache clearing");
        }
        else
        {
            needsAggressiveCacheClear = false;
            UpdateDebugText("Normal cache clearing will be sufficient");
        }
    }

    private IEnumerator Start()
    {
        // CRITICAL: Only allow auto-login in the login scene (scene 0)
        // If we're in a dashboard scene, skip auto-login entirely
        string currentSceneName = SceneManager.GetActiveScene().name;
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        
        UpdateDebugText($"LoginWithGoogle Start() in scene: {currentSceneName} (index: {currentSceneIndex})");
        
        // Define login scene names/indices - adjust these to match your setup
        bool isLoginScene = currentSceneIndex == 0 || 
                           currentSceneName.ToLower().Contains("login") || 
                           currentSceneName.ToLower().Contains("signin") ||
                           currentSceneName == "LoginScreen";
        
        if (!isLoginScene)
        {
            UpdateDebugText($"Not in login scene - skipping auto-login and manual login setup");
            UpdateDebugText($"LoginWithGoogle in dashboard scene will only provide utility functions");
            
            // Still initialize Firebase/Google for utility functions, but don't do any login logic
            yield return StartCoroutine(InitializeSDKsOnly());
            yield break; // Exit early - no login logic in dashboard scenes
        }
        
        UpdateDebugText($"In login scene - proceeding with manual login setup");
        UpdateDebugText("Auto-login is now handled by StartupManager in startup scene");
        
        // If we're in the login scene, it means manual login is required
        // (StartupManager would have redirected to dashboard if auto-login was available)
        
        UpdateDebugText($"Setting up manual login UI");
        
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
        
        // Step 4: Initialize Google Sign-In with enhanced cache clearing
        yield return StartCoroutine(InitializeGoogleSignInForLogin());
        
        // Step 5: CRITICAL - Always clear Google Sign-In cache when entering login scene
        // This ensures that even if user got here after auto-login → logout, 
        // the next manual login will show account picker
        UpdateDebugText("Clearing Google Sign-In cache to ensure fresh login...");
        try
        {
            if (auth != null) auth.SignOut();
            GoogleSignIn.DefaultInstance.SignOut();
            GoogleSignIn.DefaultInstance.Disconnect();
            UpdateDebugText("Google Sign-In cache cleared successfully");
        }
        catch (Exception ex)
        {
            UpdateDebugText($"Error clearing Google cache: {ex.Message}");
            // Continue anyway, this is not critical for initialization
        }

        // Wait for sign-out operations to complete
        yield return new WaitForSeconds(0.3f);
        
        // Step 6: NOW it's safe to sign out if first launch (after SDKs are initialized)
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
        
        // Always force sign out to ensure clean state for manual login
        if (auth != null && auth.CurrentUser != null)
        {
            UpdateDebugText("Clearing any existing Firebase session for manual login");
            auth.SignOut();
        }
        GoogleSignIn.DefaultInstance.SignOut();

        // Wait for sign-out
        yield return new WaitForSeconds(0.2f);

        // Step 7: All SDKs are initialized and ready!
        isInitialized = true;
        UpdateDebugText($"Ready for manual login! Database: {(isDatabaseReady ? "Connected" : "Fallback mode")}");
        
        // Enable login button and hide loading panel
        if (loginButton != null) loginButton.interactable = true;
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    // Initialize SDKs only (for dashboard scenes) - no login logic
    private IEnumerator InitializeSDKsOnly()
    {
        UpdateDebugText("Initializing SDKs only (no login logic)...");
        
        // Step 1: Initialize Firebase properly with dependency check
        var dependencyTask = Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => dependencyTask.IsCompleted);

        if (dependencyTask.Result != Firebase.DependencyStatus.Available)
        {
            UpdateDebugText($"Firebase dependencies failed: {dependencyTask.Result}");
            yield break;
        }

        UpdateDebugText("Firebase dependencies OK. Initializing Auth...");
        
        // Step 2: Initialize Firebase Auth
        try
        {
            auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            UpdateDebugText("Firebase Auth initialized successfully.");
        }
        catch (Exception ex)
        {
            UpdateDebugText($"Firebase Auth failed: {ex.Message}");
            yield break;
        }
        
        // Step 3: Try to initialize Firebase Database
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
            UpdateDebugText("Database ready.");
            
        }
        catch (Exception ex)
        {
            UpdateDebugText($"Database setup failed: {ex.Message}");
            isDatabaseReady = false;
        }
        
        // Step 4: Initialize Google Sign-In with enhanced cache clearing
        yield return StartCoroutine(InitializeGoogleSignInForLogin());
        
        isInitialized = true;
        UpdateDebugText("SDKs initialized successfully (dashboard mode) - logout functionality available");
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
        // CRITICAL: Prevent login attempts in dashboard scenes
        string currentSceneName = SceneManager.GetActiveScene().name;
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        
        bool isLoginScene = currentSceneIndex == 0 || 
                           currentSceneName.ToLower().Contains("login") || 
                           currentSceneName.ToLower().Contains("signin") ||
                           currentSceneName == "LoginScreen";
        
        if (!isLoginScene)
        {
            UpdateDebugText($"Login() called in dashboard scene '{currentSceneName}' - ignoring");
            return;
        }
        
        if (!isInitialized)
        {
            UpdateDebugText("Sign-in blocked: initialization not complete");
            return;
        }
        
        UpdateDebugText("Starting MANUAL login process...");
        
        // CRITICAL: Mark this as a manual login attempt
        PlayerPrefs.SetString(PREF_LAST_LOGIN_TYPE, "manual");
        PlayerPrefs.Save();
        UpdateDebugText("Marked login type as MANUAL");
        
        // CRITICAL: Always completely reinitialize Google Sign-In to prevent hanging
        // This fixes the issue where login hangs after logout
        UpdateDebugText("Reinitializing Google Sign-In configuration...");
        
        try
        {
            // Force a complete reinitialization
            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                RequestIdToken = true,
                WebClientId = GoogleAPI,
                RequestEmail = true
            };

            isGoogleSignInInitialized = true;
            UpdateDebugText("Google Sign-In reinitialized successfully");
        }
        catch (System.Exception ex)
        {
            UpdateDebugText($"Failed to reinitialize Google Sign-In: {ex.Message}");
            ShowLoginError("Failed to initialize Google Sign-In. Please restart the app.");
            ResetLoginUI();
            return;
        }

        // Disable login button and show loading
        if (loginButton != null) loginButton.interactable = false;
        if (loadingPanel != null) loadingPanel.SetActive(true);

        // IMPORTANT: Always sign out first to ensure clean state
        // This prevents account caching issues
        try
        {
            UpdateDebugText("Clearing any existing Google Sign-In session...");
            GoogleSignIn.DefaultInstance.SignOut();
            GoogleSignIn.DefaultInstance.Disconnect();
            UpdateDebugText("Previous session cleared");
        }
        catch (System.Exception ex)
        {
            UpdateDebugText($"Error clearing previous session (non-critical): {ex.Message}");
        }

        // Small delay to ensure sign-out is complete
        StartCoroutine(DelayedSignIn());
    }
    
    private IEnumerator DelayedSignIn()
    {
        // Wait a moment for any previous operations to complete
        yield return new WaitForSeconds(0.5f);
        
        UpdateDebugText("Starting fresh Google Sign-In...");
        
        // ENHANCED: Always clear cache before starting fresh sign-in
        // This prevents any cached account selection from previous sessions
        try
        {
            UpdateDebugText("Final cache clear before fresh sign-in...");
            GoogleSignIn.DefaultInstance.SignOut();
            GoogleSignIn.DefaultInstance.Disconnect();
            UpdateDebugText("Cache cleared, ready for fresh sign-in");
        }
        catch (System.Exception ex)
        {
            UpdateDebugText($"Error during final cache clear: {ex.Message}");
            // Continue anyway
        }
        
        // Small delay to ensure clearing is complete (outside try block)
        yield return new WaitForSeconds(0.2f);
        
        try
        {
        Task<GoogleSignInUser> signIn = GoogleSignIn.DefaultInstance.SignIn();
            signIn.ContinueWith(task =>
            {
                if (task.IsCanceled)
                {
                    UpdateDebugText("Google sign-in was cancelled");
                    ShowLoginError("Google sign-in was cancelled. Please try again.");
                    ResetLoginUI();
                    return;
                }
                else if (task.IsFaulted)
                {
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
            
            // SAVE SUCCESSFUL LOGIN for simple auto-login
            SaveSuccessfulLogin(userEmail, role);
            
            // Set the user's role as their city ID
            PlayerPrefs.SetString("SelectedCityId", role);
            PlayerPrefs.SetInt("ForceDefaultCity", 0);
            PlayerPrefs.Save();

            UpdateDebugText($"Loading dashboard with role: {role}");

            // Load appropriate scene based on role - SIMPLIFIED (no data loading)
            int sceneToLoad = DetermineSceneForRole(role);
            
            UpdateDebugText($"MANUAL LOGIN SUCCESS: Loading scene {sceneToLoad} directly");
            
            try
            {
                SceneManager.LoadScene(sceneToLoad);
            }
            catch (Exception ex)
            {
                UpdateDebugText($"Scene loading failed: {ex.Message}");
                ShowLoginError("Error loading dashboard. Please try again.");
                ResetLoginUI();
            }
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
            
            // SAVE SUCCESSFUL LOGIN for simple auto-login
            SaveSuccessfulLogin(userEmail, role);
            
            // Set the user's role as their city ID
            PlayerPrefs.SetString("SelectedCityId", role);
            PlayerPrefs.SetInt("ForceDefaultCity", 0);
            PlayerPrefs.Save();

            UpdateDebugText($"Loading dashboard with role: {role}");

            // Load appropriate scene based on role - SIMPLIFIED (no data loading)
            int sceneToLoad = DetermineSceneForRole(role);
            
            UpdateDebugText($"MANUAL LOGIN SUCCESS: Loading scene {sceneToLoad} directly");
            
            try
            {
                SceneManager.LoadScene(sceneToLoad);
            }
            catch (Exception ex)
            {
                UpdateDebugText($"Scene loading failed: {ex.Message}");
                ShowLoginError("Error loading dashboard. Please try again.");
                ResetLoginUI();
            }
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

    // Add this new method to save successful logins
    private void SaveSuccessfulLogin(string userEmail, string userRole)
    {
        UpdateDebugText($"Saving successful MANUAL login: {userEmail} with role {userRole}");
        
        // ALWAYS save auto-login data when user successfully logs in manually
        // This fixes the issue where auto-login doesn't work after logout
        UpdateDebugText("Saving auto-login data for successful manual login");
        
        // Save the successful login data for auto-login
        PlayerPrefs.SetString(PREF_MANUAL_LOGIN_SUCCESS, "true");
        PlayerPrefs.SetString(PREF_SAVED_USER_EMAIL, userEmail);
        PlayerPrefs.SetString(PREF_SAVED_USER_ROLE, userRole);
        PlayerPrefs.SetString(PREF_LAST_LOGIN_TYPE, "manual");
        
        // Clear any logout flag since user successfully logged in
        PlayerPrefs.DeleteKey(PREF_USER_LOGGED_OUT);
        
        // Ensure HasLaunchedBefore is set so auto-login can work
        PlayerPrefs.SetInt("HasLaunchedBefore", 1);
        
        PlayerPrefs.Save();
        
        UpdateDebugText("Manual login data saved for future auto-login");
        UpdateDebugText($"Auto-login will be available for: {userEmail} with role: {userRole}");
    }

    // Add a separate method for auto-login that doesn't clear logout flag
    private void SaveAutoLoginSession(string userEmail, string userRole)
    {
        UpdateDebugText($"Auto-login session established: {userEmail} with role {userRole}");
        
        // Set session data but DON'T clear logout flag
        // This preserves any logout intent from previous session
        PlayerPrefs.SetString("SelectedCityId", userRole);
        PlayerPrefs.SetInt("ForceDefaultCity", 0);
        PlayerPrefs.Save();
        
        UpdateDebugText("Auto-login session data set (logout flag preserved)");
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
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsnl", sceneIndex = 2, description = "BGSNL Main Dashboard (with cities dropdown)" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsg", sceneIndex = 6, description = "Groningen Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsr", sceneIndex = 7, description = "Rotterdam Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsl", sceneIndex = 8, description = "Leeuwarden Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsa", sceneIndex = 9, description = "Amsterdam Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsb", sceneIndex = 10, description = "Breda Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgsm", sceneIndex = 11, description = "Maastricht Dashboard" });
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "bgse", sceneIndex = 12, description = "Eindhoven Dashboard" });
        
        // Special case for admin - can go to main BGSNL scene
        roleSceneMappings.Add(new RoleSceneMapping { roleName = "admin", sceneIndex = 2, description = "Admin access (same as BGSNL)" });
        
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

    public void LogOut()
    {
        UpdateDebugText("=== ENHANCED LOGOUT PROCESS ===");
        
        // Get current scene info for debugging
        string currentSceneName = SceneManager.GetActiveScene().name;
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        UpdateDebugText($"Logout called from scene: {currentSceneName} (index: {currentSceneIndex})");
        
        string lastLoginType = PlayerPrefs.GetString(PREF_LAST_LOGIN_TYPE, "unknown");
        UpdateDebugText($"Last login type was: {lastLoginType}");
        
        // CRITICAL: Set up for aggressive cache clearing on next login
        if (lastLoginType == "auto")
        {
            UpdateDebugText("User is logging out after AUTO-LOGIN - setting up aggressive cache clearing");
            needsAggressiveCacheClear = true;
        }
        
        // NUCLEAR: Enhanced Google Sign-In clearing
        try
        {
            // Ensure Google Sign-In is ready for clearing
            if (!isGoogleSignInInitialized)
            {
                UpdateDebugText("Initializing Google Sign-In for logout cache clearing...");
                GoogleSignIn.Configuration = new GoogleSignInConfiguration
                {
                    RequestIdToken = true,
                    WebClientId = GoogleAPI,
                    RequestEmail = true
                };
                isGoogleSignInInitialized = true;
            }
            
            // NUCLEAR: Multiple rounds of different types of clearing
            UpdateDebugText("Performing NUCLEAR Google cache clearing...");
            
            // Round 1: Standard clearing
            GoogleSignIn.DefaultInstance.SignOut();
            GoogleSignIn.DefaultInstance.Disconnect();
            UpdateDebugText("Round 1: Standard clearing completed");
            
            // Round 2: Clear with different config
            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                RequestIdToken = false,
                WebClientId = GoogleAPI,
                RequestEmail = false
            };
            GoogleSignIn.DefaultInstance.SignOut();
            GoogleSignIn.DefaultInstance.Disconnect();
            UpdateDebugText("Round 2: Minimal config clearing completed");
            
            // Round 3: Null config and clear again
            GoogleSignIn.Configuration = null;
            GoogleSignIn.DefaultInstance.SignOut();
            GoogleSignIn.DefaultInstance.Disconnect();
            UpdateDebugText("Round 3: Null config clearing completed");
            
            UpdateDebugText("NUCLEAR Google cache clearing completed");
            
        }
        catch (System.Exception ex)
        {
            UpdateDebugText($"Error during nuclear Google clearing: {ex.Message}");
        }
        
        // Clear Firebase auth
        UpdateDebugText("Clearing Firebase auth...");
        try
        {
            if (auth != null)
            {
                auth.SignOut();
                UpdateDebugText("Firebase signed out");
            }
        }
        catch (System.Exception ex)
        {
            UpdateDebugText($"Firebase sign out error (non-critical): {ex.Message}");
        }
        
        // Clear login and session data (but preserve privacy acknowledgment)
        UpdateDebugText("Clearing login and session data (preserving privacy acknowledgment)...");
        
        PlayerPrefs.DeleteKey(PREF_MANUAL_LOGIN_SUCCESS);
        PlayerPrefs.DeleteKey(PREF_SAVED_USER_EMAIL);
        PlayerPrefs.DeleteKey(PREF_SAVED_USER_ROLE);
        PlayerPrefs.DeleteKey("SelectedCityId");
        PlayerPrefs.DeleteKey("ForceDefaultCity");
        PlayerPrefs.DeleteKey("PullRefresh_PreservedCityId");
        // NOTE: We do NOT clear "HasLaunchedBefore" during logout to preserve privacy acknowledgment
        // NOTE: We do NOT clear privacy-related PlayerPrefs (PrivacyAcknowledged, etc.) during logout
        
        // CRITICAL: Keep track that user logged out AND what type of login they had
        PlayerPrefs.SetString(PREF_USER_LOGGED_OUT, "true");
        PlayerPrefs.SetString("LogoutAfterLoginType", lastLoginType);
        PlayerPrefs.Save();
        
        UpdateDebugText("Login data cleared (privacy acknowledgment preserved)");
        
        // Reset all local state variables
        isGoogleSignInInitialized = false;
        isInitialized = false;
        isDatabaseReady = false;
        
        UpdateDebugText("Local state variables reset");
        
        UpdateDebugText("=== ENHANCED LOGOUT PROCESS FINISHED ===");
        UpdateDebugText("Loading startup scene for clean restart...");
        
        // Go to startup scene for clean restart
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
        // Removed debug log clearing
    }

    // Debug methods to help troubleshoot auto-login
    [ContextMenu("Debug Simple Auto-Login State")]
    public void DebugSimpleAutoLoginState()
    {
        // Removed debug method
    }

    [ContextMenu("Clear Simple Auto-Login Data")]
    public void ClearSimpleAutoLoginData()
    {
        Debug.Log("Clearing simple auto-login data...");
        PlayerPrefs.DeleteKey(PREF_MANUAL_LOGIN_SUCCESS);
        PlayerPrefs.DeleteKey(PREF_SAVED_USER_EMAIL);
        PlayerPrefs.DeleteKey(PREF_SAVED_USER_ROLE);
        PlayerPrefs.DeleteKey(PREF_USER_LOGGED_OUT);
        PlayerPrefs.Save();
        Debug.Log("Simple auto-login data cleared - next launch will require manual login");
    }

    // ENHANCED DEBUG HELPERS
    [ContextMenu("Debug Enhanced Login State")]
    public void DebugEnhancedLoginState()
    {
        Debug.Log("=== ENHANCED LOGIN DEBUG ===");
        Debug.Log($"LastLoginType: '{PlayerPrefs.GetString(PREF_LAST_LOGIN_TYPE, "NOT SET")}'");
        Debug.Log($"LogoutAfterLoginType: '{PlayerPrefs.GetString("LogoutAfterLoginType", "NOT SET")}'");
        Debug.Log($"NeedsAggressiveCacheClear: {needsAggressiveCacheClear}");
        Debug.Log($"ManualLoginSuccess: {PlayerPrefs.HasKey(PREF_MANUAL_LOGIN_SUCCESS)}");
        Debug.Log($"UserLoggedOut: {PlayerPrefs.HasKey(PREF_USER_LOGGED_OUT)}");
        Debug.Log($"SavedEmail: '{PlayerPrefs.GetString(PREF_SAVED_USER_EMAIL, "NOT SET")}'");
        Debug.Log($"SavedRole: '{PlayerPrefs.GetString(PREF_SAVED_USER_ROLE, "NOT SET")}'");
        Debug.Log("=== END ENHANCED DEBUG ===");
    }

    [ContextMenu("Force Set Auto-Login State")]
    public void ForceSetAutoLoginState()
    {
        PlayerPrefs.SetString(PREF_MANUAL_LOGIN_SUCCESS, "true");
        PlayerPrefs.SetString(PREF_SAVED_USER_EMAIL, "test@example.com");
        PlayerPrefs.SetString(PREF_SAVED_USER_ROLE, "bgsnl");
        PlayerPrefs.SetString(PREF_LAST_LOGIN_TYPE, "auto");
        PlayerPrefs.DeleteKey(PREF_USER_LOGGED_OUT);
        PlayerPrefs.SetInt("HasLaunchedBefore", 1);
        PlayerPrefs.Save();
        Debug.Log("Auto-login state set for testing");
    }

    [ContextMenu("Simulate Auto-Login Logout")]
    public void SimulateAutoLoginLogout()
    {
        PlayerPrefs.SetString(PREF_USER_LOGGED_OUT, "true");
        PlayerPrefs.SetString("LogoutAfterLoginType", "auto");
        needsAggressiveCacheClear = true;
        PlayerPrefs.Save();
        Debug.Log("Simulated logout after auto-login - next manual login should show account picker");
    }

    [ContextMenu("Test Aggressive Cache Clear")]
    public void TestAggressiveCacheClear()
    {
        needsAggressiveCacheClear = true;
        Debug.Log("Set needsAggressiveCacheClear = true for testing");
    }

    private IEnumerator InitializeGoogleSignInForLogin()
    {
        UpdateDebugText("Initializing Google Sign-In for login scene...");
        
        // SUPER AGGRESSIVE: If we need cache clearing, do it BEFORE initialization
        if (needsAggressiveCacheClear)
        {
            UpdateDebugText("=== SUPER AGGRESSIVE CACHE CLEARING ===");
            
            // Try to clear with minimal config first
            try
            {
                GoogleSignIn.Configuration = new GoogleSignInConfiguration
                {
                    RequestIdToken = false,
                    WebClientId = GoogleAPI,
                    RequestEmail = false
                };
                
                // Multiple aggressive clearing rounds
                for (int i = 0; i < 5; i++)
                {
                    UpdateDebugText($"Aggressive clear round {i + 1}/5");
                    GoogleSignIn.DefaultInstance.SignOut();
                    GoogleSignIn.DefaultInstance.Disconnect();
                }
                
                // Clear configuration completely
                GoogleSignIn.Configuration = null;
                
                UpdateDebugText("Super aggressive cache clearing completed");
                
            }
            catch (Exception ex)
            {
                UpdateDebugText($"Error during super aggressive clearing: {ex.Message}");
            }
            
            // Wait outside the try block
            yield return new WaitForSeconds(0.2f);
            
            // Reset the flag since we've done the clearing
            needsAggressiveCacheClear = false;
            
            // Additional wait outside try block
            yield return new WaitForSeconds(0.5f);
        }
        
        // Now initialize properly for login
        try
        {
            GoogleSignIn.Configuration = new GoogleSignInConfiguration
            {
                RequestIdToken = true,
                WebClientId = GoogleAPI,
                RequestEmail = true
            };
            isGoogleSignInInitialized = true;
            UpdateDebugText("Google Sign-In configured successfully for login.");
        }
        catch (Exception ex)
        {
            UpdateDebugText($"Google Sign-In config failed: {ex.Message}");
            ShowLoginError("Google Sign-In initialization error. Please restart the app.");
            yield break;
        }
        
        // Always do standard clearing for login scene
        try
        {
            if (auth != null) auth.SignOut();
            GoogleSignIn.DefaultInstance.SignOut();
            GoogleSignIn.DefaultInstance.Disconnect();
            UpdateDebugText("Standard cache clearing completed for login scene");
        }
        catch (Exception ex)
        {
            UpdateDebugText($"Error during standard clearing: {ex.Message}");
        }

        yield return new WaitForSeconds(0.3f);
    }
}
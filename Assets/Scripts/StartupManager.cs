using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles app startup, auto-login check, and initial scene routing
/// This should be in Scene 0 (Startup/Loading scene)
/// </summary>
public class StartupManager : MonoBehaviour
{
    [Header("Loading UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private Image loadingSpinner; // Optional rotating spinner
    [SerializeField] private float spinSpeed = 90f; // Degrees per second
    
    [Header("Scene Configuration")]
    [SerializeField] private int loginSceneIndex = 1; // Scene index for login
    [SerializeField] private int defaultDashboardScene = 2; // Default dashboard scene (BGSNL)
    
    [Header("Auto-Login Settings")]
    [SerializeField] private float minimumLoadingTime = 1.5f; // Minimum time to show loading screen
    [SerializeField] private bool debugMode = true;
    
    // Auto-login PlayerPrefs keys (must match LoginWithGoogle.cs)
    private const string PREF_MANUAL_LOGIN_SUCCESS = "ManualLoginSuccess";
    private const string PREF_SAVED_USER_EMAIL = "SavedUserEmail";
    private const string PREF_SAVED_USER_ROLE = "SavedUserRole";
    private const string PREF_USER_LOGGED_OUT = "UserLoggedOut";
    private const string PREF_LAST_LOGIN_TYPE = "LastLoginType"; // "auto" or "manual"
    
    // Role to scene mapping (same as LoginWithGoogle)
    [System.Serializable]
    public class RoleSceneMapping
    {
        public string roleName; // e.g., "bgsnl", "bgsg", etc.
        public int sceneIndex; // Scene build index for this role
        public string description; // Optional description
    }
    
    [Header("Role Scene Mappings")]
    [SerializeField] private RoleSceneMapping[] roleSceneMappings = new RoleSceneMapping[]
    {
        new RoleSceneMapping { roleName = "bgsnl", sceneIndex = 2, description = "BGSNL Dashboard" },
        new RoleSceneMapping { roleName = "admin", sceneIndex = 2, description = "Admin (BGSNL Dashboard)" },
        new RoleSceneMapping { roleName = "bgsg", sceneIndex = 6, description = "Groningen Dashboard" },
        new RoleSceneMapping { roleName = "bgsr", sceneIndex = 7, description = "Rotterdam Dashboard" },
        new RoleSceneMapping { roleName = "bgsl", sceneIndex = 8, description = "Leeuwarden Dashboard" },
        new RoleSceneMapping { roleName = "bgsa", sceneIndex = 9, description = "Amsterdam Dashboard" },
        new RoleSceneMapping { roleName = "bgsb", sceneIndex = 10, description = "Breda Dashboard" },
        new RoleSceneMapping { roleName = "bgsm", sceneIndex = 11, description = "Maastricht Dashboard" },
        new RoleSceneMapping { roleName = "bgse", sceneIndex = 12, description = "Eindhoven Dashboard" }
    };
    
    private float startTime;
    
    private void Awake()
    {
        // Ensure this scene persists during the startup process
        DontDestroyOnLoad(gameObject);
        startTime = Time.time;
        
        LogDebug("StartupManager: App starting up...");
    }
    
    private void Start()
    {
        // Show loading UI
        if (loadingPanel != null)
            loadingPanel.SetActive(true);
            
        UpdateLoadingText("Initializing...");
        
        // Start the startup process
        StartCoroutine(StartupProcess());
        
        // Start spinner animation if available
        if (loadingSpinner != null)
            StartCoroutine(SpinLoadingIcon());
    }
    
    private IEnumerator StartupProcess()
    {
        LogDebug("Starting startup process...");
        
        // Step 1: Check for auto-login data
        UpdateLoadingText("Checking login status...");
        yield return new WaitForSeconds(0.3f); // Brief pause for UI
        
        bool shouldAutoLogin = CheckAutoLoginAvailable();
        
        if (shouldAutoLogin)
        {
            // Auto-login available
            LogDebug("Auto-login available!");
            
            string savedEmail = PlayerPrefs.GetString(PREF_SAVED_USER_EMAIL, "");
            string savedRole = PlayerPrefs.GetString(PREF_SAVED_USER_ROLE, "");
            
            UpdateLoadingText($"Welcome back, {GetDisplayName(savedEmail)}!");
            yield return new WaitForSeconds(1.0f);
            
            LogDebug($"Auto-login: email={savedEmail}, role={savedRole}");
            
            // CRITICAL: Mark this as an auto-login
            PlayerPrefs.SetString(PREF_LAST_LOGIN_TYPE, "auto");
            PlayerPrefs.Save();
            LogDebug("Marked login type as AUTO");
            
            // Set session data
            PlayerPrefs.SetString("SelectedCityId", savedRole);
            PlayerPrefs.SetInt("ForceDefaultCity", 0);
            PlayerPrefs.Save();
            
            // Determine target scene
            int targetScene = DetermineSceneForRole(savedRole);
            
            LogDebug($"Auto-login: Will load scene {targetScene} for role {savedRole}");
            
            // Wait for minimum loading time
            yield return WaitForMinimumLoadingTime();
            
            // Load dashboard scene
            UpdateLoadingText("Loading dashboard...");
            yield return new WaitForSeconds(0.5f);
            
            LogDebug($"Loading dashboard scene: {targetScene}");
            SceneManager.LoadScene(targetScene);
        }
        else
        {
            // Manual login required
            LogDebug("Manual login required");
            UpdateLoadingText("Please sign in...");
            
            // Clear logout flag since we're going to login scene
            // This ensures clean state for the next login attempt
            if (PlayerPrefs.HasKey(PREF_USER_LOGGED_OUT))
            {
                PlayerPrefs.DeleteKey(PREF_USER_LOGGED_OUT);
                PlayerPrefs.Save();
                LogDebug("Cleared logout flag - ready for fresh login");
            }
            
            // Wait for minimum loading time
            yield return WaitForMinimumLoadingTime();
            
            // Load login scene
            LogDebug($"Loading login scene: {loginSceneIndex}");
            SceneManager.LoadScene(loginSceneIndex);
        }
    }
    
    private bool CheckAutoLoginAvailable()
    {
        LogDebug("=== AUTO-LOGIN CHECK ===");
        
        // Check if this is first launch - but be more lenient
        bool isFirstLaunch = !PlayerPrefs.HasKey("HasLaunchedBefore");
        LogDebug($"First launch: {isFirstLaunch}");
        
        // Check each condition with detailed logging
        bool hasManualLoginFlag = PlayerPrefs.HasKey(PREF_MANUAL_LOGIN_SUCCESS);
        string savedEmail = PlayerPrefs.GetString(PREF_SAVED_USER_EMAIL, "");
        string savedRole = PlayerPrefs.GetString(PREF_SAVED_USER_ROLE, "");
        bool hasValidEmail = !string.IsNullOrEmpty(savedEmail);
        bool hasValidRole = !string.IsNullOrEmpty(savedRole);
        bool hasValidManualLogin = hasManualLoginFlag && hasValidEmail && hasValidRole;
        bool userLoggedOut = PlayerPrefs.HasKey(PREF_USER_LOGGED_OUT);
        
        LogDebug($"ManualLoginSuccess flag: {hasManualLoginFlag}");
        LogDebug($"Saved email: '{savedEmail}' (valid: {hasValidEmail})");
        LogDebug($"Saved role: '{savedRole}' (valid: {hasValidRole})");
        LogDebug($"Valid manual login: {hasValidManualLogin}");
        LogDebug($"User logged out: {userLoggedOut}");
        
        // IMPROVED LOGIC: Don't let first launch block auto-login if we have valid login data
        // This fixes the inconsistent auto-login issue
        bool shouldAutoLogin = hasValidManualLogin && !userLoggedOut;
        
        // Only block auto-login on first launch if we don't have any login data at all
        if (isFirstLaunch && !hasValidManualLogin)
        {
            shouldAutoLogin = false;
            LogDebug("Blocking auto-login: First launch with no login data");
        }
        
        LogDebug($"SHOULD AUTO-LOGIN: {shouldAutoLogin}");
        
        if (!shouldAutoLogin)
        {
            if (userLoggedOut)
                LogDebug("Reason: User explicitly logged out");
            else if (isFirstLaunch && !hasValidManualLogin)
                LogDebug("Reason: First launch with no login data");
            else if (!hasValidManualLogin)
                LogDebug("Reason: No valid login data");
        }
        else
        {
            // If we're doing auto-login, mark that the app has launched before
            if (isFirstLaunch)
            {
                PlayerPrefs.SetInt("HasLaunchedBefore", 1);
                PlayerPrefs.Save();
                LogDebug("Marked app as launched before due to successful auto-login");
            }
        }
        
        LogDebug("=== END AUTO-LOGIN CHECK ===");
        return shouldAutoLogin;
    }
    
    private int DetermineSceneForRole(string role)
    {
        string normalizedRole = role.ToLower().Trim();
        LogDebug($"Looking up scene for role: '{normalizedRole}'");
        
        // Search through configured mappings
        foreach (var mapping in roleSceneMappings)
        {
            if (!string.IsNullOrEmpty(mapping.roleName) && 
                mapping.roleName.ToLower().Trim() == normalizedRole)
            {
                LogDebug($"Found mapping: {normalizedRole} -> Scene {mapping.sceneIndex}");
                return mapping.sceneIndex;
            }
        }
        
        // If no mapping found, use default
        LogDebug($"No mapping found for role '{normalizedRole}', using default scene {defaultDashboardScene}");
        return defaultDashboardScene;
    }
    
    private string GetDisplayName(string email)
    {
        if (string.IsNullOrEmpty(email))
            return "User";
            
        // Extract name part before @ symbol
        int atIndex = email.IndexOf('@');
        if (atIndex > 0)
        {
            return email.Substring(0, atIndex);
        }
        
        return "User";
    }
    
    private IEnumerator WaitForMinimumLoadingTime()
    {
        float elapsedTime = Time.time - startTime;
        float remainingTime = minimumLoadingTime - elapsedTime;
        
        if (remainingTime > 0)
        {
            LogDebug($"Waiting {remainingTime:F1}s more for minimum loading time");
            yield return new WaitForSeconds(remainingTime);
        }
    }
    
    private IEnumerator SpinLoadingIcon()
    {
        while (loadingSpinner != null && loadingSpinner.gameObject.activeInHierarchy)
        {
            loadingSpinner.transform.Rotate(0, 0, -spinSpeed * Time.deltaTime);
            yield return null;
        }
    }
    
    private void UpdateLoadingText(string message)
    {
        LogDebug($"Loading: {message}");
        
        if (loadingText != null)
        {
            loadingText.text = message;
        }
    }
    
    private void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[STARTUP] {message}");
        }
    }
    
    // Context menu helpers for testing
    [ContextMenu("Test Auto-Login Check")]
    private void TestAutoLoginCheck()
    {
        bool result = CheckAutoLoginAvailable();
        Debug.Log($"Auto-login check result: {result}");
    }
    
    [ContextMenu("Clear All Login Data")]
    private void ClearAllLoginData()
    {
        PlayerPrefs.DeleteKey(PREF_MANUAL_LOGIN_SUCCESS);
        PlayerPrefs.DeleteKey(PREF_SAVED_USER_EMAIL);
        PlayerPrefs.DeleteKey(PREF_SAVED_USER_ROLE);
        PlayerPrefs.DeleteKey(PREF_USER_LOGGED_OUT);
        PlayerPrefs.DeleteKey("HasLaunchedBefore");
        PlayerPrefs.DeleteKey("SelectedCityId");
        PlayerPrefs.Save();
        Debug.Log("All login data cleared");
    }
} 
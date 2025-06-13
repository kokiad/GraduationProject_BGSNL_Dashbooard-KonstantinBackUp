using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Integration script that connects the Achievement System to the existing UI infrastructure
/// This ensures achievements are properly initialized and updated when scenes change or data refreshes
/// </summary>
public class AchievementIntegration : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AchievementSystem achievementSystem;
    [SerializeField] private AchievementUI achievementUI;
    
    [Header("Scene-Based City Detection")]
    [SerializeField] private bool useSceneBasedCityDetection = true;
    [SerializeField] private string manualCityId = ""; // Override if not using scene detection
    
    [Header("Settings")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool autoInitialize = true;
    
    private bool isInitialized = false;
    private string lastCityId = "";
    private string currentSceneCityId = "";

    private void Awake()
    {
        // Detect city from current scene
        DetectCityFromScene();
        
        if (autoInitialize)
        {
            FindReferences();
        }
    }
    
    private void DetectCityFromScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        LogDebug($"Current scene: {currentSceneName}");
        
        if (useSceneBasedCityDetection)
        {
            // Extract city ID from scene name (e.g., "BGSA_achievements" -> "bgsa")
            currentSceneCityId = ExtractCityIdFromSceneName(currentSceneName);
            
            if (!string.IsNullOrEmpty(currentSceneCityId))
            {
                // Set this as the selected city
                PlayerPrefs.SetString("SelectedCityId", currentSceneCityId);
                PlayerPrefs.Save();
                LogDebug($"Auto-detected city from scene: {currentSceneCityId}");
            }
            else
            {
                LogDebug($"Could not extract city ID from scene name: {currentSceneName}");
            }
        }
        else if (!string.IsNullOrEmpty(manualCityId))
        {
            currentSceneCityId = manualCityId.ToLower();
            PlayerPrefs.SetString("SelectedCityId", currentSceneCityId);
            PlayerPrefs.Save();
            LogDebug($"Using manual city ID: {currentSceneCityId}");
        }
        else
        {
            // Fallback to PlayerPrefs or default
            currentSceneCityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
            LogDebug($"Using fallback city ID: {currentSceneCityId}");
        }
    }
    
    private string ExtractCityIdFromSceneName(string sceneName)
    {
        // Handle different scene naming patterns
        string lowerSceneName = sceneName.ToLower();
        
        // Pattern 1: "BGSA_achievements" -> "bgsa"
        if (lowerSceneName.Contains("_"))
        {
            string[] parts = lowerSceneName.Split('_');
            if (parts.Length > 0)
            {
                string cityPart = parts[0];
                
                // Convert common city codes
                switch (cityPart)
                {
                    case "bgsnl": return "bgsnl";
                    case "bgsg": return "bgsg";
                    case "bgsr": return "bgsr";
                    case "bgsl": return "bgsl";
                    case "bgsa": return "bgsa";
                    case "bgsb": return "bgsb";
                    case "bgsm": return "bgsm";
                    case "bgse": return "bgse";
                    default: return cityPart; // Return as-is if not in known list
                }
            }
        }
        
        // Pattern 2: "BGSAAchievements" or "AchievementsBGSA"
        if (lowerSceneName.Contains("bgsa")) return "bgsa";
        if (lowerSceneName.Contains("bgsg")) return "bgsg";
        if (lowerSceneName.Contains("bgsr")) return "bgsr";
        if (lowerSceneName.Contains("bgsl")) return "bgsl";
        if (lowerSceneName.Contains("bgsb")) return "bgsb";
        if (lowerSceneName.Contains("bgsm")) return "bgsm";
        if (lowerSceneName.Contains("bgse")) return "bgse";
        if (lowerSceneName.Contains("bgsnl")) return "bgsnl";
        
        // Pattern 3: City names
        if (lowerSceneName.Contains("amsterdam")) return "bgsa";
        if (lowerSceneName.Contains("groningen")) return "bgsg";
        if (lowerSceneName.Contains("rotterdam")) return "bgsr";
        if (lowerSceneName.Contains("leeuwarden")) return "bgsl";
        if (lowerSceneName.Contains("breda")) return "bgsb";
        if (lowerSceneName.Contains("maastricht")) return "bgsm";
        if (lowerSceneName.Contains("eindhoven")) return "bgse";
        
        return ""; // Could not detect
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (achievementSystem != null)
        {
            AchievementSystem.OnAchievementUnlocked -= OnAchievementUnlocked;
        }
        
        GoogleSheetsService.OnDataUpdated -= OnDataUpdated;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void FindReferences()
    {
        if (achievementSystem == null)
        {
            achievementSystem = FindObjectOfType<AchievementSystem>();
            if (achievementSystem == null)
            {
                LogDebug("AchievementSystem not found in scene. Please assign it manually.");
            }
            else
            {
                LogDebug("Auto-found AchievementSystem");
            }
        }

        if (achievementUI == null)
        {
            achievementUI = FindObjectOfType<AchievementUI>();
            if (achievementUI == null)
            {
                LogDebug("AchievementUI not found in scene. Please assign it manually.");
            }
            else
            {
                LogDebug("Auto-found AchievementUI");
            }
        }
    }
    
    private void Start()
    {
        InitializeIntegration();
    }
    
    public void InitializeIntegration()
    {
        if (isInitialized) return;

        LogDebug($"Initializing Achievement Integration for city: {currentSceneCityId}");

        // Ensure we have the core components
        if (achievementSystem == null || achievementUI == null)
        {
            LogDebug("Missing core achievement components, retrying in 1 second...");
            StartCoroutine(DelayedInitialization());
            return;
        }

        // Subscribe to events
        AchievementSystem.OnAchievementUnlocked += OnAchievementUnlocked;
        GoogleSheetsService.OnDataUpdated += OnDataUpdated;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Initialize achievements for the current city
        if (achievementSystem != null)
        {
            achievementSystem.InitializeCurrentCityAchievements();
        }

        // Initial refresh
        StartCoroutine(DelayedRefreshAchievements());

        isInitialized = true;
        lastCityId = currentSceneCityId;
        
        LogDebug($"Achievement Integration initialized successfully for {currentSceneCityId}");
    }
    
    private System.Collections.IEnumerator DelayedInitialization()
    {
        yield return new WaitForSeconds(1f);
        FindReferences();
        InitializeIntegration();
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-detect city when scene changes
        DetectCityFromScene();
        
        LogDebug($"Scene loaded: {scene.name}, detected city: {currentSceneCityId}");

        // Check if city changed
        if (currentSceneCityId != lastCityId)
        {
            LogDebug($"City changed from {lastCityId} to {currentSceneCityId}");
            OnCityChanged(currentSceneCityId);
        }

        // Refresh achievements after scene load
        if (achievementSystem != null && achievementUI != null)
        {
            StartCoroutine(DelayedRefreshAchievements());
        }
    }
    
    private System.Collections.IEnumerator DelayedRefreshAchievements()
    {
        // Wait for other systems to initialize
        yield return new WaitForSeconds(0.5f);
        
        if (achievementSystem != null)
        {
            LogDebug("Refreshing achievements after delay");
            achievementSystem.InitializeCurrentCityAchievements();
            achievementSystem.CheckAchievementProgress();
        }
    }
    
    private void OnCityChanged(string newCityId)
    {
        if (string.IsNullOrEmpty(newCityId)) return;

        LogDebug($"Processing city change to: {newCityId}");

        lastCityId = newCityId;

        // Reinitialize achievements for new city
        if (achievementSystem != null)
        {
            achievementSystem.InitializeCurrentCityAchievements();
            achievementSystem.CheckAchievementProgress();
        }
    }
    
    private void OnDataUpdated()
    {
        LogDebug("Data updated, checking achievements");
        
        if (achievementSystem != null)
        {
            achievementSystem.CheckAchievementProgress();
        }
    }
    
    private void OnAchievementUnlocked(Achievement achievement, int milestoneLevel)
    {
        LogDebug($"Achievement unlocked: {achievement.GetCurrentTitle()} - Level {milestoneLevel} for city {achievement.cityId}");
        
        // Optional: Show notification if you have a notification system
        // NotificationManager.ShowAchievementNotification(achievement, milestoneLevel);
    }
    
    
    // Call this method when the user manually refreshes data (e.g., pull-to-refresh)
    
    public void OnManualDataRefresh()
    {
        LogDebug("Manual data refresh triggered, checking achievements");
        StartCoroutine(DelayedAchievementCheck());
    }
    
    private System.Collections.IEnumerator DelayedAchievementCheck()
    {
        // Wait a moment for data to be processed
        yield return new WaitForSeconds(1f);
        
        if (achievementSystem != null)
        {
            LogDebug("Performing delayed achievement check after manual refresh");
            achievementSystem.CheckAchievementProgress();
        }
    }
    
    
    // Public method to manually trigger achievement check
    // Can be called from UI buttons or other scripts
    
    public void CheckAchievements()
    {
        if (achievementSystem != null)
        {
            LogDebug("Manual achievement check requested");
            achievementSystem.CheckAchievementProgress();
        }
        else
        {
            LogDebug("Cannot check achievements - AchievementSystem not available");
        }
    }
    
    
    // Force refresh both achievements and UI
    
    public void ForceRefreshAchievements()
    {
        LogDebug("Force refreshing achievements");
        
        if (achievementSystem != null)
        {
            achievementSystem.InitializeCurrentCityAchievements();
            achievementSystem.CheckAchievementProgress();
        }
    }
    
    private void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[AchievementIntegration] {message}");
        }
    }
    
    // Context menu methods for testing
    [ContextMenu("Test Achievement Integration")]
    public void TestIntegration()
    {
        LogDebug($"Testing integration in scene: {SceneManager.GetActiveScene().name}");
        LogDebug($"Detected city: {currentSceneCityId}");
        LogDebug($"Achievement System: {(achievementSystem != null ? "Found" : "Missing")}");
        LogDebug($"Achievement UI: {(achievementUI != null ? "Found" : "Missing")}");
    }
    
    [ContextMenu("Force Refresh All")]
    public void TestForceRefresh()
    {
        LogDebug("Testing force refresh of achievements");
        ForceRefreshAchievements();
    }

    [ContextMenu("Detect City From Scene")]
    public void TestCityDetection()
    {
        DetectCityFromScene();
        LogDebug($"Scene: {SceneManager.GetActiveScene().name} -> City: {currentSceneCityId}");
    }
} 
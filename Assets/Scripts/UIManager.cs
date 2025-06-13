using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("Data References")]
    [SerializeField] private DataModelClasses dataModel;
    [SerializeField] private GoogleSheetsService sheetsService;
    
    [Header("UI References")]
    [SerializeField] private Image logoImage;
    
    [Header("BGSNL Social Media Metrics")]
    [SerializeField] private TextMeshProUGUI instagramFollowersText;
    [SerializeField] private TextMeshProUGUI tiktokFollowersText;
    [SerializeField] private TextMeshProUGUI tiktokLikesText;
    
    [Header("BGSNL Event Metrics")]
    [SerializeField] private TextMeshProUGUI ticketsSoldText;
    [SerializeField] private TextMeshProUGUI averageAttendanceText;
    [SerializeField] private TextMeshProUGUI numberOfEventsText;
    
    [Header("City Configuration")]
    [SerializeField] private Sprite bgsnlLogo;
    [SerializeField] private List<CityConfig> cityConfigs = new List<CityConfig>();
    
    [Header("Settings")]
    [SerializeField] private string mainSceneName = "HomeScreen";
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool forceDefaultCity = true;
    
    private bool isInitialized = false;
    private string currentCityId = "bgsnl"; // Default city ID
    private bool isFirstRun = true; // Track if this is the first run of the app
    
    // Singleton instance
    public static UIManager Instance { get; private set; }
    
    [System.Serializable]
    public class CityConfig
    {
        public string cityId; // Required: e.g., "bgsnl", "groningen"
        public string cityName; // Optional: e.g., "BGSNL", "Groningen"
        public Sprite cityLogo; // Required: the sprite for this city
    }
    
    private void Awake()
    {
        LogDebug("[UIManager] Initializing...");
        
        // Setup singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            isFirstRun = true; // This is the first time UIManager is created
        }
        else if (Instance != this)
        {
            Debug.LogWarning("[UIManager] Multiple UIManager instances detected! Destroying this one.");
            Destroy(gameObject);
            return;
        }
        
        // Find references if not set in inspector
        FindDataReferences();
        
        // Check if we should force default city
        bool shouldForceDefaultCity = forceDefaultCity;
        bool isLoginSession = false;
        
        if (PlayerPrefs.HasKey("ForceDefaultCity"))
        {
            shouldForceDefaultCity = PlayerPrefs.GetInt("ForceDefaultCity") == 1;
            isLoginSession = PlayerPrefs.GetInt("ForceDefaultCity") == 0; // 0 means user just logged in
            LogDebug($"[UIManager] Found ForceDefaultCity in PlayerPrefs: {shouldForceDefaultCity}, isLoginSession: {isLoginSession}");
        }
        
        // If this is a login session, don't reset to default - respect the login-set city
        if (isLoginSession)
        {
            LogDebug("[UIManager] Login session detected - will use login-set city ID");
            // Don't reset, just ensure we're not forcing default next time
            PlayerPrefs.SetInt("ForceDefaultCity", 1);
            PlayerPrefs.Save();
        }
        // Only reset to default city when the app first starts (not login) or when explicitly requested
        else if (isFirstRun && shouldForceDefaultCity)
        {
            ResetToDefaultCity();
            LogDebug("[UIManager] First run or forced reset - Reset to default city (BGSNL)");
            isFirstRun = false;
        }
        
        isInitialized = true;
        LogDebug("[UIManager] Initialization complete.");
    }
    
    private void ResetToDefaultCity()
    {
        currentCityId = "bgsnl";
        PlayerPrefs.SetString("SelectedCityId", currentCityId);
        PlayerPrefs.Save();
    }
    
    private void LogDebug(string message)
    {
        // Removed debug logging in production
    }
    
    private void LogError(string message)
    {
        Debug.LogError(message); // Keep error logging
    }
    
    private void FindDataReferences()
    {
        if (dataModel == null)
        {
            dataModel = FindObjectOfType<DataModelClasses>();
            if (dataModel == null)
            {
                LogError("[UIManager] Could not find DataModelClasses!");
            }
        }
        
        if (sheetsService == null)
        {
            sheetsService = FindObjectOfType<GoogleSheetsService>();
            if (sheetsService == null)
            {
                LogError("[UIManager] Could not find GoogleSheetsService!");
            }
        }
    }
    
    private void Start()
    {
        if (!isInitialized) return;
        
        LogDebug("[UIManager] Start method called. Setting up update sequence...");
        
        // Check if a city selection is in PlayerPrefs
        if (PlayerPrefs.HasKey("SelectedCityId"))
        {
            string savedCity = PlayerPrefs.GetString("SelectedCityId").ToLower();
            
            if (IsCityValid(savedCity))
            {
                currentCityId = savedCity;
                LogDebug($"[UIManager] Using city from PlayerPrefs: '{currentCityId}'");
            }
            else
            {
                LogError($"[UIManager] City in PlayerPrefs '{savedCity}' is not valid, defaulting to BGSNL");
                ResetToDefaultCity();
            }
        }
        else
        {
            LogDebug("[UIManager] No city found in PlayerPrefs, using default BGSNL");
            ResetToDefaultCity();
        }
        
        LogDebug($"[UIManager] Current city ID set to: '{currentCityId}'");
        
        // Wait longer for the data to fully load
        StartCoroutine(WaitAndUpdateDashboard());
    }
    
    private bool IsCityValid(string cityId)
    {
        if (string.IsNullOrEmpty(cityId))
            return false;
            
        // Always accept BGSNL
        if (cityId.ToLower() == "bgsnl")
            return true;
            
        // Check configured cities
        foreach (var config in cityConfigs)
        {
            if (config.cityId.ToLower() == cityId.ToLower())
                return true;
        }
        
        // Accept any valid BGS city role (even if not configured in inspector)
        string[] validBgsCities = { "bgsg", "bgsr", "bgsl", "bgsa", "bgsb", "bgsm", "bgse", "admin" };
        foreach (string validCity in validBgsCities)
        {
            if (cityId.ToLower() == validCity.ToLower())
            {
                LogDebug($"[UIManager] Accepting valid BGS city '{cityId}' even though not configured in inspector");
                return true;
            }
        }
        
        return false;
    }
    
    private IEnumerator WaitAndUpdateDashboard()
    {
        LogDebug("[UIManager] Waiting for data to load...");
        
        // Wait for data loading to complete (up to 10 seconds)
        float timeout = 10f;
        float timer = 0f;
        while (timer < timeout)
        {
            if (dataModel != null &&
                dataModel.SocialMediaMetrics.Count > 0 &&
                dataModel.EventMetrics.Count > 0)
            {
                break;
            }
            yield return new WaitForSeconds(0.2f);
            timer += 0.2f;
        }
        
        // Find UI references first
        FindUIReferences();
        
        // Update the dashboard
        LogDebug("[UIManager] Wait complete, updating dashboard...");
        UpdateDashboard();
    }
    
    
    // Updates the dashboard UI with metrics for the currently selected city
    
    public void UpdateDashboard()
    {
        if (!isInitialized)
        {
            LogError("[UIManager] Cannot update dashboard - not properly initialized!");
            return;
        }
        
        if (SceneManager.GetActiveScene().name != mainSceneName)
        {
            return;
        }
        
        FindUIReferences();
        
        if (dataModel == null)
        {
            LogError("[UIManager] Cannot update dashboard - DataModel is null!");
            return;
        }
        
        UpdateLogo(currentCityId);
        
        City selectedCity = dataModel.GetCityById(currentCityId);
        if (selectedCity == null)
        {
            LogError($"[UIManager] Could not find city with ID: {currentCityId}");
            return;
        }
        
        string cityId = selectedCity.ID;
        
        if (dataModel.SocialMediaMetrics.Count == 0 && dataModel.EventMetrics.Count == 0)
        {
            LogError("[UIManager] No metrics data found in the model! Try forcing a refresh.");
            if (sheetsService != null)
            {
                sheetsService.ForceRefresh();
                StartCoroutine(DelayedRetryUpdateDashboard(1.0f));
                return;
            }
        }
        
        SocialMediaMetrics socialMetrics = dataModel.GetLatestSocialMediaMetrics(cityId);
        EventMetrics eventMetrics = dataModel.GetLatestEventMetrics(cityId);
        
        UpdateSocialMediaMetrics(socialMetrics, selectedCity);
        UpdateEventMetrics(eventMetrics, selectedCity);
    }
    
    private IEnumerator DelayedRetryUpdateDashboard(float delay)
    {
        yield return new WaitForSeconds(delay);
        LogDebug("[UIManager] Retrying dashboard update after data refresh");
        UpdateDashboard();
    }
    
    private void UpdateSocialMediaMetrics(SocialMediaMetrics socialMetrics, City selectedCity)
    {
        if (socialMetrics != null)
        {
            if (instagramFollowersText != null)
            {
                instagramFollowersText.text = socialMetrics.InstagramFollowers;
            }
            else
            {
                LogError("[UIManager] Instagram followers text component not found!");
            }
            
            if (tiktokFollowersText != null)
            {
                tiktokFollowersText.text = socialMetrics.TikTokFollowers;
            }
            else
            {
                LogError("[UIManager] TikTok followers text component not found!");
            }
            
            if (tiktokLikesText != null)
            {
                tiktokLikesText.text = socialMetrics.TikTokLikes;
            }
            else
            {
                LogError("[UIManager] TikTok likes text component not found!");
            }
        }
        else
        {
            if (instagramFollowersText != null) instagramFollowersText.text = "0";
            if (tiktokFollowersText != null) tiktokFollowersText.text = "0";
            if (tiktokLikesText != null) tiktokLikesText.text = "0";
        }
    }
    
    private void UpdateEventMetrics(EventMetrics eventMetrics, City selectedCity)
    {
        if (eventMetrics != null)
        {
            if (ticketsSoldText != null)
            {
                ticketsSoldText.text = eventMetrics.TicketsSold;
            }
            else
            {
                LogError("[UIManager] Tickets sold text component not found!");
            }
            
            if (averageAttendanceText != null)
            {
                averageAttendanceText.text = eventMetrics.AverageAttendance;
            }
            else
            {
                LogError("[UIManager] Average attendance text component not found!");
            }
            
            if (numberOfEventsText != null)
            {
                numberOfEventsText.text = eventMetrics.NumberOfEvents;
            }
            else
            {
                LogError("[UIManager] Number of events text component not found!");
            }
        }
        else
        {
            if (ticketsSoldText != null) ticketsSoldText.text = "0";
            if (averageAttendanceText != null) averageAttendanceText.text = "0";
            if (numberOfEventsText != null) numberOfEventsText.text = "0";
        }
    }
    
    private void UpdateLogo(string cityId)
    {
        if (logoImage == null)
        {
            LogError("[UIManager] Cannot update logo - logoImage is null!");
            return;
        }
        
        Sprite logoToUse = bgsnlLogo;
        bool foundCustomLogo = false;
        
        if (cityId.ToLower() == "bgsnl" || cityId.ToLower() == "admin")
        {
            if (bgsnlLogo != null)
            {
                logoToUse = bgsnlLogo;
                foundCustomLogo = true;
            }
            else
            {
                LogError("[UIManager] BGSNL logo field is not assigned!");
            }
        }
        // For other cities, check config
        else
        {
            Debug.Log($"[UIManager] Searching for logo for city '{cityId}' among {cityConfigs.Count} configs");
            
            foreach (var cityConfig in cityConfigs)
            {
                if (cityConfig.cityId.ToLower() == cityId.ToLower())
                {
                    if (cityConfig.cityLogo != null)
                    {
                        logoToUse = cityConfig.cityLogo;
                        foundCustomLogo = true;
                        break;
                    }
                    else
                    {
                        LogError($"[UIManager] City config for '{cityId}' found but has no logo sprite assigned!");
                    }
                }
            }
            
            // If no custom logo found, try to use BGSNL logo as fallback for any BGS city
            if (!foundCustomLogo)
            {
                string[] validBgsCities = { "bgsg", "bgsr", "bgsl", "bgsa", "bgsb", "bgsm", "bgse" };
                foreach (string validCity in validBgsCities)
                {
                    if (cityId.ToLower() == validCity.ToLower())
                    {
                        LogDebug($"[UIManager] No custom logo for '{cityId}', using BGSNL logo as fallback");
                        logoToUse = bgsnlLogo;
                        foundCustomLogo = true;
                        break;
                    }
                }
            }
        }
        
        // Apply the logo
        if (logoToUse != null)
        {
            logoImage.sprite = logoToUse;
        }
        else
        {
            LogError($"[UIManager] No logo found for '{cityId}'");
        }
        
        // Log if using fallback
        if (!foundCustomLogo && logoToUse == bgsnlLogo)
        {
            LogDebug($"[UIManager] Using BGSNL logo as fallback for '{cityId}'");
        }
    }
    
    
    // Forces a refresh of the dashboard with current city data
    
    public void RefreshDashboard()
    {
        LogDebug("[UIManager] Manually refreshing dashboard...");
        
        // Store current city ID to ensure it's maintained after refresh
        string cityToRefresh = currentCityId;
        if (string.IsNullOrEmpty(cityToRefresh))
        {
            cityToRefresh = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
            LogDebug($"[UIManager] Using city from PlayerPrefs: '{cityToRefresh}'");
        }
        
        // Force a refresh of data
        if (sheetsService != null)
        {
            sheetsService.ForceRefresh();
            
            // Use a more direct method to update the dashboard with the specific city
            StartCoroutine(RefreshWithSpecificCity(cityToRefresh));
        }
        else
        {
            LogError("[UIManager] Cannot refresh - GoogleSheetsService is null!");
        }
    }
    
    
    // Refreshes the dashboard with a specific city ID
    
    public IEnumerator RefreshWithSpecificCity(string cityId)
    {
        // Wait for data refresh to complete
        yield return new WaitForSeconds(1.0f);
        
        LogDebug($"[UIManager] Refreshing dashboard for specific city: '{cityId}'");
        
        // Make sure this is the current city
        currentCityId = cityId;
        
        // Ensure it's saved to PlayerPrefs
        PlayerPrefs.SetString("SelectedCityId", cityId);
        PlayerPrefs.Save();
        
        // Update the dashboard with this specific city
        UpdateDashboard();
    }
    
    
    // Load city data for the specified city ID
    
    public void LoadCity(string cityId)
    {
        if (string.IsNullOrEmpty(cityId))
        {
            LogError("[UIManager] Cannot load city with null or empty ID");
            return;
        }
        
        // Normalize cityId to lowercase
        string normalizedCityId = cityId.ToLower();
        
        Debug.Log($"[UIManager] LoadCity called with city ID: '{normalizedCityId}'");
        
        // Check if this is a valid city
        if (!IsCityValid(normalizedCityId))
        {
            Debug.LogError($"[UIManager] Invalid city ID: '{normalizedCityId}'");
            return;
        }
        
        // Set current city
        currentCityId = normalizedCityId;
        
        // Save to PlayerPrefs
        PlayerPrefs.SetString("SelectedCityId", currentCityId);
        PlayerPrefs.Save();
        Debug.Log($"[UIManager] Saved city ID '{currentCityId}' to PlayerPrefs");
        
        // Force a data model update if needed
        if (dataModel != null)
        {
            // Get the city object
            City city = dataModel.GetCityById(currentCityId);
            if (city == null && currentCityId.ToLower() == "bgsnl")
            {
                city = new City("BGSNL", "bgsnl");
                dataModel.AddCity(city);
                Debug.Log("[UIManager] Created default BGSNL city");
            }
            
            if (city != null)
            {
                // Update logo first
                UpdateLogo(currentCityId);
                
                // Get latest metrics for this city
                var socialMetrics = dataModel.GetLatestSocialMediaMetrics(currentCityId);
                var eventMetrics = dataModel.GetLatestEventMetrics(currentCityId);
                
                // Update metrics displays
                UpdateSocialMediaMetrics(socialMetrics, city);
                UpdateEventMetrics(eventMetrics, city);
                
                Debug.Log($"[UIManager] Updated all data for city: {city.Name}");
            }
        }
        
        // Update the dashboard to ensure everything is in sync
        UpdateDashboard();
    }
    
    
    // Called when the scene changes to set up UI elements
    
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[UIManager] Scene loaded: {scene.name}");
        
        // Define the exact list of valid dashboard scenes
        string[] validDashboardScenes = { 
            "HomeScreen",   // Default and BGSNL specific scene
            "BGSG",         // Groningen
            "BGSR",         // Rotterdam
            "BGSL",         // Leeuwarden
            "BGSA",         // Amsterdam
            "BGSB",         // Breda  
            "BGSM",         // Maastricht
            "BGSE"          // Eindhoven
        };
        
        // Check if this is a valid dashboard scene
        bool isDashboardScene = false;
        foreach (string validScene in validDashboardScenes)
        {
            if (scene.name == validScene)
            {
                isDashboardScene = true;
                break;
            }
        }
        
        if (isDashboardScene)
        {
            Debug.Log($"[UIManager] Valid dashboard scene detected: {scene.name}");
            
            // Check if this is a login session (ForceDefaultCity = 0)
            bool isLoginSession = PlayerPrefs.HasKey("ForceDefaultCity") && PlayerPrefs.GetInt("ForceDefaultCity") == 0;
            
            if (isLoginSession)
            {
                Debug.Log("[UIManager] Login session - preserving login-set city ID");
                // Don't reset anything, just use what was set during login
                if (PlayerPrefs.HasKey("SelectedCityId"))
                {
                    string loginCity = PlayerPrefs.GetString("SelectedCityId").ToLower();
                    if (IsCityValid(loginCity))
                    {
                        currentCityId = loginCity;
                        Debug.Log($"[UIManager] Using login-set city: '{currentCityId}'");
                        
                        // CRITICAL: Force immediate dashboard update for login sessions
                        Debug.Log($"[UIManager] Forcing immediate dashboard update for login city: '{currentCityId}'");
                        
                        // Find UI references immediately
                        FindUIReferences();
                        
                        // Update dashboard immediately without delay for login sessions
                        UpdateDashboard();
                        
                        // Also schedule a delayed update as backup
                        StartCoroutine(DelayedUpdateDashboard());
                    }
                }
                
                // Mark that login session is processed
                PlayerPrefs.SetInt("ForceDefaultCity", 1);
                PlayerPrefs.Save();
                
                // Exit early since we've already updated the dashboard
                return;
            }
            else
        {
            // CRITICAL CHECK: Check if we have a special backup city ID from pull-to-refresh
            if (PlayerPrefs.HasKey("PullRefresh_PreservedCityId"))
            {
                string preservedCity = PlayerPrefs.GetString("PullRefresh_PreservedCityId");
                
                if (!string.IsNullOrEmpty(preservedCity) && IsCityValid(preservedCity))
                {
                    Debug.Log($"[CRITICAL] Found preserved city ID: '{preservedCity}' from pull-to-refresh. Using this instead of PlayerPrefs.");
                    
                    // Override the SelectedCityId in PlayerPrefs with our preserved value
                    PlayerPrefs.SetString("SelectedCityId", preservedCity);
                    PlayerPrefs.SetInt("ForceDefaultCity", 0);
                    PlayerPrefs.Save();
                    
                    // Also set current city directly
                    currentCityId = preservedCity;
                    
                    // IMPORTANT: Do not clear the backup key yet - it's kept until it updates the dashboard
                    // This ensures if there are any late resets, the backup is still there
                }
            }
            
                // Get the city ID from PlayerPrefs (normal operation)
            if (PlayerPrefs.HasKey("SelectedCityId"))
            {
                string savedCity = PlayerPrefs.GetString("SelectedCityId").ToLower();
                
                if (IsCityValid(savedCity))
                {
                    // Note: This is deliberately different from ResetToDefaultCity which would
                    // overwrite PlayerPrefs with "bgsnl"
                    currentCityId = savedCity;
                    Debug.Log($"[UIManager] Loaded city from PlayerPrefs: '{currentCityId}'");
                        }
                        else
                        {
                            Debug.LogError($"[UIManager] City in PlayerPrefs '{savedCity}' is not valid, defaulting to BGSNL");
                            ResetToDefaultCity();
                }
            }
            else
            {
                // Default to BGSNL if no city is saved
                ResetToDefaultCity();
                Debug.Log("[UIManager] No city found in PlayerPrefs, defaulting to BGSNL");
                }
            }
            
            // Make sure we have fresh UI references for the new scene
            FindUIReferences();
            
            // Small delay to ensure all UI components are fully loaded
            StartCoroutine(DelayedUpdateDashboard());
        }
    }
    
    private IEnumerator DelayedUpdateDashboard()
    {
        yield return new WaitForSeconds(0.5f);
        Debug.Log($"[UIManager] Running delayed dashboard update for city: '{currentCityId}'");
        
        // Make one final check for preserved city before updating
        if (PlayerPrefs.HasKey("PullRefresh_PreservedCityId"))
        {
            string preservedCity = PlayerPrefs.GetString("PullRefresh_PreservedCityId");
            if (IsCityValid(preservedCity))
            {
                // Override whatever city might have been set since
                currentCityId = preservedCity;
                Debug.Log($"Final city override using preserved city: '{preservedCity}'");
                
                // Now it's safe to remove the backup
                PlayerPrefs.DeleteKey("PullRefresh_PreservedCityId");
                PlayerPrefs.Save();
            }
        }
        
        UpdateDashboard();
    }
    
    private void FindUIReferences()
    {
        LogDebug("[UIManager] Finding UI references...");
        
        // First attempt direct assigned references from inspector
        if (logoImage == null)
        {
            logoImage = GameObject.FindWithTag("LogoImage")?.GetComponent<Image>();
            LogDebug($"[UIManager] Logo image found by tag: {(logoImage != null ? "Yes" : "No")}");
        }
            
        if (instagramFollowersText == null)
        {
            var obj = GameObject.FindWithTag("InstagramFollowers");
            instagramFollowersText = obj?.GetComponent<TextMeshProUGUI>();
            LogDebug($"[UIManager] Instagram followers text found by tag: {(instagramFollowersText != null ? "Yes" : "No")}");
            if (obj != null && instagramFollowersText == null)
            {
                LogError($"[UIManager] Found GameObject with tag InstagramFollowers but it has no TextMeshProUGUI component!");
            }
        }
            
        if (tiktokFollowersText == null)
        {
            tiktokFollowersText = GameObject.FindWithTag("TikTokFollowers")?.GetComponent<TextMeshProUGUI>();
            LogDebug($"[UIManager] TikTok followers text found by tag: {(tiktokFollowersText != null ? "Yes" : "No")}");
        }
            
        if (tiktokLikesText == null)
        {
            tiktokLikesText = GameObject.FindWithTag("TikTokLikes")?.GetComponent<TextMeshProUGUI>();
            LogDebug($"[UIManager] TikTok likes text found by tag: {(tiktokLikesText != null ? "Yes" : "No")}");
        }
            
        if (ticketsSoldText == null)
        {
            ticketsSoldText = GameObject.FindWithTag("TicketsSold")?.GetComponent<TextMeshProUGUI>();
            LogDebug($"[UIManager] Tickets sold text found by tag: {(ticketsSoldText != null ? "Yes" : "No")}");
        }
            
        if (averageAttendanceText == null)
        {
            averageAttendanceText = GameObject.FindWithTag("AverageAttendance")?.GetComponent<TextMeshProUGUI>();
            LogDebug($"[UIManager] Average attendance text found by tag: {(averageAttendanceText != null ? "Yes" : "No")}");
        }
            
        if (numberOfEventsText == null)
        {
            numberOfEventsText = GameObject.FindWithTag("NumberOfEvents")?.GetComponent<TextMeshProUGUI>();
            LogDebug($"[UIManager] Number of events text found by tag: {(numberOfEventsText != null ? "Yes" : "No")}");
        }
        
        // If still not found, try looking by name
        if (logoImage == null)
            logoImage = GameObject.Find("LogoImage")?.GetComponent<Image>();
        
        if (instagramFollowersText == null)
            instagramFollowersText = GameObject.Find("InstagramFollowersText")?.GetComponent<TextMeshProUGUI>();
        
        if (tiktokFollowersText == null)
            tiktokFollowersText = GameObject.Find("TikTokFollowersText")?.GetComponent<TextMeshProUGUI>();
        
        if (tiktokLikesText == null)
            tiktokLikesText = GameObject.Find("TikTokLikesText")?.GetComponent<TextMeshProUGUI>();
        
        if (ticketsSoldText == null)
            ticketsSoldText = GameObject.Find("TicketsSoldText")?.GetComponent<TextMeshProUGUI>();
        
        if (averageAttendanceText == null)
            averageAttendanceText = GameObject.Find("AverageAttendanceText")?.GetComponent<TextMeshProUGUI>();
        
        if (numberOfEventsText == null)
            numberOfEventsText = GameObject.Find("NumberOfEventsText")?.GetComponent<TextMeshProUGUI>();
        
        // Log results
        LogDebug("[UIManager] UI References found: " +
                 $"Logo: {(logoImage != null)}, " +
                 $"Instagram: {(instagramFollowersText != null)}, " +
                 $"TikTok: {(tiktokFollowersText != null)}, " +
                 $"TikTok Likes: {(tiktokLikesText != null)}, " +
                 $"Tickets: {(ticketsSoldText != null)}, " +
                 $"Attendance: {(averageAttendanceText != null)}, " +
                 $"Events: {(numberOfEventsText != null)}");
    }
    
    
    // Reset to BGSNL when the application quits to ensure it starts with BGSNL next time
    
    private void OnApplicationQuit()
    {
        // Force default city on next startup
        PlayerPrefs.SetInt("ForceDefaultCity", 1);
        PlayerPrefs.SetString("SelectedCityId", "bgsnl");
        PlayerPrefs.Save();
        LogDebug("[UIManager] Application quitting - Reset preferences to BGSNL for next startup");
    }
    
    private void OnApplicationPause(bool pause)
    {
        // If pausing the application, also reset (important for mobile builds)
        if (pause)
        {
            PlayerPrefs.SetInt("ForceDefaultCity", 1);
            PlayerPrefs.SetString("SelectedCityId", "bgsnl");
            PlayerPrefs.Save();
            LogDebug("[UIManager] Application paused - Reset preferences to BGSNL for next startup");
        }
    }
} 
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
        if (PlayerPrefs.HasKey("ForceDefaultCity"))
        {
            shouldForceDefaultCity = PlayerPrefs.GetInt("ForceDefaultCity") == 1;
            LogDebug($"[UIManager] Found ForceDefaultCity in PlayerPrefs: {shouldForceDefaultCity}");
        }
        
        // Only reset to default city when the app first starts or when explicitly requested
        if (isFirstRun && shouldForceDefaultCity)
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
        if (debugMode)
        {
            Debug.Log(message);
        }
    }
    
    private void LogError(string message)
    {
        Debug.LogError(message); // Always log errors
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
            else
            {
                LogDebug("[UIManager] Found DataModelClasses reference.");
            }
        }
        
        if (sheetsService == null)
        {
            sheetsService = FindObjectOfType<GoogleSheetsService>();
            if (sheetsService == null)
            {
                LogError("[UIManager] Could not find GoogleSheetsService!");
            }
            else
            {
                LogDebug("[UIManager] Found GoogleSheetsService reference.");
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
        DebugDumpCityConfigs();
        
        // Wait longer for the data to fully load
        StartCoroutine(WaitAndUpdateDashboard());
    }
    
    private void DebugDumpCityConfigs()
    {
        if (!debugMode) return;
        
        Debug.Log("======== CITY CONFIGS DUMP ========");
        Debug.Log($"BGSNL Logo assigned: {bgsnlLogo != null}");
        Debug.Log($"Number of city configs: {cityConfigs.Count}");
        
        foreach (var city in cityConfigs)
        {
            Debug.Log($"City: ID='{city.cityId}', Name='{city.cityName}', Logo={city.cityLogo != null}");
        }
        
        Debug.Log("===================================");
    }
    
    private bool IsCityValid(string cityId)
    {
        if (string.IsNullOrEmpty(cityId))
            return false;
            
        if (cityId.ToLower() == "bgsnl")
            return true;
            
        foreach (var config in cityConfigs)
        {
            if (config.cityId.ToLower() == cityId.ToLower())
                return true;
        }
        
        return false;
    }
    
    private IEnumerator WaitAndUpdateDashboard()
    {
        LogDebug("[UIManager] Waiting for data to load...");
        
        // Wait for data loading to complete
        yield return new WaitForSeconds(1.5f);
        
        // Find UI references first
        FindUIReferences();
        
        // Update the dashboard
        LogDebug("[UIManager] Wait complete, updating dashboard...");
        UpdateDashboard();
    }
    
    /// <summary>
    /// Updates the dashboard UI with metrics for the currently selected city
    /// </summary>
    public void UpdateDashboard()
    {
        if (!isInitialized)
        {
            LogError("[UIManager] Cannot update dashboard - not properly initialized!");
            return;
        }
        
        if (SceneManager.GetActiveScene().name != mainSceneName)
        {
            LogDebug($"[UIManager] Not in main scene ({mainSceneName}), skipping dashboard update.");
            return;
        }
        
        // Find UI references if needed
        FindUIReferences();
        
        if (dataModel == null)
        {
            LogError("[UIManager] Cannot update dashboard - DataModel is null!");
            return;
        }
        
        LogDebug($"[UIManager] Beginning dashboard update for city ID: '{currentCityId}'");
        LogDebug($"[UIManager] UI References - Logo: {(logoImage != null ? "Found" : "Missing")}, " +
                 $"Instagram: {(instagramFollowersText != null ? "Found" : "Missing")}, " +
                 $"TikTok: {(tiktokFollowersText != null ? "Found" : "Missing")}, " + 
                 $"TikTok Likes: {(tiktokLikesText != null ? "Found" : "Missing")}, " +
                 $"Tickets: {(ticketsSoldText != null ? "Found" : "Missing")}, " +
                 $"Attendance: {(averageAttendanceText != null ? "Found" : "Missing")}, " +
                 $"Events: {(numberOfEventsText != null ? "Found" : "Missing")}");
        
        // Update the logo based on selected city
        UpdateLogo(currentCityId);
        
        // Get the city by ID
        City selectedCity = dataModel.GetCityById(currentCityId);
        if (selectedCity == null)
        {
            LogDebug($"[UIManager] City with ID '{currentCityId}' not found, checking if we need to create it");
            
            // Check if we should create a city for this ID
            if (currentCityId.ToLower() == "bgsnl")
            {
                selectedCity = new City("BGSNL", "bgsnl");
                dataModel.AddCity(selectedCity);
                LogDebug("[UIManager] Created BGSNL city in data model");
            }
            else 
            {
                // Look in our city configs for a matching city ID
                foreach (var cityConfig in cityConfigs)
                {
                    if (cityConfig.cityId.ToLower() == currentCityId.ToLower())
                    {
                        string cityName = !string.IsNullOrEmpty(cityConfig.cityName) ? 
                            cityConfig.cityName : cityConfig.cityId.ToUpper();
                        
                        selectedCity = new City(cityName, cityConfig.cityId.ToLower());
                        dataModel.AddCity(selectedCity);
                        LogDebug($"[UIManager] Created city in data model: {cityName} (ID: {cityConfig.cityId.ToLower()})");
                        break;
                    }
                }
            }
            
            // If still not found, fall back to first city
            if (selectedCity == null)
            {
                if (dataModel.Cities.Count > 0)
                {
                    selectedCity = dataModel.Cities[0];
                    currentCityId = selectedCity.ID;
                    LogDebug($"[UIManager] Falling back to first city: {selectedCity.Name} (ID: {selectedCity.ID})");
                }
                else
                {
                    LogError("[UIManager] No cities found in DataModelClasses and couldn't create one");
            return;
                }
            }
        }
        
        string cityId = selectedCity.ID;
        LogDebug($"[UIManager] Looking for metrics for city: {selectedCity.Name} (ID: {cityId})");
        
        // Check that there's actual data in the model
        if (dataModel.SocialMediaMetrics.Count == 0 && dataModel.EventMetrics.Count == 0)
        {
            LogError("[UIManager] No metrics data found in the model! Try forcing a refresh.");
            if (sheetsService != null)
            {
                LogDebug("[UIManager] Attempting to force refresh data...");
                sheetsService.ForceRefresh();
                
                // Add a small delay to let data load
                StartCoroutine(DelayedRetryUpdateDashboard(1.0f));
                return;
            }
        }
        
        // Dump all metrics data for debugging
        if (debugMode)
        {
            DumpMetricsData();
        }
        
        // Get metrics for selected city
        SocialMediaMetrics socialMetrics = dataModel.GetLatestSocialMediaMetrics(cityId);
        EventMetrics eventMetrics = dataModel.GetLatestEventMetrics(cityId);
        
        // Update social media metrics
        UpdateSocialMediaMetrics(socialMetrics, selectedCity);
        
        // Update event metrics
        UpdateEventMetrics(eventMetrics, selectedCity);
        
        LogDebug("[UIManager] Dashboard update complete.");
    }
    
    private IEnumerator DelayedRetryUpdateDashboard(float delay)
    {
        yield return new WaitForSeconds(delay);
        LogDebug("[UIManager] Retrying dashboard update after data refresh");
        UpdateDashboard();
    }
    
    private void DumpMetricsData()
    {
        Debug.Log("======== METRICS DATA DUMP ========");
        Debug.Log($"City count: {dataModel.Cities.Count}");
        foreach (var city in dataModel.Cities)
        {
            Debug.Log($"City: {city.Name} (ID: {city.ID})");
        }
        
        Debug.Log($"Social media metrics count: {dataModel.SocialMediaMetrics.Count}");
        foreach (var metric in dataModel.SocialMediaMetrics)
        {
            Debug.Log($"Social metrics for {metric.AssociatedCity.Name}: Instagram={metric.InstagramFollowers}, TikTok={metric.TikTokFollowers}, Likes={metric.TikTokLikes}");
        }
        
        Debug.Log($"Event metrics count: {dataModel.EventMetrics.Count}");
        foreach (var metric in dataModel.EventMetrics)
        {
            Debug.Log($"Event metrics for {metric.AssociatedCity.Name}: Tickets={metric.TicketsSold}, Attendance={metric.AverageAttendance}, Events={metric.NumberOfEvents}");
        }
        Debug.Log("===================================");
    }
    
    private void UpdateSocialMediaMetrics(SocialMediaMetrics socialMetrics, City selectedCity)
    {
        if (socialMetrics != null)
        {
            LogDebug($"[UIManager] Found social media metrics for {selectedCity.Name}: Instagram={socialMetrics.InstagramFollowers}, TikTok={socialMetrics.TikTokFollowers}, Likes={socialMetrics.TikTokLikes}");
            
            if (instagramFollowersText != null)
            {
                instagramFollowersText.text = socialMetrics.InstagramFollowers;
                LogDebug($"[UIManager] Set Instagram followers text to: {instagramFollowersText.text}");
            }
            else
            {
                LogError("[UIManager] Instagram followers text component not found!");
            }
            
            if (tiktokFollowersText != null)
            {
                tiktokFollowersText.text = socialMetrics.TikTokFollowers;
                LogDebug($"[UIManager] Set TikTok followers text to: {tiktokFollowersText.text}");
            }
            else
            {
                LogError("[UIManager] TikTok followers text component not found!");
            }
            
            if (tiktokLikesText != null)
            {
                tiktokLikesText.text = socialMetrics.TikTokLikes;
                LogDebug($"[UIManager] Set TikTok likes text to: {tiktokLikesText.text}");
            }
            else
            {
                LogError("[UIManager] TikTok likes text component not found!");
            }
        }
        else
        {
            LogDebug($"[UIManager] No social media metrics found for {selectedCity.Name} (ID: {selectedCity.ID})");
            
            // Set default values
            if (instagramFollowersText != null) instagramFollowersText.text = "0";
            if (tiktokFollowersText != null) tiktokFollowersText.text = "0";
            if (tiktokLikesText != null) tiktokLikesText.text = "0";
        }
    }
    
    private void UpdateEventMetrics(EventMetrics eventMetrics, City selectedCity)
    {
        if (eventMetrics != null)
        {
            LogDebug($"[UIManager] Found event metrics for {selectedCity.Name}: Tickets={eventMetrics.TicketsSold}, Attendance={eventMetrics.AverageAttendance}, Events={eventMetrics.NumberOfEvents}");
            
            if (ticketsSoldText != null)
            {
                ticketsSoldText.text = eventMetrics.TicketsSold;
                LogDebug($"[UIManager] Set tickets sold text to: {ticketsSoldText.text}");
            }
            else
            {
                LogError("[UIManager] Tickets sold text component not found!");
            }
            
            if (averageAttendanceText != null)
            {
                averageAttendanceText.text = eventMetrics.AverageAttendance;
                LogDebug($"[UIManager] Set average attendance text to: {averageAttendanceText.text}");
            }
            else
            {
                LogError("[UIManager] Average attendance text component not found!");
            }
            
            if (numberOfEventsText != null)
            {
                numberOfEventsText.text = eventMetrics.NumberOfEvents;
                LogDebug($"[UIManager] Set number of events text to: {numberOfEventsText.text}");
            }
            else
            {
                LogError("[UIManager] Number of events text component not found!");
            }
        }
        else
        {
            LogDebug($"[UIManager] No event metrics found for {selectedCity.Name} (ID: {selectedCity.ID})");
            
            // Set default values
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
        
        // Default to BGSNL logo
        Sprite logoToUse = bgsnlLogo;
        bool foundCustomLogo = false;
        
        // Print all cityConfigs for debugging
        LogDebug($"[UIManager] Updating logo for city: '{cityId}'");
        
        // BGSNL special case - always use bgsnlLogo
        if (cityId.ToLower() == "bgsnl")
        {
            if (bgsnlLogo != null)
            {
                logoToUse = bgsnlLogo;
                LogDebug($"[UIManager] Using BGSNL logo from bgsnlLogo field: {bgsnlLogo.name}");
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
                        LogDebug($"[UIManager] Found custom logo '{cityConfig.cityLogo.name}' for city: '{cityId}'");
                        foundCustomLogo = true;
                        break;
                    }
                    else
                    {
                        LogError($"[UIManager] City config for '{cityId}' found but has no logo sprite assigned!");
                    }
                }
            }
        }
        
        // Apply the logo
        if (logoToUse != null)
        {
            logoImage.sprite = logoToUse;
            LogDebug($"[UIManager] Updated logo for '{cityId}' to '{logoToUse.name}'");
        }
        else
        {
            LogError($"[UIManager] No logo found for '{cityId}'");
        }
        
        // Log if using default
        if (!foundCustomLogo)
        {
            LogError($"[UIManager] No custom logo found for '{cityId}', using default logo");
        }
    }
    
    /// <summary>
    /// Forces a refresh of the dashboard with current city data
    /// </summary>
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
    
    /// <summary>
    /// Refreshes the dashboard with a specific city ID
    /// </summary>
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
    
    /// <summary>
    /// Load city data for the specified city ID
    /// </summary>
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
    
    /// <summary>
    /// Called when the scene changes to set up UI elements
    /// </summary>
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
        
        // If main scene, update the dashboard
        if (scene.name == mainSceneName)
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
                    
                    // IMPORTANT: Do not clear the backup key yet - we'll keep it until the dashboard is updated
                    // This ensures if there are any late resets, our backup is still there
                }
            }
            
            // Get the city ID from PlayerPrefs
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
                    // Before defaulting to BGSNL, check if we have a preserved city
                    if (PlayerPrefs.HasKey("PullRefresh_PreservedCityId"))
                    {
                        string preservedCity = PlayerPrefs.GetString("PullRefresh_PreservedCityId");
                        if (IsCityValid(preservedCity))
                        {
                            currentCityId = preservedCity;
                            Debug.Log($"Using preserved city as fallback: '{preservedCity}'");
                        }
                        else
                        {
                            Debug.LogError($"[UIManager] City in PlayerPrefs '{savedCity}' is not valid, defaulting to BGSNL");
                            ResetToDefaultCity();
                        }
                }
                else
                {
                    Debug.LogError($"[UIManager] City in PlayerPrefs '{savedCity}' is not valid, defaulting to BGSNL");
                    ResetToDefaultCity();
                }
            }
            }
            else
            {
                // Before defaulting to BGSNL, check if we have a preserved city
                if (PlayerPrefs.HasKey("PullRefresh_PreservedCityId"))
                {
                    string preservedCity = PlayerPrefs.GetString("PullRefresh_PreservedCityId");
                    if (IsCityValid(preservedCity))
                    {
                        currentCityId = preservedCity;
                        Debug.Log($"No city in PlayerPrefs, using preserved city: '{preservedCity}'");
                    }
                    else
                    {
                        // Default to BGSNL if no city is saved
                        ResetToDefaultCity();
                        Debug.Log("[UIManager] No valid city found in PlayerPrefs or backup, defaulting to BGSNL");
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
    
    /// <summary>
    /// Reset to BGSNL when the application quits to ensure it starts with BGSNL next time
    /// </summary>
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
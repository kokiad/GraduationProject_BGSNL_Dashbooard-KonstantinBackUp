using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Linq;

/// <summary>
/// Implements pull-to-refresh functionality for mobile devices
/// Attach this script to a UI container (Panel, Canvas, etc.)
/// </summary>
public class PullToRefresh : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Refresh Settings")]
    [SerializeField] private float pullThreshold = 100f; // How far to pull before triggering refresh
    [SerializeField] private float maxPullDistance = 150f; // Maximum pull distance allowed
    
    [Header("Animation Settings")]
    [SerializeField] private float snapBackDuration = 0.5f; // How long the snap back animation takes
    [SerializeField] private AnimationCurve snapBackCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Animation curve for elastic effect
    [SerializeField] private int overshootBounces = 2; // Number of bounces in the elastic effect
    [SerializeField] private float overshootAmount = 0.2f; // How strong the elastic effect is (0-1)
    
    [Header("Visual Settings")]
    [SerializeField] private GameObject refreshIndicator; // Optional visual indicator for refresh
    [SerializeField] private bool debugMode = true;
    
    [Header("References")]
    [SerializeField] private RectTransform contentRectTransform; // The content that will be pulled
    
    private bool isPulling = false;
    private float startY;
    private float currentPullDistance = 0f;
    private bool isRefreshing = false;
    private Vector3 originalPosition;
    
    private UIManager uiManager;
    private GoogleSheetsService sheetsService;
    private DataModelClasses dataModel;
    private string currentRefreshingCityId = ""; // Track which city we're refreshing
    
    // For debugging data synchronization issues
    private string lastRefreshedCity = "";
    private System.DateTime lastRefreshTime;
    
    private void Awake()
    {
        // If contentRectTransform is not assigned, use this object's RectTransform
        if (contentRectTransform == null)
        {
            contentRectTransform = GetComponent<RectTransform>();
            if (contentRectTransform == null)
            {
                LogError("PullToRefresh requires a RectTransform component!");
                this.enabled = false;
                return;
            }
        }
        
        originalPosition = contentRectTransform.localPosition;
        
        // Find references to required components
        FindReferences();
        
        // Set up a default animation curve if none is provided
        if (snapBackCurve.keys.Length == 0)
        {
            snapBackCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        }
        
        // Initially hide refresh indicator if set
        if (refreshIndicator != null)
        {
            refreshIndicator.SetActive(false);
        }
    }
    
    private void FindReferences()
    {
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
            if (uiManager == null)
            {
                LogWarning("UIManager not found!");
            }
        }
        
        if (sheetsService == null)
        {
            sheetsService = FindObjectOfType<GoogleSheetsService>();
            if (sheetsService == null)
            {
                LogWarning("GoogleSheetsService not found!");
            }
        }
        
        if (dataModel == null)
        {
            dataModel = FindObjectOfType<DataModelClasses>();
            if (dataModel == null)
            {
                LogWarning("DataModelClasses not found!");
            }
        }
    }
    
    private void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[PullToRefresh] {message}");
        }
    }
    
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[PullToRefresh] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[PullToRefresh] {message}");
    }
    
    private void LogDataState()
    {
        if (!debugMode || dataModel == null) return;
        
        LogDebug("CURRENT DATA STATE:");
        
        // Log cities
        LogDebug($"  Cities: {dataModel.Cities.Count}");
        foreach (var city in dataModel.Cities)
        {
            LogDebug($"  - {city.Name} (ID: {city.ID})");
        }
        
        // Log social media metrics
        if (dataModel.SocialMediaMetrics.Count > 0)
        {
            LogDebug($"  Social Media Metrics: {dataModel.SocialMediaMetrics.Count}");
            foreach (var metric in dataModel.SocialMediaMetrics)
            {
                if (metric.AssociatedCity != null)
                {
                    LogDebug($"  - {metric.AssociatedCity.Name}: Instagram={metric.InstagramFollowers}, " +
                            $"TikTok={metric.TikTokFollowers}, Likes={metric.TikTokLikes}, " +
                            $"Time={metric.Timestamp}");
                }
                else
                {
                    LogDebug("  - INVALID METRIC: Associated city is null!");
                }
            }
        }
        else
        {
            LogDebug("  No social media metrics available!");
        }
        
        // Log event metrics
        if (dataModel.EventMetrics.Count > 0)
        {
            LogDebug($"  Event Metrics: {dataModel.EventMetrics.Count}");
            foreach (var metric in dataModel.EventMetrics)
            {
                if (metric.AssociatedCity != null)
                {
                    LogDebug($"  - {metric.AssociatedCity.Name}: Tickets={metric.TicketsSold}, " +
                            $"Attendance={metric.AverageAttendance}, Events={metric.NumberOfEvents}, " +
                            $"Time={metric.Timestamp}");
                }
                else
                {
                    LogDebug("  - INVALID METRIC: Associated city is null!");
                }
            }
        }
        else
        {
            LogDebug("  No event metrics available!");
        }
    }
    
    private void Start()
    {
        // Find references if needed
        FindReferences();
    }
    
    private void OnEnable()
    {
        // Find references if needed
        FindReferences();
        
        // Clear any stale preserved city data when entering a new scene
        if (SceneManager.GetActiveScene().name != "HomeScreen")
        {
            if (PlayerPrefs.HasKey("PullRefresh_PreservedCityId"))
            {
                PlayerPrefs.DeleteKey("PullRefresh_PreservedCityId");
                PlayerPrefs.Save();
                LogDebug("[Critical] Cleared preserved city ID when entering non-home scene");
            }
        }
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        // Only activate for UPWARD drags (negative delta.y means dragging up)
        if (eventData.delta.y > 0)
        {
            return;
        }
        
        // Check if at bottom position instead of top
        if (contentRectTransform.localPosition.y >= originalPosition.y - 5f)
        {
            isPulling = true;
            startY = eventData.position.y;
            currentPullDistance = 0f;
            LogDebug("Begin pull-to-refresh (upward direction)");
            
            // Show refresh indicator if set
            if (refreshIndicator != null)
            {
                refreshIndicator.SetActive(true);
                refreshIndicator.transform.localScale = Vector3.one * 0.5f; // Start small
            }
        }
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (!isPulling) return;
        
        // Calculate pull distance for UPWARD direction (negative value)
        float pullDistance = (startY - eventData.position.y) * 0.5f;
        
        // Don't allow negative pulls (which would be downward in this context)
        if (pullDistance < 0)
        {
            pullDistance = 0;
        }
        
        // Add a slight resistance curve for a more natural feel
        pullDistance = Mathf.Pow(pullDistance, 0.8f);
        
        // Cap the maximum pull distance
        pullDistance = Mathf.Min(pullDistance, maxPullDistance);
        
        currentPullDistance = pullDistance;
        
        // Move content UPWARD based on pull distance
        Vector3 newPosition = originalPosition;
        newPosition.y -= pullDistance; // Subtract to move upward
        contentRectTransform.localPosition = newPosition;
        
        // Update refresh indicator if available
        if (refreshIndicator != null)
        {
            float progressPercent = Mathf.Clamp01(pullDistance / pullThreshold);
            // Smoothly scale up the indicator as we pull
            refreshIndicator.transform.localScale = Vector3.one * (0.5f + 0.5f * progressPercent);
            // Optionally add rotation for a spinner effect
            refreshIndicator.transform.Rotate(0, 0, -progressPercent * 5f);
        }
        
        LogDebug($"Dragging upward: {pullDistance:F1} / {pullThreshold:F1} (max: {maxPullDistance:F1})");
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isPulling) return;
        
        isPulling = false;
        
        // Check if pulled past threshold to trigger refresh
        if (currentPullDistance >= pullThreshold && !isRefreshing)
        {
            TriggerRefresh();
        }
        else
        {
            // Not enough to trigger refresh, just snap back with elastic effect
            StartCoroutine(ElasticSnapBack(false));
        }
    }
    
    private void TriggerRefresh()
    {
        LogDebug("Refresh triggered!");
        isRefreshing = true;
        lastRefreshTime = System.DateTime.Now;
        
        // Get current city ID from PlayerPrefs and store it for the refresh process
        currentRefreshingCityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        lastRefreshedCity = currentRefreshingCityId;
        
        LogDebug($"Starting refresh for city ID: {currentRefreshingCityId}");
        LogDataState();
        
        // Show loading indicator at full size with rotation animation
        if (refreshIndicator != null)
        {
            refreshIndicator.transform.localScale = Vector3.one;
            StartCoroutine(AnimateRefreshIndicator());
        }
        
        // Double-check that we have necessary references
        if (sheetsService == null || uiManager == null)
        {
            FindReferences();
        }
        
        // Start the complete refresh process
        StartCoroutine(ExecuteCompleteRefresh());
    }
    
    private IEnumerator AnimateRefreshIndicator()
    {
        // Continuously rotate the refresh indicator while refreshing
        while (isRefreshing && refreshIndicator != null && refreshIndicator.activeSelf)
        {
            refreshIndicator.transform.Rotate(0, 0, -10f);
            yield return null;
        }
    }
    
    private IEnumerator ExecuteCompleteRefresh()
    {
        LogDebug("Starting refresh process...");
        
        // Get current city ID and store it in our special key
        string currentCity = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        PlayerPrefs.SetString("PullRefresh_PreservedCityId", currentCity);
        PlayerPrefs.Save();
        LogDebug($"[Critical] Preserved city ID for refresh: '{currentCity}'");
        
        // Force refresh the data
        if (sheetsService != null)
        {
            // Start the refresh
            LogDebug("[Critical] Starting data refresh");
            yield return StartCoroutine(sheetsService.RefreshAllData());
            
            // Wait a moment for data to be processed
            yield return new WaitForSeconds(0.5f);
            
            // IMPORTANT: Mimic the exact city button behavior
            LogDebug($"[Critical] Using city button approach for: '{currentCity}'");
            
            // Set up PlayerPrefs exactly like the city buttons do
            PlayerPrefs.SetInt("ForceDefaultCity", 0);
            PlayerPrefs.SetString("SelectedCityId", currentCity);
            PlayerPrefs.Save();
            
            // Load the city data first
            if (uiManager != null)
            {
                uiManager.LoadCity(currentCity);
                yield return new WaitForSeconds(0.3f);
            }
            
            // Complete the refresh animation
            isRefreshing = false;
            if (refreshIndicator != null)
            {
                refreshIndicator.SetActive(false);
            }
            
            // Start the snap back animation
            StartCoroutine(ElasticSnapBack(true));
            
            // CRITICAL: Reload the scene like city buttons do
            // This ensures a complete refresh with proper city context
            LogDebug($"[Critical] Reloading scene to ensure proper refresh");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else
        {
            LogError("Cannot refresh - GoogleSheetsService not found!");
            isRefreshing = false;
            if (refreshIndicator != null)
            {
                refreshIndicator.SetActive(false);
            }
            StartCoroutine(ElasticSnapBack(false));
        }
    }
    
    private bool CheckIfHasData()
    {
        if (dataModel == null)
        {
            LogDebug("[CheckIfHasData] No DataModelClasses found");
            return false;
        }
        
        // Get current city ID from our preserved key
        string cityId = PlayerPrefs.GetString("PullRefresh_PreservedCityId", "bgsnl");
        LogDebug($"[CheckIfHasData] Checking data for city: '{cityId}'");
        
        // Check if we have metrics data for this city
        bool hasSocialData = false;
        bool hasEventData = false;
        
        if (dataModel.SocialMediaMetrics != null && dataModel.SocialMediaMetrics.Count > 0)
        {
            hasSocialData = dataModel.SocialMediaMetrics.Any(m => 
                m.AssociatedCity != null && 
                m.AssociatedCity.ID != null && 
                m.AssociatedCity.ID.ToLower() == cityId.ToLower());
        }
        
        if (dataModel.EventMetrics != null && dataModel.EventMetrics.Count > 0)
        {
            hasEventData = dataModel.EventMetrics.Any(m => 
                m.AssociatedCity != null && 
                m.AssociatedCity.ID != null && 
                m.AssociatedCity.ID.ToLower() == cityId.ToLower());
        }
        
        LogDebug($"[CheckIfHasData] Has social data: {hasSocialData}, Has event data: {hasEventData}");
        return hasSocialData || hasEventData;
    }
    
    private IEnumerator ElasticSnapBack(bool wasRefreshed)
    {
        // If the refresh completed successfully, log results
        if (wasRefreshed)
        {
            LogDebug($"Refresh completed for city: {currentRefreshingCityId}");
            LogDebug($"Refresh duration: {(System.DateTime.Now - lastRefreshTime).TotalSeconds:F1} seconds");
            LogDataState();
        }
        
        LogDebug("Starting elastic snap back animation");
        
        float duration = snapBackDuration;
        float elapsed = 0f;
        Vector3 startPosition = contentRectTransform.localPosition;
        
        // Create keyframes for an oscillating bounce effect
        AnimationCurve bounceCurve = new AnimationCurve();
        
        // Add the starting keyframe
        bounceCurve.AddKey(0f, 0f);
        
        // Add oscillation keyframes
        for (int i = 1; i <= overshootBounces; i++)
        {
            float time = i / (overshootBounces + 1f);
            float value = Mathf.Sin(time * Mathf.PI) * overshootAmount * (1f - time);
            
            // Alternate between positive and negative values for oscillation
            if (i % 2 == 0)
                value = -value;
                
            bounceCurve.AddKey(time, value);
        }
        
        // Add the ending keyframe
        bounceCurve.AddKey(1f, 0f);
        
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            
            // Apply the base easing curve
            float easedT = snapBackCurve.Evaluate(t);
            
            // Basic movement toward original position
            Vector3 basePosition = Vector3.Lerp(startPosition, originalPosition, easedT);
            
            // Add bounce effect perpendicular to the movement direction
            float bounceAmount = bounceCurve.Evaluate(t);
            
            // Apply the bounce offset perpendicular to movement (in this case, horizontally)
            Vector3 finalPosition = basePosition;
            finalPosition.x += bounceAmount * 20f; // Scale the bounce effect
            
            contentRectTransform.localPosition = finalPosition;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Ensure we end exactly at the original position
        contentRectTransform.localPosition = originalPosition;
        
        // Hide refresh indicator
        if (refreshIndicator != null)
        {
            refreshIndicator.SetActive(false);
        }
        
        // Reset refresh state
        isRefreshing = false;
        currentRefreshingCityId = "";
        LogDebug("Elastic snap back complete");
    }

    // Improved direct data fetch method that doesn't clear existing data
    public void FetchFreshDataWithoutClearing()
    {
        if (sheetsService != null)
        {
            LogDebug("Manual fetch of fresh data without clearing");
            
            // Get current city ID
            string cityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
            LogDebug("Current city ID: " + cityId);
            
            // Log current data state
            LogDebug("Data state before refresh:");
            LogDataState();
            
            // Start the direct fetch coroutines
            StartCoroutine(DirectDataFetch(cityId));
        }
        else
        {
            LogError("Cannot fetch data - GoogleSheetsService not found");
        }
    }

    private IEnumerator DirectDataFetch(string cityId)
    {
        LogDebug($"[Critical] Current city to preserve: '{cityId}'");
        
        // IMPORTANT: Create our own backup of city ID that persists through refresh
        if (!string.IsNullOrEmpty(cityId))
        {
            // Store in a special key that won't be overwritten during refresh
            PlayerPrefs.SetString("PullRefresh_PreservedCityId", cityId);
            PlayerPrefs.Save();
            LogDebug($"[Critical] Backed up city ID to special key: '{cityId}'");
        }
        
        // EXACT BGSNL BUTTON APPROACH: Force reload scene with city ID saved in PlayerPrefs
        LogDebug($"[IMPORTANT DEBUG] Using BGSNL button approach for city: '{cityId}'");
        
        // Save the city ID to PlayerPrefs exactly as the BGSNL button does
        PlayerPrefs.SetInt("ForceDefaultCity", 0); // Tell system not to reset to default
        PlayerPrefs.SetString("SelectedCityId", cityId);
        PlayerPrefs.Save();
        LogDebug($"Saved city ID '{cityId}' to PlayerPrefs");
        
        // Wait a moment before reloading scene
        yield return new WaitForSeconds(0.3f);
        
        // Instead of just refreshing, reload the entire scene like the BGSNL button does
        string currentScene = SceneManager.GetActiveScene().name;
        LogDebug($"Reloading scene '{currentScene}' with city ID '{cityId}' from PlayerPrefs");
        
        // This is the key difference - reload entire scene to force complete refresh
        SceneManager.LoadScene(currentScene);
        
        // This code won't execute since we're reloading the scene, but included for completeness
        LogDebug("Scene reload initiated");
    }
} 
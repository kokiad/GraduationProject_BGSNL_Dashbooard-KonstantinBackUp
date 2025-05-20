using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;

public class GoogleSheetsService : MonoBehaviour
{
    [Header("Google Sheets API Configuration")]
    [SerializeField] private string apiKey;
    [SerializeField] private string spreadsheetId;
    [SerializeField] private string socialMediaSheetName = "SocialMedia";
    [SerializeField] private string eventsSheetName = "Events";
    
    [Header("Cache Settings")]
    [SerializeField] private string cacheDirectory = "GoogleSheetsCache";
    [SerializeField] private float cacheDurationHours = 24f;
    
    [Header("References")]
    [SerializeField] private DataModelClasses dataModel;
    
    private const string API_URL_FORMAT = "https://sheets.googleapis.com/v4/spreadsheets/{0}/values/{1}?key={2}";
    private Dictionary<string, DateTime> lastFetchTimes = new Dictionary<string, DateTime>();
    
    // Add a property to access the currently selected city
    public string selectedCity
    {
        get
        {
            // Always read from PlayerPrefs to ensure we have the latest value
            return PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        }
    }
    
    private void Awake()
    {
        Debug.Log(@"GoogleSheetsService: Initializing
IMPORTANT SETUP INFORMATION:
1. Make sure your Google Sheets are public (File -> Share -> Anyone with the link -> Viewer)
2. Verify that your sheets are named exactly 'SocialMedia' and 'Events' (case sensitive)
3. Each sheet should have headers in row 1 with these columns:
   - SocialMedia: city_id, instagram_followers, tiktok_followers, tiktok_likes, timestamp
   - Events: city_id, tickets_sold, average_attendance, number_of_events, timestamp
4. Make sure you have data rows below the headers
5. Verify your API key has Google Sheets API access enabled");
        
        // Ensure cache directory exists
        string cachePath = Path.Combine(Application.persistentDataPath, cacheDirectory);
        if (!Directory.Exists(cachePath))
        {
            Directory.CreateDirectory(cachePath);
        }
        
        if (dataModel == null)
        {
            dataModel = FindObjectOfType<DataModelClasses>();
            if (dataModel == null)
            {
                Debug.LogError("DataModelClasses reference not set and could not be found in scene!");
            }
        }
    }
    
    private void Start()
    {
        Debug.Log("GoogleSheetsService: Starting data loading process");
        Debug.Log($"Using spreadsheet ID: {spreadsheetId}");
        Debug.Log($"Social Media sheet name: {socialMediaSheetName}");
        Debug.Log($"Events sheet name: {eventsSheetName}");
        
        // Display spreadsheet URL for easy access
        Debug.Log($"Spreadsheet URL: https://docs.google.com/spreadsheets/d/{spreadsheetId}/edit");
        
        // Display API endpoints for manual verification
        Debug.Log($"Social Media API URL: {string.Format(API_URL_FORMAT, spreadsheetId, $"{socialMediaSheetName}!A1:Z1000", "[your-api-key]")}");
        Debug.Log($"Events API URL: {string.Format(API_URL_FORMAT, spreadsheetId, $"{eventsSheetName}!A1:Z1000", "[your-api-key]")}");
        
        // Verify access with a simple test
        StartCoroutine(VerifyApiAccess());
        
        // First try to refresh from the network
        StartCoroutine(RefreshAllDataAndFallbackToCache());
    }
    
    /// <summary>
    /// Verify that the API key and permissions are set up correctly
    /// </summary>
    private IEnumerator VerifyApiAccess()
    {
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(spreadsheetId))
        {
            Debug.LogError("API Key or Spreadsheet ID not configured!");
            yield break;
        }
        
        // Try accessing basic spreadsheet metadata which requires less permissions
        string metadataUrl = $"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}?key={apiKey}";
        Debug.Log($"Verifying API access with metadata URL (credentials hidden)");
        
        using (UnityWebRequest request = UnityWebRequest.Get(metadataUrl))
        {
            yield return request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"API Access Verification FAILED: {request.error}");
                Debug.LogError("Please check if your API key has Google Sheets API access enabled in the Google Cloud Console");
                Debug.LogError("Also verify that your spreadsheet permissions are set to 'Anyone with the link can view'");
            }
            else
            {
                Debug.Log("API Access Verification Successful - Connection to Google Sheets API is working");
                
                // Check if the response contains sheet information
                string response = request.downloadHandler.text;
                if (response.Contains("\"sheets\":[") && response.Contains("\"properties\":{"))
                {
                    Debug.Log("Spreadsheet metadata accessed successfully");
                    
                    // Check if SocialMedia sheet exists
                    if (response.Contains($"\"title\":\"{socialMediaSheetName}\""))
                    {
                        Debug.Log($"'{socialMediaSheetName}' sheet exists in the spreadsheet");
                    }
                    else
                    {
                        Debug.LogError($"'{socialMediaSheetName}' sheet NOT FOUND in the spreadsheet!");
                    }
                    
                    // Check if Events sheet exists
                    if (response.Contains($"\"title\":\"{eventsSheetName}\""))
                    {
                        Debug.Log($"'{eventsSheetName}' sheet exists in the spreadsheet");
                    }
                    else
                    {
                        Debug.LogError($"'{eventsSheetName}' sheet NOT FOUND in the spreadsheet!");
                    }
                }
                else
                {
                    Debug.LogWarning("API connection successful but unable to verify sheet names");
                }
            }
        }
    }
    
    /// <summary>
    /// Refreshes data from network, and falls back to cache if network fails
    /// </summary>
    private IEnumerator RefreshAllDataAndFallbackToCache()
    {
        bool networkSuccess = true;
        
        // Check for preserved city ID first
        string originalCityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        if (PlayerPrefs.HasKey("PullRefresh_PreservedCityId"))
        {
            string preservedCity = PlayerPrefs.GetString("PullRefresh_PreservedCityId");
            Debug.Log($"[GoogleSheetsService] Found preserved city ID: '{preservedCity}', using this instead of '{originalCityId}'");
            originalCityId = preservedCity;
        }
        Debug.Log($"[GoogleSheetsService] Preserving city selection: '{originalCityId}'");
        
        // Backup existing data before refresh
        List<SocialMediaMetrics> socialMediaBackup = null;
        List<EventMetrics> eventMetricsBackup = null;
        
        if (dataModel != null)
        {
            // Create backups of existing data
            if (dataModel.SocialMediaMetrics != null && dataModel.SocialMediaMetrics.Count > 0)
            {
                socialMediaBackup = new List<SocialMediaMetrics>(dataModel.SocialMediaMetrics);
                Debug.Log($"Backed up {socialMediaBackup.Count} social media metrics");
            }
            
            if (dataModel.EventMetrics != null && dataModel.EventMetrics.Count > 0)
            {
                eventMetricsBackup = new List<EventMetrics>(dataModel.EventMetrics);
                Debug.Log($"Backed up {eventMetricsBackup.Count} event metrics");
            }
            
            // Clear existing data before refresh - but we have backups now
            dataModel.ClearSocialMediaMetrics();
            dataModel.ClearEventMetrics();
        }
        
        // Try to fetch from network first
        Debug.Log("Attempting to fetch data from network...");
        
        // Fetch social media data
        string socialMediaRange = $"{socialMediaSheetName}!A1:Z1000";
        yield return FetchSheetData(socialMediaRange, ProcessSocialMediaData, true);
        
        // Fetch event data
        string eventsRange = $"{eventsSheetName}!A1:Z1000";
        yield return FetchSheetData(eventsRange, ProcessEventData, true);
        
        // Check if we have metrics data
        if (dataModel.SocialMediaMetrics.Count == 0 || dataModel.EventMetrics.Count == 0)
        {
            Debug.Log("Network fetch failed to get all data. Restoring backup or falling back to cache...");
            networkSuccess = false;
            
            // First try to restore from our backups
            if ((socialMediaBackup != null && socialMediaBackup.Count > 0) ||
                (eventMetricsBackup != null && eventMetricsBackup.Count > 0))
            {
                Debug.Log("Restoring data from backup...");
                
                // Clear any partial data that might have come through
                dataModel.ClearSocialMediaMetrics();
                dataModel.ClearEventMetrics();
                
                // Restore from backups
                if (socialMediaBackup != null && socialMediaBackup.Count > 0)
                {
                    foreach (var metric in socialMediaBackup)
                    {
                        dataModel.AddSocialMediaMetrics(metric);
                    }
                    Debug.Log($"Restored {socialMediaBackup.Count} social media metrics from backup");
                }
                
                if (eventMetricsBackup != null && eventMetricsBackup.Count > 0)
                {
                    foreach (var metric in eventMetricsBackup)
                    {
                        dataModel.AddEventMetrics(metric);
                    }
                    Debug.Log($"Restored {eventMetricsBackup.Count} event metrics from backup");
                }
            }
            else
            {
                // No backups available, try cache as a last resort
                Debug.Log("No backup available, falling back to cache...");
            
            // Clear data again before loading from cache
            dataModel.ClearSocialMediaMetrics();
            dataModel.ClearEventMetrics();
            
            // Try to load cached data as fallback
            LoadCachedData();
            }
        }
        
        if (networkSuccess)
        {
            Debug.Log("Successfully refreshed all data from network");
        }
        else if (dataModel.SocialMediaMetrics.Count > 0 && dataModel.EventMetrics.Count > 0)
        {
            if (socialMediaBackup != null || eventMetricsBackup != null)
            {
                Debug.Log("Successfully restored data from backup");
            }
            else
        {
            Debug.Log("Successfully loaded data from cache");
            }
        }
        else
        {
            Debug.LogWarning("Failed to load data from network, backup, and cache");
        }
    }
    
    /// <summary>
    /// Fetches social media metrics data from Google Sheets
    /// </summary>
    public IEnumerator FetchSocialMediaData()
    {
        string sheetRange = $"{socialMediaSheetName}!A1:Z1000"; // Adjust range as needed
        yield return FetchSheetData(sheetRange, ProcessSocialMediaData);
    }
    
    /// <summary>
    /// Fetches event metrics data from Google Sheets
    /// </summary>
    public IEnumerator FetchEventData()
    {
        string sheetRange = $"{eventsSheetName}!A1:Z1000"; // Adjust range as needed
        yield return FetchSheetData(sheetRange, ProcessEventData);
    }
    
    /// <summary>
    /// Generic method to fetch data from a specific sheet and process it
    /// </summary>
    private IEnumerator FetchSheetData(string sheetRange, Action<List<List<string>>> processDataCallback, bool forceRefresh = false)
    {
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(spreadsheetId))
        {
            Debug.LogError("API Key or Spreadsheet ID not configured!");
            yield break;
        }
        
        // Check if we need to refresh the cache
        bool shouldRefresh = forceRefresh;
        string cacheKey = sheetRange.Split('!')[0];
        
        if (!shouldRefresh && lastFetchTimes.ContainsKey(cacheKey))
        {
            TimeSpan elapsed = DateTime.Now - lastFetchTimes[cacheKey];
            shouldRefresh = elapsed.TotalHours >= cacheDurationHours;
        }
        
        if (!shouldRefresh)
        {
            Debug.Log($"Using cached data for {cacheKey} (cache still valid)");
            yield break;
        }
        
        string url = string.Format(API_URL_FORMAT, spreadsheetId, sheetRange, apiKey);
        
        Debug.Log($"Requesting data from URL: {url}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error fetching sheet data: {request.error}");
                
                // Try to use cached data as fallback
                if (TryLoadFromCache(cacheKey, out string cachedJson))
                {
                    Debug.Log($"Using cached data for {cacheKey} as fallback due to network error");
                    try
                    {
                        ProcessJsonResponse(cachedJson, processDataCallback);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error processing cached data: {ex.Message}");
                    }
                }
                
                yield break;
            }
            
            string jsonResult = request.downloadHandler.text;
            
            // Special logging for Events sheet to help diagnose problems
            if (cacheKey.ToLower() == "events")
            {
                Debug.Log("=== DETAILED EVENTS SHEET RESPONSE INSPECTION ===");
                Debug.Log($"Raw JSON response length: {jsonResult.Length}");
                
                // Check specific parts of the JSON
                bool hasValues = jsonResult.Contains("\"values\":");
                bool hasRangeField = jsonResult.Contains("\"range\":");
                bool hasMajorDimensionField = jsonResult.Contains("\"majorDimension\":");
                
                Debug.Log($"JSON contents check - has values: {hasValues}, has range: {hasRangeField}, has majorDimension: {hasMajorDimensionField}");
                
                // Try to extract the values part
                if (hasValues)
                {
                    int valuesIndex = jsonResult.IndexOf("\"values\":");
                    string valuesPart = jsonResult.Substring(valuesIndex + 9, Math.Min(200, jsonResult.Length - valuesIndex - 9));
                    Debug.Log($"Values start: {valuesPart}");
                }
                
                // Examine data rows count 
                if (jsonResult.Contains("[[") && jsonResult.Contains("]]"))
                {
                    string[] rows = jsonResult.Split(new[] {"],["}, StringSplitOptions.None);
                    Debug.Log($"Approximate row count: {rows.Length}");
                }
                
                Debug.Log("=== END EVENTS SHEET INSPECTION ===");
            }
            
            // Debug the raw response
            Debug.Log($"Raw JSON response for {cacheKey}: {jsonResult}");

            // Cache the results
            SaveToCache(cacheKey, jsonResult);
            lastFetchTimes[cacheKey] = DateTime.Now;
            
            // Process the data
            ProcessJsonResponse(jsonResult, processDataCallback);
        }
    }
    
    /// <summary>
    /// Processes the JSON response from Google Sheets API
    /// </summary>
    private void ProcessJsonResponse(string jsonResponse, Action<List<List<string>>> processCallback)
    {
        try
        {
            // Parse the JSON response
            Debug.Log($"Attempting to parse JSON: {jsonResponse.Substring(0, Math.Min(200, jsonResponse.Length))}...");
            
            // Check if the response contains the structure we expect
            if (jsonResponse.Contains("\"values\":"))
            {
                Debug.Log("JSON contains 'values' property");
            }
            else
            {
                Debug.LogError("JSON does not contain 'values' property. Full response: " + jsonResponse);
                return;
            }
            
            List<List<string>> values = null;
            
            // Try using the GoogleSheetsResponse class first
            try
            {
                GoogleSheetsResponse gsResponse = JsonUtility.FromJson<GoogleSheetsResponse>(jsonResponse);
                
                if (gsResponse != null && gsResponse.values != null && gsResponse.values.Length > 0)
                {
                    Debug.Log($"Successfully parsed with GoogleSheetsResponse: {gsResponse.values.Length} rows");
                    
                    // Convert string[][] to List<List<string>>
                    values = new List<List<string>>();
                    foreach (string[] row in gsResponse.values)
                    {
                        values.Add(new List<string>(row));
                    }
                }
                else
                {
                    Debug.LogWarning("GoogleSheetsResponse parsing returned no data");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error parsing with GoogleSheetsResponse: {ex.Message}");
            }
            
            // Try the custom parser if GoogleSheetsResponse didn't work
            if (values == null || values.Count == 0)
            {
                Debug.Log("Trying custom parser");
                values = ParseGoogleSheetsJson(jsonResponse);
            }
            
            // Try manual extraction if custom parser didn't work
            if (values == null || values.Count == 0)
            {
                Debug.LogWarning("Custom parser found no data in response");
                
                // Try to directly parse using string manipulation
                if (jsonResponse.Contains("\"values\":[[") && jsonResponse.Contains("]]"))
                {
                    Debug.Log("Found values array pattern, attempting manual extraction");
                    string simpleExtract = jsonResponse.Substring(
                        jsonResponse.IndexOf("\"values\":[[") + 10,
                        jsonResponse.LastIndexOf("]]") - jsonResponse.IndexOf("\"values\":[[") - 8
                    );
                    Debug.Log($"Extracted array content: {simpleExtract}");
                    
                    // Parse simple comma-separated values for testing
                    List<List<string>> manualValues = new List<List<string>>();
                    string[] rows = simpleExtract.Split(new[] { "],[" }, StringSplitOptions.None);
                    foreach (string row in rows)
                    {
                        List<string> cellValues = new List<string>();
                        string cleanRow = row.Trim('[', ']', '"');
                        string[] cells = cleanRow.Split(new[] { "\",\"" }, StringSplitOptions.None);
                        foreach (string cell in cells)
                        {
                            cellValues.Add(cell.Trim('"'));
                        }
                        manualValues.Add(cellValues);
                    }
                    
                    if (manualValues.Count > 0)
                    {
                        Debug.Log($"Manual parsing found {manualValues.Count} rows");
                        values = manualValues;
                    }
                }
                
                // Fallback to Unity's JsonUtility with SheetResponse if still no values
                if (values == null || values.Count == 0) 
                {
                    Debug.Log("Attempting JsonUtility fallback with SheetResponse");
                    SheetResponse response = JsonUtility.FromJson<SheetResponse>(jsonResponse);
                    
                    if (response != null && response.values != null && response.values.Count > 0)
                    {
                        values = response.values;
                        Debug.Log("Successfully parsed JSON using JsonUtility fallback");
                    }
                    else
                    {
                        Debug.LogError("All parsing methods failed to extract data from response");
                        return;
                    }
                }
            }
            
            // Log the successful parsing results
            Debug.Log($"Successfully parsed {values.Count} rows of data");
            if (values.Count > 0)
            {
                Debug.Log($"First row has {values[0].Count} columns");
                string firstRowSample = string.Join(", ", values[0].Take(Math.Min(5, values[0].Count)));
                Debug.Log($"First row sample: {firstRowSample}");
            }
            
            // Pass the parsed data to the appropriate callback
            processCallback(values);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error parsing Google Sheets response: {ex.Message}");
            Debug.LogError($"Stack trace: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// Processes social media metrics data
    /// </summary>
    private void ProcessSocialMediaData(List<List<string>> values)
    {
        Debug.Log("=== SOCIAL MEDIA PROCESSING STARTED ===");
        
        if (values == null || values.Count < 2)
        {
            Debug.LogWarning("Not enough data in social media sheet");
            return;
        }
        
        // Get header row for column mapping
        List<string> headers = values[0];
        Dictionary<string, int> columnMap = CreateColumnMap(headers);
        
        // Verify we have the required columns
        string[] requiredColumns = { "city_id", "instagram_followers", "tiktok_followers", "tiktok_likes" };
        foreach (var column in requiredColumns)
        {
            if (!columnMap.ContainsKey(column))
            {
                Debug.LogError($"Required column '{column}' not found in sheet headers!");
                return;
            }
        }
        
        Debug.Log($"SocialMedia sheet headers: {string.Join(", ", headers)}");
        
        // Clear existing social media data to prevent duplicates
        if (dataModel != null)
        {
            dataModel.ClearSocialMediaMetrics();
        }
        
        // Dictionary to track the best metrics for each city to prevent zeroes overriding non-zeroes
        Dictionary<string, SocialMediaMetrics> bestMetrics = new Dictionary<string, SocialMediaMetrics>();
        
        // Process data rows
        for (int i = 1; i < values.Count; i++)
        {
            List<string> row = values[i];
            
            try
            {
                // Get city_id first - it's required
                int cityIdIndex = columnMap["city_id"];
                if (cityIdIndex >= row.Count)
                {
                    Debug.LogWarning($"Row {i} doesn't have enough columns to contain city_id");
                    continue;
                }
                
                string cityId = row[cityIdIndex].ToLower().Trim();
                if (string.IsNullOrEmpty(cityId))
                {
                    if (i == 1)
                    {
                        cityId = "bgsnl";
                        Debug.Log("First row in SocialMedia sheet has been assigned ID 'bgsnl'");
                    }
                    else
                    {
                        Debug.LogWarning($"Missing city ID in row {i}");
                        continue;
                    }
                }
                
                // Special debug logging for Eindhoven and Maastricht
                if (cityId == "bgse" || cityId == "bgsm")
                {
                    Debug.Log($"[CRITICAL DEBUG] Found {cityId} data in row {i}:");
                    Debug.Log($"[CRITICAL DEBUG] Raw row data: {string.Join(" | ", row)}");
                }
                
                // Find the associated city
                City city = dataModel.GetCityById(cityId);
                if (city == null)
                {
                    if (cityId.ToLower() == "bgsnl")
                    {
                        city = new City("BGSNL", "bgsnl");
                        dataModel.AddCity(city);
                        Debug.Log("Created default BGSNL city entry for SocialMedia");
                    }
                    else
                    {
                        Debug.LogWarning($"Unknown city ID: {cityId}");
                        continue;
                    }
                }
                
                // Extract metrics values safely
                string instagramFollowers = "0";
                string tiktokFollowers = "0";
                string tiktokLikes = "0";
                
                if (columnMap["instagram_followers"] < row.Count)
                {
                    instagramFollowers = row[columnMap["instagram_followers"]];
                }
                
                if (columnMap["tiktok_followers"] < row.Count)
                {
                    tiktokFollowers = row[columnMap["tiktok_followers"]];
                }
                
                if (columnMap["tiktok_likes"] < row.Count)
                {
                    tiktokLikes = row[columnMap["tiktok_likes"]];
                }
                
                // Create metrics object
                SocialMediaMetrics metrics = new SocialMediaMetrics(
                    instagramFollowers,
                    tiktokFollowers,
                    tiktokLikes,
                    city,
                    DateTime.Now
                );
                
                // Special debug logging for metrics creation
                if (cityId == "bgse" || cityId == "bgsm")
                {
                    Debug.Log($"[CRITICAL DEBUG] Created metrics for {cityId}:");
                    Debug.Log($"[CRITICAL DEBUG] Instagram: {metrics.InstagramFollowers}");
                    Debug.Log($"[CRITICAL DEBUG] TikTok: {metrics.TikTokFollowers}");
                    Debug.Log($"[CRITICAL DEBUG] Likes: {metrics.TikTokLikes}");
                }
                
                // Only add to dataModel if this is the best metrics for this city
                bool shouldAdd = true;
                if (bestMetrics.ContainsKey(cityId))
                {
                    var existing = bestMetrics[cityId];
                    
                    // If existing metrics has non-zero values and current one has all zeroes, don't replace
                    if ((existing.InstagramFollowers != "0" || existing.TikTokFollowers != "0" || existing.TikTokLikes != "0") &&
                        (metrics.InstagramFollowers == "0" && metrics.TikTokFollowers == "0" && metrics.TikTokLikes == "0"))
                    {
                        shouldAdd = false;
                        Debug.Log($"[CRITICAL DEBUG] Skipping zero-value metrics for {city.Name} (ID: {city.ID}) because better metrics exist");
                    }
                    else if (metrics.InstagramFollowers != "0" || metrics.TikTokFollowers != "0" || metrics.TikTokLikes != "0")
                    {
                        shouldAdd = true;
                        Debug.Log($"[CRITICAL DEBUG] Replacing existing metrics for {city.Name} (ID: {city.ID}) with better non-zero metrics");
                    }
                }
                
                if (shouldAdd)
                {
                    bestMetrics[cityId] = metrics;
                    Debug.Log($"[CRITICAL DEBUG] Added/Updated best metrics for {city.Name} (ID: {city.ID})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing social media row {i}: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
        
        // Now add all the best metrics to the data model
        foreach (var metric in bestMetrics.Values)
        {
            dataModel.AddSocialMediaMetrics(metric);
            Debug.Log($"[CRITICAL DEBUG] Final social media metrics for {metric.AssociatedCity.Name} (ID: {metric.AssociatedCity.ID}) - " +
                     $"Instagram: {metric.InstagramFollowers}, TikTok: {metric.TikTokFollowers}, Likes: {metric.TikTokLikes}");
        }
        
        Debug.Log($"Processed {values.Count - 1} social media metrics entries, added {bestMetrics.Count} final metrics");
        Debug.Log("=== SOCIAL MEDIA PROCESSING COMPLETED ===");
    }
    
    /// <summary>
    /// Processes event metrics data
    /// </summary>
    private void ProcessEventData(List<List<string>> values)
    {
        Debug.Log("=== EVENTS PROCESSING STARTED ===");
        
        if (values == null || values.Count < 2)
        {
            Debug.LogWarning("Not enough data in events sheet");
            return;
        }
        
        // Get header row for column mapping
        List<string> headers = values[0];
        Dictionary<string, int> columnMap = CreateColumnMap(headers);
        
        Debug.Log($"Events sheet headers: {string.Join(", ", headers)}");
        
        // Clear existing events data to prevent duplicates
        if (dataModel != null)
        {
            dataModel.ClearEventMetrics();
        }
        
        // Dictionary to track the best metrics for each city to prevent zeroes overriding non-zeroes
        Dictionary<string, EventMetrics> bestMetrics = new Dictionary<string, EventMetrics>();
        
        // Process data rows
        for (int i = 1; i < values.Count; i++)
        {
            List<string> row = values[i];
            if (row.Count < headers.Count) 
            {
                Debug.LogWarning($"Events row {i} has fewer columns ({row.Count}) than headers ({headers.Count})");
                continue;
            }
            
            try
            {
                Dictionary<string, string> rawData = new Dictionary<string, string>();
                
                // Map column values based on headers
                foreach (var column in columnMap)
                {
                    if (column.Value < row.Count)
                    {
                        rawData[column.Key.ToLower()] = row[column.Value];
                    }
                }
                
                // Log raw data for debugging
                Debug.Log($"Processing Events row {i}: {string.Join(", ", rawData.Select(kv => $"{kv.Key}={kv.Value}"))}");
                
                // Extract city ID - handling special case for first data row
                string cityId;
                if (i == 1) 
                {
                    // First data row is for BGSNL (national combined metrics)
                    // If city_id is missing or empty, set a default value of "bgsnl"
                    if (!rawData.TryGetValue("city_id", out cityId) || string.IsNullOrEmpty(cityId))
                    {
                        cityId = "bgsnl";
                        Debug.Log("First row in Events sheet has been assigned ID 'bgsnl'");
                    }
                }
                else 
                {
                    // For other rows, require a city ID
                    if (!rawData.TryGetValue("city_id", out cityId) || string.IsNullOrEmpty(cityId))
                    {
                        Debug.LogWarning($"Missing city ID in row {i}");
                        continue;
                    }
                }
                
                // Normalize city ID (lowercase)
                cityId = cityId.ToLower().Trim();
                Debug.Log($"Events row {i} city ID: '{cityId}'");
                
                // Find the associated city
                City city = dataModel.GetCityById(cityId);
                if (city == null)
                {
                    // If it's the BGSNL data row but no city exists for it, create a default one
                    if (cityId.ToLower() == "bgsnl")
                    {
                        city = new City("BGSNL", "bgsnl");
                        dataModel.AddCity(city);
                        Debug.Log("Created default BGSNL city entry for Events");
                    }
                    else
                    {
                        Debug.LogWarning($"Unknown city ID: {cityId}");
                        continue;
                    }
                }
                
                // Check if required fields exist
                bool hasTicketsSold = rawData.ContainsKey("tickets_sold");
                bool hasAverageAttendance = rawData.ContainsKey("average_attendance");
                bool hasNumberOfEvents = rawData.ContainsKey("number_of_events");
                
                Debug.Log($"Events row {i} has fields - tickets_sold: {hasTicketsSold}, " +
                          $"average_attendance: {hasAverageAttendance}, number_of_events: {hasNumberOfEvents}");
                
                // Create and populate metrics object
                EventMetrics metrics = new EventMetrics("0", "0", "0", city, DateTime.Now);
                metrics.UpdateFromRawData(rawData);
                
                // Log the metrics
                Debug.Log($"Created events metrics for {city.Name} (ID: {city.ID}) - " +
                          $"Tickets: {metrics.TicketsSold}, Attendance: {metrics.AverageAttendance}, Events: {metrics.NumberOfEvents}");
                
                // Only add to dataModel if this is the best metrics for this city
                bool shouldAdd = true;
                if (bestMetrics.ContainsKey(cityId))
                {
                    var existing = bestMetrics[cityId];
                    
                    // If existing metrics has non-zero values and current one has all zeroes, don't replace
                    if ((existing.TicketsSold != "0" || existing.AverageAttendance != "0" || existing.NumberOfEvents != "0") &&
                        (metrics.TicketsSold == "0" && metrics.AverageAttendance == "0" && metrics.NumberOfEvents == "0"))
                    {
                        shouldAdd = false;
                        Debug.Log($"Skipping zero-value metrics for {city.Name} (ID: {city.ID}) because better metrics exist");
                    }
                    else if (metrics.TicketsSold != "0" || metrics.AverageAttendance != "0" || metrics.NumberOfEvents != "0")
                    {
                        shouldAdd = true;
                        Debug.Log($"Replacing existing metrics for {city.Name} (ID: {city.ID}) with better non-zero metrics");
                    }
                }
                
                if (shouldAdd)
                {
                    bestMetrics[cityId] = metrics;
                    Debug.Log($"Added/Updated best metrics for {city.Name} (ID: {city.ID})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing event row {i}: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
        
        // Now add all the best metrics to the data model
        foreach (var metric in bestMetrics.Values)
        {
            dataModel.AddEventMetrics(metric);
            Debug.Log($"Final event metrics for {metric.AssociatedCity.Name} (ID: {metric.AssociatedCity.ID}) - " +
                     $"Tickets: {metric.TicketsSold}, Attendance: {metric.AverageAttendance}, Events: {metric.NumberOfEvents}");
        }
        
        Debug.Log($"Processed {values.Count - 1} event metrics entries, added {bestMetrics.Count} final metrics");
        Debug.Log("=== EVENTS PROCESSING COMPLETED ===");
    }
    
    /// <summary>
    /// Creates a mapping of column names to indices
    /// </summary>
    private Dictionary<string, int> CreateColumnMap(List<string> headers)
    {
        Dictionary<string, int> map = new Dictionary<string, int>();
        
        Debug.Log($"[CRITICAL] Creating column map from headers: {string.Join(", ", headers)}");
        
        for (int i = 0; i < headers.Count; i++)
        {
            string header = headers[i].Trim().ToLower().Replace(" ", "_");
            map[header] = i;
            Debug.Log($"[CRITICAL] Mapped header '{headers[i]}' -> '{header}' to index {i}");
        }
        
        // Log the final map
        Debug.Log("[CRITICAL] Final column map:");
        foreach (var kvp in map)
        {
            Debug.Log($"[CRITICAL] {kvp.Key} -> {kvp.Value}");
        }
        
        return map;
    }
    
    /// <summary>
    /// Saves data to the cache
    /// </summary>
    private void SaveToCache(string key, string jsonData)
    {
        try
        {
            string filePath = Path.Combine(Application.persistentDataPath, cacheDirectory, $"{key}.json");
            File.WriteAllText(filePath, jsonData, Encoding.UTF8);
            Debug.Log($"Data cached for {key}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving to cache: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Tries to load data from the cache
    /// </summary>
    private bool TryLoadFromCache(string key, out string jsonData)
    {
        jsonData = null;
        try
        {
            string filePath = Path.Combine(Application.persistentDataPath, cacheDirectory, $"{key}.json");
            if (File.Exists(filePath))
            {
                jsonData = File.ReadAllText(filePath, Encoding.UTF8);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading from cache: {ex.Message}");
        }
        return false;
    }
    
    /// <summary>
    /// Loads all cached data
    /// </summary>
    private void LoadCachedData()
    {
        try
        {
            string cachePath = Path.Combine(Application.persistentDataPath, cacheDirectory);
            if (!Directory.Exists(cachePath)) return;
            
            string[] files = Directory.GetFiles(cachePath, "*.json");
            
            foreach (string file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string jsonData = File.ReadAllText(file, Encoding.UTF8);
                
                if (fileName == socialMediaSheetName)
                {
                    ProcessJsonResponse(jsonData, ProcessSocialMediaData);
                }
                else if (fileName == eventsSheetName)
                {
                    ProcessJsonResponse(jsonData, ProcessEventData);
                }
            }
            
            Debug.Log($"Loaded {files.Length} cached data files");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading cached data: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Forces a refresh of all data
    /// </summary>
    public void ForceRefresh()
    {
        StartCoroutine(RefreshAllData());
    }

    // Add a custom JSON parser for Google Sheets API response
    private List<List<string>> ParseGoogleSheetsJson(string json)
    {
        try
        {
            Debug.Log("Entering custom JSON parser");
            
            // Check if the JSON is in a valid format
            if (!json.Contains("\"values\":"))
            {
                Debug.LogError("JSON doesn't contain 'values' key");
                return null;
            }
            
            // First, clean up the response to handle possible formatting issues
            // We'll extract just the values array to make parsing simpler
            int valuesIndex = json.IndexOf("\"values\":");
            if (valuesIndex < 0)
            {
                Debug.LogError("Couldn't find 'values' field in JSON");
                return null;
            }
            
            // Extract the values portion (skipping "values":)
            string valuesJson = json.Substring(valuesIndex + 9);
            
            // Find the actual array start
            int arrayStart = valuesJson.IndexOf('[');
            if (arrayStart < 0)
            {
                Debug.LogError("No array start found in values JSON");
                return null;
            }
            
            // Find the corresponding array end by counting brackets
            int openBrackets = 0;
            int arrayEnd = -1;
            
            for (int i = arrayStart; i < valuesJson.Length; i++)
            {
                if (valuesJson[i] == '[') openBrackets++;
                if (valuesJson[i] == ']') 
                {
                    openBrackets--;
                    if (openBrackets == 0)
                    {
                        arrayEnd = i;
                        break;
                    }
                }
            }
            
            if (arrayEnd < 0)
            {
                Debug.LogError("Couldn't find matching closing bracket for values array");
                return null;
            }
            
            // Extract just the values array
            string valuesArray = valuesJson.Substring(arrayStart, arrayEnd - arrayStart + 1);
            Debug.Log($"Extracted values array: {valuesArray.Substring(0, Math.Min(100, valuesArray.Length))}...");
            
            // Now we have a clean array to parse - check for empty
            if (valuesArray == "[]" || valuesArray == "[[]]")
            {
                Debug.LogError("Values array is empty");
                return null;
            }
            
            // Split the array into rows by finding each row's start and end
            List<List<string>> result = new List<List<string>>();
            int currentPos = 1; // Skip first '['
            
            while (currentPos < valuesArray.Length - 1) // Stop before last ']'
            {
                // Find next row start
                while (currentPos < valuesArray.Length && valuesArray[currentPos] != '[')
                {
                    currentPos++;
                }
                
                if (currentPos >= valuesArray.Length - 1)
                {
                    break; // End of array
                }
                
                // Find matching closing bracket for this row
                int rowStart = currentPos;
                int rowOpenBrackets = 1; // We're already inside a '['
                int rowEnd = -1;
                
                for (int i = rowStart + 1; i < valuesArray.Length; i++)
                {
                    if (valuesArray[i] == '[') rowOpenBrackets++;
                    if (valuesArray[i] == ']') 
                    {
                        rowOpenBrackets--;
                        if (rowOpenBrackets == 0)
                        {
                            rowEnd = i;
                            break;
                        }
                    }
                }
                
                if (rowEnd < 0)
                {
                    Debug.LogError("Malformed row in values array");
                    break;
                }
                
                // Extract this row
                string rowJson = valuesArray.Substring(rowStart, rowEnd - rowStart + 1);
                
                // Parse the row into a list of cell values
                List<string> cellValues = new List<string>();
                bool inQuotes = false;
                StringBuilder cellBuilder = new StringBuilder();
                
                // Skip the first character '[' and last character ']'
                for (int i = 1; i < rowJson.Length - 1; i++)
                {
                    char c = rowJson[i];
                    
                    if (c == '"')
                    {
                        inQuotes = !inQuotes;
                        
                        // If we're ending a quoted string, add it as a cell value
                        if (!inQuotes)
                        {
                            cellValues.Add(cellBuilder.ToString());
                            cellBuilder.Clear();
                        }
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        // End of a cell, unless we're in quotes
                        if (cellBuilder.Length > 0)
                        {
                            cellValues.Add(cellBuilder.ToString());
                            cellBuilder.Clear();
                        }
                    }
                    else if (inQuotes)
                    {
                        // Add this character to the current cell if we're inside quotes
                        cellBuilder.Append(c);
                    }
                }
                
                // Add any remaining cell value
                if (cellBuilder.Length > 0)
                {
                    cellValues.Add(cellBuilder.ToString());
                }
                
                // Add this row to the result
                if (cellValues.Count > 0)
                {
                    result.Add(cellValues);
                }
                
                // Move past this row
                currentPos = rowEnd + 1;
            }
            
            Debug.Log($"Custom parser found {result.Count} rows of data");
            if (result.Count > 0)
            {
                Debug.Log($"First row has {result[0].Count} cells");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in custom JSON parser: {ex.Message}");
            Debug.LogError($"Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Refreshes all data from Google Sheets
    /// </summary>
    public IEnumerator RefreshAllData()
    {
        Debug.Log("Starting to refresh all Google Sheets data...");
        
        // Get the current city ID that we need to maintain
        string cityToMaintain = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        
        // Check for preserved city from pull-to-refresh
        if (PlayerPrefs.HasKey("PullRefresh_PreservedCityId"))
        {
            string preservedCity = PlayerPrefs.GetString("PullRefresh_PreservedCityId");
            Debug.Log($"[CRITICAL] Found preserved city ID from pull-to-refresh: '{preservedCity}'");
            cityToMaintain = preservedCity;
        }
        Debug.Log($"[CRITICAL] Refreshing data while maintaining city: '{cityToMaintain}'");
        
        // Store ALL existing data as backup
        List<SocialMediaMetrics> allSocialMediaBackup = null;
        List<EventMetrics> allEventMetricsBackup = null;
        
        if (dataModel != null)
        {
            // Backup ALL metrics
            if (dataModel.SocialMediaMetrics != null && dataModel.SocialMediaMetrics.Count > 0)
            {
                allSocialMediaBackup = new List<SocialMediaMetrics>(dataModel.SocialMediaMetrics);
                Debug.Log($"Backed up all {allSocialMediaBackup.Count} social media metrics");
            }
            
            if (dataModel.EventMetrics != null && dataModel.EventMetrics.Count > 0)
            {
                allEventMetricsBackup = new List<EventMetrics>(dataModel.EventMetrics);
                Debug.Log($"Backed up all {allEventMetricsBackup.Count} event metrics");
            }
        }
        
        // Clear existing data
        if (dataModel != null)
        {
            dataModel.ClearSocialMediaMetrics();
            dataModel.ClearEventMetrics();
        }
        
        // Fetch new data
        yield return FetchSocialMediaData();
        yield return FetchEventData();
        
        // Verify we got data for our city
        bool hasNewCityData = false;
        if (dataModel != null)
        {
            hasNewCityData = (dataModel.SocialMediaMetrics != null && 
                             dataModel.SocialMediaMetrics.Any(m => m.AssociatedCity != null && 
                                                                 m.AssociatedCity.ID.ToLower() == cityToMaintain.ToLower())) ||
                            (dataModel.EventMetrics != null && 
                             dataModel.EventMetrics.Any(m => m.AssociatedCity != null && 
                                                           m.AssociatedCity.ID.ToLower() == cityToMaintain.ToLower()));
            
            // If we didn't get new data for our city, restore from backup
            if (!hasNewCityData)
            {
                Debug.LogWarning($"[CRITICAL] No new data found for city '{cityToMaintain}', restoring from backup");
                
                // Restore only the data for our specific city
                if (allSocialMediaBackup != null)
                {
                    var cityMetrics = allSocialMediaBackup.Where(m => 
                        m.AssociatedCity != null && 
                        m.AssociatedCity.ID.ToLower() == cityToMaintain.ToLower()).ToList();
                        
                    if (cityMetrics.Any())
                    {
                        foreach (var metric in cityMetrics)
                        {
                            dataModel.AddSocialMediaMetrics(metric);
                        }
                        Debug.Log($"Restored {cityMetrics.Count} social media metrics for city '{cityToMaintain}'");
                    }
                }
                
                if (allEventMetricsBackup != null)
                {
                    var cityMetrics = allEventMetricsBackup.Where(m => 
                        m.AssociatedCity != null && 
                        m.AssociatedCity.ID.ToLower() == cityToMaintain.ToLower()).ToList();
                        
                    if (cityMetrics.Any())
                    {
                        foreach (var metric in cityMetrics)
                        {
                            dataModel.AddEventMetrics(metric);
                        }
                        Debug.Log($"Restored {cityMetrics.Count} event metrics for city '{cityToMaintain}'");
                    }
                }
            }
            else
            {
                Debug.Log($"[CRITICAL] Successfully fetched new data for city '{cityToMaintain}'");
            }
        }
        
        // Ensure the city ID is properly maintained
        PlayerPrefs.SetString("SelectedCityId", cityToMaintain);
        PlayerPrefs.SetInt("ForceDefaultCity", 0);
        PlayerPrefs.Save();
        Debug.Log($"[CRITICAL] Completed refresh while maintaining city: '{cityToMaintain}'");
    }
}

// Helper classes for JSON deserialization
[Serializable]
public class SheetResponse
{
    public List<List<string>> values;
}

// Use a class structure that matches Google Sheets API format
[Serializable]
public class GoogleSheetsResponse
{
    [SerializeField]
    public string range;
    
    [SerializeField]
    public string majorDimension;
    
    [SerializeField]
    public string[][] values;
}

// Setup Instructions (for reference, not included in the code):
// 1. Create a Google Cloud Project: https://console.cloud.google.com/
// 2. Enable Google Sheets API
// 3. Create API Key in Credentials
// 4. Set the API Key in the inspector
// 5. Make your Google Sheet public or share it with appropriate permissions
// 6. Get the Spreadsheet ID from the URL (between /d/ and /edit)
// 7. Set up two sheets: "SocialMedia" and "Events" with appropriate headers
//    - SocialMedia headers: city_id, instagram_followers, tiktok_followers, tiktok_likes, timestamp
//    - Events headers: city_id, tickets_sold, average_attendance, number_of_events, timestamp


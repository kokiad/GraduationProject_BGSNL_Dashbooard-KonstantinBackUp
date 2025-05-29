using System.Collections.Generic;
using UnityEngine;
using XCharts.Runtime;
using System.Linq;
using System.Globalization;

public class ChartManager : MonoBehaviour
{
    [Header("Chart References")]
    [SerializeField] private BaseChart InstagramFollowersLineChart;
    [SerializeField] private BaseChart TiktokLikesBar;
    [SerializeField] private BaseChart TicketsSoldPieChart;
    
    // New additional chart references
    [SerializeField] private BaseChart TikTokFollowersLineChart;
    [SerializeField] private BaseChart NumberOfEventsPieChart;
    [SerializeField] private BaseChart AverageAttendancePieChart;

    [Header("Service References")]
    [SerializeField] private DataModelClasses dataModel;
    [SerializeField] private GoogleSheetsService sheetsService;

    [Header("Settings")]
    [SerializeField] private bool debugMode = true;

    // City colors for consistent visualization across all charts
    private Dictionary<string, Color32> cityColors = new Dictionary<string, Color32>
    {
        {"bgsnl", new Color32(255, 99, 132, 255)},   // Red
        {"bgsg", new Color32(255, 159, 64, 255)},    // Orange  
        {"bgsr", new Color32(255, 205, 86, 255)},    // Yellow
        {"bgsl", new Color32(75, 192, 192, 255)},    // Teal
        {"bgsa", new Color32(54, 162, 235, 255)},    // Blue
        {"bgsb", new Color32(153, 102, 255, 255)},   // Purple
        {"bgsm", new Color32(201, 203, 207, 255)},   // Gray
        {"bgse", new Color32(255, 99, 255, 255)}     // Pink
    };

    private bool isInitialized = false;

    private void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[ChartManager] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[ChartManager] {message}");
    }

    private void Awake()
    {
        LogDebug("Initializing ChartManager...");
        FindReferences();
    }

    private void FindReferences()
    {
        if (dataModel == null)
        {
            dataModel = FindObjectOfType<DataModelClasses>();
            if (dataModel == null)
            {
                LogError("DataModelClasses not found!");
                return;
            }
            LogDebug("Found DataModelClasses in scene");
        }

        if (sheetsService == null)
        {
            sheetsService = FindObjectOfType<GoogleSheetsService>();
            if (sheetsService == null)
            {
                LogError("GoogleSheetsService not found!");
                return;
            }
            LogDebug("Found GoogleSheetsService in scene");
        }

        isInitialized = (dataModel != null && sheetsService != null);
        LogDebug($"ChartManager initialized: {isInitialized}");

        // Validate chart references
        if (InstagramFollowersLineChart == null)
            LogError("Instagram Followers Line Chart not assigned!");
        if (TiktokLikesBar == null)
            LogError("TikTok Likes Bar Chart not assigned!");
        if (TicketsSoldPieChart == null)
            LogError("Tickets Sold Pie Chart not assigned!");
    }

    private void Start()
    {
        if (!isInitialized)
        {
            LogError("Cannot initialize charts - missing required references");
            return;
        }
        
        if (dataModel != null && dataModel.Cities.Count > 0)
        {
            LogDebug("Starting delayed chart update");
            StartCoroutine(DelayedChartUpdate());
        }
        else
        {
            LogDebug("No cities found, waiting for data");
        }
    }

    private System.Collections.IEnumerator DelayedChartUpdate()
    {
        yield return new WaitForSeconds(1f);
        UpdateAllCharts();
    }

    private void OnDestroy()
    {
        if (sheetsService != null)
        {
            GoogleSheetsService.OnDataUpdated -= OnDataUpdated;
        }
    }

    private void OnDataUpdated()
    {
        LogDebug("Data updated, refreshing charts");
        UpdateAllCharts();
    }

    private void UpdateAllCharts()
    {
        LogDebug("Updating all charts with latest data");
        
        UpdateInstagramLineChart();
        UpdateInstagramBarChart();
        UpdateTicketsPieChart();
        
        // Update new additional charts
        UpdateTikTokFollowersLineChart();
        UpdateNumberOfEventsPieChart();
        UpdateAverageAttendancePieChart();
    }

    // Helper method to parse numbers that might contain commas, dots, or K suffix
    private int ParseFollowerCount(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            LogDebug("Empty value, returning 0");
            return 0;
        }

        // Clean and normalize the string
        string cleanValue = value.Replace(" ", "").Trim().ToUpper();
        
        LogDebug($"Parsing follower count: '{value}' -> cleaned: '{cleanValue}'");
        
        // Handle "K" suffix (thousands)
        bool hasKSuffix = cleanValue.EndsWith("K");
        if (hasKSuffix)
        {
            cleanValue = cleanValue.Substring(0, cleanValue.Length - 1); // Remove "K"
            
            // Parse the number part (might have decimal)
            if (float.TryParse(cleanValue.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out float kValue))
            {
                int kResult = Mathf.RoundToInt(kValue * 1000);
                LogDebug($"Parsed K value: {kValue} * 1000 = {kResult}");
                return kResult;
            }
        }
        
        // Handle regular numbers with commas/dots
        string numericValue = cleanValue.Replace(",", "").Replace(".", "");
        
        if (int.TryParse(numericValue, out int result))
        {
            LogDebug($"Successfully parsed: {result}");
            return result;
        }
        
        // Try parsing as decimal and convert to int
        if (float.TryParse(cleanValue.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out float floatResult))
        {
            int intResult = Mathf.RoundToInt(floatResult);
            LogDebug($"Parsed as float and rounded: {intResult}");
            return intResult;
        }
        
        LogDebug($"Failed to parse '{value}', returning 0");
        return 0;
    }

    private void UpdateInstagramLineChart()
    {
        LogDebug("=== UPDATING Instagram Line Chart (Multiple Lines Over Time) ===");
        
        if (InstagramFollowersLineChart == null)
        {
            LogError("Instagram Followers Line Chart is null!");
            return;
        }

        // Clear existing data and series
        InstagramFollowersLineChart.ClearData();
        InstagramFollowersLineChart.RemoveAllSerie();

        // Setup chart title with better spacing
        var title = InstagramFollowersLineChart.GetOrAddChartComponent<Title>();
        title.text = "Instagram Followers Growth";
        title.subText = "Growth over time by city";
        title.itemGap = 15; // Add spacing between title and subtitle

        // Setup legend - horizontal under title/subtitle with more spacing
        var legend = InstagramFollowersLineChart.GetOrAddChartComponent<Legend>();
        legend.show = true;
        legend.location.align = Location.Align.TopCenter;
        legend.location.top = 120; // More space from title/subtitle
        legend.orient = Orient.Horizonal; // Horizontal layout
        legend.itemWidth = 45;
        legend.itemHeight = 16;

        // Setup X-axis (time periods)
        var xAxis = InstagramFollowersLineChart.GetOrAddChartComponent<XAxis>();
        xAxis.type = Axis.AxisType.Category;
        xAxis.data.Clear();
        
        // Add time periods
        string[] timeLabels = {"Jan", "Feb", "Mar", "Apr", "May", "Jun"};
        foreach (var label in timeLabels)
        {
            InstagramFollowersLineChart.AddXAxisData(label);
        }

        // Setup Y-axis
        var yAxis = InstagramFollowersLineChart.GetOrAddChartComponent<YAxis>();
        yAxis.type = Axis.AxisType.Value;

        // Configure chart area with more space for title/subtitle/legend
        var grid = InstagramFollowersLineChart.EnsureChartComponent<GridCoord>();
        grid.show = true;
        grid.left = 80f;
        grid.right = 50f;
        grid.top = 180f; // Increased from 160f to give more space for header elements
        grid.bottom = 60f;

        // Add a line series for each city
        foreach (var city in dataModel.Cities)
        {
            if (city == null) continue;

            var serie = InstagramFollowersLineChart.AddSerie<Line>(city.Name);
            if (serie == null)
            {
                LogError($"Failed to add line series for {city.Name}");
                continue;
            }

            // Configure line appearance with consistent colors
            serie.symbol.show = true;
            serie.symbol.type = SymbolType.Circle;
            serie.symbol.size = 6f;
            serie.lineStyle.width = 3f;
            
            // Set city-specific color (consistent across charts)
            var cityColor = GetCityColor(city.ID);
            serie.lineStyle.color = cityColor;
            serie.symbol.color = cityColor;

            // Get current Instagram followers for this city with improved parsing
            var metrics = dataModel.GetLatestSocialMediaMetrics(city.ID);
            int currentFollowers = 0;
            if (metrics != null)
            {
                currentFollowers = ParseFollowerCount(metrics.InstagramFollowers);
                LogDebug($"Current followers for {city.Name} (ID: {city.ID}): {currentFollowers} (raw: '{metrics.InstagramFollowers}')");
            }
            else
            {
                LogDebug($"No follower data for {city.Name}, using 0");
            }

            // Generate sample growth data leading to current value
            for (int i = 0; i < timeLabels.Length; i++)
            {
                // Simulate growth: start from 60% of current, grow to current
                float growthFactor = 0.6f + (0.4f * i / (timeLabels.Length - 1));
                int followers = Mathf.RoundToInt(currentFollowers * growthFactor);
                
                // Add some variation for realism (except last point)
                if (i < timeLabels.Length - 1)
                {
                    followers += UnityEngine.Random.Range(-50, 50);
                    followers = Mathf.Max(0, followers);
                }
                
                InstagramFollowersLineChart.AddData(serie.index, followers);
                LogDebug($"Added data point for {city.Name} at {timeLabels[i]}: {followers}");
            }
        }

        InstagramFollowersLineChart.RefreshChart();
        LogDebug("=== FINISHED Instagram Line Chart Update ===");
    }

    private void UpdateInstagramBarChart()
    {
        if (TiktokLikesBar == null)
        {
            LogError("TikTok Likes Bar Chart not assigned!");
            return;
        }

        LogDebug("Updating TikTok Likes Bar Chart");

        // Clear existing data
        TiktokLikesBar.RemoveData();

        // Set up basic chart configuration with better margins for multiple bars
        var grid = TiktokLikesBar.EnsureChartComponent<GridCoord>();
        grid.show = true;
        grid.left = 100f; // Increased left margin
        grid.right = 80f; // Increased right margin  
        grid.top = 160f; // Increased to match line chart spacing
        grid.bottom = 80f; // Increased bottom margin

        // Configure title for TikTok Likes
        var title = TiktokLikesBar.EnsureChartComponent<Title>();
        title.show = true;
        title.text = "TikTok Likes";
        title.subText = "Current likes by city";
        title.itemGap = 15; // Match line chart spacing

        // Hide legend since X-axis labels already show city names clearly
        var legend = TiktokLikesBar.EnsureChartComponent<Legend>();
        legend.show = false; // Cleaner design without redundant legend

        // Configure X axis with proper alignment
        var xAxis = TiktokLikesBar.EnsureChartComponent<XAxis>();
        xAxis.show = true;
        xAxis.type = Axis.AxisType.Category;
        xAxis.data.Clear();
        xAxis.boundaryGap = true; // Ensure proper spacing around categories

        // Configure Y axis
        var yAxis = TiktokLikesBar.EnsureChartComponent<YAxis>();
        yAxis.show = true;
        yAxis.type = Axis.AxisType.Value;

        if (dataModel == null || dataModel.Cities == null || dataModel.Cities.Count == 0)
        {
            LogError("No cities available for TikTok likes bar chart");
            return;
        }

        // SINGLE SERIES approach for proper bar centering on X-axis points
        var serie = TiktokLikesBar.AddSerie<Bar>("TikTok Likes");
        if (serie == null)
        {
            LogError("Failed to add TikTok Likes Bar serie!");
            return;
        }

        // Configure bars for perfect alignment
        serie.barWidth = 0.75f; // Good width

        // Add data for each city with individual colors
        foreach (var city in dataModel.Cities)
        {
            if (city == null || string.IsNullOrEmpty(city.ID)) continue;

            var socialMetrics = dataModel.GetLatestSocialMediaMetrics(city.ID);
            if (socialMetrics == null) continue;

            int likesCount = ParseFollowerCount(socialMetrics.TikTokLikes);
            if (likesCount <= 0) continue; // Skip cities with no likes

            // Add city to X-axis
            xAxis.data.Add(city.ID.ToUpper());

            // Add data point with individual color
            var serieData = TiktokLikesBar.AddData(serie.index, likesCount);
            if (serieData != null)
            {
                // Set individual color for each bar (consistent with other charts)
                serieData.EnsureComponent<ItemStyle>();
                var cityColor = GetCityColor(city.ID);
                serieData.itemStyle.color = cityColor;
                LogDebug($"Added centered bar for {city.Name} (ID: {city.ID}) with {likesCount} likes, color: {cityColor}");
            }
        }

        LogDebug("TikTok Likes bar chart updated with single series for perfect X-axis alignment");
    }

    private void UpdateTicketsPieChart()
    {
        if (TicketsSoldPieChart == null)
        {
            LogError("Tickets Sold Pie Chart not assigned!");
            return;
        }

        LogDebug("Updating Tickets Pie Chart");

        // Clear existing data
        TicketsSoldPieChart.RemoveData();

        // Configure title positioned higher
        var title = TicketsSoldPieChart.EnsureChartComponent<Title>();
        title.show = true;
        title.text = "Tickets Sold";
        title.subText = "Distribution by city";
        title.itemGap = 15;

        // Configure legend - positioned to match other charts spacing
        var legend = TicketsSoldPieChart.EnsureChartComponent<Legend>();
        legend.show = true;
        legend.orient = Orient.Horizonal;
        legend.location.align = Location.Align.TopCenter;
        legend.location.top = 120; // Match other charts spacing (was 80)
        legend.itemWidth = 45;
        legend.itemHeight = 16;

        // Create the pie series
        var pieSerie = TicketsSoldPieChart.AddSerie<Pie>("Tickets Sold");
        if (pieSerie == null)
        {
            LogError("Failed to create pie series");
            return;
        }

        // Position pie chart higher for better spacing from legend
        pieSerie.center[0] = 0.5f; // Horizontal center
        pieSerie.center[1] = 0.45f; // Move up more for better spacing (was 0.50f)
        pieSerie.radius[0] = 0f; // Inner radius (0 for full pie)
        pieSerie.radius[1] = 0.25f; // Good size radius

        if (dataModel == null || dataModel.Cities == null || dataModel.Cities.Count == 0)
        {
            LogError("No cities available for pie chart");
            return;
        }

        LogDebug("Processing event data for pie chart");

        int validCityCount = 0;
        foreach (var city in dataModel.Cities)
        {
            if (city == null || string.IsNullOrEmpty(city.ID)) continue;

            var eventMetrics = dataModel.GetLatestEventMetrics(city.ID);
            if (eventMetrics == null) continue;

            int ticketsSold = ParseFollowerCount(eventMetrics.TicketsSold);
            if (ticketsSold <= 0) continue; // Skip cities with no ticket sales

            validCityCount++;

            // Get city color
            Color32 cityColor = GetCityColor(city.ID);

            // Add data to pie chart
            var serieData = TicketsSoldPieChart.AddData(pieSerie.index, ticketsSold, city.ID.ToUpper());
            if (serieData != null)
            {
                serieData.EnsureComponent<ItemStyle>();
                serieData.itemStyle.color = cityColor;
            }

            LogDebug($"Added pie slice for {city.Name} (ID: {city.ID}) with {ticketsSold} tickets, color: {cityColor}");
        }

        LogDebug($"Pie chart updated with {validCityCount} cities, properly spaced to avoid overlap");
    }

    private void UpdateTikTokFollowersLineChart()
    {
        if (TikTokFollowersLineChart == null)
        {
            LogError("TikTok Followers Line Chart not assigned!");
            return;
        }

        LogDebug("Updating TikTok Followers Line Chart");

        // Clear existing data
        TikTokFollowersLineChart.RemoveData();

        // Configure title
        var title = TikTokFollowersLineChart.EnsureChartComponent<Title>();
        title.show = true;
        title.text = "TikTok Followers Growth";
        title.subText = "Growth over time by city";
        title.itemGap = 15;

        // Configure legend
        var legend = TikTokFollowersLineChart.EnsureChartComponent<Legend>();
        legend.show = true;
        legend.orient = Orient.Horizonal;
        legend.location.align = Location.Align.TopCenter;
        legend.location.top = 120;
        legend.itemWidth = 45;
        legend.itemHeight = 16;

        // Configure X axis (months)
        var xAxis = TikTokFollowersLineChart.EnsureChartComponent<XAxis>();
        xAxis.show = true;
        xAxis.type = Axis.AxisType.Category;
        xAxis.data.Clear();
        string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun" };
        foreach (string month in months)
        {
            xAxis.data.Add(month);
        }

        // Configure Y axis
        var yAxis = TikTokFollowersLineChart.GetOrAddChartComponent<YAxis>();
        yAxis.type = Axis.AxisType.Value;

        // Configure chart area with more space for title/subtitle/legend
        var grid = TikTokFollowersLineChart.EnsureChartComponent<GridCoord>();
        grid.show = true;
        grid.left = 80f;
        grid.right = 50f;
        grid.top = 180f; // Same spacing as Instagram chart
        grid.bottom = 60f;

        // Add a line series for each city
        foreach (var city in dataModel.Cities)
        {
            if (city == null || string.IsNullOrEmpty(city.ID)) continue;

            var socialMetrics = dataModel.GetLatestSocialMediaMetrics(city.ID);
            if (socialMetrics == null) continue;

            int currentFollowers = ParseFollowerCount(socialMetrics.TikTokFollowers);
            if (currentFollowers <= 0) continue;

            // Create line series for this city
            var serie = TikTokFollowersLineChart.AddSerie<Line>(city.ID.ToUpper());
            if (serie == null) continue;

            // Configure line appearance
            serie.symbol.show = true;
            serie.symbol.size = 6f;
            serie.lineStyle.width = 3f;

            // Set city color
            Color32 cityColor = GetCityColor(city.ID);
            serie.lineStyle.color = cityColor;
            serie.symbol.color = cityColor;

            // Generate growth data leading to current followers
            int startFollowers = Mathf.Max(1, currentFollowers - UnityEngine.Random.Range(currentFollowers / 4, currentFollowers / 2));
            
            for (int month = 0; month < 6; month++)
            {
                float progress = month / 5f;
                int monthlyFollowers = Mathf.RoundToInt(Mathf.Lerp(startFollowers, currentFollowers, progress));
                monthlyFollowers += UnityEngine.Random.Range(-monthlyFollowers / 20, monthlyFollowers / 20);
                serie.AddData(monthlyFollowers);
            }

            LogDebug($"Added TikTok line for {city.Name} (ID: {city.ID}) ending at {currentFollowers} followers");
        }

        LogDebug("TikTok Followers line chart updated");
    }

    private void UpdateNumberOfEventsPieChart()
    {
        if (NumberOfEventsPieChart == null)
        {
            LogError("Number of Events Pie Chart not assigned!");
            return;
        }

        LogDebug("Updating Number of Events Pie Chart");

        // Clear existing data
        NumberOfEventsPieChart.RemoveData();

        // Configure title positioned higher
        var title = NumberOfEventsPieChart.EnsureChartComponent<Title>();
        title.show = true;
        title.text = "Number of Events";
        title.subText = "Distribution by city";
        title.itemGap = 15;

        // Configure legend - positioned to match other charts spacing
        var legend = NumberOfEventsPieChart.EnsureChartComponent<Legend>();
        legend.show = true;
        legend.orient = Orient.Horizonal;
        legend.location.align = Location.Align.TopCenter;
        legend.location.top = 120; // Match other charts spacing
        legend.itemWidth = 45;
        legend.itemHeight = 16;

        // Create the pie series
        var pieSerie = NumberOfEventsPieChart.AddSerie<Pie>("Number of Events");
        if (pieSerie == null)
        {
            LogError("Failed to create number of events pie series");
            return;
        }

        // Position pie chart higher for better spacing from legend
        pieSerie.center[0] = 0.5f; // Horizontal center
        pieSerie.center[1] = 0.45f; // Move up for better spacing
        pieSerie.radius[0] = 0f; // Inner radius (0 for full pie)
        pieSerie.radius[1] = 0.25f; // Good size radius

        if (dataModel == null || dataModel.Cities == null || dataModel.Cities.Count == 0)
        {
            LogError("No cities available for number of events pie chart");
            return;
        }

        LogDebug("Processing event data for number of events pie chart");

        int validCityCount = 0;
        foreach (var city in dataModel.Cities)
        {
            if (city == null || string.IsNullOrEmpty(city.ID)) continue;

            var eventMetrics = dataModel.GetLatestEventMetrics(city.ID);
            if (eventMetrics == null) continue;

            int eventsCount = ParseFollowerCount(eventMetrics.NumberOfEvents);
            if (eventsCount <= 0) continue; // Skip cities with no events

            validCityCount++;

            // Get city color
            Color32 cityColor = GetCityColor(city.ID);

            // Add data to pie chart
            var serieData = NumberOfEventsPieChart.AddData(pieSerie.index, eventsCount, city.ID.ToUpper());
            if (serieData != null)
            {
                serieData.EnsureComponent<ItemStyle>();
                serieData.itemStyle.color = cityColor;
            }

            LogDebug($"Added pie slice for {city.Name} (ID: {city.ID}) with {eventsCount} events, color: {cityColor}");
        }

        LogDebug($"Number of events pie chart updated with {validCityCount} cities, properly spaced to avoid overlap");
    }

    private void UpdateAverageAttendancePieChart()
    {
        if (AverageAttendancePieChart == null)
        {
            LogError("Average Attendance Pie Chart not assigned!");
            return;
        }

        LogDebug("Updating Average Attendance Pie Chart");

        // Clear existing data
        AverageAttendancePieChart.RemoveData();

        // Configure title positioned higher
        var title = AverageAttendancePieChart.EnsureChartComponent<Title>();
        title.show = true;
        title.text = "Average Attendance";
        title.subText = "Distribution by city";
        title.itemGap = 15;

        // Configure legend - positioned to match other charts spacing
        var legend = AverageAttendancePieChart.EnsureChartComponent<Legend>();
        legend.show = true;
        legend.orient = Orient.Horizonal;
        legend.location.align = Location.Align.TopCenter;
        legend.location.top = 120; // Match other charts spacing
        legend.itemWidth = 45;
        legend.itemHeight = 16;

        // Create the pie series
        var pieSerie = AverageAttendancePieChart.AddSerie<Pie>("Average Attendance");
        if (pieSerie == null)
        {
            LogError("Failed to create average attendance pie series");
            return;
        }

        // Position pie chart higher for better spacing from legend
        pieSerie.center[0] = 0.5f; // Horizontal center
        pieSerie.center[1] = 0.45f; // Move up for better spacing
        pieSerie.radius[0] = 0f; // Inner radius (0 for full pie)
        pieSerie.radius[1] = 0.25f; // Good size radius

        if (dataModel == null || dataModel.Cities == null || dataModel.Cities.Count == 0)
        {
            LogError("No cities available for average attendance pie chart");
            return;
        }

        LogDebug("Processing event data for average attendance pie chart");

        int validCityCount = 0;
        foreach (var city in dataModel.Cities)
        {
            if (city == null || string.IsNullOrEmpty(city.ID)) continue;

            var eventMetrics = dataModel.GetLatestEventMetrics(city.ID);
            if (eventMetrics == null) continue;

            int attendanceCount = ParseFollowerCount(eventMetrics.AverageAttendance);
            if (attendanceCount <= 0) continue; // Skip cities with no attendance data

            validCityCount++;

            // Get city color
            Color32 cityColor = GetCityColor(city.ID);

            // Add data to pie chart
            var serieData = AverageAttendancePieChart.AddData(pieSerie.index, attendanceCount, city.ID.ToUpper());
            if (serieData != null)
            {
                serieData.EnsureComponent<ItemStyle>();
                serieData.itemStyle.color = cityColor;
            }

            LogDebug($"Added pie slice for {city.Name} (ID: {city.ID}) with {attendanceCount} average attendance, color: {cityColor}");
        }

        LogDebug($"Average attendance pie chart updated with {validCityCount} cities, properly spaced to avoid overlap");
    }

    private Color32 GetCityColor(string cityId)
    {
        if (string.IsNullOrEmpty(cityId))
        {
            LogDebug("City ID is null or empty, using default color");
            return new Color32(158, 158, 158, 255);
        }

        string normalizedId = cityId.ToLower().Trim();
        if (cityColors.ContainsKey(normalizedId))
        {
            LogDebug($"Found color for city ID '{normalizedId}': {cityColors[normalizedId]}");
            return cityColors[normalizedId];
        }
        
        LogDebug($"No color found for city ID '{normalizedId}', using default gray");
        return new Color32(158, 158, 158, 255); // Gray for unknown cities
    }

    // Public method to manually refresh charts
    public void RefreshCharts()
    {
        LogDebug("Manual chart refresh requested");
        UpdateAllCharts();
    }

    // Subscribe to data updates when enabled
    private void OnEnable()
    {
        GoogleSheetsService.OnDataUpdated += OnDataUpdated;
    }

    private void OnDisable()
    {
        GoogleSheetsService.OnDataUpdated -= OnDataUpdated;
    }
} 
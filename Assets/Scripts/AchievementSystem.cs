using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[Serializable]
public class Achievement
{
    public string id;
    public string cityId;
    public AchievementType type;
    public string title;
    public int[] milestones; // Progressive milestone values
    public int currentMilestoneIndex; // Which milestone we're currently working towards
    public bool isCompleted; // True when all milestones are completed
    public DateTime lastUnlockDate; // When the last milestone was completed
    
    public Achievement(string id, string cityId, AchievementType type, string title, int[] milestones)
    {
        this.id = id;
        this.cityId = cityId;
        this.type = type;
        this.title = title;
        this.milestones = milestones;
        this.currentMilestoneIndex = 0;
        this.isCompleted = false;
    }
    
    public int GetCurrentTarget()
    {
        if (currentMilestoneIndex >= milestones.Length)
            return milestones[milestones.Length - 1]; // Return final milestone if completed
        return milestones[currentMilestoneIndex];
    }
    
    public int GetPreviousTarget()
    {
        if (currentMilestoneIndex <= 0)
            return 0;
        return milestones[currentMilestoneIndex - 1];
    }
    
    public bool HasNextMilestone()
    {
        return currentMilestoneIndex < milestones.Length;
    }
    
    public string GetCurrentTitle()
    {
        if (isCompleted)
            return $"{title} Master"; // Special title when all milestones completed
        return $"{title} - {GetCurrentTarget():N0}";
    }
    
    public string GetCurrentDescription()
    {
        if (isCompleted)
            return $"All {title.ToLower()} milestones achieved!";
        return $"{GetCurrentTarget():N0}";
    }
}

public enum AchievementType
{
    InstagramFollowers,
    TikTokFollowers,
    TikTokLikes,
    TicketsSold,
    NumberOfEvents,
    AverageAttendance
}

[Serializable]
public class UnlockedAchievement
{
    public string achievementId;
    public int milestoneLevel; // Which milestone was unlocked (0, 1, 2, etc.)
    public DateTime unlockDate;
    public int valueAtUnlock; // The actual value when unlocked
    
    public UnlockedAchievement(string achievementId, int milestoneLevel, DateTime unlockDate, int valueAtUnlock)
    {
        this.achievementId = achievementId;
        this.milestoneLevel = milestoneLevel;
        this.unlockDate = unlockDate;
        this.valueAtUnlock = valueAtUnlock;
    }
}

public class AchievementSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DataModelClasses dataModel;
    [SerializeField] private AchievementUI achievementUI;
    
    [Header("Settings")]
    [SerializeField] private bool enableNotifications = true;
    [SerializeField] private bool debugMode = true;
    
    [Header("Milestone Configuration - Edit these values to customize achievement goals")]
    [SerializeField] private bool useCustomMilestones = false;
    [SerializeField] private int[] instagramFollowersMilestones = { 100, 500, 1000, 2000, 5000, 10000, 25000, 50000, 100000 };
    [SerializeField] private int[] tiktokFollowersMilestones = { 50, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000 };
    [SerializeField] private int[] tiktokLikesMilestones = { 1000, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000 };
    [SerializeField] private int[] ticketsSoldMilestones = { 50, 100, 250, 500, 1000, 2000, 5000 };
    [SerializeField] private int[] numberOfEventsMilestones = { 5, 10, 25, 50, 100, 200 };
    [SerializeField] private int[] averageAttendanceMilestones = { 10, 20, 50, 100, 200, 500 };
    
    // Default milestones (will be overridden by inspector values if useCustomMilestones is true)
    private readonly Dictionary<AchievementType, int[]> defaultMilestones = new Dictionary<AchievementType, int[]>
    {
        { AchievementType.InstagramFollowers, new[] { 100, 500, 1000, 2000, 5000, 10000, 25000, 50000, 100000 } },
        { AchievementType.TikTokFollowers, new[] { 50, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000 } },
        { AchievementType.TikTokLikes, new[] { 1000, 5000, 10000, 25000, 50000, 100000, 250000, 500000, 1000000 } },
        { AchievementType.TicketsSold, new[] { 50, 100, 250, 500, 1000, 2000, 5000 } },
        { AchievementType.NumberOfEvents, new[] { 5, 10, 25, 50, 100, 200 } },
        { AchievementType.AverageAttendance, new[] { 10, 20, 50, 100, 200, 500 } }
    };
    
    private readonly Dictionary<AchievementType, string> achievementTitles = new Dictionary<AchievementType, string>
    {
        { AchievementType.InstagramFollowers, "Instagram Followers Growth" },
        { AchievementType.TikTokFollowers, "TikTok Followers Growth" },
        { AchievementType.TikTokLikes, "TikTok Likes Growth" },
        { AchievementType.TicketsSold, "Tickets Sold" },
        { AchievementType.NumberOfEvents, "Events Organized" },
        { AchievementType.AverageAttendance, "Average Attendance" }
    };
    
    // Storage keys
    private const string ACHIEVEMENTS_KEY = "AchievementData_";
    private const string UNLOCKED_KEY = "UnlockedAchievements_";
    
    // Current city achievements
    private Dictionary<string, Achievement> currentCityAchievements = new Dictionary<string, Achievement>();
    private List<UnlockedAchievement> unlockedAchievements = new List<UnlockedAchievement>();
    
    // Events
    public static event Action<Achievement, int> OnAchievementUnlocked; // Achievement, milestone level
    public static event Action<Achievement> OnAchievementProgressUpdated;
    
    public static AchievementSystem Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        FindReferences();
    }
    
    private void FindReferences()
    {
        if (dataModel == null)
        {
            dataModel = FindObjectOfType<DataModelClasses>();
        }
        
        if (achievementUI == null)
        {
            achievementUI = FindObjectOfType<AchievementUI>();
        }
    }
    
    private void Start()
    {
        // Subscribe to data updates
        GoogleSheetsService.OnDataUpdated += OnDataUpdated;
        
        // Initialize achievements for current city
        InitializeCurrentCityAchievements();
    }
    
    private void OnDestroy()
    {
        GoogleSheetsService.OnDataUpdated -= OnDataUpdated;
    }
    
    private void OnDataUpdated()
    {
        LogDebug("Data updated - checking achievement progress");
        CheckAchievementProgress();
    }
    
    public void InitializeCurrentCityAchievements()
    {
        string currentCityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        LogDebug($"Initializing achievements for city: {currentCityId}");
        
        // Load existing achievements or create new ones
        LoadAchievementsForCity(currentCityId);
        LoadUnlockedAchievements(currentCityId);
        
        // Check progress immediately
        CheckAchievementProgress();
    }
    
    private void LoadAchievementsForCity(string cityId)
    {
        currentCityAchievements.Clear();
        
        foreach (var achievementType in Enum.GetValues(typeof(AchievementType)).Cast<AchievementType>())
        {
            string achievementId = $"{cityId}_{achievementType}";
            
            // Try to load existing achievement
            Achievement achievement = LoadAchievement(achievementId);
            
            if (achievement == null)
            {
                // Create new achievement with current milestone configuration
                int[] milestones = GetMilestonesForType(achievementType);
                
                achievement = new Achievement(
                    achievementId,
                    cityId,
                    achievementType,
                    achievementTitles[achievementType],
                    milestones
                );
                
                LogDebug($"Created new achievement: {achievement.GetCurrentTitle()}");
            }
            else
            {
                // IMPORTANT: Always update the title from the current dictionary to override old titles
                achievement.title = achievementTitles[achievementType];
                LogDebug($"Loaded existing achievement and updated title to: {achievement.GetCurrentTitle()} (Level {achievement.currentMilestoneIndex})");
            }
            
            currentCityAchievements[achievementId] = achievement;
        }
    }
    
    /// <summary>
    /// Gets the milestone array for a specific achievement type, respecting the custom milestone settings
    /// </summary>
    private int[] GetMilestonesForType(AchievementType type)
    {
        int[] milestones;
        
        if (useCustomMilestones)
        {
            LogDebug($"Using CUSTOM milestones for {type}");
            switch (type)
            {
                case AchievementType.InstagramFollowers:
                    milestones = instagramFollowersMilestones;
                    break;
                case AchievementType.TikTokFollowers:
                    milestones = tiktokFollowersMilestones;
                    break;
                case AchievementType.TikTokLikes:
                    milestones = tiktokLikesMilestones;
                    break;
                case AchievementType.TicketsSold:
                    milestones = ticketsSoldMilestones;
                    break;
                case AchievementType.NumberOfEvents:
                    milestones = numberOfEventsMilestones;
                    break;
                case AchievementType.AverageAttendance:
                    milestones = averageAttendanceMilestones;
                    break;
                default:
                    LogDebug($"No custom milestone found for {type}, using default");
                    milestones = defaultMilestones[type];
                    break;
            }
        }
        else
        {
            LogDebug($"Using DEFAULT milestones for {type}");
            milestones = defaultMilestones[type];
        }
        
        // Log the actual values being used
        string milestoneStr = string.Join(", ", milestones);
        LogDebug($"  Milestones for {type}: [{milestoneStr}] (Count: {milestones.Length})");
        
        return milestones;
    }
    
    private void LoadUnlockedAchievements(string cityId)
    {
        unlockedAchievements.Clear();
        string key = UNLOCKED_KEY + cityId;
        
        if (PlayerPrefs.HasKey(key))
        {
            try
            {
                string json = PlayerPrefs.GetString(key);
                var wrapper = JsonUtility.FromJson<UnlockedAchievementListWrapper>(json);
                unlockedAchievements = wrapper.achievements.ToList();
                LogDebug($"Loaded {unlockedAchievements.Count} unlocked achievements for {cityId}");
            }
            catch (Exception ex)
            {
                LogDebug($"Error loading unlocked achievements: {ex.Message}");
            }
        }
    }
    
    public void CheckAchievementProgress()
    {
        if (dataModel == null)
        {
            LogDebug("DataModel is null, cannot check achievement progress");
            return;
        }
        
        string currentCityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        
        foreach (var kvp in currentCityAchievements)
        {
            Achievement achievement = kvp.Value;
            int currentValue = GetCurrentValueForAchievement(achievement);
            
            bool progressMade = CheckSingleAchievementProgress(achievement, currentValue);
            if (progressMade)
            {
                SaveAchievement(achievement);
                OnAchievementProgressUpdated?.Invoke(achievement);
            }
        }
        
        // Update UI
        if (achievementUI != null)
        {
            achievementUI.UpdateAchievementDisplay(currentCityAchievements.Values.ToList(), unlockedAchievements);
        }
    }
    
    private bool CheckSingleAchievementProgress(Achievement achievement, int currentValue)
    {
        bool madeProgress = false;
        
        // Check if we've unlocked any new milestones
        while (achievement.HasNextMilestone() && currentValue >= achievement.GetCurrentTarget())
        {
            // Unlock this milestone
            UnlockMilestone(achievement, currentValue);
            madeProgress = true;
            
            // Move to next milestone
            achievement.currentMilestoneIndex++;
            
            // Check if all milestones are completed
            if (!achievement.HasNextMilestone())
            {
                achievement.isCompleted = true;
                LogDebug($"🏆 ACHIEVEMENT COMPLETED: {achievement.title} for {achievement.cityId}!");
            }
        }
        
        return madeProgress;
    }
    
    private void UnlockMilestone(Achievement achievement, int currentValue)
    {
        int milestoneLevel = achievement.currentMilestoneIndex;
        DateTime unlockDate = DateTime.Now;
        
        // Create unlocked achievement record
        var unlockedAchievement = new UnlockedAchievement(
            achievement.id,
            milestoneLevel,
            unlockDate,
            currentValue
        );
        
        unlockedAchievements.Add(unlockedAchievement);
        SaveUnlockedAchievements();
        
        LogDebug($"🎉 MILESTONE UNLOCKED: {achievement.GetCurrentTitle()} (Level {milestoneLevel + 1}) with value {currentValue:N0}!");
        
        // Trigger events
        OnAchievementUnlocked?.Invoke(achievement, milestoneLevel);
        
        // Show notification if enabled
        if (enableNotifications)
        {
            ShowAchievementNotification(achievement, milestoneLevel, currentValue);
        }
    }
    
    private int GetCurrentValueForAchievement(Achievement achievement)
    {
        if (dataModel == null) return 0;
        
        string cityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        
        switch (achievement.type)
        {
            case AchievementType.InstagramFollowers:
                var socialMetrics = dataModel.GetLatestSocialMediaMetrics(cityId);
                return socialMetrics != null ? ParseFollowerCount(socialMetrics.InstagramFollowers) : 0;
                
            case AchievementType.TikTokFollowers:
                socialMetrics = dataModel.GetLatestSocialMediaMetrics(cityId);
                return socialMetrics != null ? ParseFollowerCount(socialMetrics.TikTokFollowers) : 0;
                
            case AchievementType.TikTokLikes:
                socialMetrics = dataModel.GetLatestSocialMediaMetrics(cityId);
                return socialMetrics != null ? ParseFollowerCount(socialMetrics.TikTokLikes) : 0;
                
            case AchievementType.TicketsSold:
                var eventMetrics = dataModel.GetLatestEventMetrics(cityId);
                return eventMetrics != null ? ParseFollowerCount(eventMetrics.TicketsSold) : 0;
                
            case AchievementType.NumberOfEvents:
                eventMetrics = dataModel.GetLatestEventMetrics(cityId);
                return eventMetrics != null ? ParseFollowerCount(eventMetrics.NumberOfEvents) : 0;
                
            case AchievementType.AverageAttendance:
                eventMetrics = dataModel.GetLatestEventMetrics(cityId);
                return eventMetrics != null ? ParseFollowerCount(eventMetrics.AverageAttendance) : 0;
                
            default:
                return 0;
        }
    }
    
    private int ParseFollowerCount(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        
        string cleanValue = value.Replace(" ", "").Trim().ToUpper();
        
        // Handle "K" suffix (thousands)
        if (cleanValue.EndsWith("K"))
        {
            cleanValue = cleanValue.Substring(0, cleanValue.Length - 1);
            if (float.TryParse(cleanValue.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float kValue))
            {
                return Mathf.RoundToInt(kValue * 1000);
            }
        }
        
        // Handle regular numbers
        string numericValue = cleanValue.Replace(",", "").Replace(".", "");
        if (int.TryParse(numericValue, out int result))
        {
            return result;
        }
        
        return 0;
    }
    
    private void ShowAchievementNotification(Achievement achievement, int milestoneLevel, int currentValue)
    {
        // For now, just log. Later can integrate with Unity's Mobile Notifications
        string message = $"🎉 Achievement Unlocked!\n{achievement.GetCurrentTitle()}\nReached {currentValue:N0}!";
        LogDebug($"NOTIFICATION: {message}");
    }
    
    private void SaveAchievement(Achievement achievement)
    {
        string key = ACHIEVEMENTS_KEY + achievement.id;
        string json = JsonUtility.ToJson(achievement);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }
    
    private Achievement LoadAchievement(string achievementId)
    {
        string key = ACHIEVEMENTS_KEY + achievementId;
        if (PlayerPrefs.HasKey(key))
        {
            try
            {
                string json = PlayerPrefs.GetString(key);
                return JsonUtility.FromJson<Achievement>(json);
            }
            catch (Exception ex)
            {
                LogDebug($"Error loading achievement {achievementId}: {ex.Message}");
            }
        }
        return null;
    }
    
    private void SaveUnlockedAchievements()
    {
        string currentCityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        string key = UNLOCKED_KEY + currentCityId;
        
        var wrapper = new UnlockedAchievementListWrapper { achievements = unlockedAchievements.ToArray() };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }
    
    // Public API methods
    public List<Achievement> GetCurrentCityAchievements()
    {
        return currentCityAchievements.Values.ToList();
    }
    
    public List<UnlockedAchievement> GetUnlockedAchievements()
    {
        return unlockedAchievements;
    }
    
    public int GetUnlockedAchievementCount()
    {
        return unlockedAchievements.Count;
    }
    
    public float GetOverallProgress()
    {
        if (currentCityAchievements.Count == 0) return 0f;
        
        float totalProgress = 0f;
        foreach (var achievement in currentCityAchievements.Values)
        {
            if (achievement.isCompleted)
            {
                totalProgress += 1f;
            }
            else
            {
                // Calculate partial progress for current milestone
                float milestoneProgress = (float)achievement.currentMilestoneIndex / achievement.milestones.Length;
                totalProgress += milestoneProgress;
            }
        }
        
        return totalProgress / currentCityAchievements.Count;
    }
    
    private void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[AchievementSystem] {message}");
        }
    }
    
    // Context menu methods for testing
    [ContextMenu("Force Check Achievements")]
    public void ForceCheckAchievements()
    {
        LogDebug("Forcing achievement check...");
        CheckAchievementProgress();
    }
    
    [ContextMenu("Clear All Achievement Data")]
    public void ClearAllAchievementData()
    {
        string currentCityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        
        // Clear saved achievements
        foreach (AchievementType type in Enum.GetValues(typeof(AchievementType)))
        {
            string achievementId = $"{currentCityId}_{type}";
            PlayerPrefs.DeleteKey(ACHIEVEMENTS_KEY + achievementId);
        }
        
        // Clear unlocked achievements
        PlayerPrefs.DeleteKey(UNLOCKED_KEY + currentCityId);
        PlayerPrefs.Save();
        
        LogDebug($"Cleared all achievement data for city: {currentCityId}");
        
        // Reinitialize
        InitializeCurrentCityAchievements();
    }
    
    [ContextMenu("Apply Custom Milestones")]
    public void ApplyCustomMilestones()
    {
        LogDebug("Applying custom milestones - clearing existing achievement data");
        
        // Clear existing achievement data to force recreation with new milestones
        string currentCityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        
        // Clear PlayerPrefs for achievements
        foreach (var achievementType in Enum.GetValues(typeof(AchievementType)).Cast<AchievementType>())
        {
            string achievementId = $"{currentCityId}_{achievementType}";
            PlayerPrefs.DeleteKey(ACHIEVEMENTS_KEY + achievementId);
        }
        
        // Also clear unlocked achievements
        PlayerPrefs.DeleteKey(UNLOCKED_KEY + currentCityId);
        PlayerPrefs.Save();
        
        // Recreate achievements with new milestones
        InitializeCurrentCityAchievements();
        
        LogDebug("Custom milestones applied and achievements recreated!");
    }
    
    [ContextMenu("Force Recreate Achievements")]
    public void ForceRecreateAchievements()
    {
        LogDebug("Force recreating achievements with updated milestone configuration");
        
        string currentCityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        
        // Clear all achievement and unlocked data
        foreach (var achievementType in Enum.GetValues(typeof(AchievementType)).Cast<AchievementType>())
        {
            string achievementId = $"{currentCityId}_{achievementType}";
            PlayerPrefs.DeleteKey(ACHIEVEMENTS_KEY + achievementId);
        }
        PlayerPrefs.DeleteKey(UNLOCKED_KEY + currentCityId);
        PlayerPrefs.Save();
        
        // Recreate everything
        InitializeCurrentCityAchievements();
        
        LogDebug("Achievements recreated with new milestone configuration!");
    }
    
    [ContextMenu("Debug Current Milestones")]
    public void DebugCurrentMilestones()
    {
        LogDebug("=== CURRENT MILESTONE CONFIGURATION ===");
        LogDebug($"Using Custom Milestones: {useCustomMilestones}");
        
        foreach (AchievementType type in Enum.GetValues(typeof(AchievementType)))
        {
            int[] milestones = GetMilestonesForType(type);
            string milestoneStr = string.Join(", ", milestones);
            LogDebug($"{type}: [{milestoneStr}]");
        }
        
        LogDebug("=== END MILESTONE CONFIGURATION ===");
    }
    
    [ContextMenu("Force Regenerate Achievements with Current Inspector Values")]
    public void ForceRegenerateAchievements()
    {
        LogDebug("=== FORCE REGENERATING ACHIEVEMENTS ===");
        LogDebug($"useCustomMilestones is set to: {useCustomMilestones}");
        
        if (!useCustomMilestones)
        {
            LogDebug("WARNING: useCustomMilestones is FALSE - inspector values will be ignored!");
            LogDebug("Set useCustomMilestones to TRUE to use inspector values");
        }
        
        // Log all current inspector values
        LogDebug("Current inspector milestone arrays:");
        LogDebug($"  Instagram Followers: [{string.Join(", ", instagramFollowersMilestones)}] (Count: {instagramFollowersMilestones.Length})");
        LogDebug($"  TikTok Followers: [{string.Join(", ", tiktokFollowersMilestones)}] (Count: {tiktokFollowersMilestones.Length})");
        LogDebug($"  TikTok Likes: [{string.Join(", ", tiktokLikesMilestones)}] (Count: {tiktokLikesMilestones.Length})");
        LogDebug($"  Tickets Sold: [{string.Join(", ", ticketsSoldMilestones)}] (Count: {ticketsSoldMilestones.Length})");
        LogDebug($"  Number of Events: [{string.Join(", ", numberOfEventsMilestones)}] (Count: {numberOfEventsMilestones.Length})");
        LogDebug($"  Average Attendance: [{string.Join(", ", averageAttendanceMilestones)}] (Count: {averageAttendanceMilestones.Length})");
        
        // Calculate expected total
        int expectedTotal = 0;
        if (useCustomMilestones)
        {
            expectedTotal = instagramFollowersMilestones.Length + tiktokFollowersMilestones.Length + 
                           tiktokLikesMilestones.Length + ticketsSoldMilestones.Length + 
                           numberOfEventsMilestones.Length + averageAttendanceMilestones.Length;
        }
        else
        {
            foreach (var kvp in defaultMilestones)
            {
                expectedTotal += kvp.Value.Length;
            }
        }
        LogDebug($"Expected total milestone count: {expectedTotal}");
        
        // Clear all existing achievement data
        string currentCityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        LogDebug($"Clearing achievement data for city: {currentCityId}");
        
        // Clear PlayerPrefs for all achievements
        foreach (var achievementType in System.Enum.GetValues(typeof(AchievementType)).Cast<AchievementType>())
        {
            string achievementId = $"{currentCityId}_{achievementType}";
            PlayerPrefs.DeleteKey(ACHIEVEMENTS_KEY + achievementId);
            LogDebug($"Cleared achievement data for: {achievementId}");
        }
        
        // Clear unlocked achievements
        PlayerPrefs.DeleteKey(UNLOCKED_KEY + currentCityId);
        LogDebug($"Cleared unlocked achievements for: {currentCityId}");
        
        // Clear any cached data
        PlayerPrefs.Save();
        
        // Force recreation of achievements with current inspector values
        LogDebug("Forcing recreation of achievements...");
        
        // Clear the in-memory cache first
        currentCityAchievements.Clear();
        
        // Recreate achievements (this will use the updated titles from the dictionary)
        LoadAchievementsForCity(currentCityId);
        
        // Force check achievement progress to ensure everything is up to date
        CheckAchievementProgress();
        
        // Save all updated achievements with new titles
        foreach (var achievement in currentCityAchievements.Values)
        {
            SaveAchievement(achievement);
        }
        PlayerPrefs.Save();
        
        LogDebug("=== FORCE REGENERATION COMPLETE ===");
        LogDebug("Trophy count should now reflect current inspector values");
        LogDebug("Please refresh the achievement UI manually to see the updated count");
        
        // Note: User should manually refresh the achievement UI after this operation
    }
    
    [ContextMenu("Update Achievement Titles Only")]
    public void UpdateAchievementTitlesOnly()
    {
        LogDebug("=== UPDATING ACHIEVEMENT TITLES ONLY ===");
        
        string currentCityId = PlayerPrefs.GetString("SelectedCityId", "bgsnl");
        LogDebug($"Updating titles for city: {currentCityId}");
        
        // Update titles for all current achievements
        foreach (var kvp in currentCityAchievements)
        {
            Achievement achievement = kvp.Value;
            string oldTitle = achievement.title;
            achievement.title = achievementTitles[achievement.type];
            
            LogDebug($"Updated {achievement.type}: '{oldTitle}' -> '{achievement.title}'");
            
            // Save the updated achievement
            SaveAchievement(achievement);
        }
        
        PlayerPrefs.Save();
        
        // Refresh the UI if available
        if (achievementUI != null)
        {
            achievementUI.RefreshDisplay();
        }
        
        LogDebug("=== TITLE UPDATE COMPLETE ===");
        LogDebug("New titles should now be visible in the UI");
    }
}

// Helper wrapper for JSON serialization
[Serializable]
public class UnlockedAchievementListWrapper
{
    public UnlockedAchievement[] achievements;
} 
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System;

[Serializable]
public class AchievementUIElement
{
    [Header("Achievement Configuration")]
    public AchievementType achievementType; // Which achievement this represents
    public GameObject achievementObject; // The main achievement GameObject
    
    [Header("UI Components")]
    public TextMeshProUGUI statText; // Shows achievement title (e.g., "Reach 1000 Instagram Followers") 
    public TextMeshProUGUI progressText; // Shows progress numbers (e.g., "825/1000")
    
    [Header("Visual Settings")]
    public Color normalColor = Color.white;
    
    public void UpdateDisplay(Achievement achievement, int currentValue)
    {
        if (achievementObject == null) return;
        
        // Update achievement title
        if (statText != null)
        {
            statText.text = achievement.GetCurrentTitle();
            statText.alpha = 1f; // Fix any alpha issues
        }
        
        // Update progress numbers with K formatting for large numbers
        if (progressText != null)
        {
            string formattedCurrent = FormatNumber(currentValue);
            string formattedTarget = FormatNumber(achievement.GetCurrentTarget());
            progressText.text = $"{formattedCurrent}/{formattedTarget}";
            progressText.alpha = 1f; // Fix any alpha issues
        }
    }
    
    private string FormatNumber(int number)
    {
        if (number >= 1000000)
        {
            // Format millions: 1M, 1.5M, 2.3M, etc.
            float millions = number / 1000000f;
            if (millions == Mathf.Floor(millions))
            {
                return $"{millions:F0}M"; // Whole millions: 1M, 2M
            }
            else
            {
                return $"{millions:F1}M"; // Decimal millions: 1.5M, 2.3M
            }
        }
        else if (number >= 1000)
        {
            // Format thousands: 1K, 1.5K, 50K, etc.
            float thousands = number / 1000f;
            if (thousands == Mathf.Floor(thousands))
            {
                return $"{thousands:F0}K"; // Whole thousands: 1K, 50K
            }
            else
            {
                return $"{thousands:F1}K"; // Decimal thousands: 1.5K, 2.3K
            }
        }
        else
        {
            // Less than 1000: show as-is
            return number.ToString();
        }
    }
}

[Serializable]
public class AchievementTrophySet
{
    [Header("Achievement Type")]
    public AchievementType achievementType;
    
    [Header("Trophy Prefabs for Each Milestone")]
    public GameObject[] trophyPrefabs; // Array of trophy prefabs for each milestone level
    
    // Hidden fields - only needed for auto-generated trophy text (which user doesn't want)
    [HideInInspector] public string achievementDisplayName = ""; // e.g., "Instagram", "TikTok", "Events" - leave empty if not needed
    [HideInInspector] public Sprite achievementIcon; // Optional icon for the trophy - not currently used
    [HideInInspector] public bool modifyTrophyText = false; // Set to true if you want trophy text to be auto-generated
    
    public GameObject GetTrophyPrefab(int milestoneLevel)
    {
        if (trophyPrefabs == null || trophyPrefabs.Length == 0) return null;
        
        // If milestone level exceeds available prefabs, use the last one
        int index = Mathf.Min(milestoneLevel, trophyPrefabs.Length - 1);
        return trophyPrefabs[index];
    }
    
    public string GetMilestoneName(int milestoneLevel)
    {
        // Only generate milestone name if achievement display name is provided
        if (string.IsNullOrEmpty(achievementDisplayName))
            return $"Achievement {milestoneLevel + 1}";
            
        return $"{achievementDisplayName} Level {milestoneLevel + 1}";
    }
}

[Serializable]
public class TrophyConfiguration
{
    [Header("Trophy Sets (One per Achievement Type)")]
    public AchievementTrophySet[] achievementTrophySets = new AchievementTrophySet[0];
    
    [Header("Trophy Container")]
    public RectTransform trophyContainer; // Where trophies will be spawned
    public ScrollRect trophyScrollRect; // For horizontal scrolling
    
    public AchievementTrophySet GetTrophySet(AchievementType achievementType)
    {
        return achievementTrophySets.FirstOrDefault(set => set.achievementType == achievementType);
    }
    
    public GameObject GetTrophyPrefab(AchievementType achievementType, int milestoneLevel)
    {
        var trophySet = GetTrophySet(achievementType);
        return trophySet?.GetTrophyPrefab(milestoneLevel);
    }
}

public class AchievementUI : MonoBehaviour
{
    [Header("Achievement Display")]
    [SerializeField] private List<AchievementUIElement> achievementElements = new List<AchievementUIElement>();
    
    [Header("Trophy System")]
    [SerializeField] private TrophyConfiguration trophyConfig;
    [SerializeField] private TextMeshProUGUI trophyCountText; // Optional: shows total trophy count
    
    [Header("Auto-Discovery (Optional)")]
    [SerializeField] private bool useAutoDiscovery = false;
    [SerializeField] private Transform achievementsParent; // Parent object containing achievements
    
    [Header("Settings")]
    [SerializeField] private bool debugMode = true;
    
    [Header("Dynamic Content Sizing")]
    [SerializeField] private float trophyWidth = 300f; // Width of each trophy
    [SerializeField] private float trophySpacing = 100f; // Spacing between trophies (from Horizontal Layout Group)
    [SerializeField] private float leftPadding = 100f; // Left padding of the content (from Horizontal Layout Group)
    [SerializeField] private float rightPadding = 100f; // Right padding of the content (from Horizontal Layout Group)
    [SerializeField] private float minContentWidth = 1000f; // Minimum content width when no trophies
    
    // Private variables
    private List<GameObject> currentTrophyObjects = new List<GameObject>();
    private AchievementSystem achievementSystem;
    private DataModelClasses dataModel;
    
    private bool contentPositionFixed = false;
    private Vector3 targetContentPosition = new Vector3(4280f, 0, 0);
    
    private void Awake()
    {
        FindReferences();
    }
    
    private void Start()
    {
        StartCoroutine(AutomaticInitialDisplay());
        
        // Fix ScrollRect content position issue
        if (trophyConfig != null && trophyConfig.trophyScrollRect != null)
        {
            StartCoroutine(FixScrollRectPosition());
        }
    }
    
    private void FindReferences()
    {
        if (achievementSystem == null)
            achievementSystem = FindObjectOfType<AchievementSystem>();
        
        if (dataModel == null)
            dataModel = FindObjectOfType<DataModelClasses>();
    }
    
    private System.Collections.IEnumerator AutomaticInitialDisplay()
    {
        yield return new WaitForSeconds(2f);
        
        if (useAutoDiscovery)
            AutoDiscoverAchievements();
            
        RefreshDisplay();
    }
    
    private void AutoDiscoverAchievements()
    {
        if (achievementsParent == null)
        {
            LogDebug("Auto-discovery enabled but no achievements parent set");
            return;
        }
        
        achievementElements.Clear();
        
        // Find achievement objects by name patterns
        string[] achievementNames = { "InstagramFollowersAchievement", "TikTokFollowersAchievement", 
                                    "TikTokLikesAchievement", "TicketsSoldAchievement", 
                                    "NumberOfEventsAchievement", "AverageAttendanceAchievement" };
        
        foreach (string name in achievementNames)
        {
            Transform found = achievementsParent.Find(name);
            if (found != null)
            {
                AchievementUIElement element = new AchievementUIElement();
                element.achievementObject = found.gameObject;
                
                // Set achievement type based on name
                if (name.Contains("Instagram")) element.achievementType = AchievementType.InstagramFollowers;
                else if (name.Contains("TikTokFollowers")) element.achievementType = AchievementType.TikTokFollowers;
                else if (name.Contains("TikTokLikes")) element.achievementType = AchievementType.TikTokLikes;
                else if (name.Contains("Tickets")) element.achievementType = AchievementType.TicketsSold;
                else if (name.Contains("Events")) element.achievementType = AchievementType.NumberOfEvents;
                else if (name.Contains("Attendance")) element.achievementType = AchievementType.AverageAttendance;
                
                // Auto-find text components
                Transform statTransform = found.Find("Stat");
                if (statTransform != null)
                {
                    Transform statTextTransform = statTransform.Find("Text (TMP)");
                    if (statTextTransform != null)
                        element.statText = statTextTransform.GetComponent<TextMeshProUGUI>();
                }
                
                Transform progressTransform = found.Find("Progress");
                if (progressTransform != null)
                {
                    Transform progressTextTransform = progressTransform.Find("Text (TMP)");
                    if (progressTextTransform != null)
                        element.progressText = progressTextTransform.GetComponent<TextMeshProUGUI>();
                }
                
                achievementElements.Add(element);
                LogDebug($"Auto-discovered achievement: {name}");
            }
        }
        
        LogDebug($"Auto-discovery complete. Found {achievementElements.Count} achievements");
    }
    
    public void UpdateAchievementDisplay(List<Achievement> achievements, List<UnlockedAchievement> unlockedAchievements)
    {
        foreach (var achievement in achievements)
        {
            var uiElement = achievementElements.FirstOrDefault(element => element.achievementType == achievement.type);
            if (uiElement != null)
            {
                int currentValue = GetCurrentValueForAchievement(achievement);
                uiElement.UpdateDisplay(achievement, currentValue);
            }
        }
        
        UpdateTrophyDisplay(unlockedAchievements);
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
    
    private void UpdateTrophyDisplay(List<UnlockedAchievement> unlockedAchievements)
    {
        if (trophyConfig == null || trophyConfig.trophyContainer == null) return;
        
        // Clear existing trophies
        foreach (var trophy in currentTrophyObjects)
        {
            if (trophy != null) Destroy(trophy);
        }
        currentTrophyObjects.Clear();
        
        int trophiesCreated = 0;
        
        // Create trophies for unlocked achievements
        foreach (var unlockedAchievement in unlockedAchievements.OrderBy(ua => ua.unlockDate))
        {
            AchievementType achievementType = ExtractAchievementTypeFromId(unlockedAchievement.achievementId);
            
            AchievementTrophySet trophySet = trophyConfig.GetTrophySet(achievementType);
            if (trophySet == null) continue;
            
            GameObject trophyPrefab = trophySet.GetTrophyPrefab(unlockedAchievement.milestoneLevel);
            if (trophyPrefab == null) continue;
            
            GameObject trophyInstance = Instantiate(trophyPrefab, trophyConfig.trophyContainer);
            currentTrophyObjects.Add(trophyInstance);
            trophiesCreated++;
            
            if (trophySet.modifyTrophyText)
            {
                var textComponents = trophyInstance.GetComponentsInChildren<TextMeshProUGUI>();
                if (textComponents.Length > 0)
                {
                    string trophyName = trophySet.GetMilestoneName(unlockedAchievement.milestoneLevel);
                    textComponents[0].text = trophyName;
                }
            }
        }
        
        if (!contentPositionFixed && trophyConfig.trophyContainer != null)
        {
            StartCoroutine(FixContentPositionOnce());
        }
        
        UpdateTrophyCount(currentTrophyObjects.Count);
        ResizeContentForTrophyCount(currentTrophyObjects.Count);
    }
    
    private System.Collections.IEnumerator FixContentPositionOnce()
    {
        // Wait for layout to update
        yield return null;
        yield return null; // Wait two frames to be sure
        
        if (trophyConfig != null && trophyConfig.trophyContainer != null)
        {
            // Set the target position (your desired X position)
            trophyConfig.trophyContainer.localPosition = targetContentPosition;
            
            // Reset scroll position to start (optional - remove if you don't want this)
            if (trophyConfig.trophyScrollRect != null)
            {
                trophyConfig.trophyScrollRect.horizontalNormalizedPosition = 0f;
            }
            
            LogDebug($"Fixed content position to: {targetContentPosition} - scrolling is now free");
        }
    }
    
    private AchievementType ExtractAchievementTypeFromId(string achievementId)
    {
        // Extract type from ID like "bgsnl_InstagramFollowers"
        string[] parts = achievementId.Split('_');
        string typeString = parts.Length > 1 ? parts[1] : achievementId;
        
        // Try to parse the achievement type
        if (Enum.TryParse<AchievementType>(typeString, out AchievementType result))
        {
            return result;
        }
        
        // Fallback to InstagramFollowers if parsing fails
        return AchievementType.InstagramFollowers;
    }
    
    private string GetMilestoneNameForDisplay(AchievementType achievementType, int milestoneLevel)
    {
        var trophySet = trophyConfig.GetTrophySet(achievementType);
        return trophySet?.GetMilestoneName(milestoneLevel) ?? $"Level {milestoneLevel + 1}";
    }
    
    private void UpdateTrophyCount(int count)
    {
        if (trophyCountText != null)
        {
            int totalPossibleTrophies = CalculateTotalPossibleTrophies();
            trophyCountText.text = $"{count}/{totalPossibleTrophies}";
        }
    }
    
    private int CalculateTotalPossibleTrophies()
    {
        if (achievementSystem == null)
        {
            LogDebug("AchievementSystem not found, cannot calculate total milestones");
            return 0;
        }
        
        // Get all achievements from the achievement system
        var allAchievements = achievementSystem.GetCurrentCityAchievements();
        if (allAchievements == null || allAchievements.Count == 0)
        {
            LogDebug("No achievements found in AchievementSystem");
            return 0;
        }
        
        int total = 0;
        LogDebug($"=== DETAILED MILESTONE COUNT DEBUG ===");
        LogDebug($"Found {allAchievements.Count} achievements in system");
        
        foreach (var achievement in allAchievements)
        {
            if (achievement != null && achievement.milestones != null)
            {
                // Each milestone represents one possible trophy
                int milestonesForThisAchievement = achievement.milestones.Length;
                total += milestonesForThisAchievement;
                LogDebug($"Achievement {achievement.type} has {milestonesForThisAchievement} possible milestones/trophies");
                
                // Log actual milestone values for detailed debugging
                string milestoneValues = string.Join(", ", achievement.milestones);
                LogDebug($"  Milestone values: [{milestoneValues}]");
            }
            else
            {
                LogDebug($"Achievement {achievement?.type} has null milestones!");
            }
        }
        
        LogDebug($"Total possible trophies/milestones across all achievements: {total}");
        LogDebug($"Expected total should be: 9+9+9+7+6+6 = 46");
        LogDebug($"=== END DETAILED MILESTONE COUNT DEBUG ===");
        return total;
    }
    
    private string FormatAchievementTypeName(string type)
    {
        switch (type)
        {
            case "InstagramFollowers": return "Instagram";
            case "TikTokFollowers": return "TikTok";
            case "TikTokLikes": return "TikTok Likes";
            case "TicketsSold": return "Tickets";
            case "NumberOfEvents": return "Events";
            case "AverageAttendance": return "Attendance";
            default: return type;
        }
    }
    
    private System.Collections.IEnumerator ScrollToNewestTrophy()
    {
        yield return new WaitForEndOfFrame();
        if (trophyConfig.trophyScrollRect != null)
        {
            trophyConfig.trophyScrollRect.horizontalNormalizedPosition = 1f; // Scroll to right (newest)
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
    
    private void LogDebug(string message)
    {
        // Removed debug logging in production
    }
    
    public void RefreshDisplay()
    {
        if (achievementSystem != null)
        {
            var achievements = achievementSystem.GetCurrentCityAchievements();
            var unlockedAchievements = achievementSystem.GetUnlockedAchievements();
            UpdateAchievementDisplay(achievements, unlockedAchievements);
        }
    }
    
    // Debug methods
    [ContextMenu("Refresh Display")]
    private void TestRefreshDisplay()
    {
        RefreshDisplay();
    }
    
    [ContextMenu("Auto-Discover Achievements")]
    private void TestAutoDiscovery()
    {
        AutoDiscoverAchievements();
    }
    
    private System.Collections.IEnumerator FixScrollRectPosition()
    {
        // Wait for everything to initialize
        yield return new WaitForSeconds(0.1f);
        
        if (trophyConfig.trophyScrollRect != null && trophyConfig.trophyContainer != null)
        {
            // Store the desired position (what you set in inspector)
            Vector3 desiredPosition = new Vector3(4280f, trophyConfig.trophyContainer.localPosition.y, 0);
            
            // Force the content to the desired position
            trophyConfig.trophyContainer.localPosition = desiredPosition;
            
            // Also set the ScrollRect's normalized position to prevent auto-repositioning
            trophyConfig.trophyScrollRect.horizontalNormalizedPosition = 0f;
            
            LogDebug($"Fixed content position to: {desiredPosition}");
        }
    }
    
    private void ResizeContentForTrophyCount(int trophyCount)
    {
        if (trophyConfig == null || trophyConfig.trophyContainer == null) return;
        
        float contentWidth;
        
        if (trophyCount == 0)
        {
            contentWidth = minContentWidth;
        }
        else
        {
            contentWidth = leftPadding + (trophyCount * trophyWidth) + ((trophyCount - 1) * trophySpacing) + rightPadding;
        }
        
        RectTransform contentRect = trophyConfig.trophyContainer;
        Vector2 sizeDelta = contentRect.sizeDelta;
        sizeDelta.x = contentWidth;
        contentRect.sizeDelta = sizeDelta;
    }
} 
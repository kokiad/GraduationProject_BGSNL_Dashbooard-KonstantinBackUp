using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// Handles UI button interactions for the BGSNL Dashboard
/// Attach this script to button GameObjects to handle specific functions
/// </summary>
public class UIButtonHandler : MonoBehaviour
{
    public enum ButtonType
    {
        DropdownToggle,
        CitiesScene,
        CityButton,
        ChartsScene,
        AchievementsScene,
        HomeSoloCities // Refresh button for individual city scenes
    }

    [Header("Button Configuration")]
    [SerializeField] private ButtonType buttonType;
    [SerializeField] private string cityId; // Only used for CityButton type
    [SerializeField] private GameObject dropdownMenu; // Only used for DropdownToggle
    
    [Header("Debugging")]
    [SerializeField] private bool debugMode = true;
    
    private Button button;
    
    // Static reference to track active dropdown for touch outside detection
    private static GameObject activeDropdown;
    // Flag to prevent immediate reopening when toggling the dropdown
    private bool isTogglingDropdown = false;
    
    private void Awake()
    {
        button = GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError($"[UIButtonHandler] No Button component found on {gameObject.name}");
            return;
        }
        
        // If this is a CityButton but no cityId is set, try to infer from name
        if (buttonType == ButtonType.CityButton && string.IsNullOrEmpty(cityId))
        {
            InferCityIdFromName();
        }
    }
    
    private void InferCityIdFromName()
    {
        // Try to use the button or GameObject name as cityId if not set
        string name = gameObject.name.ToLower();
        
        if (name.StartsWith("citybutton_"))
        {
            cityId = name.Substring(11); // Remove "citybutton_" prefix
        }
        else if (name.EndsWith("button"))
        {
            cityId = name.Substring(0, name.Length - 6); // Remove "button" suffix
        }
        else if (name.Contains("_"))
        {
            cityId = name.Split('_')[0]; // Use part before underscore
        }
        else
        {
            cityId = name; // Just use the name as is
        }
        
        LogDebug($"[UIButtonHandler] Inferred cityId '{cityId}' from name '{gameObject.name}'");
    }
    
    private void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log(message);
        }
    }
    
    private void Start()
    {
        // Setup button click handler based on type
        button.onClick.RemoveAllListeners();
        
        switch (buttonType)
        {
            case ButtonType.DropdownToggle:
                button.onClick.AddListener(ToggleDropdownMenu);
                break;
                
            case ButtonType.CitiesScene:
                button.onClick.AddListener(GoToCitiesScene);
                break;
                
            case ButtonType.CityButton:
                button.onClick.AddListener(SelectCityAndGoHome);
                break;
                
            case ButtonType.ChartsScene:
                button.onClick.AddListener(GoToChartsScene);
                break;
                
            case ButtonType.AchievementsScene:
                button.onClick.AddListener(GoToAchievementsScene);
                break;
                
            case ButtonType.HomeSoloCities:
                button.onClick.AddListener(RefreshCurrentScene);
                break;
        }
        
        // Log setup for clarity
        if (buttonType == ButtonType.CityButton)
        {
            LogDebug($"[UIButtonHandler] Setup CityButton '{gameObject.name}' with cityId: '{cityId}'");
        }
    }
    
    private void Update()
    {
        // Check for touches outside dropdown menu
        if (activeDropdown != null && Input.touchCount > 0 && !isTogglingDropdown)
        {
            Touch touch = Input.GetTouch(0);
            
            if (touch.phase == TouchPhase.Began)
            {
                // Check if touch is outside dropdown and not on UI elements
                if (!IsPointerOverUIElement(touch.position) || !IsPointerOverDropdown(touch.position))
                {
                    CloseActiveDropdown();
                    LogDebug("[UIButtonHandler] Touched outside dropdown, closing it");
        }
            }
        }
        
        // Fallback for testing in editor with mouse clicks
        #if UNITY_EDITOR
        if (activeDropdown != null && Input.GetMouseButtonDown(0) && !isTogglingDropdown)
        {
            if (!IsPointerOverUIElement(Input.mousePosition) || !IsPointerOverDropdown(Input.mousePosition))
            {
                CloseActiveDropdown();
                LogDebug("[UIButtonHandler] Clicked outside dropdown, closing it");
            }
        }
        #endif
    }
    
    private bool IsPointerOverUIElement(Vector2 screenPosition)
    {
        // Check if a UI element is under the pointer
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;
        
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        return results.Count > 0;
    }
    
    private bool IsPointerOverDropdown(Vector2 screenPosition)
    {
        if (activeDropdown == null) return false;
        
        // Cast a ray to check if it's over the dropdown
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPosition;
        
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        
        foreach (RaycastResult result in results)
        {
            // Check if this result is the dropdown or a child of the dropdown
            if (result.gameObject == activeDropdown || 
                (result.gameObject.transform.IsChildOf(activeDropdown.transform)))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private static void CloseActiveDropdown()
    {
        if (activeDropdown != null)
        {
            activeDropdown.SetActive(false);
            activeDropdown = null;
        }
    }
    
    private void ToggleDropdownMenu()
    {
        if (dropdownMenu == null)
        {
            Debug.LogError("[UIButtonHandler] Cannot toggle dropdown menu - reference is null!");
            return;
        }
        
        // If we're already toggling, ignore this click completely
        if (isTogglingDropdown)
        {
            LogDebug("[UIButtonHandler] Ignoring toggle request - already toggling");
            return;
        }
        
        // Set flag to prevent double-toggling
        isTogglingDropdown = true;
        
        // Get current state
        bool isCurrentlyActive = dropdownMenu.activeSelf;
        
        // If currently open, close it
        if (isCurrentlyActive)
        {
            dropdownMenu.SetActive(false);
            activeDropdown = null;
            LogDebug("[UIButtonHandler] Closed dropdown menu");
        }
        // If currently closed, open it
        else
        {
            // Close any other active dropdown first
            if (activeDropdown != null && activeDropdown != dropdownMenu)
            {
                activeDropdown.SetActive(false);
                LogDebug("[UIButtonHandler] Closed another active dropdown first");
            }
            
            dropdownMenu.SetActive(true);
            activeDropdown = dropdownMenu;
            LogDebug("[UIButtonHandler] Opened dropdown menu");
        }
        
        // Use a delayed coroutine to reset the flag
        StartCoroutine(ResetToggleFlag());
    }
    
    private IEnumerator ResetToggleFlag()
    {
        // Wait for a longer time to prevent any possibility of double-clicks
        yield return new WaitForSeconds(0.7f);
        isTogglingDropdown = false;
        LogDebug("[UIButtonHandler] Reset toggle flag - ready for next toggle");
    }
    
    private void GoToCitiesScene()
    {
        // Close dropdown if it exists
        if (dropdownMenu != null)
        {
            dropdownMenu.SetActive(false);
            activeDropdown = null;
        }
        
        // Load cities scene
        LogDebug("[UIButtonHandler] Loading CitiesScreen scene");
        SceneManager.LoadScene("CitiesScreen");
    }
    
    private void GoToChartsScene()
    {
        // Close dropdown if it exists
        if (dropdownMenu != null)
        {
            dropdownMenu.SetActive(false);
            activeDropdown = null;
        }
        
        // Determine which charts scene to load
        string targetScene;
        
        if (!string.IsNullOrEmpty(cityId))
        {
            // Use the cityId set in inspector
            targetScene = DetermineChartsSceneForCity(cityId);
            LogDebug($"[UIButtonHandler] Using cityId from inspector: '{cityId}' -> {targetScene}");
        }
        else
        {
            // No cityId set in inspector, default to original ChartsScreen
            targetScene = "ChartsScreen";
            LogDebug("[UIButtonHandler] No cityId set in inspector, using default ChartsScreen");
        }
        
        // Load charts scene
        LogDebug($"[UIButtonHandler] Loading charts scene: {targetScene}");
        SceneManager.LoadScene(targetScene);
    }
    
    private string DetermineChartsSceneForCity(string cityId)
    {
        // Normalize city ID
        string normalizedCityId = cityId.ToLower().Trim();
        
        // Map city IDs to their charts scene names
        switch (normalizedCityId)
        {
            case "bgsnl":
            case "admin":
                return "ChartsScreen"; // Default charts scene for BGSNL/admin
                
            case "bgsg":
                return "BGSG_ChartsScreen";
                
            case "bgsr":
                return "BGSR_ChartsScreen";
                
            case "bgsl":
                return "BGSL_ChartsScreen";
                
            case "bgsa":
                return "BGSA_ChartsScreen";
                
            case "bgsb":
                return "BGSB_ChartsScreen";
                
            case "bgsm":
                return "BGSM_ChartsScreen";
                
            case "bgse":
                return "BGSE_ChartsScreen";
                
            default:
                LogDebug($"[UIButtonHandler] Unknown city ID '{cityId}', falling back to default ChartsScreen");
                return "ChartsScreen"; // Fallback to default
        }
    }
    
    private void GoToAchievementsScene()
    {
        // Close dropdown if it exists
        if (dropdownMenu != null)
        {
            dropdownMenu.SetActive(false);
            activeDropdown = null;
        }
        
        // Determine which achievements scene to load
        string targetScene;
        
        if (!string.IsNullOrEmpty(cityId))
        {
            // Use the cityId set in inspector
            targetScene = DetermineAchievementsSceneForCity(cityId);
            LogDebug($"[UIButtonHandler] Using cityId from inspector: '{cityId}' -> {targetScene}");
        }
        else
        {
            // No cityId set in inspector, default to original AchievementsScreen
            targetScene = "AchievementsScreen";
            LogDebug("[UIButtonHandler] No cityId set in inspector, using default AchievementsScreen");
        }
        
        // Load achievements scene
        LogDebug($"[UIButtonHandler] Loading achievements scene: {targetScene}");
        SceneManager.LoadScene(targetScene);
    }
    
    private string DetermineAchievementsSceneForCity(string cityId)
    {
        // Normalize city ID
        string normalizedCityId = cityId.ToLower().Trim();
        
        // Map city IDs to their achievements scene names
        switch (normalizedCityId)
        {
            case "bgsnl":
            case "admin":
                return "AchievementsScreen"; // Default achievements scene for BGSNL/admin
                
            case "bgsg":
                return "BGSG_AchievementsScreen";
                
            case "bgsr":
                return "BGSR_AchievementsScreen";
                
            case "bgsl":
                return "BGSL_AchievementsScreen";
                
            case "bgsa":
                return "BGSA_AchievementsScreen";
                
            case "bgsb":
                return "BGSB_AchievementsScreen";
                
            case "bgsm":
                return "BGSM_AchievementsScreen";
                
            case "bgse":
                return "BGSE_AchievementsScreen";
                
            default:
                LogDebug($"[UIButtonHandler] Unknown city ID '{cityId}', falling back to default AchievementsScreen");
                return "AchievementsScreen"; // Fallback to default
        }
    }
    
    private void SelectCityAndGoHome()
    {
        // Double-check cityId
        if (string.IsNullOrEmpty(cityId))
        {
            InferCityIdFromName();
            
            if (string.IsNullOrEmpty(cityId))
            {
                Debug.LogError($"[UIButtonHandler] Cannot select city - cityId is null or empty for button {gameObject.name}");
                return;
            }
        }
        
        Debug.Log($"[IMPORTANT DEBUG] SelectCityAndGoHome execution path for cityId: '{cityId}'");
        
        // Tell the system not to force default city
        PlayerPrefs.SetInt("ForceDefaultCity", 0);
        PlayerPrefs.SetString("SelectedCityId", cityId);
        PlayerPrefs.Save();
        LogDebug($"[UIButtonHandler] City button clicked: {gameObject.name} with cityId: {cityId}");
        LogDebug($"[UIButtonHandler] Set SelectedCityId to '{cityId}' in PlayerPrefs");
        
        // Try to find UIManager in current scene
        UIManager manager = FindObjectOfType<UIManager>();
        if (manager != null)
        {
            Debug.Log($"[IMPORTANT DEBUG] Before calling LoadCity with '{cityId}'");
            LogDebug($"[UIButtonHandler] Found UIManager, calling LoadCity with '{cityId}'");
            manager.LoadCity(cityId);
            Debug.Log($"[IMPORTANT DEBUG] After calling LoadCity with '{cityId}'");
        }
        
        // Go to main scene
        Debug.Log($"[IMPORTANT DEBUG] Before loading HomeScreen");
        LogDebug("[UIButtonHandler] Loading HomeScreen scene");
        SceneManager.LoadScene("HomeScreen");
        Debug.Log($"[IMPORTANT DEBUG] After loading HomeScreen (this may not be seen)");
    }
    
    private void RefreshCurrentScene()
    {
        LogDebug("[UIButtonHandler] HomeSoloCities button clicked - navigating to city dashboard");
        
        // Close dropdown if it exists
        if (dropdownMenu != null)
        {
            dropdownMenu.SetActive(false);
            activeDropdown = null;
        }
        
        // Determine which city ID to use
        string targetCityId;
        string targetScene;
        
        if (!string.IsNullOrEmpty(cityId))
        {
            // Use the cityId set in inspector
            targetCityId = cityId;
            targetScene = DetermineDashboardSceneForCity(targetCityId);
            LogDebug($"[UIButtonHandler] Using cityId from inspector: '{cityId}' -> {targetScene}");
        }
        else
        {
            // No cityId set in inspector, default to HomeScreen
            targetCityId = "bgsnl";
            targetScene = "HomeScreen";
            LogDebug("[UIButtonHandler] No cityId set in inspector, using default HomeScreen");
        }
        
        // Set up PlayerPrefs for the target city
        PlayerPrefs.SetInt("ForceDefaultCity", 0);
        PlayerPrefs.SetString("SelectedCityId", targetCityId);
        PlayerPrefs.Save();
        
        LogDebug($"[UIButtonHandler] Navigating to city dashboard: '{targetScene}' for city: '{targetCityId}'");
        
        // Load the city's dashboard scene
        SceneManager.LoadScene(targetScene);
    }
    
    private string DetermineDashboardSceneForCity(string cityId)
    {
        // Normalize city ID
        string normalizedCityId = cityId.ToLower().Trim();
        
        // Map city IDs to their dashboard scene names
        switch (normalizedCityId)
        {
            case "bgsnl":
            case "admin":
                return "HomeScreen"; // Default dashboard scene for BGSNL/admin
                
            case "bgsg":
                return "BGSG";
                
            case "bgsr":
                return "BGSR";
                
            case "bgsl":
                return "BGSL";
                
            case "bgsa":
                return "BGSA";
                
            case "bgsb":
                return "BGSB";
                
            case "bgsm":
                return "BGSM";
                
            case "bgse":
                return "BGSE";
                
            default:
                LogDebug($"[UIButtonHandler] Unknown city ID '{cityId}', falling back to default HomeScreen");
                return "HomeScreen"; // Fallback to default
        }
    }
    
    private void OnDestroy()
    {
        // Clear dropdown reference if this is the object being destroyed
        if (dropdownMenu != null && activeDropdown == dropdownMenu)
        {
            activeDropdown = null;
        }
    }
    
    // Reset to BGSNL when quitting the application to ensure it starts with BGSNL next time
    private void OnApplicationQuit()
    {
        // Force default city on next startup
        PlayerPrefs.SetInt("ForceDefaultCity", 1);
        PlayerPrefs.SetString("SelectedCityId", "bgsnl");
        PlayerPrefs.Save();
        LogDebug("[UIButtonHandler] Application quitting - Reset preferences to BGSNL for next startup");
    }
} 
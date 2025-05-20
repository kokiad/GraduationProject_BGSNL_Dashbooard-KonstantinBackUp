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
        AchievementsScene
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
        
        // Load charts scene
        LogDebug("[UIButtonHandler] Loading ChartsScreen scene");
        SceneManager.LoadScene("ChartsScreen");
    }
    
    private void GoToAchievementsScene()
    {
        // Close dropdown if it exists
        if (dropdownMenu != null)
        {
            dropdownMenu.SetActive(false);
            activeDropdown = null;
        }
        
        // Load achievements scene
        LogDebug("[UIButtonHandler] Loading AchievementsScreen scene");
        SceneManager.LoadScene("AchievementsScreen");
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
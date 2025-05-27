using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Simplified GDPR Manager for BGSNL Internal Association App
/// Uses Legitimate Interest legal basis instead of complex consent system
/// </summary>
public class SimpleGDPRManager : MonoBehaviour
{
    [Header("GDPR UI Components")]
    [SerializeField] private GameObject privacyNoticePanel;
    [SerializeField] private GameObject privacyPolicyPanel;
    [SerializeField] private GameObject dataManagementPanel;
    [SerializeField] private GameObject viewMyDataPanel;
    [SerializeField] private TextMeshProUGUI privacyNoticeText;
    [SerializeField] private TextMeshProUGUI privacyPolicyText;
    [SerializeField] private TextMeshProUGUI viewMyDataText;
    [SerializeField] private Button acknowledgeButton;
    [SerializeField] private Button viewPrivacyPolicyButton;
    [SerializeField] private Button deleteDataButton;
    [SerializeField] private Button closePrivacyPolicyButton;
    [SerializeField] private Button closeViewMyDataButton;
    [SerializeField] private Button viewMyDataButton;

    [Header("Settings")]
    [SerializeField] private string organizationName = "BGSNL (Bulgarian Society Netherlands)";
    [SerializeField] private string contactEmail = "info@bgsnl.nl";
    [SerializeField] private bool debugMode = true;

    // Simple PlayerPrefs keys
    private const string PREF_PRIVACY_ACKNOWLEDGED = "PrivacyAcknowledged";
    private const string PREF_PRIVACY_ACKNOWLEDGE_DATE = "PrivacyAcknowledgeDate";
    private const string PREF_PRIVACY_VERSION = "PrivacyVersion";
    
    private const string CURRENT_PRIVACY_VERSION = "1.0";

    public static SimpleGDPRManager Instance { get; private set; }
    public static event System.Action<bool> OnPrivacyAcknowledged;

    private void Awake()
    {
        // Singleton pattern
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
    }

    private void Start()
    {
        SetupUI();
        HideAllPanels();
    }

    private void SetupUI()
    {
        // Setup button listeners
        if (acknowledgeButton != null)
            acknowledgeButton.onClick.AddListener(() => AcknowledgePrivacyNotice());
            
        if (viewPrivacyPolicyButton != null)
            viewPrivacyPolicyButton.onClick.AddListener(() => ShowPrivacyPolicy());
            
        if (deleteDataButton != null)
            deleteDataButton.onClick.AddListener(() => DeleteUserData());
            
        if (closePrivacyPolicyButton != null)
            closePrivacyPolicyButton.onClick.AddListener(() => HidePrivacyPolicy());
            
        if (closeViewMyDataButton != null)
            closeViewMyDataButton.onClick.AddListener(() => HideViewMyData());

        if (viewMyDataButton != null)
            viewMyDataButton.onClick.AddListener(() => ViewMyData());

        UpdatePrivacyNoticeText();
        UpdatePrivacyPolicyText();
    }

    /// <summary>
    /// Check if user needs to see privacy notice (only on first use)
    /// </summary>
    public bool NeedsPrivacyAcknowledgment()
    {
        bool hasAcknowledged = PlayerPrefs.HasKey(PREF_PRIVACY_ACKNOWLEDGED);
        string acknowledgedVersion = PlayerPrefs.GetString(PREF_PRIVACY_VERSION, "");
        
        bool needsUpdate = acknowledgedVersion != CURRENT_PRIVACY_VERSION;
        
        LogDebug($"Privacy check - Has acknowledged: {hasAcknowledged}, Version: {acknowledgedVersion}, Current: {CURRENT_PRIVACY_VERSION}, Needs update: {needsUpdate}");
        
        return !hasAcknowledged || needsUpdate;
    }

    /// <summary>
    /// Show privacy notice (simple acknowledgment, not consent)
    /// </summary>
    public void ShowPrivacyNotice()
    {
        LogDebug("Showing privacy notice");
        HideAllPanels();
        if (privacyNoticePanel != null)
        {
            privacyNoticePanel.SetActive(true);
        }
    }

    /// <summary>
    /// User acknowledges they've read the privacy information
    /// </summary>
    public void AcknowledgePrivacyNotice()
    {
        LogDebug("User acknowledged privacy notice");
        
        // Save acknowledgment
        PlayerPrefs.SetString(PREF_PRIVACY_ACKNOWLEDGED, "true");
        PlayerPrefs.SetString(PREF_PRIVACY_ACKNOWLEDGE_DATE, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.SetString(PREF_PRIVACY_VERSION, CURRENT_PRIVACY_VERSION);
        PlayerPrefs.Save();
        
        // Hide notice
        HideAllPanels();
        
        // Notify that privacy has been acknowledged
        OnPrivacyAcknowledged?.Invoke(true);
        
        LogDebug("Privacy acknowledgment saved");
    }

    /// <summary>
    /// Show full privacy policy
    /// </summary>
    public void ShowPrivacyPolicy()
    {
        LogDebug("Showing privacy policy");
        HideAllPanels();
        if (privacyPolicyPanel != null)
        {
            privacyPolicyPanel.SetActive(true);
        }
    }

    public void HidePrivacyPolicy()
    {
        LogDebug("Hiding privacy policy panel");
        
        if (privacyPolicyPanel != null)
        {
            privacyPolicyPanel.SetActive(false);
        }
        
        // Check if we're in the startup flow and privacy notice should still be showing
        if (privacyNoticePanel != null && !PlayerPrefs.HasKey(PREF_PRIVACY_ACKNOWLEDGED))
        {
            // We're in startup flow and privacy hasn't been acknowledged yet
            // Make sure privacy notice is visible
            LogDebug("Returning to privacy notice during startup flow");
            privacyNoticePanel.SetActive(true);
            return;
        }
        
        // If we're not in startup flow, this is normal privacy policy viewing
        LogDebug("Privacy policy closed outside of startup flow");
    }

    /// <summary>
    /// Delete all user data (GDPR Right to Erasure)
    /// </summary>
    public void DeleteUserData()
    {
        LogDebug("User requested data deletion - deleting immediately");
        
        // Clear all user data immediately
        ClearAllUserData();
        
        LogDebug("All user data deleted. Returning to startup.");
        
        // Return to startup scene
        SceneManager.LoadScene(0);
    }
    
    /// <summary>
    /// Get summary of user's personal data (GDPR Article 15 - Right to Access)
    /// </summary>
    public string GetUserDataSummary()
    {
        string summary = "=== YOUR PERSONAL DATA ===\n\n";
        
        summary += "AUTHENTICATION DATA:\n";
        summary += $"• Email: {PlayerPrefs.GetString("SavedUserEmail", "Not set")}\n";
        summary += $"• Role: {PlayerPrefs.GetString("SavedUserRole", "Not set")}\n";
        summary += $"• Selected City: {PlayerPrefs.GetString("SelectedCityId", "Not set")}\n\n";
        
        summary += "PRIVACY DATA:\n";
        summary += $"• Privacy Acknowledged: {PlayerPrefs.GetString(PREF_PRIVACY_ACKNOWLEDGED, "No")}\n";
        summary += $"• Acknowledgment Date: {PlayerPrefs.GetString(PREF_PRIVACY_ACKNOWLEDGE_DATE, "Not set")}\n";
        summary += $"• Privacy Policy Version: {PlayerPrefs.GetString(PREF_PRIVACY_VERSION, "Not set")}\n\n";
        
        summary += "SESSION DATA:\n";
        summary += $"• Has Launched Before: {(PlayerPrefs.HasKey("HasLaunchedBefore") ? "Yes" : "No")}\n";
        summary += $"• Manual Login Success: {(PlayerPrefs.HasKey("ManualLoginSuccess") ? "Yes" : "No")}\n";
        summary += $"• Last Login Type: {PlayerPrefs.GetString("LastLoginType", "Not set")}\n\n";
        
        summary += "DATA PROCESSING:\n";
        summary += $"• Legal Basis: Legitimate Interest (Article 6(1)(f) GDPR)\n";
        summary += $"• Purpose: Internal association operations\n";
        summary += $"• Data Controller: {organizationName}\n";
        summary += $"• Contact: {contactEmail}\n\n";
        
        summary += $"Data summary generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
        
        return summary;
    }

    private void ClearAllUserData()
    {
        // Clear all user-related PlayerPrefs
        PlayerPrefs.DeleteKey("SavedUserEmail");
        PlayerPrefs.DeleteKey("SavedUserRole");
        PlayerPrefs.DeleteKey("SelectedCityId");
        PlayerPrefs.DeleteKey("ManualLoginSuccess");
        PlayerPrefs.DeleteKey("UserLoggedOut");
        PlayerPrefs.DeleteKey("LastLoginType");
        PlayerPrefs.DeleteKey("HasLaunchedBefore");
        PlayerPrefs.DeleteKey(PREF_PRIVACY_ACKNOWLEDGED);
        PlayerPrefs.DeleteKey(PREF_PRIVACY_ACKNOWLEDGE_DATE);
        PlayerPrefs.DeleteKey(PREF_PRIVACY_VERSION);
        
        PlayerPrefs.Save();
        
        LogDebug("All user data cleared from device");
    }

    private void HideAllPanels()
    {
        if (privacyNoticePanel != null) privacyNoticePanel.SetActive(false);
        if (privacyPolicyPanel != null) privacyPolicyPanel.SetActive(false);
        if (dataManagementPanel != null) dataManagementPanel.SetActive(false);
        if (viewMyDataPanel != null) viewMyDataPanel.SetActive(false);
    }

    private void UpdatePrivacyNoticeText()
    {
        if (privacyNoticeText != null)
        {
            privacyNoticeText.text = $@"<b>Privacy Information</b>

Welcome to the {organizationName} Dashboard.

<b>Data Processing Notice:</b>
We process your email address and role information for internal association operations based on legitimate interest (GDPR Article 6(1)(f)).

<b>What we do:</b>
• Authenticate board members via Google Sign-In
• Control access to association data
• Provide dashboard functionality

<b>Your Rights:</b>
• View our full privacy policy
• View your personal data (use 'View My Data' function)
• Delete your data (use 'Delete My Data' function)
• Contact us with questions at {contactEmail}

<b>Contact:</b> {contactEmail}

By continuing, you acknowledge that you have been informed about our data processing.";
        }
    }

    private void UpdatePrivacyPolicyText()
    {
        if (privacyPolicyText != null)
        {
            privacyPolicyText.text = $@"<b>BGSNL Dashboard Privacy Policy</b>

<b>Last Updated:</b> {DateTime.Now:yyyy-MM-dd}
<b>Version:</b> 1.0

<b>1. Data Controller</b>
{organizationName}
Address: Hoendiep 59, Groningen, 9718TC, The Netherlands
Email: {contactEmail}
Data Protection Officer: vladislavmarinov3142@gmail.com

<b>2. Introduction</b>
BGSNL respects your privacy and is committed to protecting your personal data. This privacy policy explains how we collect, use, and protect your personal information when you use the BGSNL Dashboard application, in accordance with the EU General Data Protection Regulation (GDPR) and the Dutch Implementation Act (UAVG).

<b>3. Personal Data We Collect</b>

<b>3.1 Authentication Data</b>
• Email address (required)
• User role/permissions (automatically assigned)
• Authentication tokens (temporary, for session management)

<b>3.2 Technical Data</b>
• Device information (for technical support)
• App usage logs (for security and troubleshooting)
• Error reports (for app improvement)

<b>4. Legal Basis for Processing</b>
We process your personal data based on:

<b>4.1 Legitimate Interest (Article 6(1)(f) GDPR)</b>
• Authentication and access control: Necessary for providing secure access to the dashboard
• Security monitoring: Necessary to protect our systems and your data
• Technical support: Necessary to resolve issues and maintain service quality

<b>4.2 Legal Obligation (Article 6(1)(c) GDPR)</b>
• Security logging: Required for cybersecurity compliance
• Data breach notification: Required under GDPR

<b>5. How We Use Your Data</b>

<b>5.1 Primary Purposes</b>
• Authenticate your identity and provide secure access
• Determine your access permissions and role
• Maintain security of our systems
• Provide technical support when needed

<b>6. Data Sharing and Recipients</b>

<b>6.1 Third-Party Services</b>
We share limited data with the following trusted service providers:

• <b>Google LLC</b> (Authentication services)
  - Data shared: Email address, authentication tokens
  - Purpose: Secure authentication via Google Sign-In
  - Legal basis: Legitimate interest (security)
  - Location: EU/EEA and USA (with adequate safeguards)

• <b>Google Firebase</b> (Data storage and hosting)
  - Data shared: User roles, app data
  - Purpose: Secure data storage and app functionality
  - Legal basis: Legitimate interest (service provision)
  - Location: EU/EEA (primary: Belgium via Firebase europe-west1)

<b>6.2 No Data Sales</b>
We do not sell, rent, or trade your personal data to third parties for marketing purposes.

<b>7. International Data Transfers</b>
Some of our service providers (Google) may process data outside the EU/EEA. We ensure adequate protection through:
• Standard Contractual Clauses approved by the European Commission
• Adequate security measures
Google ensures GDPR compliance through Standard Contractual Clauses approved by the European Commission.

<b>8. Data Retention</b>

<b>8.1 Retention Periods</b>
• Authentication data: Until you delete your account or withdraw consent
• Security logs: 12 months maximum
• Technical support data: 12 months after issue resolution
• Google's security logs: Automatically deleted after 12 months

<b>8.2 Automatic Deletion</b>
We automatically delete data when retention periods expire or when you exercise your right to erasure.

<b>9. Your Rights Under GDPR</b>
You have the following rights regarding your personal data:

<b>9.1 Right to Access (Article 15)</b>
• Request a copy of all personal data we hold about you
• How to exercise: Use the 'View My Data' function in the app or email {contactEmail}

<b>9.2 Right to Rectification (Article 16)</b>
• Correct inaccurate or incomplete personal data
• How to exercise: Contact {contactEmail} with corrections

<b>9.3 Right to Erasure (Article 17)</b>
• Request deletion of your personal data
• How to exercise: Use the 'Delete My Data' function in the app or email {contactEmail}

<b>9.4 Right to Restrict Processing (Article 18)</b>
• Limit how we process your data in certain circumstances
• How to exercise: Contact {contactEmail}

<b>9.5 Right to Data Portability (Article 20)</b>
• Receive your data in a machine-readable format
• How to exercise: Contact {contactEmail} for data view assistance

<b>9.6 Right to Object (Article 21)</b>
• Object to processing based on legitimate interests
• How to exercise: Contact {contactEmail}

<b>9.7 Right to Withdraw Consent (Article 7)</b>
• Withdraw consent for analytics or marketing at any time
• How to exercise: Use app settings or contact {contactEmail}

<b>9.8 Response Time</b>
We will respond to your requests within one month of receipt.

<b>10. Data Security</b>
We implement appropriate technical and organizational measures to protect your data:

<b>10.1 Technical Measures</b>
• Encryption in transit: All data transmitted using HTTPS/TLS
• Secure authentication: OAuth 2.0 with Google
• Access controls: Role-based permissions
• Regular updates: Security patches and updates

<b>10.2 Organizational Measures</b>
• Staff training: Regular privacy and security training
• Access limitations: Need-to-know basis only
• Incident procedures: Data breach response plan
• Regular audits: Compliance and security reviews

<b>11. Data Breach Notification</b>
In case of a personal data breach:
• We will notify the Dutch Data Protection Authority within 72 hours
• We will notify affected users without undue delay if there is high risk
• We will document all breaches and remedial actions taken

<b>12. Cookies and Tracking</b>
The BGSNL Dashboard app does not use cookies or tracking technologies beyond what is necessary for authentication and app functionality.

<b>13. Changes to This Policy</b>
We may update this privacy policy from time to time. When we do:
• We will notify you through the app
• We will update the ""Last updated"" date
• We may request renewed consent if required by law
• Previous versions will be archived for reference

<b>14. Contact Information</b>

<b>14.1 Privacy Questions</b>
Email: {contactEmail}
Response time: Within 5 business days

<b>14.2 Data Protection Officer</b>
Email: vladislavmarinov3142@gmail.com
Role: Independent oversight of data protection compliance

<b>14.3 General Contact</b>
Email: {contactEmail}
Website: https://www.bulgariansociety.nl/

<b>15. Supervisory Authority</b>
You have the right to lodge a complaint with the supervisory authority:

<b>Dutch Data Protection Authority (Autoriteit Persoonsgegevens)</b>
Website: autoriteitpersoonsgegevens.nl
Phone: +31 70 888 8500
Email: info@autoriteitpersoonsgegevens.nl

Address:
Autoriteit Persoonsgegevens
Postbus 93374
2509 AJ Den Haag
The Netherlands

<b>16. Legal Framework</b>
This privacy policy is governed by:
• EU General Data Protection Regulation (GDPR) - Regulation (EU) 2016/679
• Dutch Implementation Act (UAVG) - Uitvoeringswet Algemene verordening gegevensbescherming
• Dutch Telecommunications Act - Telecommunicatiewet (where applicable)

<b>Effective Date:</b> {DateTime.Now:yyyy-MM-dd}
<b>Document Version:</b> 1.0
<b>Language:</b> English

This privacy policy is designed to be transparent and understandable. If you have any questions about this policy or our data practices, please contact us at {contactEmail}.";
        }
    }

    private void LogDebug(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[SimpleGDPR] {message}");
        }
    }

    // Context menu helpers for testing
    [ContextMenu("Show Privacy Notice")]
    private void TestShowPrivacyNotice()
    {
        ShowPrivacyNotice();
    }

    [ContextMenu("Clear Privacy Data")]
    private void TestClearPrivacyData()
    {
        PlayerPrefs.DeleteKey(PREF_PRIVACY_ACKNOWLEDGED);
        PlayerPrefs.DeleteKey(PREF_PRIVACY_ACKNOWLEDGE_DATE);
        PlayerPrefs.DeleteKey(PREF_PRIVACY_VERSION);
        PlayerPrefs.Save();
        Debug.Log("Privacy acknowledgment data cleared");
    }

    // Add this method for dropdown integration
    public void ShowPrivacyPolicyFromDropdown()
    {
        LogDebug("Showing privacy policy from dropdown menu");
        ShowPrivacyPolicy();
    }
    
    // Add method specifically for dropdown-based data management
    public void HandleDataManagementDropdown()
    {
        LogDebug("Data management accessed via dropdown");
        // This can be called when the dropdown opens
        // Useful for analytics or logging
    }

    /// <summary>
    /// Show user their personal data (GDPR Article 15 - Right to Access)
    /// </summary>
    public void ViewMyData()
    {
        LogDebug("User requested to view their personal data");
        
        HideAllPanels(); // Close any other open panels
        
        if (viewMyDataPanel != null)
        {
            // Update the text with user's data
            if (viewMyDataText != null)
            {
                viewMyDataText.text = GetUserDataSummary();
            }
            
            // Show the panel
            viewMyDataPanel.SetActive(true);
            LogDebug("Showing view my data panel");
        }
        else
        {
            LogDebug("View My Data panel not assigned - logging data instead");
            string userData = GetUserDataSummary();
            Debug.Log($"[Your Personal Data]\n{userData}");
        }
    }

    public void HideViewMyData()
    {
        LogDebug("Hiding view my data panel");
        
        if (viewMyDataPanel != null)
        {
            viewMyDataPanel.SetActive(false);
        }
    }
} 
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

// ModelType enum needs to match FeedbackManager
public enum ModelType
{
    DQN,
    PPO
}

/// <summary>
/// Displays DQN and PPO feedback side-by-side for player comparison and selection
/// </summary>
public class FeedbackComparisonUI : MonoBehaviour
{
    [System.Serializable]
    public class FeedbackChoice
    {
        public ModelType chosenModel;
        public FeedbackMessage dqnFeedback;
        public FeedbackMessage ppoFeedback;
        public float responseTime; // Time taken to choose
    }

    [Header("Main Panel")]
    [SerializeField] private GameObject comparisonPanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI overallQuestionScoreText; // Average of DQN and PPO overall scores
    
    [Header("DQN Feedback (Left)")]
    [SerializeField] private GameObject dqnPanel;
    [SerializeField] private TextMeshProUGUI dqnTitle;
    [SerializeField] private TextMeshProUGUI dqnMessage;
    [SerializeField] private TextMeshProUGUI dqnPerformanceText;
    [SerializeField] private Button chooseDQNButton;
    [SerializeField] private Slider dqnConfidenceBar;
    [SerializeField] private Slider dqnClarityBar;
    [SerializeField] private Slider dqnPaceBar;
    [SerializeField] private Slider dqnToneBar;
    [SerializeField] private Slider dqnOverallBar;
    
    [Header("DQN Slider Value Labels")]
    [SerializeField] private TextMeshProUGUI dqnConfidenceValue;
    [SerializeField] private TextMeshProUGUI dqnClarityValue;
    [SerializeField] private TextMeshProUGUI dqnPaceValue;
    [SerializeField] private TextMeshProUGUI dqnToneValue;
    [SerializeField] private TextMeshProUGUI dqnOverallValue;
    
    [Header("PPO Feedback (Right)")]
    [SerializeField] private GameObject ppoPanel;
    [SerializeField] private TextMeshProUGUI ppoTitle;
    [SerializeField] private TextMeshProUGUI ppoMessage;
    [SerializeField] private TextMeshProUGUI ppoPerformanceText;
    [SerializeField] private Button choosePPOButton;
    [SerializeField] private Slider ppoConfidenceBar;
    [SerializeField] private Slider ppoClarityBar;
    [SerializeField] private Slider ppoPaceBar;
    [SerializeField] private Slider ppoToneBar;
    [SerializeField] private Slider ppoOverallBar;

    [Header("PPO Slider Value Labels")]
    [SerializeField] private TextMeshProUGUI ppoConfidenceValue;
    [SerializeField] private TextMeshProUGUI ppoClarityValue;
    [SerializeField] private TextMeshProUGUI ppoPaceValue;
    [SerializeField] private TextMeshProUGUI ppoToneValue;
    [SerializeField] private TextMeshProUGUI ppoOverallValue;

    [Header("Visual Feedback")]
    [SerializeField] private Color excellentColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color goodColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color needsImprovementColor = new Color(0.9f, 0.3f, 0.3f);
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField] private float fadeInDuration = 0.5f;

    [Header("Display Mode")]
    [SerializeField] public bool showOnlyDQN = false;
    [Tooltip("When true, only DQN feedback is displayed. When false, both DQN and PPO are shown side-by-side.")]

    [Header("Events")]
    public UnityEvent<FeedbackChoice> OnFeedbackChosen;

    private FeedbackMessage currentDQNFeedback;
    private FeedbackMessage currentPPOFeedback;
    private float comparisonStartTime;
    private bool isDisplaying = false;

    // Public property to check if comparison is currently being displayed
    public bool IsDisplaying => isDisplaying;

    // Public methods to get current feedback messages
    public FeedbackMessage GetCurrentDQNFeedback() => currentDQNFeedback;
    public FeedbackMessage GetCurrentPPOFeedback() => currentPPOFeedback;

    private void Awake()
    {
        if (canvasGroup == null && comparisonPanel != null)
            canvasGroup = comparisonPanel.GetComponent<CanvasGroup>();

        if (canvasGroup == null && comparisonPanel != null)
            canvasGroup = comparisonPanel.AddComponent<CanvasGroup>();

        // Setup button listeners
        if (chooseDQNButton != null)
            chooseDQNButton.onClick.AddListener(() => OnChoiceMade(ModelType.DQN));

        if (choosePPOButton != null)
            choosePPOButton.onClick.AddListener(() => OnChoiceMade(ModelType.PPO));

        // Make all sliders non-interactable (read-only)
        MakeSliderReadOnly(dqnConfidenceBar);
        MakeSliderReadOnly(dqnClarityBar);
        MakeSliderReadOnly(dqnPaceBar);
        MakeSliderReadOnly(dqnToneBar);
        MakeSliderReadOnly(dqnOverallBar);
        
        MakeSliderReadOnly(ppoConfidenceBar);
        MakeSliderReadOnly(ppoClarityBar);
        MakeSliderReadOnly(ppoPaceBar);
        MakeSliderReadOnly(ppoToneBar);
        MakeSliderReadOnly(ppoOverallBar);

        // Initially hide
        if (comparisonPanel != null)
            comparisonPanel.SetActive(false);
    }

    /// <summary>
    /// Make a slider read-only (non-interactable)
    /// </summary>
    private void MakeSliderReadOnly(Slider slider)
    {
        if (slider != null)
        {
            slider.interactable = false;
        }
    }

    /// <summary>
    /// Show both DQN and PPO feedback for comparison
    /// </summary>
    public void ShowComparison(FeedbackMessage dqnFeedback, FeedbackMessage ppoFeedback)
    {
        if (isDisplaying)
        {
            StopAllCoroutines();
        }

        currentDQNFeedback = dqnFeedback;
        currentPPOFeedback = ppoFeedback;

        // Record DQN and PPO scores separately for end-of-game summary
        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.RecordDQNScore(dqnFeedback.currentPerformance);
            FeedbackManager.Instance.RecordPPOScore(ppoFeedback.currentPerformance);
        }

        StartCoroutine(DisplayComparisonCoroutine());
    }

    private IEnumerator DisplayComparisonCoroutine()
    {
        isDisplaying = true;
        Debug.Log("🎯 FeedbackComparisonUI: isDisplaying = TRUE");
        comparisonStartTime = Time.time;

        // Set instruction text based on display mode
        if (instructionText != null)
        {
            if (showOnlyDQN)
                instructionText.text = "Review your feedback:";
            else
                instructionText.text = "Choose the feedback that would help you most:";
        }

        // Populate DQN side
        PopulateFeedbackPanel(
            dqnTitle, dqnMessage, dqnPerformanceText,
            dqnConfidenceBar, dqnClarityBar, dqnPaceBar, dqnToneBar, dqnOverallBar,
            dqnConfidenceValue, dqnClarityValue, dqnPaceValue, dqnToneValue,
            currentDQNFeedback, "Feedback B"
        );

        // Show/hide PPO panel based on display mode
        if (ppoPanel != null)
            ppoPanel.SetActive(!showOnlyDQN);

        // Populate PPO side only if not in DQN-only mode
        if (!showOnlyDQN)
        {
            PopulateFeedbackPanel(
                ppoTitle, ppoMessage, ppoPerformanceText,
                ppoConfidenceBar, ppoClarityBar, ppoPaceBar, ppoToneBar, ppoOverallBar,
                ppoConfidenceValue, ppoClarityValue, ppoPaceValue, ppoToneValue,
                currentPPOFeedback, "Feedback A"
            );
        }

        // Calculate and display overall question score
        if (overallQuestionScoreText != null && currentDQNFeedback != null)
        {
            float scoreOverall;
            
            if (showOnlyDQN)
            {
                // In DQN-only mode, use DQN's overall score
                scoreOverall = currentDQNFeedback.currentPerformance.overall;
            }
            else
            {
                // In comparison mode, use average of DQN and PPO
                if (currentPPOFeedback != null)
                    scoreOverall = (currentDQNFeedback.currentPerformance.overall + currentPPOFeedback.currentPerformance.overall) / 2f;
                else
                    scoreOverall = currentDQNFeedback.currentPerformance.overall;
            }
            
            int percentage = Mathf.RoundToInt(scoreOverall * 100f);
            Color scoreColor = GetScoreColor(scoreOverall);
            string hexColor = ColorUtility.ToHtmlStringRGB(scoreColor);

            overallQuestionScoreText.text = $"<b>Overall Question Score:</b> <color=#{hexColor}>{percentage}%</color>";

            // Ensure the bar graph will use the same overall score that the player sees
            if (FeedbackManager.Instance != null)
            {
                FeedbackManager.Instance.SetNextHistoryOverall(scoreOverall);
            }
        }

        // Enable buttons based on display mode
        if (chooseDQNButton != null) chooseDQNButton.interactable = true;
        if (choosePPOButton != null) 
        {
            // Hide PPO button if in DQN-only mode
            choosePPOButton.gameObject.SetActive(!showOnlyDQN);
            choosePPOButton.interactable = !showOnlyDQN;
        }

        // Show panel with fade in
        if (comparisonPanel != null)
            comparisonPanel.SetActive(true);

        yield return StartCoroutine(FadeIn());
    }

    private void PopulateFeedbackPanel(
        TextMeshProUGUI title,
        TextMeshProUGUI message,
        TextMeshProUGUI performanceText,
        Slider confidenceBar, Slider clarityBar, Slider paceBar, Slider toneBar, Slider overallBar,
        TextMeshProUGUI confidenceValue, TextMeshProUGUI clarityValue, TextMeshProUGUI paceValue, TextMeshProUGUI toneValue,
        FeedbackMessage feedback,
        string modelName)
    {
        if (title != null)
            title.text = $"{modelName}";

        if (message != null)
            message.text = feedback.message;

        // The UI now focuses on the feedback message only.
        // Hide detailed numeric/stat widgets (sliders and value labels) to keep comparison minimal.
        if (performanceText != null)
        {
            performanceText.text = ""; // remove numeric summary from the popup
        }

        // Disable slider visuals and numeric labels if they exist
        HideStatWidgets(confidenceBar, clarityBar, paceBar, toneBar, overallBar,
                        confidenceValue, clarityValue, paceValue, toneValue);
    }

    /// <summary>
    /// Hide the slider widgets and their numeric labels so the comparison boxes only show message text.
    /// </summary>
    private void HideStatWidgets(Slider confidence, Slider clarity, Slider pace, Slider tone, Slider overall,
                                 TextMeshProUGUI confidenceLabel, TextMeshProUGUI clarityLabel,
                                 TextMeshProUGUI paceLabel, TextMeshProUGUI toneLabel)
    {
        // Sliders may be missing in some layouts; guard each.
        if (confidence != null) confidence.gameObject.SetActive(false);
        if (clarity != null) clarity.gameObject.SetActive(false);
        if (pace != null) pace.gameObject.SetActive(false);
        if (tone != null) tone.gameObject.SetActive(false);
        if (overall != null) overall.gameObject.SetActive(false);

        if (confidenceLabel != null) confidenceLabel.gameObject.SetActive(false);
        if (clarityLabel != null) clarityLabel.gameObject.SetActive(false);
        if (paceLabel != null) paceLabel.gameObject.SetActive(false);
        if (toneLabel != null) toneLabel.gameObject.SetActive(false);
    }

    private void UpdateSlider(Slider slider, float value, TextMeshProUGUI valueLabel = null)
    {
        if (slider == null) return;

        // Ensure slider is non-interactable (read-only)
        slider.interactable = false;
        slider.value = value;
        
        // Update numerical value label
        if (valueLabel != null)
        {
            valueLabel.text = $"{(value * 100):F0}%";
        }
        
        // Color code based on value
        var fillImage = slider.fillRect?.GetComponent<Image>();
        if (fillImage != null)
        {
            if (value >= 0.7f)
                fillImage.color = excellentColor;
            else if (value >= 0.5f)
                fillImage.color = goodColor;
            else
                fillImage.color = needsImprovementColor;
        }
    }

    private void OnChoiceMade(ModelType chosenModel)
    {
        float responseTime = Time.time - comparisonStartTime;

        // Create choice record
        var choice = new FeedbackChoice
        {
            chosenModel = chosenModel,
            dqnFeedback = currentDQNFeedback,
            ppoFeedback = currentPPOFeedback,
            responseTime = responseTime
        };

        // Visual feedback - highlight chosen panel
        StartCoroutine(HighlightChoice(chosenModel));

        // Invoke event
        OnFeedbackChosen?.Invoke(choice);

        Debug.Log($"Player chose {chosenModel} feedback in {responseTime:F2}s");
    }

    private IEnumerator HighlightChoice(ModelType chosen)
    {
        // Disable buttons
        if (chooseDQNButton != null) chooseDQNButton.interactable = false;
        if (choosePPOButton != null) choosePPOButton.interactable = false;

        // Highlight chosen panel
        GameObject chosenPanel = (chosen == ModelType.DQN) ? dqnPanel : ppoPanel;
        var chosenImage = chosenPanel?.GetComponent<Image>();
        if (chosenImage != null)
        {
            chosenImage.color = selectedColor;
        }

        // Wait a moment
        yield return new WaitForSeconds(1.5f);

        // Fade out
        yield return StartCoroutine(FadeOut());

        // Clear the overall question score text
        if (overallQuestionScoreText != null)
            overallQuestionScoreText.text = "";

        // Hide panel
        if (comparisonPanel != null)
            comparisonPanel.SetActive(false);

        isDisplaying = false;
        Debug.Log("🎯 FeedbackComparisonUI: isDisplaying = FALSE (after fade)");
    }

    private IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOut()
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeInDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Get color based on score performance
    /// </summary>
    private Color GetScoreColor(float score)
    {
        if (score >= 0.7f)
            return excellentColor;
        else if (score >= 0.5f)
            return goodColor;
        else
            return needsImprovementColor;
    }

    /// <summary>
    /// Manually close the comparison panel
    /// </summary>
    public void HideComparison()
    {
        if (isDisplaying)
        {
            StopAllCoroutines();
            
            // Clear the overall question score text
            if (overallQuestionScoreText != null)
                overallQuestionScoreText.text = "";
            
            if (comparisonPanel != null)
                comparisonPanel.SetActive(false);
            isDisplaying = false;
            Debug.Log("🎯 FeedbackComparisonUI: isDisplaying = FALSE (manual hide)");
        }
    }
}

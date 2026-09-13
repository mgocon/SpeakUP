using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Displays a comprehensive score breakdown and summary at the end of the game.
/// Shows all individual question scores and overall statistics.
/// </summary>
public class GameSummaryScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject summaryPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI overallScoreText;
    [SerializeField] private TextMeshProUGUI totalQuestionsText;
    [SerializeField] private TextMeshProUGUI averageBreakdownText;
    [SerializeField] private Transform questionListContainer;
    [SerializeField] private GameObject questionScoreItemPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private Image graphImage; // Image element for the graph background

    [Header("Score Colors")]
    [SerializeField] private Color excellentColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color goodColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color needsImprovementColor = new Color(0.9f, 0.3f, 0.3f);

    private FeedbackManager feedbackManager;
    private FeedbackComparisonUI feedbackComparisonUI;
    private List<GameObject> questionItems = new List<GameObject>();
    private bool hasBeenInitialized = false; // Track if Start() has already run
    
    // Store original RectTransform values for resetting when switching modes
    private Vector2 originalOverallScoreAnchoredPos;
    private Vector2 originalTotalQuestionsAnchoredPos;
    private Vector2 originalGraphImageAnchoredPos;
    [Header("Graph")]
    [SerializeField] private RectTransform graphContainer;
    [SerializeField] private Color graphBarColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField] private float barSpacing = 6f;
    [SerializeField] private float minBarWidth = 24f;
    [SerializeField] private bool allowScrollExpansion = true;
    private readonly List<GameObject> graphItems = new List<GameObject>();
    [Header("Bar Limits")]
    [Range(0.5f, 1f)]
    [SerializeField] private float maxBarHeightPercent = 0.92f;
    [Header("Discrete Levels")]
    [Tooltip("If >0, bars will snap to the nearest step (e.g. 25 => 0,25,50,75,100). Set 0 to disable snapping.")]
    [SerializeField] private int levelStepPercent = 25;
    [SerializeField] private bool drawGridLines = true;
    [SerializeField] private Color gridLineColor = new Color(1f,1f,1f,0.12f);
    [SerializeField] private float gridLineThickness = 2f;
    [SerializeField] private bool showLevelLabels = true;

    private void Awake()
    {
        // Initialize even if GameObject is inactive
        if (closeButton != null)
            closeButton.onClick.AddListener(HideSummary);
    }

    private void Start()
    {
        // Only run initialization once to avoid deactivating summaryPanel if ShowSummary() was called first
        if (hasBeenInitialized)
        {
            Debug.Log("GameSummaryScreen.Start: Already initialized, skipping");
            return;
        }
        
        hasBeenInitialized = true;
        feedbackManager = FeedbackManager.Instance;
        
        // Only deactivate summaryPanel if it's active at game start
        // Don't deactivate if ShowSummary() already activated it
        if (summaryPanel != null && summaryPanel.activeSelf)
        {
            Debug.Log("GameSummaryScreen.Start: summaryPanel was active, deactivating it");
            summaryPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Show the summary screen with all scores
    /// </summary>
    public void ShowSummary()
    {
        Debug.Log("=== GameSummaryScreen.ShowSummary() called ===");
        
        // Mark as initialized to prevent Start() from deactivating the panel
        hasBeenInitialized = true;
        
        // CRITICAL FIX: Ensure this GameObject and summaryPanel are active FIRST
        if (summaryPanel != null)
        {
            Debug.Log($"Activating summaryPanel: {summaryPanel.name}");
            summaryPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("summaryPanel is NULL! Cannot show summary screen.");
            // Try to activate this GameObject as fallback
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                Debug.Log("Activated GameSummaryScreen GameObject as fallback");
            }
        }
        
        // Get FeedbackManager instance (handles persistent scene case)
        if (feedbackManager == null)
        {
            Debug.Log("FeedbackManager was null, trying to get Instance...");
            
            // First try the singleton Instance
            feedbackManager = FeedbackManager.Instance;
            
            // If Instance is null, try FindObjectOfType (works across all loaded scenes including persistent)
            if (feedbackManager == null)
            {
                Debug.Log("FeedbackManager.Instance is null, searching all scenes...");
                feedbackManager = FindObjectOfType<FeedbackManager>(true); // true includes inactive objects
            }
        }
        
        if (feedbackManager == null)
        {
            Debug.LogError("Could not find FeedbackManager! Make sure the Persistent scene is loaded.");
            
            // Show a helpful message to the player - panel is already active from above
            if (titleText != null)
                titleText.text = "<b>Error: FeedbackManager Not Found</b>";
            if (overallScoreText != null)
                overallScoreText.text = "Please start the game from the Main Menu";
            
            return;
        }
        else
        {
            Debug.Log("FeedbackManager found successfully!");
        }

        // Get FeedbackComparisonUI instance to check DQN-only mode
        if (feedbackComparisonUI == null)
        {
            feedbackComparisonUI = FindObjectOfType<FeedbackComparisonUI>(true);
        }

        // Store original positions if not already stored
        if (overallScoreText != null && originalOverallScoreAnchoredPos == Vector2.zero)
        {
            originalOverallScoreAnchoredPos = overallScoreText.GetComponent<RectTransform>().anchoredPosition;
        }
        if (totalQuestionsText != null && originalTotalQuestionsAnchoredPos == Vector2.zero)
        {
            originalTotalQuestionsAnchoredPos = totalQuestionsText.GetComponent<RectTransform>().anchoredPosition;
        }
        if (graphImage != null && originalGraphImageAnchoredPos == Vector2.zero)
        {
            originalGraphImageAnchoredPos = graphImage.GetComponent<RectTransform>().anchoredPosition;
        }

        // Check if DQN-only mode is enabled and adjust layout accordingly
        bool dqnOnlyMode = feedbackComparisonUI != null && feedbackComparisonUI.showOnlyDQN;
        if (dqnOnlyMode)
        {
            CenterElementsForDQNOnly();
        }
        else
        {
            RestoreElementPositions();
        }

        // Clear previous items
        ClearQuestionItems();

        // Get session data
        var breakdown = feedbackManager.GetSessionScoreBreakdown();
        Debug.Log($"Session breakdown: Questions={breakdown.questionCount}, Overall={breakdown.avgOverall:F2}");
        
        // Update title
        if (titleText != null)
        {
            titleText.text = "<b>Game Summary</b>";
            Debug.Log($"Title set: {titleText.text}");
        }
        else
        {
            Debug.LogWarning("titleText is NULL!");
        }

        // Update overall score
        if (overallScoreText != null)
        {
            if (breakdown.questionCount > 0)
            {
                string scoreText = GetColoredScore(breakdown.avgOverall, large: true);
                overallScoreText.text = $"<b>Overall Performance:</b>\n{scoreText}";
                Debug.Log($"Overall score set: {overallScoreText.text}");
            }
            else
            {
                overallScoreText.text = "<b>Overall Performance:</b>\n<color=#AAAAAA>No data</color>";
                Debug.Log("No data for overall score");
            }
        }
        else
        {
            Debug.LogWarning("overallScoreText is NULL!");
        }

        // Update total questions
        if (totalQuestionsText != null)
        {
            totalQuestionsText.text = $"<b>Questions Answered:</b> {breakdown.questionCount}";
            Debug.Log($"Total questions set: {totalQuestionsText.text}");
        }
        else
        {
            Debug.LogWarning("totalQuestionsText is NULL!");
        }

        // Update average breakdown
        if (averageBreakdownText != null && breakdown.questionCount > 0)
        {
            // Check if DQN-only mode is enabled - if so, hide breakdown
            if (dqnOnlyMode)
            {
                averageBreakdownText.text = "";
                Debug.Log("DQN-only mode: Hiding feedback breakdown details");
            }
            else
            {
                // Get DQN and PPO breakdowns
                var dqnBreakdown = feedbackManager.GetDQNScoreBreakdown();
                var ppoBreakdown = feedbackManager.GetPPOScoreBreakdown();
                
                Debug.Log($"Feedback B breakdown: Questions={dqnBreakdown.questionCount}, Overall={dqnBreakdown.avgOverall:F2}");
                Debug.Log($"Feedback A breakdown: Questions={ppoBreakdown.questionCount}, Overall={ppoBreakdown.avgOverall:F2}");

                string breakdownText = "<b>Average Performance Breakdown:</b>\n\n";
                
                // Overall average (from selected feedback)
                breakdownText += $"<b>Your Selected Feedback:</b>\n";
                breakdownText += $"  Overall: {GetColoredScore(breakdown.avgOverall)}\n";
                breakdownText += $"  Confidence: {GetColoredScore(breakdown.avgConfidence)}\n";
                breakdownText += $"  Clarity: {GetColoredScore(breakdown.avgClarity)}\n";
                breakdownText += $"  Pace: {GetColoredScore(breakdown.avgPace)}\n";
                breakdownText += $"  Tone: {GetColoredScore(breakdown.avgTone)}\n\n";

                // DQN average
                if (dqnBreakdown.questionCount > 0)
                {
                    breakdownText += $"<b>Feedback B Algorithm Scores:</b>\n";
                    breakdownText += $"  Overall: {GetColoredScore(dqnBreakdown.avgOverall)}\n";
                    breakdownText += $"  Confidence: {GetColoredScore(dqnBreakdown.avgConfidence)}\n";
                    breakdownText += $"  Clarity: {GetColoredScore(dqnBreakdown.avgClarity)}\n";
                    breakdownText += $"  Pace: {GetColoredScore(dqnBreakdown.avgPace)}\n";
                    breakdownText += $"  Tone: {GetColoredScore(dqnBreakdown.avgTone)}\n\n";
                }

                // PPO average
                if (ppoBreakdown.questionCount > 0)
                {
                    breakdownText += $"<b>Feedback A Algorithm Scores:</b>\n";
                    breakdownText += $"  Overall: {GetColoredScore(ppoBreakdown.avgOverall)}\n";
                    breakdownText += $"  Confidence: {GetColoredScore(ppoBreakdown.avgConfidence)}\n";
                    breakdownText += $"  Clarity: {GetColoredScore(ppoBreakdown.avgClarity)}\n";
                    breakdownText += $"  Pace: {GetColoredScore(ppoBreakdown.avgPace)}\n";
                    breakdownText += $"  Tone: {GetColoredScore(ppoBreakdown.avgTone)}";
                }

                averageBreakdownText.text = breakdownText;
                averageBreakdownText.alignment = TextAlignmentOptions.Center;
                Debug.Log($"Breakdown text set ({breakdownText.Length} chars):\n{breakdownText}");
            }
        }
        else if (averageBreakdownText != null)
        {
            averageBreakdownText.text = "";
            Debug.Log("Breakdown text cleared (no data)");
        }
        else
        {
            Debug.LogWarning("averageBreakdownText is NULL!");
        }

        // TODO: Add individual question scores if FeedbackManager tracks them
        // For now, we show the summary stats

        // Panel is already active from the beginning of ShowSummary()
        // Just need to render the graph now that layout is ready
        Debug.Log("Summary panel was already activated at start of ShowSummary!");
        
        // Force a layout rebuild to ensure rect sizes are available
        if (summaryPanel != null)
        {
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(summaryPanel.GetComponent<RectTransform>());
        }
        
        // Render performance graph once the panel is active (rect sizes available)
        RenderPerformanceGraph();
    }

    /// <summary>
    /// Center elements for DQN-only mode
    /// </summary>
    private void CenterElementsForDQNOnly()
    {
        Debug.Log("CenterElementsForDQNOnly: Adjusting layout for DQN-only mode");
        
        // Center Overall Score Text
        if (overallScoreText != null)
        {
            RectTransform overallRt = overallScoreText.GetComponent<RectTransform>();
            if (overallRt != null)
            {
                overallRt.anchorMin = new Vector2(0.5f, 0.5f);
                overallRt.anchorMax = new Vector2(0.5f, 0.5f);
                overallRt.pivot = new Vector2(0.5f, 0.5f);
                overallRt.anchoredPosition = new Vector2(0, 65); // Keep the multiline score block above the question count
                Debug.Log("Centered Overall Score Text");
            }
        }
        
        // Center Total Questions Text
        if (totalQuestionsText != null)
        {
            RectTransform totalRt = totalQuestionsText.GetComponent<RectTransform>();
            if (totalRt != null)
            {
                totalRt.anchorMin = new Vector2(0.5f, 0.5f);
                totalRt.anchorMax = new Vector2(0.5f, 0.5f);
                totalRt.pivot = new Vector2(0.5f, 0.5f);
                totalRt.anchoredPosition = new Vector2(0, -55); // Leave room for the large percentage
                Debug.Log("Centered Total Questions Text");
            }
        }
        
        // Center Image (graph background)
        if (graphImage != null)
        {
            RectTransform imageRt = graphImage.GetComponent<RectTransform>();
            if (imageRt != null)
            {
                imageRt.anchorMin = new Vector2(0.5f, 0.5f);
                imageRt.anchorMax = new Vector2(0.5f, 0.5f);
                imageRt.pivot = new Vector2(0.5f, 0.5f);
                imageRt.anchoredPosition = new Vector2(0, -155); // Below the separated text blocks
                Debug.Log("Centered Image");
            }
        }
    }

    /// <summary>
    /// Restore elements to original positions
    /// </summary>
    private void RestoreElementPositions()
    {
        Debug.Log("RestoreElementPositions: Restoring original layout");
        
        // Restore Overall Score Text
        if (overallScoreText != null)
        {
            RectTransform overallRt = overallScoreText.GetComponent<RectTransform>();
            if (overallRt != null)
            {
                overallRt.anchorMin = Vector2.zero;
                overallRt.anchorMax = Vector2.zero;
                overallRt.pivot = new Vector2(0.5f, 0.5f);
                overallRt.anchoredPosition = originalOverallScoreAnchoredPos;
                Debug.Log($"Restored Overall Score Text to {originalOverallScoreAnchoredPos}");
            }
        }
        
        // Restore Total Questions Text
        if (totalQuestionsText != null)
        {
            RectTransform totalRt = totalQuestionsText.GetComponent<RectTransform>();
            if (totalRt != null)
            {
                totalRt.anchorMin = Vector2.zero;
                totalRt.anchorMax = Vector2.zero;
                totalRt.pivot = new Vector2(0.5f, 0.5f);
                totalRt.anchoredPosition = originalTotalQuestionsAnchoredPos;
                Debug.Log($"Restored Total Questions Text to {originalTotalQuestionsAnchoredPos}");
            }
        }
        
        // Restore Image
        if (graphImage != null)
        {
            RectTransform imageRt = graphImage.GetComponent<RectTransform>();
            if (imageRt != null)
            {
                imageRt.anchorMin = Vector2.zero;
                imageRt.anchorMax = Vector2.zero;
                imageRt.pivot = new Vector2(0.5f, 0.5f);
                imageRt.anchoredPosition = originalGraphImageAnchoredPos;
                Debug.Log($"Restored Image to {originalGraphImageAnchoredPos}");
            }
        }
    }

    /// <summary>
    /// Render a simple bar graph of per-question overall scores to show improvement over time.
    /// </summary>
    private void RenderPerformanceGraph()
    {
        // Clear previous
        ClearGraphItems();

        if (graphContainer == null)
            return;

        // Force UI canvases/layout to rebuild so rect sizes are valid at runtime
        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(graphContainer);

        if (feedbackManager == null)
            feedbackManager = FeedbackManager.Instance;

        if (feedbackManager == null)
            return;

        var history = feedbackManager.GetSessionOverallHistory();
        int count = history != null ? history.Count : 0;
        Debug.Log($"GameSummaryScreen: RenderPerformanceGraph - history count={count}");
        if (count == 0)
        {
            graphContainer.gameObject.SetActive(false);
            return;
        }

        graphContainer.gameObject.SetActive(true);

        // Re-check container size after rebuild
        float containerWidth = graphContainer.rect.width;
        float containerHeight = graphContainer.rect.height;
        Debug.Log($"Graph container size: {containerWidth}x{containerHeight}");

        float totalSpacing = barSpacing * (count + 1);
        float availableWidth = Mathf.Max(1f, containerWidth - totalSpacing);
        float barWidth = availableWidth / Mathf.Max(1, count);

        // Determine whether graphContainer is a ScrollRect content (safe to expand)
        var parentScroll = graphContainer.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        bool isScrollContent = parentScroll != null && parentScroll.content == graphContainer;

        // Draw grid lines for discrete levels (behind bars)
        List<GameObject> gridObjects = new List<GameObject>();
        if (drawGridLines && levelStepPercent > 0)
        {
            int step = Mathf.Max(1, levelStepPercent);
            for (int p = step; p <= 100; p += step)
            {
                float y = (p / 100f) * containerHeight * maxBarHeightPercent;
                // create thin horizontal line across container
                GameObject line = new GameObject($"grid_{p}", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                line.transform.SetParent(graphContainer, false);
                var lrt = line.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(1f, 0f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.anchoredPosition = new Vector2(0f, y);
                lrt.sizeDelta = new Vector2(0f, gridLineThickness);
                var limg = line.GetComponent<UnityEngine.UI.Image>();
                limg.color = gridLineColor;

                // optional label on left
                if (showLevelLabels)
                {
                    GameObject label = new GameObject($"gridlabel_{p}", typeof(RectTransform));
                    label.transform.SetParent(graphContainer, false);
                    var lbrt = label.GetComponent<RectTransform>();
                    lbrt.anchorMin = new Vector2(0f, 0f);
                    lbrt.anchorMax = new Vector2(0f, 0f);
                    lbrt.pivot = new Vector2(0f, 0.5f);
                    lbrt.anchoredPosition = new Vector2(2f, y - (gridLineThickness * 0.5f));
                    lbrt.sizeDelta = new Vector2(40f, 18f);
                    var t = label.AddComponent<TextMeshProUGUI>();
                    t.text = $"{p}%";
                    t.fontSize = 12;
                    t.alignment = TextAlignmentOptions.Left;
                    t.color = gridLineColor;
                    gridObjects.Add(label);
                }

                gridObjects.Add(line);
            }
        }

        // If bars would become too narrow, enforce a minimum width and optionally expand the container
        if (barWidth < minBarWidth)
        {
            float old = barWidth;
            float requiredWidth = totalSpacing + (minBarWidth * count);
            Debug.Log($"GameSummaryScreen: barWidth too small ({old:F2}) — requiredWidth={requiredWidth:F2}");
            if (allowScrollExpansion && isScrollContent)
            {
                // Adjust the container's size so a ScrollRect content can expand; this will make the bars readable
                Vector2 sd = graphContainer.sizeDelta;
                sd.x = requiredWidth;
                graphContainer.sizeDelta = sd;
                // Recompute available width using the expanded container
                containerWidth = graphContainer.rect.width;
                availableWidth = Mathf.Max(1f, containerWidth - totalSpacing);
                barWidth = availableWidth / Mathf.Max(1, count);
            }
            else
            {
                // Can't expand: fall back to available width per bar (may be < minBarWidth)
                barWidth = availableWidth / Mathf.Max(1, count);
            }
        }

        for (int i = 0; i < count; i++)
        {
            float value = Mathf.Clamp01(history[i]);
            // If level stepping is enabled, snap to nearest level
            if (levelStepPercent > 0)
            {
                float percent = value * 100f;
                float snapped = Mathf.Round(percent / levelStepPercent) * levelStepPercent;
                value = Mathf.Clamp01(snapped / 100f);
            }
            // Create bar
            GameObject bar = new GameObject($"bar_{i}", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bar.transform.SetParent(graphContainer, false);
            var rt = bar.GetComponent<RectTransform>();
            // Anchor and pivot at bottom-left so bars grow upwards from the bottom
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            float x = barSpacing + i * (barWidth + barSpacing);
            float h = value * containerHeight * maxBarHeightPercent;
            // Clamp height so bars stay within container
            h = Mathf.Clamp(h, 0f, containerHeight);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(barWidth, h);

            var img = bar.GetComponent<UnityEngine.UI.Image>();
            img.color = graphBarColor;

            // Add percent label above bar (bottom-centered pivot so it sits just above the bar)
            GameObject labelObj = new GameObject($"label_{i}", typeof(RectTransform));
            labelObj.transform.SetParent(graphContainer, false);
            var labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(0f, 0f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            float labelY = h + 6f;
            // Make sure label stays inside container vertically
            float labelHeight = 18f;
            if (labelY + labelHeight > containerHeight)
            {
                labelY = containerHeight - labelHeight - 2f;
            }
            labelRt.anchoredPosition = new Vector2(x + barWidth * 0.5f, labelY);
            labelRt.sizeDelta = new Vector2(barWidth, labelHeight);

            var tmp = labelObj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"{Mathf.RoundToInt(value * 100)}%";
            // Adaptive font size so labels remain readable when bars are narrow
            float adaptiveFont = Mathf.Clamp(barWidth * 0.25f + 8f, 8f, 14f);
            tmp.fontSize = adaptiveFont;
            tmp.alignment = TextAlignmentOptions.Top;
            tmp.color = Color.white;

            graphItems.Add(bar);
            graphItems.Add(labelObj);
        }

        // Ensure grid lines sit behind bars (move them to first siblings)
        if (drawGridLines && graphContainer != null)
        {
            // move any grid objects created earlier to bottom of sibling order
            for (int si = 0; si < graphContainer.childCount; si++)
            {
                var child = graphContainer.GetChild(si);
                if (child.name.StartsWith("grid_") || child.name.StartsWith("gridlabel_"))
                {
                    child.SetSiblingIndex(0);
                }
            }
        }

    }

    private void ClearGraphItems()
    {
        foreach (var g in graphItems)
        {
            if (g != null)
                Destroy(g);
        }
        graphItems.Clear();
    }

    /// <summary>
    /// Hide the summary screen
    /// </summary>
    public void HideSummary()
    {
        if (summaryPanel != null)
            summaryPanel.SetActive(false);
    }

    /// <summary>
    /// Create a question score item (for individual question breakdown)
    /// </summary>
    private void CreateQuestionScoreItem(int questionNumber, float score, InterviewPerformance performance)
    {
        if (questionScoreItemPrefab == null || questionListContainer == null)
            return;

        GameObject item = Instantiate(questionScoreItemPrefab, questionListContainer);
        questionItems.Add(item);

        // Find text components in the item
        TextMeshProUGUI[] texts = item.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 0)
        {
            // First text: Question number and overall score
            texts[0].text = $"<b>Question {questionNumber}:</b> {GetColoredScore(score)}";
            
            // Second text (if exists): Detailed breakdown
            if (texts.Length > 1)
            {
                texts[1].text = $"Confidence: {GetColoredScore(performance.confidence)} | " +
                              $"Clarity: {GetColoredScore(performance.clarity)} | " +
                              $"Pace: {GetColoredScore(performance.pace)} | " +
                              $"Tone: {GetColoredScore(performance.tone)}";
            }
        }
    }

    /// <summary>
    /// Clear all question score items
    /// </summary>
    private void ClearQuestionItems()
    {
        foreach (var item in questionItems)
        {
            if (item != null)
                Destroy(item);
        }
        questionItems.Clear();
    }

    /// <summary>
    /// Get colored score string
    /// </summary>
    private string GetColoredScore(float score, bool large = false)
    {
        int percentage = Mathf.RoundToInt(score * 100f);
        Color color = GetScoreColor(score);
        string hexColor = ColorUtility.ToHtmlStringRGB(color);
        
        if (large)
            return $"<size=150%><color=#{hexColor}><b>{percentage}%</b></color></size>";
        else
            return $"<color=#{hexColor}>{percentage}%</color>";
    }

    /// <summary>
    /// Get color based on score
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
    /// Toggle summary panel
    /// </summary>
    public void ToggleSummary()
    {
        if (summaryPanel != null)
        {
            if (summaryPanel.activeSelf)
                HideSummary();
            else
                ShowSummary();
        }
    }
}

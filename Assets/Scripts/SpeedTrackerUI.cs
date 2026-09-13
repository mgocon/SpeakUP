using UnityEngine;
using TMPro;

/// <summary>
/// Displays algorithm speed comparison for DQN vs PPO
/// </summary>
public class SpeedTrackerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI dqnSpeedText;
    [SerializeField] private TextMeshProUGUI ppoSpeedText;
    [SerializeField] private TextMeshProUGUI comparisonText;
    [SerializeField] private GameObject speedPanel;
    // If speedPanel is not assigned, we'll toggle the individual text objects instead

    [Header("Settings")]
    [SerializeField] private bool showOnStart = false;
    [SerializeField] private bool updateRealtime = true;
    [SerializeField] private float updateInterval = 0.5f;

    private FeedbackManager feedbackManager;
    private FeedbackComparisonUI feedbackComparisonUI;
    private bool lastDQNOnlyMode;
    private float lastUpdateTime;

    private void Start()
    {
        feedbackManager = FeedbackManager.Instance;
        feedbackComparisonUI = FindObjectOfType<FeedbackComparisonUI>(true);
        lastDQNOnlyMode = IsDQNOnlyMode();

        // Initialize visibility. If a speedPanel GameObject is assigned, use it.
        // Otherwise toggle the individual text objects so the UI can be placed anywhere.
        SetVisibility(!lastDQNOnlyMode && showOnStart);
    }

    private void Update()
    {
        bool dqnOnlyMode = IsDQNOnlyMode();
        if (dqnOnlyMode != lastDQNOnlyMode)
        {
            lastDQNOnlyMode = dqnOnlyMode;
            SetVisibility(!dqnOnlyMode && showOnStart);
        }

        if (dqnOnlyMode)
        {
            SetVisibility(false);
            return;
        }

        if (!updateRealtime || feedbackManager == null)
            return;

        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateSpeedDisplay();
            lastUpdateTime = Time.time;
        }
    }

    private bool IsDQNOnlyMode()
    {
        if (feedbackComparisonUI == null)
            feedbackComparisonUI = FindObjectOfType<FeedbackComparisonUI>(true);

        return feedbackComparisonUI != null && feedbackComparisonUI.showOnlyDQN;
    }

    public void UpdateSpeedDisplay()
    {
        if (feedbackManager == null)
            return;

        var stats = feedbackManager.GetSpeedStats();
        float dqnAvg = stats.dqnAvg;
        float ppoAvg = stats.ppoAvg;
        float dqnLast = stats.dqnLast;
        float ppoLast = stats.ppoLast;
        int dqnCount = stats.dqnCount;
        int ppoCount = stats.ppoCount;

        // Update DQN speed
        if (dqnSpeedText != null)
        {
            dqnSpeedText.text = $"<b>Feedback B Speed</b>\n" +
                               $"Last: <color=#FFD700>{dqnLast:F2}ms</color>\n" +
                               $"Avg: {dqnAvg:F2}ms\n" +
                               $"Runs: {dqnCount}";
        }

        // Update PPO speed
        if (ppoSpeedText != null)
        {
            ppoSpeedText.text = $"<b>Feedback A Speed</b>\n" +
                               $"Last: <color=#FFD700>{ppoLast:F2}ms</color>\n" +
                               $"Avg: {ppoAvg:F2}ms\n" +
                               $"Runs: {ppoCount}";
        }

        // Update comparison
        if (comparisonText != null && dqnCount > 0 && ppoCount > 0)
        {
            string faster = dqnAvg < ppoAvg ? "Feedback B" : "Feedback A";
            float difference = Mathf.Abs(dqnAvg - ppoAvg);
            float percentDiff = (difference / Mathf.Max(dqnAvg, ppoAvg)) * 100f;

            comparisonText.text = $"<b>{faster}</b> is <color=#00FF00>{percentDiff:F1}%</color> faster";
        }
    }

    public void TogglePanel()
    {
        if (speedPanel != null)
        {
            bool isActive = speedPanel.activeSelf;
            speedPanel.SetActive(!isActive);
            return;
        }

        // No speedPanel assigned: toggle individual text objects
        bool anyActive = false;
        if (dqnSpeedText != null && dqnSpeedText.gameObject.activeSelf) anyActive = true;
        else if (ppoSpeedText != null && ppoSpeedText.gameObject.activeSelf) anyActive = true;
        else if (comparisonText != null && comparisonText.gameObject.activeSelf) anyActive = true;

        SetVisibility(!anyActive);
    }

    /// <summary>
    /// Set visibility for the speed UI. If a speedPanel is assigned, it will be used.
    /// Otherwise the individual text objects will be shown/hidden.
    /// </summary>
    public void SetVisibility(bool visible)
    {
        if (speedPanel != null)
        {
            speedPanel.SetActive(visible);
            return;
        }

        if (dqnSpeedText != null)
            dqnSpeedText.gameObject.SetActive(visible);
        if (ppoSpeedText != null)
            ppoSpeedText.gameObject.SetActive(visible);
        if (comparisonText != null)
            comparisonText.gameObject.SetActive(visible);
    }

    public void Show() => SetVisibility(true);
    public void Hide() => SetVisibility(false);
}

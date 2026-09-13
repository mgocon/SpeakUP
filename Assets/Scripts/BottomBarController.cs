using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BottomBarController : MonoBehaviour
{
    public TextMeshProUGUI barText;
    public TextMeshProUGUI personNameText;

    [Header("Speak Image Slide")]
    public RectTransform speakImage;
    public float slideDistance = 150f;
    public float slideDuration = 0.25f;

    [Header("Voice Recognition")]
    public VoskSpeechToText voskSpeechToText;

    [Header("AI Feedback Integration")]
    public FeedbackManager feedbackManager;
    public FeedbackComparisonUI feedbackComparisonUI;  // NEW: Show both DQN and PPO
    public SpeedTrackerUI speedTrackerUI;
    public GameSummaryScreen gameSummaryScreen; // NEW: End-of-game summary
    public bool autoGenerateFeedback = true;
    private float responseStartTime;
    private string currentTranscription = "";

    private bool isSpeakImageVisible = false;
    private Coroutine slideCoroutine;
    [Header("Speak Button")]
    public Button speakButton;
    private bool isRecording = false;
    [Header("Done Button")]
    public Button doneButton;
    
    [Header("Popup Boxes")]
    public RectTransform leftBox;
    public RectTransform rightBox;
    public RectTransform topBox; // slides from above when showing (third option)
    public float boxesSlideDistance = 300f;
    public float boxesSlideDuration = 0.25f;

    [Header("End Popup")]
    public RectTransform endBox;
    public Button endBoxButton;
    public float endBoxDistance = 200f;
    public float endBoxDuration = 0.25f;
    private Coroutine endBoxCoroutine;
    private bool endPopupActive = false;
    [Tooltip("Scene name to return to when player presses the end-popup button. GameManager will be asked to load this by name.")]
    public string mainMenuSceneName = "MAIN_MENU";

    private bool areBoxesVisible = false;
    private Coroutine leftBoxCoroutine;
    private Coroutine rightBoxCoroutine;
    private Coroutine topBoxCoroutine;
    private Vector2 rightBoxOriginalAnchoredPosition;
    private Vector2 rightBoxOriginalAnchorMin;
    private Vector2 rightBoxOriginalAnchorMax;
    private Vector2 rightBoxOriginalPivot;
    private bool rightBoxLayoutCaptured;
    private bool rightBoxCenteredForDQNOnly;
    private int sentenceIndex = -1;
    private StoryScene currentScene;
    private List<int> playbackOrder;
    private State state = State.COMPLETED;

    private enum State { PLAYING, COMPLETED }

    // Store last generated feedbacks for comparison so we can record the chosen one later
    private FeedbackMessage lastDQNFeedback;
    private FeedbackMessage lastPPOFeedback;
    public void PlayScene(StoryScene scene)
    {
        currentScene = scene;
        sentenceIndex = -1;

        int count = currentScene.sentences != null ? currentScene.sentences.Count : 0;
        int targetCount = (currentScene.sentencesToUse <= 0) ? count : Mathf.Clamp(currentScene.sentencesToUse, 1, count);

        List<int> indices = new List<int>(count);
        for (int i = 0; i < count; i++) indices.Add(i);

        if (currentScene != null && currentScene.randomizeSentences && count > 1)
        {
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = indices[i];
                indices[i] = indices[j];
                indices[j] = tmp;
            }
            playbackOrder = indices.GetRange(0, Mathf.Min(targetCount, indices.Count));
        }
        else
        {
            playbackOrder = indices.GetRange(0, Mathf.Min(targetCount, indices.Count));
        }

        PlayNextSentence();
    }

    public void PlayNextSentence()
    {
        sentenceIndex++;
        if (playbackOrder == null || sentenceIndex < 0 || sentenceIndex >= playbackOrder.Count) return;

        int idx = playbackOrder[sentenceIndex];
        string sentenceText = currentScene.sentences[idx].text;

        StartCoroutine(TypeText(sentenceText));
        personNameText.text = currentScene.sentences[idx].speaker.speakerName;
        personNameText.color = currentScene.sentences[idx].speaker.textColor;

        // ✅ Log this sentence to CSV
        if (DataLogger.Instance != null)
        {
            DataLogger.Instance.LogSentence(sentenceText, idx);
        }
    }

    public bool IsCompleted() => state == State.COMPLETED;

    public bool IsLastSentence() => playbackOrder != null && (sentenceIndex + 1 == playbackOrder.Count);

    private IEnumerator TypeText(string text)
    {
        barText.text = "";
        state = State.PLAYING;
        int wordIndex = 0;

        while (state != State.COMPLETED)
        {
            barText.text += text[wordIndex];
            yield return new WaitForSeconds(0.05f);
            if (++wordIndex == text.Length)
            {
                state = State.COMPLETED;
                break;
            }
        }
    }

    public void OnSpeakButtonPressed()
    {
        if (speakImage == null) return;
        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
            slideCoroutine = null;
        }
        slideCoroutine = StartCoroutine(SlideSpeakImage(!isSpeakImageVisible));
        
        if (voskSpeechToText != null)
        {
            voskSpeechToText.ToggleRecording();
            isRecording = !isRecording;
            
            // Track when recording starts
            if (isRecording)
            {
                responseStartTime = Time.time;
                currentTranscription = "";
                Debug.Log("🎤 Started recording player response");
            }
        }

        // disable speak button after pressing once to avoid multiple toggles
        if (speakButton != null)
            speakButton.interactable = false;
    }

    private IEnumerator SlideSpeakImage(bool show)
    {
        isSpeakImageVisible = show;
        Vector2 start = speakImage.anchoredPosition;
        Vector2 end = start + (show ? Vector2.up * slideDistance : Vector2.down * slideDistance);
        float elapsed = 0f;
        if (show) speakImage.gameObject.SetActive(true);

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            speakImage.anchoredPosition = Vector2.Lerp(start, end, t);
            yield return null;
        }

        speakImage.anchoredPosition = end;
        slideCoroutine = null;
        if (!show) speakImage.gameObject.SetActive(false);
    }

    // Toggle both popup boxes (left from left, right from right)
    public void OnPopupBoxesPressed()
    {
        areBoxesVisible = !areBoxesVisible;
        bool dqnOnlyMode = feedbackComparisonUI != null && feedbackComparisonUI.showOnlyDQN;

        SetDQNOnlyBoxLayout(dqnOnlyMode);

        // Left box: slides horizontally to the right when showing (hidden in DQN-only mode)
        if (leftBox != null)
        {
            if (!dqnOnlyMode)
            {
                if (leftBoxCoroutine != null)
                {
                    StopCoroutine(leftBoxCoroutine);
                    leftBoxCoroutine = null;
                }
                leftBoxCoroutine = StartCoroutine(SlideBox(leftBox, areBoxesVisible, Vector2.right, boxesSlideDistance, boxesSlideDuration, () => leftBoxCoroutine = null));
            }
        }

        // Right box: slides horizontally to the left when showing
        if (rightBox != null)
        {
            if (rightBoxCoroutine != null)
            {
                StopCoroutine(rightBoxCoroutine);
                rightBoxCoroutine = null;
            }

            if (dqnOnlyMode && areBoxesVisible)
            {
                rightBox.gameObject.SetActive(true);
                rightBox.anchoredPosition = new Vector2(0f, rightBox.anchoredPosition.y);
            }
            else
            {
                rightBoxCoroutine = StartCoroutine(SlideBox(rightBox, areBoxesVisible, Vector2.left, boxesSlideDistance, boxesSlideDuration, () => rightBoxCoroutine = null));
            }
        }

        // Also show/hide the top box (third choice) if present (hidden in DQN-only mode)
        if (topBox != null)
        {
            if (!dqnOnlyMode)
            {
                if (topBoxCoroutine != null)
                {
                    StopCoroutine(topBoxCoroutine);
                    topBoxCoroutine = null;
                }
                topBoxCoroutine = StartCoroutine(SlideBox(topBox, areBoxesVisible, Vector2.down, boxesSlideDistance, boxesSlideDuration, () => topBoxCoroutine = null));
            }
        }
    }

    // New Done button: toggles popup boxes and ensures Vosk recording is stopped
    public void OnDoneButtonPressed()
    {
        // If Vosk dialog text is empty, do not allow Done to proceed
        var voskDialog = FindObjectOfType<VoskDialogText>();
        
        // --- THIS IS THE FIX ---
        // Was: if (voskDialog != null && (voskDialog.dialogueBox == null || string.IsNullOrWhiteSpace(vosKDialog.dialogueBox.text)))
        if (voskDialog != null && (voskDialog.dialogueBox == null || string.IsNullOrWhiteSpace(voskDialog.dialogueBox.text)))
        {
            Debug.LogWarning("Done is disabled until there is recognized speech in the dialog.");
            return;
        }

        // If Vosk is recording, stop it
        if (isRecording && voskSpeechToText != null)
        {
            voskSpeechToText.ToggleRecording();
            isRecording = false;
        }

        // NEW: Generate and show BOTH feedbacks immediately when Done is pressed
        GenerateFeedbackComparison();

        // Determine if we're in DQN-only mode
        bool dqnOnlyMode = feedbackComparisonUI != null && feedbackComparisonUI.showOnlyDQN;
        SetDQNOnlyBoxLayout(dqnOnlyMode);

        // Show the PPO/DQN choice boxes (these now act as "Choose PPO" / "Choose DQN")
        areBoxesVisible = !areBoxesVisible;

    Debug.Log($"OnDoneButtonPressed: areBoxesVisible={areBoxesVisible}, dqnOnlyMode={dqnOnlyMode}, leftBox={(leftBox!=null)}, rightBox={(rightBox!=null)}, topBox={(topBox!=null)}");

    // Left box (PPO) - hide if in DQN-only mode
    if (leftBox != null)
        {
            if (!dqnOnlyMode)
            {
                if (leftBoxCoroutine != null)
                {
                    StopCoroutine(leftBoxCoroutine);
                    leftBoxCoroutine = null;
                }
                leftBoxCoroutine = StartCoroutine(SlideBox(leftBox, areBoxesVisible, Vector2.right, boxesSlideDistance, boxesSlideDuration, () => leftBoxCoroutine = null));
            }
            else
            {
                // In DQN-only mode, ensure left box stays hidden
                leftBox.gameObject.SetActive(false);
            }
        }

        // Right box (DQN) - always show/hide based on areBoxesVisible
        if (rightBox != null)
        {
            if (rightBoxCoroutine != null)
            {
                StopCoroutine(rightBoxCoroutine);
                rightBoxCoroutine = null;
            }

            if (dqnOnlyMode && areBoxesVisible)
            {
                rightBox.gameObject.SetActive(true);
                rightBox.anchoredPosition = new Vector2(0f, rightBox.anchoredPosition.y);
            }
            else
            {
                rightBoxCoroutine = StartCoroutine(SlideBox(rightBox, areBoxesVisible, Vector2.left, boxesSlideDistance, boxesSlideDuration, () => rightBoxCoroutine = null));
            }
        }

        // Top box (Neither) - hide if in DQN-only mode
        if (topBox != null)
        {
            if (!dqnOnlyMode)
            {
                Debug.Log($"OnDoneButtonPressed: preparing topBox (activeBefore={topBox.gameObject.activeSelf})");
                // If we're showing boxes and the topBox (or its parent) is inactive, try to activate the GameObject so the slide coroutine can manipulate it.
                if (areBoxesVisible && !topBox.gameObject.activeSelf)
                {
                    topBox.gameObject.SetActive(true);
                    Debug.Log("OnDoneButtonPressed: topBox set active before starting slide");
                }

                if (topBoxCoroutine != null)
                {
                    StopCoroutine(topBoxCoroutine);
                    topBoxCoroutine = null;
                }
                Debug.Log("OnDoneButtonPressed: starting topBox slide coroutine");
                topBoxCoroutine = StartCoroutine(SlideBox(topBox, areBoxesVisible, Vector2.down, boxesSlideDistance, boxesSlideDuration, () => topBoxCoroutine = null));
            }
            else
            {
                // In DQN-only mode, ensure top box stays hidden
                topBox.gameObject.SetActive(false);
            }
        }

        // disable Done button to avoid multiple presses until next question
        if (doneButton != null)
            doneButton.interactable = false;
    }

    // Show the end-of-level popup (call when player finished all questions and there is no next scene)
    public void ShowEndPopup()
    {
        Debug.Log("=== ShowEndPopup() called ===");
        // Mark popup active so external input (spacebar) can be ignored while visible
        endPopupActive = true;
        
        // --- NEW: Log summary data BEFORE showing the screen ---
        DataLogger.Instance?.LogLevelSummary();
        // --- End of new code ---

        // Show the game summary screen first
        if (gameSummaryScreen != null)
        {
            Debug.Log($"GameSummaryScreen found (active={gameSummaryScreen.gameObject.activeSelf}), calling ShowSummary()");
            
            // Ensure the GameSummaryScreen GameObject is active
            if (!gameSummaryScreen.gameObject.activeSelf)
            {
                Debug.Log("Activating GameSummaryScreen GameObject");
                gameSummaryScreen.gameObject.SetActive(true);
            }
            
            gameSummaryScreen.ShowSummary();
        }
        else
        {
            Debug.LogError("GameSummaryScreen is NULL in BottomBarController! Please assign it in the Inspector.");
        }
        
        if (endBox == null)
        {
            Debug.LogWarning("endBox is NULL in BottomBarController!");
            return;
        }

        if (endBoxCoroutine != null)
        {
            StopCoroutine(endBoxCoroutine);
            endBoxCoroutine = null;
        }

        // Use SlideBox with upward movement for the end box
        Debug.Log("Starting endBox slide animation");
        endBoxCoroutine = StartCoroutine(SlideBox(endBox, true, Vector2.up, endBoxDistance, endBoxDuration, () => endBoxCoroutine = null));

        // Ensure the end button is interactable
        if (endBoxButton != null)
        {
            endBoxButton.interactable = true;
            Debug.Log("endBoxButton set to interactable");
        }
        else
        {
            Debug.LogWarning("endBoxButton is NULL!");
        }
    }

    public void HideEndPopup()
    {
        if (endBox == null) return;

        if (endBoxCoroutine != null)
        {
            StopCoroutine(endBoxCoroutine);
            endBoxCoroutine = null;
        }

        // Mark popup inactive immediately so input can resume while it animates away
        endPopupActive = false;
        endBoxCoroutine = StartCoroutine(SlideBox(endBox, false, Vector2.up, endBoxDistance, endBoxDuration, () => endBoxCoroutine = null));
    }

    // Public query for other systems (e.g., GameController) to check whether end popup is visible
    public bool IsEndPopupActive()
    {
        return endPopupActive;
    }

    // Called by the end-popup button. Ask GameManager to load the main menu by name.
    public void OnEndPopupBackToMainMenuPressed()
    {
        // Reset FeedbackManager session data before returning to main menu
        if (FeedbackManager.Instance != null)
        {
            FeedbackManager.Instance.ResetSession();
            Debug.Log("Session reset when returning to main menu");
        }
        
        // Optionally hide popup immediately
        HideEndPopup();

        if (GameManager.instance != null)
        {
            GameManager.instance.LoadGameByName(mainMenuSceneName);
        }
        else
        {
            Debug.LogWarning("GameManager.instance not found. Cannot return to main menu.");
        }
    }

    private IEnumerator SlideBox(RectTransform box, bool show, Vector2 direction, float distance, float duration, System.Action onComplete)
    {
        if (box == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        Vector2 start = box.anchoredPosition;
        Vector2 end = start + (show ? direction * distance : -direction * distance);
        float elapsed = 0f;

        if (show) box.gameObject.SetActive(true);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            box.anchoredPosition = Vector2.Lerp(start, end, t);
            yield return null;
        }

        box.anchoredPosition = end;

        if (!show) box.gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    private void SetDQNOnlyBoxLayout(bool dqnOnlyMode)
    {
        if (rightBox == null)
            return;

        if (!rightBoxLayoutCaptured)
        {
            rightBoxOriginalAnchoredPosition = rightBox.anchoredPosition;
            rightBoxOriginalAnchorMin = rightBox.anchorMin;
            rightBoxOriginalAnchorMax = rightBox.anchorMax;
            rightBoxOriginalPivot = rightBox.pivot;
            rightBoxLayoutCaptured = true;
        }

        if (dqnOnlyMode && !rightBoxCenteredForDQNOnly)
        {
            rightBox.anchorMin = new Vector2(0.5f, rightBox.anchorMin.y);
            rightBox.anchorMax = new Vector2(0.5f, rightBox.anchorMax.y);
            rightBox.anchoredPosition = new Vector2(0f, rightBox.anchoredPosition.y);
            rightBoxCenteredForDQNOnly = true;
        }
        else if (!dqnOnlyMode && rightBoxCenteredForDQNOnly)
        {
            rightBox.anchorMin = rightBoxOriginalAnchorMin;
            rightBox.anchorMax = rightBoxOriginalAnchorMax;
            rightBox.pivot = rightBoxOriginalPivot;
            rightBox.anchoredPosition = rightBoxOriginalAnchoredPosition;
            rightBoxCenteredForDQNOnly = false;
        }
    }

    private void Awake()
    {
        // Note: We don't use OnFeedbackChosen event anymore
        // Player directly clicks PPO/DQN buttons to choose

        if (rightBox != null)
        {
            rightBoxOriginalAnchoredPosition = rightBox.anchoredPosition;
            rightBoxOriginalAnchorMin = rightBox.anchorMin;
            rightBoxOriginalAnchorMax = rightBox.anchorMax;
            rightBoxOriginalPivot = rightBox.pivot;
            rightBoxLayoutCaptured = true;
        }
        
        if (voskSpeechToText == null)
        {
            var voskManager = GameObject.Find("VoskManager");
            if (voskManager != null)
            {
                voskSpeechToText = voskManager.GetComponent<VoskSpeechToText>();
            }
            if (voskSpeechToText == null)
            {
                voskSpeechToText = FindObjectOfType<VoskSpeechToText>();
            }
            if (voskSpeechToText == null)
            {
                Debug.LogWarning("VoskSpeechToText not found in scene.");
            }
        }

        // Find feedback components if not assigned
        if (feedbackManager == null)
        {
            feedbackManager = FindObjectOfType<FeedbackManager>();
            if (feedbackManager == null)
            {
                Debug.LogWarning("⚠️ FeedbackManager not found. Feedback features will be disabled.");
            }
        }

        if (feedbackComparisonUI == null)
        {
            feedbackComparisonUI = FindObjectOfType<FeedbackComparisonUI>();
            if (feedbackComparisonUI == null)
            {
                Debug.LogWarning("⚠️ FeedbackComparisonUI not found. Feedback display will be disabled.");
            }
        }

        // Subscribe to Vosk events if available
        if (voskSpeechToText != null)
        {
            var voskDialogText = FindObjectOfType<VoskDialogText>();
            if (voskDialogText != null)
            {
                // We'll capture transcription from VoskDialogText
                Debug.Log("✅ Connected to Vosk for transcription capture");
            }
        }
    }

    // ✅ Optional UI helper methods for buttons
    // Tracks whether the player has selected an algorithm (PPO or DQN)
    [HideInInspector]
    public bool algorithmChosen = false;

    public void OnAlgorithmButtonClicked_DQN()
    {
        // Player chose DQN feedback (after seeing both)
        DataLogger.Instance?.LogAlgorithmChoice("DQN");
        algorithmChosen = true;

        Debug.Log("✅ Player chose DQN feedback");

        // Record the chosen model's performance into FeedbackManager once
        if (feedbackManager != null && lastDQNFeedback != null && lastDQNFeedback.currentPerformance != null)
        {
            feedbackManager.RecordDQNScore(lastDQNFeedback.currentPerformance);
            feedbackManager.RecordPerformanceScore(lastDQNFeedback.currentPerformance);
            // Record into history as the player's chosen performance (so summaries/graphs include it)
            feedbackManager.RecordPerformanceHistory(lastDQNFeedback.currentPerformance, lastDQNFeedback.action);
            Debug.Log("Recorded DQN and session performance for chosen feedback.");
        }

        // Hide comparison panel
        if (feedbackComparisonUI != null)
        {
            feedbackComparisonUI.HideComparison();
        }

        // Hide speak image and popup boxes after choice
        HideUIAfterAlgorithmChoice();

        // Advance to next question
        var gc = FindObjectOfType<GameController>();
        if (gc != null)
        {
            gc.Advance();
            Debug.Log("✅ Advancing to next question");
        }
        else
        {
            Debug.LogWarning("⚠️ GameController not found - cannot advance");
        }
    }

    public void OnAlgorithmButtonClicked_PPO()
    {
        // Player chose PPO feedback (after seeing both)
        DataLogger.Instance?.LogAlgorithmChoice("PPO");
        algorithmChosen = true;

        Debug.Log("✅ Player chose PPO feedback");

        // Record the chosen model's performance into FeedbackManager once
        if (feedbackManager != null && lastPPOFeedback != null && lastPPOFeedback.currentPerformance != null)
        {
            feedbackManager.RecordPPOScore(lastPPOFeedback.currentPerformance);
            feedbackManager.RecordPerformanceScore(lastPPOFeedback.currentPerformance);
            // Record into history as the player's chosen performance (so summaries/graphs include it)
            feedbackManager.RecordPerformanceHistory(lastPPOFeedback.currentPerformance, lastPPOFeedback.action);
            Debug.Log("Recorded PPO and session performance for chosen feedback.");
        }

        // Hide comparison panel
        if (feedbackComparisonUI != null)
        {
            feedbackComparisonUI.HideComparison();
        }

        // Hide speak image and popup boxes after choice
        HideUIAfterAlgorithmChoice();

        // Advance to next question
        var gc = FindObjectOfType<GameController>();
        if (gc != null)
        {
            gc.Advance();
            Debug.Log("✅ Advancing to next question");
        }
        else
        {
            Debug.LogWarning("⚠️ GameController not found - cannot advance");
        }
    }

    public void OnAlgorithmButtonClicked_Neither()
    {
        // Player chose neither feedback option
        DataLogger.Instance?.LogAlgorithmChoice("Neither");
        algorithmChosen = true;

        Debug.Log("ℹ️ Player chose 'Neither' (no algorithm preferred)");

        // Hide comparison panel
        if (feedbackComparisonUI != null)
        {
            feedbackComparisonUI.HideComparison();
        }

        // If both feedbacks exist, record an average performance for the session (no algorithm-specific tally)
        if (feedbackManager != null && lastDQNFeedback != null && lastPPOFeedback != null &&
            lastDQNFeedback.currentPerformance != null && lastPPOFeedback.currentPerformance != null)
        {
            InterviewPerformance avg = new InterviewPerformance();
            avg.confidence = (lastDQNFeedback.currentPerformance.confidence + lastPPOFeedback.currentPerformance.confidence) / 2f;
            avg.clarity = (lastDQNFeedback.currentPerformance.clarity + lastPPOFeedback.currentPerformance.clarity) / 2f;
            avg.pace = (lastDQNFeedback.currentPerformance.pace + lastPPOFeedback.currentPerformance.pace) / 2f;
            avg.tone = (lastDQNFeedback.currentPerformance.tone + lastPPOFeedback.currentPerformance.tone) / 2f;
            avg.overall = (lastDQNFeedback.currentPerformance.overall + lastPPOFeedback.currentPerformance.overall) / 2f;

            feedbackManager.RecordPerformanceScore(avg);
            // Record averaged performance into history as 'Neither' choice
            feedbackManager.RecordPerformanceHistory(avg, FeedbackAction.MaintainCurrentApproach);
            Debug.Log("Recorded averaged session performance for 'Neither' choice.");
        }

        // Hide speak image and popup boxes after choice
        HideUIAfterAlgorithmChoice();

        // Advance to next question
        var gc = FindObjectOfType<GameController>();
        if (gc != null)
        {
            gc.Advance();
            Debug.Log("✅ Advancing to next question");
        }
        else
        {
            Debug.LogWarning("⚠️ GameController not found - cannot advance");
        }
    }

    // Hide speak image and popup boxes using existing coroutines
    private void HideUIAfterAlgorithmChoice()
    {
        // Hide speak image if visible
        if (isSpeakImageVisible && speakImage != null)
        {
            if (slideCoroutine != null)
            {
                StopCoroutine(slideCoroutine);
                slideCoroutine = null;
            }
            slideCoroutine = StartCoroutine(SlideSpeakImage(false));
        }

        // Hide popup boxes if visible
        if (areBoxesVisible)
        {
            bool dqnOnlyMode = feedbackComparisonUI != null && feedbackComparisonUI.showOnlyDQN;
            
            // Hide all visible boxes
            if (leftBox != null && leftBox.gameObject.activeSelf)
            {
                if (leftBoxCoroutine != null)
                {
                    StopCoroutine(leftBoxCoroutine);
                    leftBoxCoroutine = null;
                }
                leftBoxCoroutine = StartCoroutine(SlideBox(leftBox, false, Vector2.right, boxesSlideDistance, boxesSlideDuration, () => leftBoxCoroutine = null));
            }

            if (rightBox != null && rightBox.gameObject.activeSelf)
            {
                if (rightBoxCoroutine != null)
                {
                    StopCoroutine(rightBoxCoroutine);
                    rightBoxCoroutine = null;
                }
                rightBoxCoroutine = StartCoroutine(SlideBox(rightBox, false, Vector2.left, boxesSlideDistance, boxesSlideDuration, () => rightBoxCoroutine = null));
            }

            if (topBox != null && topBox.gameObject.activeSelf && !dqnOnlyMode)
            {
                if (topBoxCoroutine != null)
                {
                    StopCoroutine(topBoxCoroutine);
                    topBoxCoroutine = null;
                }
                topBoxCoroutine = StartCoroutine(SlideBox(topBox, false, Vector2.down, boxesSlideDistance, boxesSlideDuration, () => topBoxCoroutine = null));
            }
            
            areBoxesVisible = false;
        }
    }

    // Allow external code (GameController) to re-enable the speak button when moving to next question
    public void SetSpeakButtonInteractable(bool enabled)
    {
        if (speakButton != null)
            speakButton.interactable = enabled;
    }

    // Allow external code (GameController) to re-enable the Done button when moving to next question
    public void SetDoneButtonInteractable(bool enabled)
    {
        if (doneButton != null)
            doneButton.interactable = enabled;
         }
    
    // Make sure this method is PUBLIC
    // Check if the current sentence is marked as a question requiring algorithm choice
    public bool IsCurrentSentenceAQuestion()
    {
        if (currentScene == null || currentScene.sentences == null) return false;
        if (playbackOrder == null || sentenceIndex < 0 || sentenceIndex >= playbackOrder.Count) return false;
        
        int idx = playbackOrder[sentenceIndex];
        if (idx < 0 || idx >= currentScene.sentences.Count) return false;
        
        return currentScene.sentences[idx].isQuestion;
    }

    /// <summary>
    /// Generate BOTH DQN and PPO feedback for player comparison
    /// </summary>
    private void GenerateFeedbackComparison()
    {
        if (feedbackManager == null || feedbackComparisonUI == null)
        {
            Debug.LogWarning("⚠️ FeedbackManager or FeedbackComparisonUI not available. Skipping feedback generation.");
            return;
        }

        // Get the transcribed text from VoskDialogText
        var voskDialogText = FindObjectOfType<VoskDialogText>();
        if (voskDialogText != null)
        {
            currentTranscription = GetTranscriptionFromVosk(voskDialogText);
        }

        // Calculate response duration
        float responseDuration = Time.time - responseStartTime;

    // Generate DQN feedback WITHOUT updating session totals or recording history
    feedbackManager.SetModelType(FeedbackManager.ModelType.DQN);
    lastDQNFeedback = feedbackManager.GenerateFeedback(currentTranscription, responseDuration, updateSessionScore: false, recordHistory: false);

    // Generate PPO feedback WITHOUT updating session totals or recording history
    feedbackManager.SetModelType(FeedbackManager.ModelType.PPO);
    lastPPOFeedback = feedbackManager.GenerateFeedback(currentTranscription, responseDuration, updateSessionScore: false, recordHistory: false);

        // --- NEW: Log feedback scores to data logger ---
        DataLogger.Instance?.LogFeedbackScores(lastDQNFeedback, lastPPOFeedback);
        // --- End of new code ---

        // Display BOTH for comparison
        if (lastDQNFeedback != null && lastPPOFeedback != null && feedbackComparisonUI != null)
        {
            feedbackComparisonUI.ShowComparison(lastDQNFeedback, lastPPOFeedback);
            Debug.Log($"📊 Showing feedback comparison - DQN: {lastDQNFeedback.action} vs PPO: {lastPPOFeedback.action}");
        }
    }

    // NOTE: Unused - player now directly clicks PPO/DQN buttons instead of using OnFeedbackChosen event
    /*
    private void OnPlayerChoseFeedback(FeedbackComparisonUI.FeedbackChoice choice)
    {
        DataLogger.Instance?.LogAlgorithmChoice(choice.chosenModel.ToString());
        Debug.Log($"✅ Player chose {choice.chosenModel} feedback");
        StartCoroutine(AdvanceAfterFeedbackChoice());
    }

    private IEnumerator AdvanceAfterFeedbackChoice()
    {
        yield return new WaitForSeconds(0.5f);
        var gc = FindObjectOfType<GameController>();
        if (gc != null) gc.Advance();
    }
    */

    /// <summary>
    /// Helper method to get transcription from VoskDialogText
    /// </summary>
    private string GetTranscriptionFromVosk(VoskDialogText voskDialogText)
    {
        // VoskDialogText has a public dialogueBox field
        try
        {
            if (voskDialogText.dialogueBox != null)
            {
                string text = voskDialogText.dialogueBox.text;
                if (!string.IsNullOrEmpty(text))
                {
                    Debug.Log($"📝 Captured transcription: {text}");
                    return text;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ Could not access Vosk transcription: {e.Message}");
        }

        // Fallback: return cached transcription or empty
        if (!string.IsNullOrEmpty(currentTranscription))
        {
            return currentTranscription;
        }

        Debug.LogWarning("⚠️ No transcription available. Using empty string.");
        return "";
    }
}
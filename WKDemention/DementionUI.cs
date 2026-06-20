using System;
using System.Collections;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Demention {

public class DementionUI : MonoBehaviour {
    public static DementionUI Instance { get; private set; }

    // gets called a lot, should probably be cached
    float episodeInterval => Settings.EpisodeInterval.Value;

    [Header("Transition Durations")]
    readonly float flashFadeInTime = 0.1f;
    readonly float flashFadeOutTime = 0.4f;
    readonly float freezeFrameFadeOutTime = 1.25f;

    GameObject uiInstance;
    RawImage screenshotImage;
    Image flashImage;
    TextMeshProUGUI timerText;

    readonly PlayerState[] locationHistory = new PlayerState[10];
    int historyCount = 0;
    int historyWriteIndex = 0;

    RenderTexture capturedFrame;
    float episodeTimer;
    bool isTransitioning = false;

    bool foundPlayer = false;
    bool hasCapturedAnySnapshot = false;

    CL_GameManager cachedGameManager;

    public struct PlayerState {
        public Pose pose;
    }

    public static void Initialize() {
        if (Instance != null) return;
        GameObject uiHost = new GameObject("DementionUIHost");
        DontDestroyOnLoad(uiHost);
        Instance = uiHost.AddComponent<DementionUI>();
        Instance.LoadAndBuildUI();
    }

    void LoadAndBuildUI() {
        string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string bundlePath = Path.Combine(modPath, "Assets", "demention");

        if (!File.Exists(bundlePath)) { CleanUpFailedInit(); return; }
        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);

        if (bundle == null) { CleanUpFailedInit(); return; }
        GameObject uiPrefab = null;
        string[] assetNames = bundle.GetAllAssetNames();

        if (assetNames.Length > 0) uiPrefab = bundle.LoadAsset<GameObject>(assetNames[0]);

        if (uiPrefab == null) { bundle.Unload(false); CleanUpFailedInit(); return; }

        uiInstance = Instantiate(uiPrefab);

        DontDestroyOnLoad(uiInstance);

        bundle.Unload(false);

        Canvas ourCanvas = uiInstance.GetComponent<Canvas>();
        if (ourCanvas != null) { ourCanvas.renderMode = RenderMode.ScreenSpaceOverlay; ourCanvas.worldCamera = null; ourCanvas.sortingOrder = 32767; }
        CanvasScaler scaler = uiInstance.GetComponent<CanvasScaler>();

        if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f; }

        screenshotImage = uiInstance.transform.Find("ScreenshotImage")?.GetComponent<RawImage>();
        flashImage = uiInstance.transform.Find("FlashImage")?.GetComponent<Image>();

        timerText = uiInstance.transform.Find("TimerText")?.GetComponent<TextMeshProUGUI>();
        timerText?.gameObject.SetActive(false);

        capturedFrame = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        if (screenshotImage != null) { screenshotImage.texture = capturedFrame; screenshotImage.uvRect = new Rect(0, 1, 1, -1); }
        screenshotImage?.gameObject.SetActive(false);
        flashImage?.gameObject.SetActive(false);
        episodeTimer = episodeInterval;
    }

    void Update() {
        if (cachedGameManager == null) {
            cachedGameManager = CL_GameManager.FindObjectOfType<CL_GameManager>();
        }

        if (cachedGameManager != null && CL_GameManager.gamemode.allowLeaderboardScoring) CL_GameManager.gamemode.allowLeaderboardScoring = false;

        if (ENT_Player.playerObject == null) {
            if (foundPlayer) {
                foundPlayer = false;
                timerText?.gameObject.SetActive(false);
            }
            return;
        }

        bool isGamePaused = (cachedGameManager != null && cachedGameManager.isPaused);

        if (cachedGameManager != null && CL_GameManager.IsLoading()) {
            episodeTimer = episodeInterval;
            hasCapturedAnySnapshot = true;
        }

        if ((foundPlayer && ENT_Player.playerObject.health <= 0) || isGamePaused) {
            timerText?.gameObject.SetActive(false);
            foundPlayer = false;
        }
        else if (!foundPlayer) {
            foundPlayer = true;
            timerText?.gameObject.SetActive(true);
        }

        if (isTransitioning) return;

        if (Input.GetKeyDown(KeyCode.H) && !hasCapturedAnySnapshot && episodeTimer > (episodeInterval * 0.25f)) { // TODO: add setting
            RecordManualPose();
            hasCapturedAnySnapshot = true;
        }

        HandleTimers();
    }

    void HandleTimers() {
        episodeTimer -= Time.deltaTime;
        if (timerText != null) {
            TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0, episodeTimer));
            timerText.text = string.Format("{0:0}:{1:00}:{2:00}", t.Minutes, t.Seconds, t.Milliseconds / 10);

            float progress = 1f - (episodeTimer / episodeInterval);
            timerText.color = Color.Lerp(Color.white, Color.red, progress);
        }

        if (!hasCapturedAnySnapshot) {
            float halfTime = episodeInterval * 0.5f;
            float endThreshold = 1.0f;

            if (episodeTimer <= halfTime && episodeTimer >= endThreshold) {
                RecordCurrentPose();
                hasCapturedAnySnapshot = true;
            }
            else if (episodeTimer <= 0.1f) {
                RecordCurrentPose();
                hasCapturedAnySnapshot = true;
            }
        }

        if (episodeTimer <= 0f) StartCoroutine(ExecuteDementionSequence());
    }

    void RecordManualPose() {
        locationHistory[historyWriteIndex] = new PlayerState {
            pose = new Pose(ENT_Player.playerObject.transform.position, ENT_Player.playerObject.transform.rotation)
        };

        historyWriteIndex = (historyWriteIndex + 1) % locationHistory.Length;
        if (historyCount < locationHistory.Length) historyCount++;

        hasCapturedAnySnapshot = true;

        StartCoroutine(TriggerFlashFeedback());
    }

    void RecordCurrentPose() {
        Transform pTransform = ENT_Player.playerObject.transform;

        locationHistory[historyWriteIndex] = new PlayerState {
            pose = new Pose(pTransform.position, pTransform.rotation)
        };

        historyWriteIndex = (historyWriteIndex + 1) % locationHistory.Length;
        if (historyCount < locationHistory.Length) historyCount++;

        StartCoroutine(TriggerFlashFeedback());
    }

    public void ForceTriggerEpisode() {
        if (!isTransitioning && ENT_Player.playerObject != null) StartCoroutine(ExecuteDementionSequence());
    }

    IEnumerator ExecuteDementionSequence() {
        isTransitioning = true;
        timerText?.gameObject.SetActive(false);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshotIntoRenderTexture(capturedFrame);
        if (screenshotImage != null) { screenshotImage.color = Color.white; screenshotImage.gameObject.SetActive(true); }
        flashImage?.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < flashFadeInTime) {
            elapsed += Time.deltaTime;
            if (flashImage != null) flashImage.color = new Color(1, 1, 1, Mathf.Lerp(0, 1, elapsed / flashFadeInTime));
            yield return null;
        }

        if (historyCount > 0) {
            int randomIndex = UnityEngine.Random.Range(0, historyCount);
            PlayerState state = locationHistory[randomIndex];

            ENT_Player.playerObject.transform.position = state.pose.position;
            ENT_Player.playerObject.transform.rotation = state.pose.rotation;
        }

        elapsed = 0f;
        while (elapsed < flashFadeOutTime) {
            elapsed += Time.deltaTime;
            if (flashImage != null) flashImage.color = new Color(1, 1, 1, Mathf.Lerp(1, 0, elapsed / flashFadeOutTime));
            yield return null;
        }
        flashImage?.gameObject.SetActive(false);
        elapsed = 0f;
        Color fullOpacity = Color.white;
        while (elapsed < freezeFrameFadeOutTime) {
            elapsed += Time.deltaTime;
            fullOpacity.a = Mathf.Lerp(1, 0, elapsed / freezeFrameFadeOutTime);
            if (screenshotImage != null) screenshotImage.color = fullOpacity;
            yield return null;
        }

        screenshotImage?.gameObject.SetActive(false);

        if (timerText != null) {
            TimeSpan t = TimeSpan.FromSeconds(episodeInterval);
            timerText.text = string.Format("{0:0}:{1:00}:{2:00}", t.Minutes, t.Seconds, t.Milliseconds / 10);

            timerText.color = Color.white;

            timerText.gameObject.SetActive(true);
            timerText.alpha = 0f;

            float fadeElapsed = 0f;
            float fadeDuration = 0.5f;
            while (fadeElapsed < fadeDuration) {
                fadeElapsed += Time.deltaTime;
                timerText.alpha = Mathf.Lerp(0f, 1f, fadeElapsed / fadeDuration);
                yield return null;
            }
            timerText.alpha = 1f;
        }

        historyCount = 0;
        historyWriteIndex = 0;
        episodeTimer = episodeInterval;
        hasCapturedAnySnapshot = false;
        isTransitioning = false;
    }

    IEnumerator TriggerFlashFeedback() {
        if (flashImage == null) yield break;

        flashImage.gameObject.SetActive(true);
        flashImage.color = new Color(1, 1, 1, 0.2f);

        yield return new WaitForSeconds(0.1f);

        float elapsed = 0f;
        while (elapsed < 0.2f) {
            elapsed += Time.deltaTime;
            flashImage.color = new Color(1, 1, 1, Mathf.Lerp(0.2f, 0f, elapsed / 0.2f));
            yield return null;
        }
        flashImage.gameObject.SetActive(false);
    }

    void CleanUpFailedInit() {
        if (uiInstance != null) Destroy(uiInstance);
        Destroy(gameObject);
    }

    void OnDestroy() {
        capturedFrame?.Release();
    }
}

}
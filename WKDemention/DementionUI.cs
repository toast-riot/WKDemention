using System;
using System.Collections;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DementionUI : MonoBehaviour
{
    public static DementionUI Instance { get; private set; }

    [Header("Dementia Timer Settings")]
    private float episodeInterval = 360f;

    [Header("Transition Durations")]
    private float flashFadeInTime = 0.1f;
    private float flashFadeOutTime = 0.4f;
    private float freezeFrameFadeOutTime = 1.25f;

    private GameObject uiInstance;
    private RawImage screenshotImage;
    private Image flashImage;
    private TextMeshProUGUI timerText;

    private PlayerState[] _locationHistory = new PlayerState[10];
    private int _historyCount = 0;
    private int _historyWriteIndex = 0;

    private RenderTexture _capturedFrame;
    private float _episodeTimer;
    private bool _isTransitioning = false;

    private bool foundPlayer = false;
    private bool _hasCapturedAnySnapshot = false;

    public struct PlayerState
    {
        public Pose pose;
    }

    public static void Initialize()
    {
        if (Instance != null) return;
        GameObject uiHost = new GameObject("Demention_UIHost");
        DontDestroyOnLoad(uiHost);
        Instance = uiHost.AddComponent<DementionUI>();
        Instance.LoadAndBuildUI();
    }

    private void LoadAndBuildUI()
    {
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

        _capturedFrame = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        if (screenshotImage != null) { screenshotImage.texture = _capturedFrame; screenshotImage.uvRect = new Rect(0, 1, 1, -1); }
        if (screenshotImage != null) screenshotImage.gameObject.SetActive(false);
        if (flashImage != null) flashImage.gameObject.SetActive(false);
        _episodeTimer = episodeInterval;
    }

    void Update()
    {
        if (ENT_Player.playerObject == null)
        {
            if (foundPlayer)
            {
                foundPlayer = false;
                timerText.gameObject.SetActive(false);
            }

            return;
        }

        if (ENT_Player.playerObject != null && !foundPlayer)
        {
            foundPlayer = true;
            timerText.gameObject.SetActive(true);
        }

        if (_isTransitioning) return;

        if (Input.GetKeyDown(KeyCode.H) && !_hasCapturedAnySnapshot && _episodeTimer > (episodeInterval * 0.25f))
        {
            RecordManualPose();
            _hasCapturedAnySnapshot = true;
        }

        HandleTimers();
    }

    private void HandleTimers()
    {
        _episodeTimer -= Time.deltaTime;
        if (timerText != null)
        {
            TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0, _episodeTimer));
            timerText.text = string.Format("{0:0}:{1:00}:{2:00}", t.Minutes, t.Seconds, t.Milliseconds / 10);

            float progress = 1f - (_episodeTimer / episodeInterval);
            timerText.color = Color.Lerp(Color.white, Color.red, progress);
        }

        if (!_hasCapturedAnySnapshot)
        {
            float halfTime = episodeInterval * 0.5f;
            float endThreshold = 1.0f;

            if (_episodeTimer <= halfTime && _episodeTimer >= endThreshold)
            {
                RecordCurrentPose();
                _hasCapturedAnySnapshot = true;
            }
            else if (_episodeTimer <= 0.1f)
            {
                RecordCurrentPose();
                _hasCapturedAnySnapshot = true;
            }
        }

        if (_episodeTimer <= 0f) StartCoroutine(ExecuteDementionSequence());
    }

    private void RecordManualPose()
    {
        Rigidbody rb = ENT_Player.playerObject.GetComponent<Rigidbody>();
        _locationHistory[_historyWriteIndex] = new PlayerState
        {
            pose = new Pose(ENT_Player.playerObject.transform.position, ENT_Player.playerObject.transform.rotation)
        };

        _historyWriteIndex = (_historyWriteIndex + 1) % _locationHistory.Length;
        if (_historyCount < _locationHistory.Length) _historyCount++;

        _hasCapturedAnySnapshot = true;

        StartCoroutine(TriggerFlashFeedback());
    }

    private void RecordCurrentPose()
    {
        Transform pTransform = ENT_Player.playerObject.transform;
        Rigidbody rb = ENT_Player.playerObject.GetComponent<Rigidbody>();

        _locationHistory[_historyWriteIndex] = new PlayerState
        {
            pose = new Pose(pTransform.position, pTransform.rotation)
        };

        _historyWriteIndex = (_historyWriteIndex + 1) % _locationHistory.Length;
        if (_historyCount < _locationHistory.Length) _historyCount++;

        StartCoroutine(TriggerFlashFeedback());
    }

    public void ForceTriggerEpisode()
    {
        if (!_isTransitioning && ENT_Player.playerObject != null) StartCoroutine(ExecuteDementionSequence());
    }

    private IEnumerator ExecuteDementionSequence()
    {
        _isTransitioning = true;
        if (timerText != null) timerText.gameObject.SetActive(false);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshotIntoRenderTexture(_capturedFrame);
        if (screenshotImage != null) { screenshotImage.color = Color.white; screenshotImage.gameObject.SetActive(true); }
        if (flashImage != null) flashImage.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < flashFadeInTime)
        {
            elapsed += Time.deltaTime;
            if (flashImage != null) flashImage.color = new Color(1, 1, 1, Mathf.Lerp(0, 1, elapsed / flashFadeInTime));
            yield return null;
        }

        if (_historyCount > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, _historyCount);
            PlayerState state = _locationHistory[randomIndex];

            ENT_Player.playerObject.transform.position = state.pose.position;
            ENT_Player.playerObject.transform.rotation = state.pose.rotation;
        }

        elapsed = 0f;
        while (elapsed < flashFadeOutTime)
        {
            elapsed += Time.deltaTime;
            if (flashImage != null) flashImage.color = new Color(1, 1, 1, Mathf.Lerp(1, 0, elapsed / flashFadeOutTime));
            yield return null;
        }
        if (flashImage != null) flashImage.gameObject.SetActive(false);
        elapsed = 0f;
        Color fullOpacity = Color.white;
        while (elapsed < freezeFrameFadeOutTime)
        {
            elapsed += Time.deltaTime;
            fullOpacity.a = Mathf.Lerp(1, 0, elapsed / freezeFrameFadeOutTime);
            if (screenshotImage != null) screenshotImage.color = fullOpacity;
            yield return null;
        }

        if (screenshotImage != null) screenshotImage.gameObject.SetActive(false);

        if (timerText != null)
        {
            TimeSpan t = TimeSpan.FromSeconds(episodeInterval);
            timerText.text = string.Format("{0:0}:{1:00}:{2:00}", t.Minutes, t.Seconds, t.Milliseconds / 10);

            timerText.color = Color.white;

            timerText.gameObject.SetActive(true);
            timerText.alpha = 0f;

            float fadeElapsed = 0f;
            float fadeDuration = 0.5f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                timerText.alpha = Mathf.Lerp(0f, 1f, fadeElapsed / fadeDuration);
                yield return null;
            }
            timerText.alpha = 1f;
        }

        _historyCount = 0;
        _historyWriteIndex = 0;
        _episodeTimer = episodeInterval;
        _hasCapturedAnySnapshot = false;
        _isTransitioning = false;
    }

    private IEnumerator TriggerFlashFeedback()
    {
        if (flashImage == null) yield break;

        flashImage.gameObject.SetActive(true);
        flashImage.color = new Color(1, 1, 1, 0.2f);

        yield return new WaitForSeconds(0.1f);

        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.deltaTime;
            flashImage.color = new Color(1, 1, 1, Mathf.Lerp(0.2f, 0f, elapsed / 0.2f));
            yield return null;
        }
        flashImage.gameObject.SetActive(false);
    }

    private void CleanUpFailedInit()
    {
        if (uiInstance != null) Destroy(uiInstance);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_capturedFrame != null) _capturedFrame.Release();
    }
}
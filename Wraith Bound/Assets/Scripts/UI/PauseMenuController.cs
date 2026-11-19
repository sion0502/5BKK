using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string HubSceneName = "Hub";
    private const string DefaultBlurProfilePath = "Assets/Settings/PauseBlurVolume.asset";

    [Header("Input")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

    [Header("UI")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private int canvasSortOrder = 500;
    [SerializeField] private float buttonWidth = 360f;
    [SerializeField] private float buttonHeight = 64f;
    [SerializeField] private float buttonSpacing = 18f;
    [SerializeField] private float dimBackgroundAlpha = 0f;

    [Header("Blur")]
    [SerializeField] private VolumeProfile blurProfile;
    [SerializeField] private float blurFadeDuration = 0.25f;
    [SerializeField] private float blurVolumePriority = 15f;

    private Canvas pauseCanvas;
    private CanvasGroup pauseCanvasGroup;
    private GameObject pausePanel;
    private RectTransform mainRoot;
    private RectTransform settingsRoot;
    private SettingsPanelUI settingsPanelUI;
    private Volume pauseBlurVolume;
    private Coroutine blurFadeCoroutine;
    private bool isPaused;
    private bool isTransitioning;
    private GameObject player;
    private CharacterController playerController;
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    private readonly Dictionary<UniversalAdditionalCameraData, bool> cachedPostProcessingStates =
        new Dictionary<UniversalAdditionalCameraData, bool>();

    void Awake()
    {
        EnsureBlurVolume();
        BuildUI();
        SetPauseVisible(false);
        SetBlurWeight(0f);
    }

    void Update()
    {
        // 튜토리얼 팝업 등이 떠 있으면 ESC는 그쪽(팝업 닫기)이 씀
        if (isTransitioning || PlayerDeathDebug.IsDying || PauseMenuSuppressor.IsSuppressed)
        {
            return;
        }

        if (Input.GetKeyDown(pauseKey))
        {
            if (isPaused && IsSettingsOpen)
            {
                CloseSettings();
            }
            else if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    void OnDisable()
    {
        // 일시정지 중에 이 오브젝트가 비활성화·파괴돼도 카메라 설정이 남지 않도록 원복한다.
        RestorePlayerCameraPostProcessing();
    }

    public void PauseGame()
    {
        if (isPaused || isTransitioning)
        {
            return;
        }

        CachePlayer();
        if (player == null)
        {
            Debug.LogWarning("[PauseMenuController] Player not found.");
            return;
        }

        isPaused = true;
        if (IsSettingsOpen)
        {
            CloseSettings();
        }

        Time.timeScale = 0f;
        EnsurePlayerCameraPostProcessing();
        SetGameplayEnabled(false);
        SetPauseVisible(true);
        FadeBlur(1f);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        if (!isPaused || isTransitioning)
        {
            return;
        }

        isPaused = false;
        if (IsSettingsOpen)
        {
            CloseSettings();
        }

        Time.timeScale = 1f;
        RestorePlayerCameraPostProcessing();
        SetGameplayEnabled(true);
        SetPauseVisible(false);
        FadeBlur(0f);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void RestartStage()
    {
        if (isTransitioning)
        {
            return;
        }

        StartCoroutine(RestartStageRoutine());
    }

    /// <summary>허브(귀문의 방)로 바로 돌아감. 허브 안에서는 버튼이 보이지 않음.</summary>
    public void GoToHub()
    {
        if (isTransitioning)
        {
            return;
        }

        StartCoroutine(GoToHubRoutine());
    }

    private IEnumerator GoToHubRoutine()
    {
        isTransitioning = true;
        Time.timeScale = 1f;
        SetBlurWeight(0f);
        RestorePlayerCameraPostProcessing();
        SetGameplayEnabled(true);
        SetPauseVisible(false);

        if (Application.CanStreamedLevelBeLoaded(HubSceneName))
        {
            yield return SceneManager.LoadSceneAsync(HubSceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("[PauseMenuController] Hub scene is not in Build Settings.");
        }

        isTransitioning = false;
        isPaused = false;
    }

    public void GoToMainMenu()
    {
        if (isTransitioning)
        {
            return;
        }

        StartCoroutine(GoToMainMenuRoutine());
    }

    public void QuitToDesktop()
    {
        if (isTransitioning)
        {
            return;
        }

        Time.timeScale = 1f;
        SetBlurWeight(0f);
        RestorePlayerCameraPostProcessing();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator RestartStageRoutine()
    {
        isTransitioning = true;
        Time.timeScale = 1f;
        SetBlurWeight(0f);
        RestorePlayerCameraPostProcessing();
        SetGameplayEnabled(true);
        SetPauseVisible(false);

        Scene activeScene = SceneManager.GetActiveScene();
        TraitRuntime.ResetRunState();
        yield return SceneManager.LoadSceneAsync(activeScene.name);
        isTransitioning = false;
        isPaused = false;
    }

    private IEnumerator GoToMainMenuRoutine()
    {
        isTransitioning = true;
        Time.timeScale = 1f;
        SetBlurWeight(0f);
        RestorePlayerCameraPostProcessing();
        SetGameplayEnabled(true);
        SetPauseVisible(false);

        if (Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
        {
            yield return SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("[PauseMenuController] MainMenu scene is not in Build Settings.");
        }

        isTransitioning = false;
        isPaused = false;
    }

    private void CachePlayer()
    {
        if (player != null)
        {
            return;
        }

        player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerController = player.GetComponent<CharacterController>();
        }
    }

    private void EnsurePlayerCameraPostProcessing()
    {
        if (player == null)
        {
            return;
        }

        // 이전 일시정지에서 남은 상태가 있으면 먼저 원복하고 다시 캐시한다.
        RestorePlayerCameraPostProcessing();

        Camera[] cameras = player.GetComponentsInChildren<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera == null)
            {
                continue;
            }

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            if (cameraData == null)
            {
                continue;
            }

            // Overlay 카메라(ItemViewCamera 등)에 포스트프로세싱을 켜면
            // Base 카메라가 처리한 결과 위에 스택이 한 번 더 적용돼 화면이 어두워진다.
            if (cameraData.renderType != CameraRenderType.Base)
            {
                continue;
            }

            cachedPostProcessingStates[cameraData] = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = true;
        }
    }

    private void RestorePlayerCameraPostProcessing()
    {
        if (cachedPostProcessingStates.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<UniversalAdditionalCameraData, bool> entry in cachedPostProcessingStates)
        {
            if (entry.Key != null)
            {
                entry.Key.renderPostProcessing = entry.Value;
            }
        }

        cachedPostProcessingStates.Clear();
    }

    private void EnsureBlurVolume()
    {
        if (pauseBlurVolume != null)
        {
            return;
        }

        VolumeProfile profile = ResolveBlurProfile();
        if (profile == null)
        {
            Debug.LogWarning("[PauseMenuController] Blur VolumeProfile is not assigned.");
            return;
        }

        GameObject volumeObject = new GameObject("PauseBlurVolume");
        volumeObject.transform.SetParent(transform, false);

        pauseBlurVolume = volumeObject.AddComponent<Volume>();
        pauseBlurVolume.isGlobal = true;
        pauseBlurVolume.priority = blurVolumePriority;
        pauseBlurVolume.weight = 0f;
        pauseBlurVolume.sharedProfile = profile;
    }

    private VolumeProfile ResolveBlurProfile()
    {
        if (blurProfile != null)
        {
            return blurProfile;
        }

#if UNITY_EDITOR
        blurProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultBlurProfilePath);
#endif

        return blurProfile;
    }

    private void FadeBlur(float targetWeight)
    {
        if (pauseBlurVolume == null)
        {
            return;
        }

        if (blurFadeCoroutine != null)
        {
            StopCoroutine(blurFadeCoroutine);
        }

        blurFadeCoroutine = StartCoroutine(FadeBlurRoutine(targetWeight));
    }

    private IEnumerator FadeBlurRoutine(float targetWeight)
    {
        if (pauseBlurVolume == null)
        {
            yield break;
        }

        float startWeight = pauseBlurVolume.weight;
        float timer = 0f;

        while (timer < blurFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = blurFadeDuration <= 0f ? 1f : Mathf.Clamp01(timer / blurFadeDuration);
            pauseBlurVolume.weight = Mathf.Lerp(startWeight, targetWeight, t);
            yield return null;
        }

        pauseBlurVolume.weight = targetWeight;
        blurFadeCoroutine = null;
    }

    private void SetBlurWeight(float weight)
    {
        if (pauseBlurVolume == null)
        {
            return;
        }

        if (blurFadeCoroutine != null)
        {
            StopCoroutine(blurFadeCoroutine);
            blurFadeCoroutine = null;
        }

        pauseBlurVolume.weight = weight;
    }

    private static bool ShouldKeepEnabledDuringPause(Behaviour behaviour)
    {
        return behaviour is Camera
            || behaviour is AudioListener
            || behaviour is UniversalAdditionalCameraData;
    }

    private void SetGameplayEnabled(bool enabled)
    {
        CachePlayer();
        if (player == null)
        {
            return;
        }

        if (!enabled)
        {
            disabledBehaviours.Clear();

            Behaviour[] behaviours = player.GetComponentsInChildren<Behaviour>(true);
            foreach (Behaviour behaviour in behaviours)
            {
                if (behaviour == null || !behaviour.enabled || ShouldKeepEnabledDuringPause(behaviour))
                {
                    continue;
                }

                behaviour.enabled = false;
                disabledBehaviours.Add(behaviour);
            }

            if (playerController == null)
            {
                playerController = player.GetComponent<CharacterController>();
            }

            if (playerController != null)
            {
                playerController.enabled = false;
            }

            return;
        }

        foreach (Behaviour behaviour in disabledBehaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledBehaviours.Clear();

        if (playerController != null)
        {
            playerController.enabled = true;
        }
    }

    private void SetPauseVisible(bool visible)
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(visible);
        }

        if (pauseCanvasGroup != null)
        {
            pauseCanvasGroup.alpha = visible ? 1f : 0f;
            pauseCanvasGroup.interactable = visible;
            pauseCanvasGroup.blocksRaycasts = visible;
        }
    }

    // 버튼에 마우스를 올리면 EscHover 소리
    private static void AddHoverSound(Button button)
    {
        if (button != null && button.GetComponent<MenuHoverSound>() == null)
        {
            button.gameObject.AddComponent<MenuHoverSound>();
        }
    }

    private void BuildUI()
    {
        TMP_FontAsset menuFont = UIFontConfig.UI;

        pauseCanvas = MenuUIUtility.CreateOverlayCanvas("PauseMenuCanvas", canvasSortOrder, transform);
        pauseCanvasGroup = pauseCanvas.gameObject.AddComponent<CanvasGroup>();

        RectTransform canvasRect = pauseCanvas.GetComponent<RectTransform>();
        pausePanel = canvasRect.gameObject;

        // 기존 일시정지 화면 요소는 mainRoot 아래에 둠 (옵션 패널을 열 때 통째로 숨기기 위해)
        GameObject mainObject = new GameObject("MainRoot", typeof(RectTransform));
        mainObject.transform.SetParent(canvasRect, false);
        RectTransform mainRoot = mainObject.GetComponent<RectTransform>();
        this.mainRoot = mainRoot;
        mainRoot.anchorMin = Vector2.zero;
        mainRoot.anchorMax = Vector2.one;
        mainRoot.offsetMin = Vector2.zero;
        mainRoot.offsetMax = Vector2.zero;

        if (dimBackgroundAlpha > 0.01f)
        {
            MenuUIUtility.CreateFullScreenPanel(
                canvasRect,
                new Color(0f, 0f, 0f, dimBackgroundAlpha),
                "DimBackground");
        }

        // 버튼 목록 — 허브 안에서는 '허브맵으로'를 뺌. 개수에 맞춰 세로 가운데 정렬
        var buttons = new List<KeyValuePair<string, UnityEngine.Events.UnityAction>>
        {
            new KeyValuePair<string, UnityEngine.Events.UnityAction>("계속하기", ResumeGame),
            new KeyValuePair<string, UnityEngine.Events.UnityAction>("옵션", OpenSettings),
            new KeyValuePair<string, UnityEngine.Events.UnityAction>("다시하기", RestartStage),
        };
        if (SceneManager.GetActiveScene().name != HubSceneName)
        {
            buttons.Add(new KeyValuePair<string, UnityEngine.Events.UnityAction>("허브맵으로", GoToHub));
        }

        buttons.Add(new KeyValuePair<string, UnityEngine.Events.UnityAction>("메인메뉴로", GoToMainMenu));
        buttons.Add(new KeyValuePair<string, UnityEngine.Events.UnityAction>("바탕화면으로", QuitToDesktop));

        float step = buttonHeight + buttonSpacing;
        float startY = buttonSpacing * 1.5f + step * (buttons.Count - 4) * 0.5f;
        float lastY = startY - step * (buttons.Count - 1);

        // 참고 디자인: 은은한 연기 바탕 위에 붓질 머리 '일시정지' + 아래 구분선·조작 안내 (테두리 없음)
        Sprite smoke = MenuSkin.PanelBg;
        if (smoke != null)
        {
            Image bg = MenuSkin.AddImage(mainRoot, "Smoke", smoke, new Color(1f, 1f, 1f, 0.6f), sliced: true);
            MenuSkin.Place(bg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, (startY + 130f + lastY - 110f) * 0.5f), new Vector2(buttonWidth + 420f, startY - lastY + 400f));
        }

        MenuUIUtility.CreateHeader(mainRoot, "일시정지", menuFont, new Vector2(0f, startY + 110f), buttonWidth + 220f);
        MenuUIUtility.CreateDivider(mainRoot, new Vector2(0f, lastY - 70f), buttonWidth + 220f);
        TextMeshProUGUI hint = MenuUIUtility.CreateLabel(mainRoot, "ESC   계속하기", menuFont, new Vector2(0f, lastY - 105f),
            new Vector2(400f, 36f), 20f, TextAlignmentOptions.Center, MenuSkin.TextDim);
        hint.characterSpacing = 4f;
        for (int i = 0; i < buttons.Count; i++)
        {
            AddHoverSound(MenuUIUtility.CreateMenuButton(
                mainRoot,
                buttons[i].Key,
                menuFont,
                new Vector2(0f, startY - step * i),
                new Vector2(buttonWidth, buttonHeight),
                buttons[i].Value));
        }

        // 옵션 패널 (평소엔 숨김 — '옵션' 버튼으로 열고 '뒤로'/ESC로 닫음)
        GameObject settingsObject = new GameObject("SettingsRoot", typeof(RectTransform));
        settingsObject.transform.SetParent(canvasRect, false);
        settingsRoot = settingsObject.GetComponent<RectTransform>();
        settingsRoot.anchorMin = Vector2.zero;
        settingsRoot.anchorMax = Vector2.one;
        settingsRoot.offsetMin = Vector2.zero;
        settingsRoot.offsetMax = Vector2.zero;
        settingsPanelUI = SettingsPanelUI.Build(settingsRoot, menuFont,
            new Vector2(buttonWidth * 0.7f, buttonHeight * 0.85f), CloseSettings);
        settingsObject.SetActive(false);
    }

    private void OpenSettings()
    {
        if (settingsRoot == null)
        {
            return;
        }

        settingsPanelUI.Refresh();
        mainRoot.gameObject.SetActive(false);
        settingsRoot.gameObject.SetActive(true);
    }

    private void CloseSettings()
    {
        GameSettings.Save();
        if (settingsRoot != null)
        {
            settingsRoot.gameObject.SetActive(false);
        }

        if (mainRoot != null)
        {
            mainRoot.gameObject.SetActive(true);
        }
    }

    private bool IsSettingsOpen => settingsRoot != null && settingsRoot.gameObject.activeSelf;
}

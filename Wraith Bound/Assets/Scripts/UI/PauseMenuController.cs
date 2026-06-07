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
    private Volume pauseBlurVolume;
    private Coroutine blurFadeCoroutine;
    private bool isPaused;
    private bool isTransitioning;
    private GameObject player;
    private CharacterController playerController;
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();

    void Awake()
    {
        EnsureBlurVolume();
        BuildUI();
        SetPauseVisible(false);
        SetBlurWeight(0f);
    }

    void Update()
    {
        if (isTransitioning)
        {
            return;
        }

        if (Input.GetKeyDown(pauseKey))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
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
        Time.timeScale = 1f;
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
        SetGameplayEnabled(true);
        SetPauseVisible(false);

        Scene activeScene = SceneManager.GetActiveScene();
        yield return SceneManager.LoadSceneAsync(activeScene.name);
        isTransitioning = false;
        isPaused = false;
    }

    private IEnumerator GoToMainMenuRoutine()
    {
        isTransitioning = true;
        Time.timeScale = 1f;
        SetBlurWeight(0f);
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

        Camera[] cameras = player.GetComponentsInChildren<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera == null)
            {
                continue;
            }

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
            }
        }
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

    private void BuildUI()
    {
        TMP_FontAsset menuFont = MenuUIUtility.LoadPolFont(font);

        pauseCanvas = MenuUIUtility.CreateOverlayCanvas("PauseMenuCanvas", canvasSortOrder, transform);
        pauseCanvasGroup = pauseCanvas.gameObject.AddComponent<CanvasGroup>();

        RectTransform canvasRect = pauseCanvas.GetComponent<RectTransform>();
        pausePanel = canvasRect.gameObject;

        if (dimBackgroundAlpha > 0.01f)
        {
            MenuUIUtility.CreateFullScreenPanel(
                canvasRect,
                new Color(0f, 0f, 0f, dimBackgroundAlpha),
                "DimBackground");
        }

        float startY = buttonSpacing * 1.5f;
        MenuUIUtility.CreateMenuButton(
            canvasRect,
            "계속하기",
            menuFont,
            new Vector2(0f, startY),
            new Vector2(buttonWidth, buttonHeight),
            ResumeGame);
        MenuUIUtility.CreateMenuButton(
            canvasRect,
            "다시하기",
            menuFont,
            new Vector2(0f, startY - (buttonHeight + buttonSpacing)),
            new Vector2(buttonWidth, buttonHeight),
            RestartStage);
        MenuUIUtility.CreateMenuButton(
            canvasRect,
            "메인메뉴로",
            menuFont,
            new Vector2(0f, startY - (buttonHeight + buttonSpacing) * 2f),
            new Vector2(buttonWidth, buttonHeight),
            GoToMainMenu);
        MenuUIUtility.CreateMenuButton(
            canvasRect,
            "바탕화면으로",
            menuFont,
            new Vector2(0f, startY - (buttonHeight + buttonSpacing) * 3f),
            new Vector2(buttonWidth, buttonHeight),
            QuitToDesktop);
    }
}

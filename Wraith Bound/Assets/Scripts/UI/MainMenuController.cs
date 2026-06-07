using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private const string HubSceneName = "Hub";
    private const string DefaultTitleFontPath =
        "Assets/TextMesh Pro/Fonts/Title/ZiKuXingQiuFeiYangTi-2 SDF.asset";
    private const string SourceTitleFontPath =
        "Assets/TextMesh Pro/Fonts/Title/ZiKuXingQiuFeiYangTi-2.ttf";
    private const string ResourcesTitleFontPath = "Fonts/Title/ZiKuXingQiuFeiYangTi-2";
    private const string TitleCharacters = "鬼行";

    [Header("Scene")]
    [SerializeField] private string hubSceneName = HubSceneName;

    [Header("References")]
    [SerializeField] private Camera menuCamera;
    [SerializeField] private Volume menuBlurVolume;

    [Header("UI")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private TMP_FontAsset titleFont;
    [SerializeField] private int canvasSortOrder = 200;

    [Header("Transition")]
    [SerializeField] private float menuFadeDuration = 0.45f;
    [SerializeField] private float blurFadeDuration = 0.9f;
    [SerializeField] private float buttonWidth = 320f;
    [SerializeField] private float buttonHeight = 64f;
    [SerializeField] private float buttonSpacing = 20f;

    private Canvas menuCanvas;
    private CanvasGroup menuCanvasGroup;
    private bool isTransitioning;
    private bool isGameplayStarted;

    private GameObject hubPlayer;
    private Camera hubPlayerCamera;
    private AudioListener hubAudioListener;
    private AudioListener menuAudioListener;
    private CharacterController hubCharacterController;
    private readonly List<Behaviour> disabledHubBehaviours = new List<Behaviour>();

    void Start()
    {
        BuildMenuUI();
        menuAudioListener = menuCamera != null ? menuCamera.GetComponent<AudioListener>() : null;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StartCoroutine(InitializeMenuRoutine());
    }

    public void StartGame()
    {
        if (isTransitioning || isGameplayStarted)
        {
            return;
        }

        StartCoroutine(StartGameRoutine());
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator InitializeMenuRoutine()
    {
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(hubSceneName, LoadSceneMode.Additive);
        if (loadOperation == null)
        {
            Debug.LogError("[MainMenuController] Failed to load Hub scene additively.");
            yield break;
        }

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        Scene hubScene = SceneManager.GetSceneByName(hubSceneName);
        if (!hubScene.IsValid() || !hubScene.isLoaded)
        {
            Debug.LogError("[MainMenuController] Hub scene is not loaded.");
            yield break;
        }

        SceneManager.SetActiveScene(hubScene);
        CacheHubReferences(hubScene);
        AlignMenuCameraToHubView();
        SetHubGameplayEnabled(false);
        SuppressHubEventSystem(hubScene);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private IEnumerator StartGameRoutine()
    {
        isTransitioning = true;

        float menuTimer = 0f;
        while (menuTimer < menuFadeDuration)
        {
            menuTimer += Time.unscaledDeltaTime;
            float t = menuFadeDuration <= 0f ? 1f : Mathf.Clamp01(menuTimer / menuFadeDuration);
            if (menuCanvasGroup != null)
            {
                menuCanvasGroup.alpha = 1f - t;
            }

            yield return null;
        }

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 0f;
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
        }

        if (menuBlurVolume != null)
        {
            float blurTimer = 0f;
            float startWeight = menuBlurVolume.weight;
            while (blurTimer < blurFadeDuration)
            {
                blurTimer += Time.unscaledDeltaTime;
                float t = blurFadeDuration <= 0f ? 1f : Mathf.Clamp01(blurTimer / blurFadeDuration);
                menuBlurVolume.weight = Mathf.Lerp(startWeight, 0f, t);
                yield return null;
            }

            menuBlurVolume.weight = 0f;
            menuBlurVolume.gameObject.SetActive(false);
        }

        if (menuCamera != null)
        {
            menuCamera.enabled = false;
        }

        if (menuAudioListener != null)
        {
            menuAudioListener.enabled = false;
        }

        if (hubPlayerCamera != null)
        {
            hubPlayerCamera.enabled = true;
        }

        if (hubAudioListener != null)
        {
            hubAudioListener.enabled = true;
        }

        SetHubGameplayEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isGameplayStarted = true;
        isTransitioning = false;
    }

    private void CacheHubReferences(Scene hubScene)
    {
        hubPlayer = null;
        hubPlayerCamera = null;
        hubAudioListener = null;
        hubCharacterController = null;

        foreach (GameObject rootObject in hubScene.GetRootGameObjects())
        {
            if (hubPlayer == null && rootObject.CompareTag("Player"))
            {
                hubPlayer = rootObject;
            }
        }

        if (hubPlayer == null)
        {
            GameObject taggedPlayer = GameObject.FindWithTag("Player");
            if (taggedPlayer != null && taggedPlayer.scene.name == hubSceneName)
            {
                hubPlayer = taggedPlayer;
            }
        }

        if (hubPlayer == null)
        {
            Debug.LogWarning("[MainMenuController] Hub player not found.");
            return;
        }

        hubCharacterController = hubPlayer.GetComponent<CharacterController>();
        hubPlayerCamera = hubPlayer.GetComponentInChildren<Camera>(true);
        if (hubPlayerCamera != null)
        {
            hubAudioListener = hubPlayerCamera.GetComponent<AudioListener>();
        }
    }

    private void AlignMenuCameraToHubView()
    {
        if (menuCamera == null || hubPlayerCamera == null)
        {
            return;
        }

        Transform menuCameraTransform = menuCamera.transform;
        Transform hubCameraTransform = hubPlayerCamera.transform;
        menuCameraTransform.SetPositionAndRotation(
            hubCameraTransform.position,
            hubCameraTransform.rotation);
    }

    private void SetHubGameplayEnabled(bool enabled)
    {
        if (hubPlayer == null)
        {
            return;
        }

        if (!enabled)
        {
            disabledHubBehaviours.Clear();

            Behaviour[] behaviours = hubPlayer.GetComponentsInChildren<Behaviour>(true);
            foreach (Behaviour behaviour in behaviours)
            {
                if (behaviour == null || !behaviour.enabled)
                {
                    continue;
                }

                behaviour.enabled = false;
                disabledHubBehaviours.Add(behaviour);
            }

            if (hubCharacterController != null)
            {
                hubCharacterController.enabled = false;
            }

            if (hubPlayerCamera != null)
            {
                hubPlayerCamera.enabled = false;
            }

            if (hubAudioListener != null)
            {
                hubAudioListener.enabled = false;
            }

            return;
        }

        foreach (Behaviour behaviour in disabledHubBehaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledHubBehaviours.Clear();

        if (hubCharacterController != null)
        {
            hubCharacterController.enabled = true;
        }
    }

    private static void SuppressHubEventSystem(Scene hubScene)
    {
        foreach (GameObject rootObject in hubScene.GetRootGameObjects())
        {
            if (rootObject.name != "EventSystem")
            {
                continue;
            }

            rootObject.SetActive(false);
            return;
        }
    }

    private void BuildMenuUI()
    {
        TMP_FontAsset menuFont = MenuUIUtility.LoadPolFont(font);
        TMP_FontAsset resolvedTitleFont = ResolveTitleFont();

        menuCanvas = MenuUIUtility.CreateOverlayCanvas("MainMenuCanvas", canvasSortOrder);
        menuCanvasGroup = menuCanvas.gameObject.AddComponent<CanvasGroup>();

        RectTransform canvasRect = menuCanvas.GetComponent<RectTransform>();

        MenuUIUtility.CreateTitle(
            canvasRect,
            "鬼行",
            resolvedTitleFont,
            new Vector2(0f, -80f),
            160f,
            new Color(0.85f, 0.12f, 0.12f, 1f));

        float centerX = 0f;
        float topY = 120f;
        MenuUIUtility.CreateMenuButton(
            canvasRect,
            "게임시작",
            menuFont,
            new Vector2(centerX, topY),
            new Vector2(buttonWidth, buttonHeight),
            StartGame);
        MenuUIUtility.CreateMenuButton(
            canvasRect,
            "게임종료",
            menuFont,
            new Vector2(centerX, topY - (buttonHeight + buttonSpacing)),
            new Vector2(buttonWidth, buttonHeight),
            QuitGame);
    }

    private TMP_FontAsset ResolveTitleFont()
    {
        if (titleFont != null)
        {
            return titleFont;
        }

#if UNITY_EDITOR
        titleFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultTitleFontPath);
        if (titleFont != null)
        {
            return titleFont;
        }
#endif

        titleFont = CreateTitleFontFromSource();
        if (titleFont != null)
        {
            return titleFont;
        }

        return MenuUIUtility.LoadPolFont(font);
    }

    private TMP_FontAsset CreateTitleFontFromSource()
    {
        Font sourceFont = null;

#if UNITY_EDITOR
        sourceFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(SourceTitleFontPath);
#endif

        if (sourceFont == null)
        {
            sourceFont = Resources.Load<Font>(ResourcesTitleFontPath);
        }

        if (sourceFont == null)
        {
            return null;
        }

        TMP_FontAsset createdFont = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            160,
            8,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic);

        createdFont.TryAddCharacters(TitleCharacters, out _);
        return createdFont;
    }
}

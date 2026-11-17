using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴. 진행 기록(GameProgress) 유무에 따라 두 가지로 동작합니다.
///
/// [첫 플레이] 진행 기록 없음
///  - Intro 씬(폐병원 외부)을 배경으로 Additive 로드 — 컷씬은 자동 재생하지 않고 대기
///  - 메뉴를 누를 때마다 카메라가 병원 곳곳의 다른 시점으로 날아감 (MainMenuViews)
///  - 새로운 게임: 병원 입구로 이동 → 암전 → 이미 로드된 인트로 컷씬을 그 자리에서 재생 (로딩 없음)
///
/// [이어서 하기 가능] 진행 기록 있음 (허브에 한 번이라도 도착)
///  - 기존 방식: Hub 씬을 배경으로 Additive 로드, 메뉴 카메라를 플레이어 시점에 맞춤
///  - 이어서 하기: 로딩 없이 배경이 그대로 플레이 화면으로 전환
///  - 새로운 게임: 확인 후 진행 기록을 지우고 Intro 씬 로드
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private const string HubSceneName = "Hub";
    private const string DefaultTitleFontPath =
        "Assets/TextMesh Pro/Fonts/Title/ZiKuXingQiuFeiYangTi-2 SDF.asset";
    private const string SourceTitleFontPath =
        "Assets/TextMesh Pro/Fonts/Title/ZiKuXingQiuFeiYangTi-2.ttf";
    private const string ResourcesTitleFontPath = "Fonts/Title/ZiKuXingQiuFeiYangTi-2";
    private const string TitleCharacters = "鬼行";

    private enum MenuMode
    {
        FirstPlay,
        Returning,
    }

    [Header("Scene")]
    [SerializeField] private string hubSceneName = HubSceneName;
    [Tooltip("새로운 게임에서 재생할 인트로 컷씬 씬. 첫 플레이 메뉴의 배경으로도 씀")]
    [SerializeField] private string introSceneName = "Intro";
    [SerializeField] private float introFadeDuration = 1.2f;

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
    [SerializeField] private float startupFadeDuration = 1.0f;
    [SerializeField] private float buttonWidth = 320f;
    [SerializeField] private float buttonHeight = 64f;
    [SerializeField] private float buttonSpacing = 20f;

    [Header("First Play (병원 배경 메뉴)")]
    [Tooltip("테스트용: 진행 기록이 있어도 첫 플레이 메뉴로 띄움")]
    [SerializeField] private bool forceFirstPlayMenu;
    [Tooltip("첫 플레이 메뉴에서 버튼 열의 가로 위치 (0 = 가운데). 배경이 가려지지 않게 왼쪽으로 뺌")]
    [SerializeField] private float firstPlayButtonsX = -620f;
    [Tooltip("첫 플레이 메뉴의 배경 블러 세기 (0이면 끔 — 배경을 또렷하게 보여 줌)")]
    [SerializeField, Range(0f, 1f)] private float firstPlayBlurWeight;
    [SerializeField] private float minCameraMoveDuration = 1.1f;
    [SerializeField] private float maxCameraMoveDuration = 2.8f;
    [Tooltip("이 거리(m) 이상이면 maxCameraMoveDuration으로 이동")]
    [SerializeField] private float longMoveDistance = 90f;
    [Tooltip("이동 중 위로 솟는 높이 = 거리 × 이 값 (나무·건물 위로 넘어가는 크레인 샷)")]
    [SerializeField] private float arcHeightPerMeter = 0.12f;
    [SerializeField] private float maxArcHeight = 12f;
    [Tooltip("가만히 있을 때 손에 든 카메라처럼 살짝 흔들리는 정도")]
    [SerializeField] private float idleSway = 1f;

    private MenuMode mode;
    private Canvas menuCanvas;
    private CanvasGroup menuCanvasGroup;
    private CanvasGroup mainButtonsGroup;
    private CanvasGroup settingsGroup;
    private CanvasGroup dialogGroup;
    private TextMeshProUGUI dialogMessage;
    private Button dialogYesButton;
    private Button dialogNoButton;
    private TextMeshProUGUI fullscreenLabel;
    private TextMeshProUGUI volumeValueLabel;
    private TextMeshProUGUI sensitivityValueLabel;
    private readonly Dictionary<CanvasGroup, Coroutine> fadeRoutines = new Dictionary<CanvasGroup, Coroutine>();

    private bool isReady;
    private bool isTransitioning;
    private bool isGameplayStarted;
    private bool panelOpen;

    // Returning 모드 (허브 배경)
    private GameObject hubPlayer;
    private Camera hubPlayerCamera;
    private AudioListener hubAudioListener;
    private AudioListener menuAudioListener;
    private CharacterController hubCharacterController;
    private readonly List<Behaviour> disabledHubBehaviours = new List<Behaviour>();

    // FirstPlay 모드 (인트로 배경)
    private Camera viewCamera;
    private CutsceneDirector introCutscene;
    private CutsceneBlackScreen introBlackScreen;
    private readonly Dictionary<MainMenuViews.Id, Pose> views = new Dictionary<MainMenuViews.Id, Pose>();
    private Vector3 basePosition;
    private Quaternion baseRotation = Quaternion.identity;
    private Coroutine moveRoutine;
    private bool swayActive;

    private CutsceneBlackScreen startupBlack;

    void Start()
    {
        mode = (GameProgress.HasSave && !forceFirstPlayMenu) ? MenuMode.Returning : MenuMode.FirstPlay;
        if (mode == MenuMode.FirstPlay && !Application.CanStreamedLevelBeLoaded(introSceneName))
        {
            Debug.LogWarning($"[MainMenuController] '{introSceneName}' 씬이 빌드 세팅에 없어 허브 배경 메뉴로 띄웁니다.");
            mode = MenuMode.Returning;
        }

        menuAudioListener = menuCamera != null ? menuCamera.GetComponent<AudioListener>() : null;
        BuildMenuUI();
        SetGroupVisible(menuCanvasGroup, true);
        menuCanvasGroup.interactable = false; // 배경 준비가 끝나면 켬

        startupBlack = CutsceneBlackScreen.CreateRuntime(canvasSortOrder + 100, 1f);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StartCoroutine(mode == MenuMode.FirstPlay ? InitializeFirstPlayRoutine() : InitializeReturningRoutine());
    }

    void Update()
    {
        if (!isReady || isTransitioning || isGameplayStarted)
        {
            return;
        }

        if (panelOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePanels();
        }
    }

    void LateUpdate()
    {
        if (!swayActive || viewCamera == null)
        {
            return;
        }

        // 기준 자세 위에 아주 느린 흔들림을 얹음 (이동 중에도 계속 얹어서 끊김 없음)
        float t = Time.unscaledTime;
        Vector3 offset = new Vector3(Mathf.Sin(t * 0.31f) * 0.05f, Mathf.Sin(t * 0.23f + 1f) * 0.03f, 0f) * idleSway;
        Quaternion wobble = Quaternion.Euler(Mathf.Sin(t * 0.27f) * 0.5f * idleSway, Mathf.Sin(t * 0.19f + 2f) * 0.8f * idleSway, 0f);
        viewCamera.transform.SetPositionAndRotation(basePosition + baseRotation * offset, baseRotation * wobble);
    }

    void OnDestroy()
    {
        CutsceneDirector.HoldAutoPlay = false;
        HubArrival.MenuBackdrop = false;
    }

    // ───────────────────────── 버튼 ─────────────────────────

    public void OnNewGameClicked()
    {
        if (!CanAcceptInput())
        {
            return;
        }

        if (mode == MenuMode.FirstPlay)
        {
            StartCoroutine(StartIntroInPlaceRoutine());
            return;
        }

        ShowDialog(
            "새로운 게임을 시작하면\n지금까지의 진행 기록이 사라집니다.\n계속할까요?",
            "시작", () =>
            {
                GameProgress.Clear();
                StartCoroutine(LoadIntroSceneRoutine());
            },
            "취소", ClosePanels);
    }

    public void OnContinueClicked()
    {
        if (!CanAcceptInput())
        {
            return;
        }

        if (mode == MenuMode.FirstPlay)
        {
            MoveCameraTo(MainMenuViews.Id.Continue);
            ShowDialog("아직 저장된 기록이 없습니다.", "확인", ClosePanels, null, null);
            return;
        }

        StartCoroutine(StartGameRoutine());
    }

    public void OnSettingsClicked()
    {
        if (!CanAcceptInput())
        {
            return;
        }

        if (mode == MenuMode.FirstPlay)
        {
            MoveCameraTo(MainMenuViews.Id.Settings);
        }

        RefreshSettingsLabels();
        OpenPanel(settingsGroup);
    }

    public void OnQuitClicked()
    {
        if (!CanAcceptInput())
        {
            return;
        }

        if (mode == MenuMode.FirstPlay)
        {
            MoveCameraTo(MainMenuViews.Id.Quit);
        }

        ShowDialog("게임을 종료할까요?", "종료", QuitGame, "취소", ClosePanels);
    }

    /// <summary>예전 이름 호환 (씬·이벤트에서 StartGame을 부르던 곳).</summary>
    public void StartGame()
    {
        if (mode == MenuMode.Returning)
        {
            OnContinueClicked();
        }
        else
        {
            OnNewGameClicked();
        }
    }

    public void QuitGame()
    {
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private bool CanAcceptInput()
    {
        return isReady && !isTransitioning && !isGameplayStarted;
    }

    // ───────────────────────── 첫 플레이: 인트로 배경 ─────────────────────────

    private IEnumerator InitializeFirstPlayRoutine()
    {
        CutsceneDirector.HoldAutoPlay = true;

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(introSceneName, LoadSceneMode.Additive);
        if (loadOperation == null)
        {
            Debug.LogError("[MainMenuController] Intro 씬을 Additive로 로드하지 못했습니다.");
            yield break;
        }

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        Scene introScene = SceneManager.GetSceneByName(introSceneName);
        if (!introScene.IsValid() || !introScene.isLoaded)
        {
            Debug.LogError("[MainMenuController] Intro scene is not loaded.");
            yield break;
        }

        SceneManager.SetActiveScene(introScene); // 안개·환경광은 활성 씬 설정을 따름
        SuppressEventSystems(introScene);

        introCutscene = FindInScene<CutsceneDirector>(introScene);
        introBlackScreen = FindInScene<CutsceneBlackScreen>(introScene);
        viewCamera = FindIntroCamera(introScene);

        // 인트로 검은 화면은 시작 알파 1 — 메뉴 동안엔 걷어 둠 (새로운 게임에서 다시 덮음)
        if (introBlackScreen != null)
        {
            introBlackScreen.Alpha = 0f;
        }

        if (viewCamera == null)
        {
            // 인트로 카메라를 못 찾으면 메뉴 카메라를 인트로 맵 위에서 씀
            viewCamera = menuCamera;
        }
        else
        {
            if (menuCamera != null)
            {
                menuCamera.enabled = false;
            }

            if (menuAudioListener != null)
            {
                menuAudioListener.enabled = false;
            }
        }

        ApplyMenuBlur(firstPlayBlurWeight);
        ResolveViews();

        Pose home = views[MainMenuViews.Id.Home];
        basePosition = home.position;
        baseRotation = home.rotation;
        viewCamera.transform.SetPositionAndRotation(basePosition, baseRotation);
        swayActive = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return FinishStartupRoutine();
    }

    private IEnumerator StartIntroInPlaceRoutine()
    {
        isTransitioning = true;
        FadeGroup(mainButtonsGroup, 0f, menuFadeDuration);

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine); // '홈으로 돌아가기' 이동 중에 눌렀을 수 있음
        }

        moveRoutine = StartCoroutine(MoveCameraRoutine(views[MainMenuViews.Id.NewGame]));
        yield return moveRoutine;

        if (introCutscene == null)
        {
            Debug.LogWarning("[MainMenuController] Intro 씬에서 CutsceneDirector를 찾지 못해 씬을 새로 로드합니다.");
            yield return LoadIntroSceneRoutine();
            yield break;
        }

        // 인트로 자체의 검은 화면(정렬 600)으로 덮음 — 타임라인 0초도 검은 화면이라 그대로 이어짐
        CutsceneBlackScreen black = introBlackScreen != null
            ? introBlackScreen
            : CutsceneBlackScreen.CreateRuntime(canvasSortOrder + 100);
        yield return black.FadeTo(1f, introFadeDuration);

        swayActive = false;
        SetGroupVisible(menuCanvasGroup, false);
        menuCanvas.gameObject.SetActive(false);
        ApplyMenuBlur(0f);

        if (viewCamera != null && viewCamera != menuCamera && menuCamera != null)
        {
            menuCamera.gameObject.SetActive(false);
        }

        GameProgress.Clear();
        CutsceneDirector.HoldAutoPlay = false;
        introCutscene.Play();
        isGameplayStarted = true;
    }

    private IEnumerator LoadIntroSceneRoutine()
    {
        isTransitioning = true;

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
        }

        CutsceneBlackScreen black = CutsceneBlackScreen.CreateRuntime(canvasSortOrder + 100);
        yield return black.FadeTo(1f, introFadeDuration);

        CutsceneDirector.HoldAutoPlay = false;
        SceneManager.LoadScene(introSceneName);
    }

    private void ResolveViews()
    {
        views.Clear();
        Dictionary<string, Transform> anchors = FindViewAnchors();

        foreach (MainMenuViews.Id id in System.Enum.GetValues(typeof(MainMenuViews.Id)))
        {
            if (anchors.TryGetValue(MainMenuViews.AnchorName(id), out Transform anchor))
            {
                views[id] = new Pose(anchor.position, anchor.rotation);
                continue;
            }

            MainMenuViews.View fallback = MainMenuViews.Default(id);
            views[id] = new Pose(fallback.Position, fallback.Rotation);
        }
    }

    /// <summary>열린 씬들에서 "MenuView_*" 오브젝트를 찾음 (루트 또는 "MenuViews" 루트의 자식).</summary>
    private static Dictionary<string, Transform> FindViewAnchors()
    {
        Dictionary<string, Transform> anchors = new Dictionary<string, Transform>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith(MainMenuViews.AnchorPrefix))
                {
                    anchors[root.name] = root.transform;
                }

                if (root.name != "MenuViews")
                {
                    continue;
                }

                foreach (Transform child in root.transform)
                {
                    if (child.name.StartsWith(MainMenuViews.AnchorPrefix))
                    {
                        anchors[child.name] = child;
                    }
                }
            }
        }

        return anchors;
    }

    private void MoveCameraTo(MainMenuViews.Id id)
    {
        if (mode != MenuMode.FirstPlay || viewCamera == null || !views.TryGetValue(id, out Pose target))
        {
            return;
        }

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        moveRoutine = StartCoroutine(MoveCameraRoutine(target));
    }

    /// <summary>현재 기준 자세에서 목표까지 위로 살짝 솟았다 내려앉는 곡선으로 이동.</summary>
    private IEnumerator MoveCameraRoutine(Pose target)
    {
        Vector3 startPosition = basePosition;
        Quaternion startRotation = baseRotation;
        float distance = Vector3.Distance(startPosition, target.position);
        float duration = Mathf.Lerp(minCameraMoveDuration, maxCameraMoveDuration,
            longMoveDistance <= 0f ? 1f : Mathf.Clamp01(distance / longMoveDistance));
        float arc = Mathf.Min(distance * arcHeightPerMeter, maxArcHeight);

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float s = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; // ease in-out cubic

            basePosition = Vector3.Lerp(startPosition, target.position, s) + Vector3.up * (arc * Mathf.Sin(s * Mathf.PI));
            baseRotation = Quaternion.Slerp(startRotation, target.rotation, s);
            yield return null;
        }

        basePosition = target.position;
        baseRotation = target.rotation;
        moveRoutine = null;
    }

    private static Camera FindIntroCamera(Scene scene)
    {
        Camera fallback = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (cam.CompareTag("MainCamera"))
                {
                    return cam;
                }

                if (fallback == null)
                {
                    fallback = cam;
                }
            }
        }

        return fallback;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    // ───────────────────────── 이어서 하기: 허브 배경 ─────────────────────────

    private IEnumerator InitializeReturningRoutine()
    {
        HubArrival.MenuBackdrop = true; // 허브 깨어남 연출·독백이 메뉴 위에서 시작되지 않게
        HubArrival.FromPrologue = false;

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
        SuppressEventSystems(hubScene);
        viewCamera = menuCamera;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return FinishStartupRoutine();
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

        SetGroupVisible(menuCanvasGroup, false);

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
        HubArrival.MenuBackdrop = false; // 여기서부터 허브 진행(독백·재촉) 시작
        GameProgress.MarkHubReached();

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

    // ───────────────────────── 공통 ─────────────────────────

    private IEnumerator FinishStartupRoutine()
    {
        if (startupBlack != null)
        {
            yield return startupBlack.FadeTo(0f, startupFadeDuration);
            Destroy(startupBlack.gameObject);
            startupBlack = null;
        }

        menuCanvasGroup.interactable = true;
        menuCanvasGroup.blocksRaycasts = true;
        isReady = true;
    }

    private void ApplyMenuBlur(float weight)
    {
        if (menuBlurVolume == null)
        {
            return;
        }

        menuBlurVolume.weight = weight;
        menuBlurVolume.gameObject.SetActive(weight > 0.001f);
    }

    /// <summary>배경 씬의 EventSystem은 끔 (메뉴 씬 것 하나만 남김).</summary>
    private static void SuppressEventSystems(Scene scene)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            foreach (UnityEngine.EventSystems.EventSystem eventSystem in
                     rootObject.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
            {
                eventSystem.gameObject.SetActive(false);
            }
        }
    }

    private void OpenPanel(CanvasGroup panel)
    {
        panelOpen = true;
        FadeGroup(mainButtonsGroup, 0f, menuFadeDuration);
        if (panel != settingsGroup)
        {
            SetGroupVisible(settingsGroup, false);
        }

        if (panel != dialogGroup)
        {
            SetGroupVisible(dialogGroup, false);
        }

        FadeGroup(panel, 1f, menuFadeDuration);
    }

    private void ClosePanels()
    {
        GameSettings.Save();
        panelOpen = false;
        FadeGroup(settingsGroup, 0f, menuFadeDuration * 0.6f);
        FadeGroup(dialogGroup, 0f, menuFadeDuration * 0.6f);
        FadeGroup(mainButtonsGroup, 1f, menuFadeDuration);
        MoveCameraTo(MainMenuViews.Id.Home);
    }

    private void ShowDialog(string message, string yesLabel, UnityEngine.Events.UnityAction onYes,
        string noLabel, UnityEngine.Events.UnityAction onNo)
    {
        dialogMessage.text = message;
        ConfigureDialogButton(dialogYesButton, yesLabel, onYes);

        bool hasNo = !string.IsNullOrEmpty(noLabel);
        dialogNoButton.gameObject.SetActive(hasNo);
        if (hasNo)
        {
            ConfigureDialogButton(dialogNoButton, noLabel, onNo);
        }

        float buttonY = -80f;
        float halfGap = (buttonWidth * 0.7f + buttonSpacing) * 0.5f;
        ((RectTransform)dialogYesButton.transform).anchoredPosition = new Vector2(hasNo ? -halfGap : 0f, buttonY);
        ((RectTransform)dialogNoButton.transform).anchoredPosition = new Vector2(halfGap, buttonY);

        OpenPanel(dialogGroup);
    }

    private static void ConfigureDialogButton(Button button, string label, UnityEngine.Events.UnityAction onClick)
    {
        button.onClick.RemoveAllListeners();
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = label;
        }
    }

    private void FadeGroup(CanvasGroup group, float target, float duration)
    {
        if (group == null)
        {
            return;
        }

        if (fadeRoutines.TryGetValue(group, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
        }

        fadeRoutines[group] = StartCoroutine(FadeGroupRoutine(group, target, duration));
    }

    private IEnumerator FadeGroupRoutine(CanvasGroup group, float target, float duration)
    {
        bool show = target > 0.5f;
        group.interactable = false;
        group.blocksRaycasts = show;

        float start = group.alpha;
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, duration <= 0f ? 1f : Mathf.Clamp01(timer / duration));
            yield return null;
        }

        SetGroupVisible(group, show);
        fadeRoutines[group] = null;
    }

    private static void SetGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    // ───────────────────────── UI 생성 ─────────────────────────

    private void BuildMenuUI()
    {
        TMP_FontAsset menuFont = UIFontConfig.UI; // 메뉴 선택지 = Pretendard (제목은 붓글씨 그대로)
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

        // 제목 아래 장식 구분선
        MenuUIUtility.CreateDivider(canvasRect, new Vector2(0f, -292f), 560f, new Vector2(0.5f, 1f));

        BuildMainButtons(canvasRect, menuFont);
        BuildSettingsPanel(canvasRect, menuFont);
        BuildDialog(canvasRect, menuFont);

        SetGroupVisible(mainButtonsGroup, true);
        SetGroupVisible(settingsGroup, false);
        SetGroupVisible(dialogGroup, false);
    }

    private void BuildMainButtons(RectTransform canvasRect, TMP_FontAsset menuFont)
    {
        RectTransform column = CreateGroupRoot(canvasRect, "MainButtons", out mainButtonsGroup);

        float x = mode == MenuMode.FirstPlay ? firstPlayButtonsX : 0f;
        float topY = 120f;
        float step = buttonHeight + buttonSpacing;
        Vector2 size = new Vector2(buttonWidth, buttonHeight);

        MenuUIUtility.CreateMenuButton(column, "새로운 게임", menuFont, new Vector2(x, topY), size, OnNewGameClicked);
        Button continueButton = MenuUIUtility.CreateMenuButton(column, "이어서 하기", menuFont, new Vector2(x, topY - step), size, OnContinueClicked);
        MenuUIUtility.CreateMenuButton(column, "설정", menuFont, new Vector2(x, topY - step * 2f), size, OnSettingsClicked);
        MenuUIUtility.CreateMenuButton(column, "게임 종료", menuFont, new Vector2(x, topY - step * 3f), size, OnQuitClicked);

        if (mode == MenuMode.FirstPlay)
        {
            // 기록이 없으면 흐리게 (누르면 카메라는 움직이고 '기록 없음' 안내)
            MenuButtonStyle style = continueButton.GetComponent<MenuButtonStyle>();
            if (style != null)
            {
                style.dim = 0.4f;
            }
        }
    }

    private void BuildSettingsPanel(RectTransform canvasRect, TMP_FontAsset menuFont)
    {
        RectTransform root = CreateGroupRoot(canvasRect, "SettingsPanel", out settingsGroup);
        Vector2 panelSize = new Vector2(760f, 470f);
        RectTransform panel = MenuUIUtility.CreateCenteredPanel(root, "Background", new Vector2(0f, -60f), panelSize,
            new Color(0.05f, 0.05f, 0.06f, 0.88f));

        Color text = MenuSkin.Text;
        MenuUIUtility.CreateHeader(panel, "설정", menuFont, new Vector2(0f, 175f), 560f);

        float labelX = -220f;
        float sliderX = 60f;
        float valueX = 290f;
        Vector2 labelSize = new Vector2(220f, 50f);
        Vector2 sliderSize = new Vector2(320f, 36f);

        // 전체 음량
        MenuUIUtility.CreateLabel(panel, "전체 음량", menuFont, new Vector2(labelX, 90f), labelSize, 28f,
            TextAlignmentOptions.Left, text);
        MenuUIUtility.CreateSlider(panel, new Vector2(sliderX, 90f), sliderSize, 0f, 1f, GameSettings.MasterVolume, v =>
        {
            GameSettings.MasterVolume = v;
            RefreshSettingsLabels();
        });
        volumeValueLabel = MenuUIUtility.CreateLabel(panel, "", menuFont, new Vector2(valueX, 90f), new Vector2(110f, 50f), 26f,
            TextAlignmentOptions.Right, text);

        // 마우스 감도
        MenuUIUtility.CreateLabel(panel, "마우스 감도", menuFont, new Vector2(labelX, 10f), labelSize, 28f,
            TextAlignmentOptions.Left, text);
        MenuUIUtility.CreateSlider(panel, new Vector2(sliderX, 10f), sliderSize, GameSettings.MinSensitivity,
            GameSettings.MaxSensitivity, GameSettings.MouseSensitivity, v =>
            {
                GameSettings.MouseSensitivity = v;
                RefreshSettingsLabels();
            });
        sensitivityValueLabel = MenuUIUtility.CreateLabel(panel, "", menuFont, new Vector2(valueX, 10f), new Vector2(110f, 50f), 26f,
            TextAlignmentOptions.Right, text);

        // 전체 화면
        MenuUIUtility.CreateLabel(panel, "전체 화면", menuFont, new Vector2(labelX, -70f), labelSize, 28f,
            TextAlignmentOptions.Left, text);
        Button fullscreenButton = MenuUIUtility.CreateMenuButton(panel, "", menuFont, new Vector2(sliderX, -70f),
            new Vector2(180f, 48f), () =>
            {
                GameSettings.Fullscreen = !GameSettings.Fullscreen;
                RefreshSettingsLabels(!Screen.fullScreen); // Screen.fullScreen은 다음 프레임에 바뀜
            });
        fullscreenLabel = fullscreenButton.GetComponentInChildren<TextMeshProUGUI>(true);

        MenuUIUtility.CreateMenuButton(panel, "뒤로", menuFont, new Vector2(0f, -175f),
            new Vector2(buttonWidth * 0.7f, buttonHeight * 0.85f), ClosePanels);

        RefreshSettingsLabels();
    }

    private void RefreshSettingsLabels()
    {
        RefreshSettingsLabels(Screen.fullScreen);
    }

    private void RefreshSettingsLabels(bool fullscreen)
    {
        if (volumeValueLabel != null)
        {
            volumeValueLabel.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";
        }

        if (sensitivityValueLabel != null)
        {
            sensitivityValueLabel.text = GameSettings.MouseSensitivity.ToString("0.0") + "x";
        }

        if (fullscreenLabel != null)
        {
            fullscreenLabel.text = fullscreen ? "켜짐" : "꺼짐";
        }
    }

    private void BuildDialog(RectTransform canvasRect, TMP_FontAsset menuFont)
    {
        RectTransform root = CreateGroupRoot(canvasRect, "Dialog", out dialogGroup);
        RectTransform panel = MenuUIUtility.CreateCenteredPanel(root, "Background", new Vector2(0f, -60f),
            new Vector2(640f, 300f), new Color(0.05f, 0.05f, 0.06f, 0.88f));

        dialogMessage = MenuUIUtility.CreateLabel(panel, "", menuFont, new Vector2(0f, 45f), new Vector2(580f, 150f), 30f,
            TextAlignmentOptions.Center, MenuSkin.Text);
        MenuUIUtility.CreateDivider(panel, new Vector2(0f, -38f), 480f);

        Vector2 size = new Vector2(buttonWidth * 0.7f, buttonHeight * 0.85f);
        dialogYesButton = MenuUIUtility.CreateMenuButton(panel, "확인", menuFont, new Vector2(-120f, -80f), size, null);
        dialogNoButton = MenuUIUtility.CreateMenuButton(panel, "취소", menuFont, new Vector2(120f, -80f), size, null);
    }

    private static RectTransform CreateGroupRoot(RectTransform parent, string name, out CanvasGroup group)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        group = go.AddComponent<CanvasGroup>();
        return rect;
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

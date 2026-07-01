using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DeathEndingUI : MonoBehaviour
{
    private static DeathEndingUI instance;
    private static bool subscribedToSceneLoaded;

    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private bool useOnGuiFallback = false;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI reasonText;
    private PlayerConditions playerConditions;
    private string currentReason = "Player Dead";
    private bool isShowing;
    private float previousTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallOnSceneLoad()
    {
        EnsureInstance();

        if (!subscribedToSceneLoaded)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            subscribedToSceneLoaded = true;
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();

        if (instance != null)
            instance.ResetForScene();
    }

    public static void ShowDeathEnding(string reason = "Player Dead")
    {
        EnsureInstance();

        if (instance != null)
            instance.Show(reason);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        DeathEndingUI existing = FindFirstObjectByType<DeathEndingUI>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject host = new GameObject("DeathEndingUI");
        instance = host.AddComponent<DeathEndingUI>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
        ResetForScene();
    }

    private void Update()
    {
        if (!isShowing)
        {
            if (playerConditions == null)
                playerConditions = FindFirstObjectByType<PlayerConditions>();

            if (playerConditions != null && playerConditions.GetCurrentHealth() <= 0f)
                Show("HP 0");

            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
            RestartCurrentScene();

        if (Input.GetKeyDown(KeyCode.Escape))
            LoadMainMenu();
    }

    private void BuildUI()
    {
        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        GameplayHudCanvasSetup.EnsureOverlayCanvas(gameObject);
        canvas.sortingOrder = 1000;

        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        GameObject dim = new GameObject("DeathDim");
        dim.transform.SetParent(transform, false);

        RectTransform dimRect = dim.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.88f);
        dimImage.raycastTarget = true;

        GameObject titleGo = CreateText("DeathTitle", "YOU DIED", 112, new Vector2(0f, 105f));
        TextMeshProUGUI titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.color = new Color(0.85f, 0.05f, 0.05f, 1f);

        GameObject reasonGo = CreateText("DeathReason", "Player Dead", 38, new Vector2(0f, -15f));
        reasonText = reasonGo.GetComponent<TextMeshProUGUI>();
        reasonText.color = Color.white;

        GameObject hintGo = CreateText("DeathHint", "R : Restart    ESC : Main Menu", 32, new Vector2(0f, -90f));
        TextMeshProUGUI hintText = hintGo.GetComponent<TextMeshProUGUI>();
        hintText.color = new Color(1f, 1f, 1f, 0.8f);
    }

    private GameObject CreateText(string objectName, string text, int fontSize, Vector2 anchoredPosition)
    {
        GameObject textGo = new GameObject(objectName);
        textGo.transform.SetParent(transform, false);

        RectTransform rect = textGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(1100f, 150f);

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.font = TMP_Settings.defaultFontAsset;

        return textGo;
    }

    private void Show(string reason)
    {
        if (isShowing)
            return;

        isShowing = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (reasonText != null)
            reasonText.text = string.IsNullOrEmpty(reason) ? "Player Dead" : reason;

        currentReason = string.IsNullOrEmpty(reason) ? "Player Dead" : reason;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        Debug.Log($"Player Dead - DeathEndingUI Show: {currentReason}");
    }

    private void OnGUI()
    {
        if (!useOnGuiFallback)
            return;

        if (!isShowing)
            return;

        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        GUI.color = Color.red;
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 72,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(0f, Screen.height * 0.35f - 60f, Screen.width, 120f), "YOU DIED", titleStyle);

        GUI.color = Color.white;
        GUIStyle reasonStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 26
        };
        GUI.Label(new Rect(0f, Screen.height * 0.5f - 30f, Screen.width, 60f), currentReason, reasonStyle);

        GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22
        };
        GUI.Label(new Rect(0f, Screen.height * 0.6f - 30f, Screen.width, 60f), "R : Restart    ESC : Main Menu", hintStyle);

        GUI.color = Color.white;
    }

    private void RestartCurrentScene()
    {
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void LoadMainMenu()
    {
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;

        if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    private void ResetForScene()
    {
        playerConditions = FindFirstObjectByType<PlayerConditions>();
        isShowing = false;

        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}

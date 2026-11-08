using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프롤로그에서 죽은 뒤 허브(귀문의 방)에서 깨어나는 연출 (HubArrival.FromPrologue일 때만).
/// 바닥에 누운 시점 → 눈꺼풀이 몇 번 깜빡이며 뜸 → "허억!" → 상체를 일으켜 앉음 → 휘청이며 일어섬 → 주위를 둘러봄 → 조작 시작.
/// 눈꺼풀은 화면 위·아래 검은 막(자체 오버레이 캔버스). 대사는 자막. 끝나면 Finished 이벤트(HubStoryDirector가 진행 시작).
/// </summary>
public class HubAwakening : MonoBehaviour
{
    [Tooltip("에디터에서 허브 씬을 바로 실행해도 깨어나는 연출을 볼지 (테스트용)")]
    [SerializeField] private bool playWhenStartedDirectly = true;

    [Tooltip("컷씬 동안 숨길 HUD 등")]
    [SerializeField] private GameObject[] hideDuringWake = { };

    [Header("Timing (초)")]
    [SerializeField] private float blackSeconds = 1.6f;
    [SerializeField] private float lyingSeconds = 2.6f;
    [SerializeField] private float sitUpSeconds = 1.6f;
    [SerializeField] private float sitSeconds = 2.4f;
    [SerializeField] private float standSeconds = 1.8f;
    [SerializeField] private float lookAroundSeconds = 3.6f;

    [Header("Pose")]
    [SerializeField] private float lyingEyeHeight = 0.25f;
    [SerializeField] private float sittingEyeHeight = 0.95f;
    [Tooltip("누워서 천장을 볼 때 (x=위로 젖힘, z=고개 기울임)")]
    [SerializeField] private Vector2 lyingPitchRoll = new Vector2(-72f, 16f);
    [Tooltip("일어선 뒤 둘러보는 좌우 각도")]
    [SerializeField] private float lookAroundYaw = 65f; // 작은 방 — 양옆 벽의 문이 각각 약 60° 떨어져 있음

    [Header("Text")]
    [SerializeField] private string gaspLine = "……허억!";
    [SerializeField] private string aliveLine = "…살아 있어? 분명 그 간호사한테…";
    [SerializeField] private string whereLine = "여긴… 어디지? 병원은 아닌 것 같은데.";

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip gaspClip;
    [SerializeField] private AudioClip standUpClip;

    public event Action Finished;

    // 이번 실행에서 로드된 씬 수 — 1이면 허브를 바로 실행한 것
    private static int scenesLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCounter()
    {
        scenesLoaded = 0;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= CountScene;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += CountScene;
    }

    private static void CountScene(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single)
        {
            scenesLoaded++;
        }
    }

    public bool IsRunning { get; private set; }
    public bool HasFinished { get; private set; }

    private GameObject player;
    private Camera cam;
    private Vector3 camRestLocal;
    private RectTransform lidTop;
    private RectTransform lidBottom;
    private Image black;
    private GameObject overlay;

    private void Start()
    {
        bool fromPrologue = HubArrival.FromPrologue;
        HubArrival.FromPrologue = false;
        bool startedDirectly = Application.isEditor && playWhenStartedDirectly && scenesLoaded <= 1; // 에디터에서 허브를 바로 실행
        if (!fromPrologue && !startedDirectly)
        {
            HasFinished = true;
            Finished?.Invoke();
            return;
        }

        StartCoroutine(Run());
    }

    private static void Log(string message)
    {
        Debug.Log("[Hub] " + message);
    }

    private IEnumerator Run()
    {
        IsRunning = true;
        Log("깨어남 연출 시작");
        player = GameObject.FindWithTag("Player");
        cam = player.GetComponentInChildren<Camera>();
        camRestLocal = cam.transform.localPosition;
        PlayerControlLock controlLock = PlayerControlLock.Lock(player);
        foreach (GameObject go in hideDuringWake)
        {
            if (go != null)
            {
                go.SetActive(false);
            }
        }

        BuildOverlay();
        float yaw = player.transform.eulerAngles.y;
        SetView(yaw, lyingEyeHeight, lyingPitchRoll.x, lyingPitchRoll.y);
        SetLids(1f);
        black.color = Color.black;

        yield return new WaitForSeconds(blackSeconds);
        black.color = Color.clear;

        // 눈꺼풀: 살짝 → 감김 → 조금 더 → 감김 → 뜸 (허억과 함께 확 뜸)
        yield return Lids(1f, 0.82f, 0.35f);
        yield return Lids(0.82f, 1f, 0.25f);
        yield return new WaitForSeconds(0.45f);
        Play(gaspClip);
        SubtitleUI.ShowForDuration("나", gaspLine, 1.6f);
        yield return Lids(1f, 0.15f, 0.12f);
        yield return Lids(0.15f, 0.55f, 0.18f);
        yield return Lids(0.55f, 0f, 0.5f);

        // 누운 채로 숨을 고름 (천장을 보며 가쁘게 흔들림)
        for (float t = 0f; t < lyingSeconds; t += Time.deltaTime)
        {
            float breath = Mathf.Sin(t * 5.5f) * 1.2f * (1f - t / lyingSeconds * 0.5f);
            SetView(yaw, lyingEyeHeight + breath * 0.01f, lyingPitchRoll.x + breath, lyingPitchRoll.y);
            yield return null;
        }

        // 상체를 일으켜 앉음
        SubtitleUI.ShowForDuration("나", aliveLine, sitSeconds + sitUpSeconds);
        for (float t = 0f; t < sitUpSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / sitUpSeconds);
            SetView(yaw, Mathf.Lerp(lyingEyeHeight, sittingEyeHeight, k), Mathf.Lerp(lyingPitchRoll.x, 18f, k), Mathf.Lerp(lyingPitchRoll.y, 3f, k));
            yield return null;
        }

        // 앉아서 손바닥을 내려다보듯 고개 숙였다가 듦
        for (float t = 0f; t < sitSeconds; t += Time.deltaTime)
        {
            float k = t / sitSeconds;
            float pitch = Mathf.Lerp(18f, 32f, Mathf.Sin(k * Mathf.PI));
            SetView(yaw, sittingEyeHeight, pitch, 3f * (1f - k));
            yield return null;
        }

        // 휘청이며 일어섬
        Play(standUpClip);
        for (float t = 0f; t < standSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / standSeconds);
            float wobble = Mathf.Sin(t * 7f) * (1f - k) * 4f;
            SetView(yaw, Mathf.Lerp(sittingEyeHeight, camRestLocal.y, k), Mathf.Lerp(18f, 2f, k), wobble);
            yield return null;
        }

        // 둘러봄: 왼쪽 → 오른쪽 → 정면 (문 세 개가 눈에 들어옴)
        SubtitleUI.ShowForDuration("나", whereLine, lookAroundSeconds);
        for (float t = 0f; t < lookAroundSeconds; t += Time.deltaTime)
        {
            float k = t / lookAroundSeconds;
            float offset = k < 0.35f ? Mathf.Lerp(0f, -lookAroundYaw, Mathf.SmoothStep(0f, 1f, k / 0.35f))
                : k < 0.75f ? Mathf.Lerp(-lookAroundYaw, lookAroundYaw, Mathf.SmoothStep(0f, 1f, (k - 0.35f) / 0.4f))
                : Mathf.Lerp(lookAroundYaw, 0f, Mathf.SmoothStep(0f, 1f, (k - 0.75f) / 0.25f));
            SetView(yaw + offset, camRestLocal.y, 2f, 0f);
            yield return null;
        }

        SetView(yaw, camRestLocal.y, 2f, 0f);
        cam.transform.localPosition = camRestLocal;
        PlayerControlLock.SyncMouseLookPitch(cam, 2f);
        controlLock.Release();
        foreach (GameObject go in hideDuringWake)
        {
            if (go != null)
            {
                go.SetActive(true);
            }
        }

        Destroy(overlay);
        IsRunning = false;
        HasFinished = true;
        Log("깨어남 끝 → 조작 시작");
        Finished?.Invoke();
    }

    private void SetView(float yaw, float eyeHeight, float pitch, float roll)
    {
        player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cam.transform.localPosition = new Vector3(camRestLocal.x, eyeHeight, camRestLocal.z * (eyeHeight / Mathf.Max(0.01f, camRestLocal.y)));
        cam.transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
    }

    private IEnumerator Lids(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            SetLids(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }

        SetLids(to);
    }

    // 0=다 뜸, 1=다 감김 — 위·아래 막이 각각 화면 절반까지
    private void SetLids(float closed)
    {
        float h = Mathf.Clamp01(closed) * 0.5f;
        lidTop.anchorMin = new Vector2(0f, 1f - h);
        lidBottom.anchorMax = new Vector2(1f, h);
    }

    private void BuildOverlay()
    {
        overlay = new GameObject("HubAwakening_Overlay");
        Canvas canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 640; // 자막(700) 아래
        lidTop = Panel(overlay.transform, "LidTop", new Vector2(0f, 0.5f), Vector2.one);
        lidBottom = Panel(overlay.transform, "LidBottom", Vector2.zero, new Vector2(1f, 0.5f));
        black = Panel(overlay.transform, "Black", Vector2.zero, Vector2.one).GetComponent<Image>();
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return rect;
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}

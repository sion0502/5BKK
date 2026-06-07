using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class AbyssSinker : MonoBehaviour
{
    [Header("Scene")]
    public string nextSceneName = "Chapter1";

    [Header("Abyss Fall")]
    public float waitTime = 0.6f;
    public float fallSpeed = 18f;
    public float fallAcceleration = 12f;
    public float totalDuration = 5f;
    [Tooltip("이 거리(m)만큼 떨어진 뒤 Chapter1 In 샤프트 메시를 숨깁니다.")]
    public float shaftHideFallDistance = 6f;
    [Tooltip("이 거리(m)만큼 떨어진 뒤 천천히 위를 올려다봅니다.")]
    public float lookUpStartFallDistance = 10f;
    [Tooltip("위를 올려다볼 때 초당 최대 pitch 회전 각도.")]
    public float lookUpMaxDegreesPerSecond = 18f;
    [Tooltip("올려다보기 pitch 목표(음수 = 위). 진입 시선보다 위쪽으로만 회전합니다.")]
    [Range(-89f, 0f)]
    public float maxLookUpPitch = -78f;
    [Tooltip("추락 시작 시 숨길 심연 샤프트 메시 (Chapter1 In). 콜라이더는 유지됩니다.")]
    public Transform shaftVisualRoot;
    [Tooltip("낙하 진행률(0~1) 중 이 값 이후에만 화면 페이드가 시작됩니다.")]
    [Range(0f, 0.95f)]
    public float fadeStartProgress = 0.6f;
    public AnimationCurve fallFadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Chapter Intro")]
    public string chapterTitle = "폐병원";
    public Image fadeImage;
    public float titleFontSize = 156f;
    public float titleFadeMinAlpha = 0.42f;
    public float titleFadePeriod = 2.8f;
    public float titleExitFadeDuration = 1.2f;

    [Header("Chapter Intro Sound")]
    public AudioClip titleAppearSound;
    [Range(0f, 1f)]
    public float titleAppearVolume = 1f;

    private bool isWorking;

    private void OnTriggerEnter(Collider other)
    {
        if (isWorking || !other.CompareTag("Player"))
        {
            return;
        }

        isWorking = true;
        AbyssFallSequenceHost host = other.GetComponent<AbyssFallSequenceHost>();
        if (host == null)
        {
            host = other.gameObject.AddComponent<AbyssFallSequenceHost>();
        }

        host.StartSequence(this, other.gameObject);
    }

    internal IEnumerator RunSequenceOnPlayer(GameObject player)
    {
        Transform cameraTransform = ResolvePlayerCameraTransform(player);
        bool hasEntryCamera = cameraTransform != null;
        Quaternion entryCameraLocalRotation = hasEntryCamera
            ? cameraTransform.localRotation
            : Quaternion.identity;
        Quaternion entryPlayerRotation = player.transform.rotation;

        DisablePlayerControl(player);

        Image activeFade = ScreenFader.Prepare(fadeImage);
        ScreenFader.SetAlpha(activeFade, 0f);

        HubAbyssFallConfig fallConfig = new HubAbyssFallConfig
        {
            WaitTime = waitTime,
            FallSpeed = fallSpeed,
            FallAcceleration = fallAcceleration,
            FallDuration = Mathf.Max(totalDuration - waitTime, 0.01f),
            LookUpMaxDegreesPerSecond = lookUpMaxDegreesPerSecond,
            MaxLookUpPitch = maxLookUpPitch,
            FadeStartProgress = fadeStartProgress,
            ShaftHideFallDistance = shaftHideFallDistance,
            LookUpStartFallDistance = lookUpStartFallDistance,
            ShaftVisualRoot = ResolveShaftVisualRoot(),
            HasEntryCameraState = hasEntryCamera,
            EntryCameraLocalRotation = entryCameraLocalRotation,
            EntryPlayerRotation = entryPlayerRotation,
            FallFadeCurve = fallFadeCurve,
            FadeImage = activeFade
        };

        yield return HubAbyssFallSequence.Run(player, fallConfig);

        ChapterTitleTransitionConfig titleConfig = new ChapterTitleTransitionConfig
        {
            Title = chapterTitle,
            SceneName = nextSceneName,
            FadeDuration = 0f,
            FadeCurve = fallFadeCurve,
            TitleFontSize = titleFontSize,
            TitleFadeMinAlpha = titleFadeMinAlpha,
            TitleFadePeriod = titleFadePeriod,
            TitleExitFadeDuration = titleExitFadeDuration,
            TitleAppearSound = titleAppearSound,
            TitleAppearVolume = titleAppearVolume,
            SkipInitialFade = true
        };

        yield return ChapterTitleTransition.Run(activeFade, titleConfig);
    }

    private void DisablePlayerControl(GameObject player)
    {
        MonoBehaviour[] scripts = player.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null || script is AbyssFallSequenceHost)
            {
                continue;
            }

            script.enabled = false;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }
    }

    private Transform ResolveShaftVisualRoot()
    {
        if (shaftVisualRoot != null)
        {
            return shaftVisualRoot;
        }

        Transform parent = transform.parent;
        if (parent != null && parent.name.Contains("Chapter1"))
        {
            return parent;
        }

        GameObject shaft = GameObject.Find("Chapter1 In");
        return shaft != null ? shaft.transform : null;
    }

    private static Transform ResolvePlayerCameraTransform(GameObject player)
    {
        Camera camera = player.GetComponentInChildren<Camera>(true);
        return camera != null ? camera.transform : null;
    }
}

internal class AbyssFallSequenceHost : MonoBehaviour
{
    private bool isRunning;

    public void StartSequence(AbyssSinker sinker, GameObject player)
    {
        if (isRunning || sinker == null || player == null)
        {
            return;
        }

        isRunning = true;
        StartCoroutine(Run(sinker, player));
    }

    private IEnumerator Run(AbyssSinker sinker, GameObject player)
    {
        yield return sinker.RunSequenceOnPlayer(player);
        Destroy(this);
    }
}

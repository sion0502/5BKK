using System.Reflection;
using UnityEngine;

/// <summary>
/// 제노(Xenosaurid) 데스씬 효과음 (사운드 전용) — XenoDeathScene.
///  · 제노에게 붙잡혀 점프스케어(데스씬)가 시작되는 순간 재생.
///  · 데스씬이 끝나고 게임오버 화면(치지직)이 뜰 때는 소리가 더 나오지 않게 딱 끝냄.
///    끝나기 직전 fadeOutSeconds 동안 자연스럽게 줄이고, 게임오버로 넘어가는 순간 확실히 정지.
///  · 소리는 제노 입(머리 뼈)에서 3D로 나옴. 거리 감쇠·벽 너머·잔향·방향성은 <see cref="SpatialSoundFx"/>가 처리.
/// 프리팹·씬에 붙일 필요 없이 게임 시작 때 스스로 생성됨 (게임 로직은 읽기만 함).
/// 소리 파일은 Assets/Resources/XenoDeathScene_Mono.wav 에서 불러옴.
/// </summary>
[DisallowMultipleComponent]
public class XenosauridDeathSceneAudio : MonoBehaviour
{
    private const string ClipName = "XenoDeathRoar2";
    private const float Volume = 1f;
    private const float FadeOutSeconds = 0.1f;
    private const float FadeInSeconds = 0.03f;
    private const float FallbackSceneSeconds = 1.87f; // 몸돌림 0.14 + 아가리 0.85 + 덮침 0.13 + 쓰러짐 0.75

    private static XenosauridDeathSceneAudio instance;

    private AudioClip clip;
    private AudioSource source;
    private GameObject sourceObject;
    private bool playing;
    private bool loggedCatcher;
    private bool consumed; // 이번 사망 연출에서는 한 번만 재생
    private AudioReverbFilter reverb;
    private int debugFrames;
    private float lastLog;
    private float startTime;
    private float sceneSeconds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject go = new GameObject(nameof(XenosauridDeathSceneAudio));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<XenosauridDeathSceneAudio>();
        Debug.Log("[XenoDeathScene] ready");
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        EnemyBase catcher = PlayerDeathDebug.LastCatcher;
        bool mine = PlayerDeathDebug.IsDying && catcher != null && catcher.GetComponent<XenosauridController>() != null;

        if (!playing)
        {
            if (PlayerDeathDebug.IsDying && !loggedCatcher)
            {
                loggedCatcher = true;
                Debug.Log("[XenoDeathScene] death scene start, catcher=" + (catcher != null ? catcher.name : "null") + ", xeno=" + mine);
            }
            else if (!PlayerDeathDebug.IsDying)
            {
                loggedCatcher = false;
            }

            if (!PlayerDeathDebug.IsDying)
            {
                consumed = false;
            }

            if (mine && !PlayerDeathDebug.IsDead && !consumed)
            {
                consumed = true;
                Begin(catcher);
            }

            return;
        }

        // 게임오버(치지직)로 넘어갔거나 되살아나서 데스씬이 끝남 → 즉시 정지
        if (PlayerDeathDebug.IsDead || !mine || source == null)
        {
            Halt();
            return;
        }

        float elapsed = Time.unscaledTime - startTime; // 데스씬은 timeScale 0에서 실제 시간으로 흐름
        float fadeIn = Mathf.Clamp01(elapsed / FadeInSeconds);
        float remain = sceneSeconds - elapsed;
        float fadeOut = Mathf.Clamp01(remain / FadeOutSeconds);
        float k = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, sceneSeconds));
        float swell = Mathf.Lerp(0.35f, 1f, k * k);   // 처음엔 낮게 → 끝에서 확 터지게 (집어삼키는 "화아아악")
        source.pitch = Mathf.Lerp(0.95f, 1.15f, k * k); // 끝으로 갈수록 소리가 솟구침
        if (reverb != null)
        {
            reverb.reverbLevel = Mathf.Lerp(300f, 1500f, k); // 끝에서 잔향도 크게 울림
        }

        source.volume = Volume * swell * fadeIn * fadeOut; // 맨 끝 0.3초만 부드럽게 줄임

        if (Time.unscaledTime - lastLog >= 0.2f)
        {
            lastLog = Time.unscaledTime;
            debugFrames++;
            float[] buf = new float[256];
            source.GetOutputData(buf, 0);
            float sum = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                sum += buf[i] * buf[i];
            }

            Debug.Log("[XenoDeathScene] rms=" + Mathf.Sqrt(sum / buf.Length).ToString("0.000") + " listenerVol=" + AudioListener.volume
                + " listenerPause=" + AudioListener.pause + " timeScale=" + Time.timeScale + " t=" + elapsed.ToString("0.00"));
            Debug.Log("[XenoDeathScene] frame " + debugFrames + " isPlaying=" + source.isPlaying + " vol=" + source.volume.ToString("0.00")
                + " time=" + source.time.ToString("0.00") + " remain=" + remain.ToString("0.00")
                + " active=" + source.gameObject.activeInHierarchy + " parent=" + source.transform.parent.name
                + " dist=" + Vector3.Distance(source.transform.position, Camera.main != null ? Camera.main.transform.position : Vector3.zero).ToString("0.00"));
        }

        if (remain <= 0f)
        {
            Halt(); // 데스씬 끝 — 더 이상 소리 없음
        }
    }

    private void Begin(EnemyBase catcher)
    {
        if (clip == null)
        {
            clip = Resources.Load<AudioClip>(ClipName);
        }

        if (clip == null)
        {
            Debug.LogWarning("[XenoDeathScene] Resources/" + ClipName + " 를 찾지 못함");
            return;
        }

        Transform mouth = FindHead(catcher.transform);
        sourceObject = new GameObject("XenoDeathSceneSound");
        sourceObject.transform.SetParent(transform, false); // 괴물 뼈가 아닌 항상 켜진 오브젝트 위에서 재생 (점프스케어 2D 소리와 같은 방식)
        source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.clip = clip;
        source.volume = 0f;
        // 4가지 규칙을 timeScale 0(데스씬)에서도 확실히 동작하도록 직접 설정
        source.spatialBlend = 0f;                       // 데스씬은 카메라 바로 앞(입 클로즈업)이라 2D로 확실히 들리게
        source.priority = 0;
        source.spread = 0f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;   // 거리 감쇠
        source.minDistance = 2.5f;
        source.maxDistance = 30f;
        source.ignoreListenerPause = true;
        AudioLowPassFilter lp = sourceObject.AddComponent<AudioLowPassFilter>(); // 벽 너머 먹먹함(데스씬은 바로 앞이라 거의 열림)
        lp.cutoffFrequency = 12000f;
        AudioReverbFilter rv = sourceObject.AddComponent<AudioReverbFilter>();
        reverb = rv;   // 공간 잔향
        rv.reverbPreset = AudioReverbPreset.User;     // 크게 울리는 공간 (동굴처럼)
        rv.dryLevel = 0f;
        rv.room = -200f;
        rv.roomHF = -600f;
        rv.decayTime = 3.5f;
        rv.reflectionsLevel = -500f;
        rv.reverbLevel = 900f;
        rv.diffusion = 100f;
        rv.density = 100f;

        playing = true;
        debugFrames = 0;
        startTime = Time.unscaledTime;
        sceneSeconds = ReadSceneSeconds(catcher);
        source.Play();
        Debug.Log("[XenoDeathScene] play (" + sceneSeconds.ToString("0.00") + "s) clip=" + clip.name + " len=" + clip.length.ToString("0.00")
            + " load=" + clip.loadState + " mouth=" + mouth.name + " active=" + mouth.gameObject.activeInHierarchy);
    }

    private void Halt()
    {
        Debug.Log("[XenoDeathScene] halt at t=" + (Time.unscaledTime - startTime).ToString("0.00") + " dead=" + PlayerDeathDebug.IsDead + " dying=" + PlayerDeathDebug.IsDying);
        playing = false;
        if (source != null)
        {
            source.volume = 0f;
            source.Stop();
        }

        if (sourceObject != null)
        {
            Destroy(sourceObject);
        }

        source = null;
        sourceObject = null;
    }

    private static Transform FindHead(Transform root)
    {
        Transform found = null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(false))
        {
            string n = t.name.ToLowerInvariant();
            if (!n.EndsWith("head"))
            {
                continue;
            }

            if (t.GetComponent<Renderer>() == null)
            {
                return t; // 렌더러가 아닌 뼈(Transform)를 우선
            }

            if (found == null)
            {
                found = t;
            }
        }

        return found != null ? found : root;
    }

    // 점프스케어 연출 길이를 XenosauridJumpscare의 실제 값에서 읽음 (바뀌어도 맞게)
    private static float ReadSceneSeconds(EnemyBase catcher)
    {
        XenosauridJumpscare scare = catcher.GetComponent<XenosauridJumpscare>();
        if (scare == null)
        {
            return FallbackSceneSeconds;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        float rear = Read(scare, "rearSeconds", flags, 0.85f);
        float lunge = Read(scare, "lungeSeconds", flags, 0.13f);
        float pinned = Read(scare, "pinnedSeconds", flags, 0.75f);
        return 0.14f + rear + lunge + pinned;
    }

    private static float Read(object target, string field, BindingFlags flags, float fallback)
    {
        FieldInfo f = target.GetType().GetField(field, flags);
        if (f != null && f.GetValue(target) is float v)
        {
            return v;
        }

        return fallback;
    }
}

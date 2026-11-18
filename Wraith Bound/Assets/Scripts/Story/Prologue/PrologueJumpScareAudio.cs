using System.Collections;
using UnityEngine;

/// <summary>
/// 프롤로그 마지막, 간호사가 달려드는 순간의 점프스케어 소리 (사운드 전용).
///  · 간호사가 돌진 애니메이션(State 2)으로 바뀌는 순간(= 달려드는 시작) 정확히 재생.
///  · 소리를 씬과 따로(DontDestroyOnLoad) 틀어서, 허브로 넘어가는 암전 중에도 끊기지 않고
///    울림(잔향 꼬리)이 이어지다가 서서히 사라짐. 씬이 바뀌어도 갑자기 뚝 끊기지 않음.
/// PrologueDirector와 같은 오브젝트에 붙입니다. 연출·AI 코드는 읽기만 합니다.
/// </summary>
public class PrologueJumpScareAudio : MonoBehaviour
{
    [SerializeField] private PrologueFinaleCutscene finale;
    [SerializeField] private PrologueNurse nurse;
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.65f;
    [Tooltip("재생 시작 위치 — 클립 앞의 짧은 무음을 건너뜀")]
    [SerializeField] private float startTime = 0f;
    [Tooltip("이 시점(클립 시간, 초)부터 서서히 줄어들기 시작해서 끝까지 이어짐 — 크게 울리다가 자연스럽게 사라짐")]
    [SerializeField] private float fadeStartTime = 1.6f;

    private static readonly int StateId = Animator.StringToHash("State");
    private bool played;

    private void Update()
    {
        if (played || clip == null || finale == null || nurse == null || !finale.IsRunning)
        {
            return;
        }

        Animator anim = nurse.Animator;
        if (anim == null || anim.GetInteger(StateId) != 2)
        {
            return;
        }

        played = true;
        GameObject go = new GameObject("PrologueJumpScare_Persistent");
        DontDestroyOnLoad(go);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.clip = clip;
        source.time = Mathf.Clamp(startTime, 0f, clip.length - 0.01f);
        source.volume = volume;
        source.ignoreListenerPause = true;
        source.Play();
        go.AddComponent<Fader>().Begin(source, volume, fadeStartTime - startTime);
    }

    private class Fader : MonoBehaviour
    {
        private AudioSource source;
        private float volume;
        private float fadeAt;

        public void Begin(AudioSource s, float v, float fadeStartSeconds)
        {
            source = s;
            volume = v;
            fadeAt = Mathf.Max(0f, fadeStartSeconds);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float t = 0f;
            float length = Mathf.Max(0.1f, source.clip.length - source.time);
            while (source != null && source.isPlaying)
            {
                t += Time.unscaledDeltaTime;
                if (t > fadeAt)
                {
                    float k = Mathf.Clamp01((t - fadeAt) / Mathf.Max(0.1f, length - fadeAt));
                    source.volume = volume * (1f - k) * (1f - k); // 끝으로 갈수록 천천히 가라앉는 곡선
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}

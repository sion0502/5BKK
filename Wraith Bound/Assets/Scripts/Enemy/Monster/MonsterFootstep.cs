using System;
using UnityEngine;

public class MonsterFootstep : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] walkClips;
    [SerializeField] private AudioClip[] runClips;
    [SerializeField] private float walkVolume = 0.7f;
    [SerializeField] private float runVolume = 0.95f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Header("Fallback Auto Step")]
    [SerializeField] private bool useAutoStep = false;
    [SerializeField] private float walkStepInterval = 0.55f;
    [SerializeField] private float runStepInterval = 0.33f;
    [SerializeField] private float minMoveSpeed = 0.15f;

    private float speed;
    private bool isRunning;
    private float timer;

    private void Reset()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (!useAutoStep) return;
        if (audioSource == null) return;
        if (speed < minMoveSpeed)
        {
            // 멈춤 또는 느린 속도면 타이머를 초기화
            timer = 0f;
            return;
        }

        // 타이머 감소
        timer -= Time.deltaTime;

        // 간격은 상태에 따라 결정
        float interval = isRunning ? runStepInterval : walkStepInterval;

        // 속도에 비례한 간격 보정 (선호도에 따라 조정)
        interval = interval * Mathf.Clamp01(speed / (minMoveSpeed * 2f));

        if (timer <= 0f)
        {
            timer = interval;
            PlayStep(isRunning);
        }
    }

    public void SetMoveState(float currentSpeed, bool running)
    {
        speed = currentSpeed;
        isRunning = running;
    }

    public void PlayWalkStep()
    {
        PlayStep(false);
    }

    public void PlayRunStep()
    {
        PlayStep(true);
    }

    private void PlayStep(bool running)
    {
        if (audioSource == null) return;

        AudioClip[] clips = goingToArray(running);
        if (clips == null || clips.Length == 0) return;

        int idx = UnityEngine.Random.Range(0, clips.Length);
        AudioClip clip = clips[idx];

        float vol = running ? runVolume : walkVolume;
        float pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        audioSource.pitch = Mathf.Clamp(pitch, 0.5f, 2.0f);

        audioSource.PlayOneShot(clip, vol);
    }

    // 헬퍼: 런/워크 클립 배열 얻기
    private AudioClip[] goingToArray(bool running)
    {
        if (running)
            return runClips;
        else
            return walkClips;
    }
}

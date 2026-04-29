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
        if (speed < minMoveSpeed) return;

        timer -= Time.deltaTime;
        float interval = isRunning ? runStepInterval : walkStepInterval;

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

        AudioClip[] clips = running ? runClips : walkClips;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSource.PlayOneShot(clip, running ? runVolume : walkVolume);
    }
}

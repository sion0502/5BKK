using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Xenosaurid 전용. 소리로 추격이 시작되면 Taunt를 재생한 뒤 Run으로 넘기고,
/// 사망 연출에서는 숙인 허리/목을 펴서 얼굴이 카메라에 보이게 한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyBase))]
public class XenosauridController : MonoBehaviour
{
    [Header("추격 전 Taunt")]
    [SerializeField] string tauntState = "Base Layer.Taunt";
    [SerializeField] string runState = "Base Layer.Run 0";
    [SerializeField] string runFallbackState = "Base Layer.Run";
    [SerializeField] float tauntBlend = 0.12f;
    [SerializeField] float runBlend = 0.22f;
    [SerializeField] float tauntEndNormalized = 0.92f;
    [SerializeField] float faceTurnSpeed = 240f;

    [Header("사망 연출 — 허리 펴기")]
    [SerializeField] string headBoneName = "Xenosaurid_ Head";
    [SerializeField] string[] straightenBones =
    {
        "Xenosaurid_ Spine",
        "Xenosaurid_ Spine1",
        "Xenosaurid_ Neck",
        "Xenosaurid_ Neck1",
        "Xenosaurid_ Neck2",
        "Xenosaurid_ Head"
    };
    [SerializeField] float[] straightenDegrees = { 42f, 48f, 22f, 20f, 18f, 16f };
    [SerializeField] Vector3 localStraightenAxis = new Vector3(0f, 0f, 1f);
    [SerializeField] float faceRaise = 1.25f;
    [SerializeField] float deathFaceDistance = 0.58f;

    [Header("Taunt 비명")]
    [SerializeField] AudioSource tauntScream;
    [SerializeField] float screamFadeTime = 1.2f;

    [Header("추적 비명")]
    [SerializeField] AudioClip chaseScream;
    [SerializeField] float chaseScreamFadeIn = 0.45f;
    [SerializeField] float chaseScreamFadeOut = 0.9f;
    [SerializeField] float chaseScreamPitchMin = 0.62f;
    [SerializeField] float chaseScreamPitchMax = 1.55f;

    [Header("추적 중 입")]
    [SerializeField] float chaseJawOpen = 55f;
    [SerializeField] float jawOpenSpeed = 280f;

    [Header("발소리 — AudioSource 클립 하나만")]
    [SerializeField] AudioSource footstepSource;
    [SerializeField] float walkVolume = 0.75f;
    [SerializeField] float runVolume = 1f;
    [SerializeField] float walkPitch = 0.9f;
    [SerializeField] float runPitch = 1.05f;
    [SerializeField] float[] stepMarks = { 0.08f, 0.58f };

    EnemyBase _enemy;
    NavMeshAgent _agent;
    Animator _anim;
    Transform _head;
    Transform _jaw;
    Transform _tongue;

    bool _taunting;
    bool _roaredThisChase;
    float _tauntTimer;
    EnemyBase.State _lastState = EnemyBase.State.Patrol;
    int _tauntHash;
    int _runHash;
    float _stepTime = -1f;
    float _screamVolume = 1f;
    AudioSource _chaseVoice;
    AudioSource _chaseVoiceB;
    bool _chaseUsingB;
    bool _chaseOverlapStarted;
    float _chasePitch = 1f;
    float _jawOpen;
    int _attackHash;

    /// <summary>
    /// 시야가 없어 소리로만 쫓으므로, 발소리를 감지한 순간이 아니라 Taunt를 마치고 실제로 달려들 때를 추격으로 본다.
    /// PlayerChaseStateLink가 추격 연출을 켤 때 사용.
    /// </summary>
    public bool IsChasingAfterRoar =>
        _roaredThisChase && !_taunting && _enemy != null &&
        _enemy.RuntimeState.CurrentState == EnemyBase.State.Chase;

    void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponentInChildren<Animator>();
        _tauntHash = Animator.StringToHash(tauntState);
        _runHash = Animator.StringToHash(runState);
        _attackHash = Animator.StringToHash("Base Layer.Attack");
        CacheBones();
        if (_enemy != null && _head != null)
            _enemy.jumpscareFacePoint = _head;

        if (footstepSource == null)
            footstepSource = GetComponent<AudioSource>();
        if (footstepSource != null)
        {
            footstepSource.playOnAwake = false;
            footstepSource.spatialBlend = 1f;
            footstepSource.rolloffMode = AudioRolloffMode.Linear;
            footstepSource.minDistance = 2f;
            footstepSource.maxDistance = 40f;
        }

        PrepareVoice(tauntScream);
        PrepareChaseVoice();
    }

    void PrepareChaseVoice()
    {
        if (chaseScream == null)
            return;

        _chaseVoice = gameObject.AddComponent<AudioSource>();
        _chaseVoiceB = gameObject.AddComponent<AudioSource>();
        PrepareVoice(_chaseVoice);
        PrepareVoice(_chaseVoiceB);
        _chaseVoice.clip = chaseScream;
        _chaseVoiceB.clip = chaseScream;
        _chaseVoice.volume = 0f;
        _chaseVoiceB.volume = 0f;
    }

    static void PrepareVoice(AudioSource source)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 40f;
    }

    void LateUpdate()
    {
        if (_enemy == null)
            return;

        if (PlayerDeathDebug.IsDying && _enemy.RuntimeState.PlayerDeadLogged)
        {
            PresentDeathFace();
            TickChaseScream(false, true);
            return;
        }

        if (_taunting)
        {
            HoldTaunt();
            if (TauntFinished())
                FinishTaunt();
            TickChaseScream(false, false);
            PoseJaw(false);
            return;
        }

        EnemyBase.State state = _enemy.RuntimeState.CurrentState;
        if (state != EnemyBase.State.Chase)
            _roaredThisChase = false;
        if (state == EnemyBase.State.Chase && _lastState != EnemyBase.State.Chase)
        {
            if (HeardOnly())
                BeginTaunt();
            else
                _roaredThisChase = true; // Taunt 없이 바로 쫓기 시작
        }

        _lastState = _enemy.RuntimeState.CurrentState;
        bool doorAttack = InDoorAttack();
        bool chasing = state == EnemyBase.State.Chase && !_taunting && !doorAttack;
        if (doorAttack)
            StopDoorAttackSounds();
        TickChaseScream(chasing, doorAttack);
        if (!doorAttack)
            TickFootsteps();
        PoseJaw(chasing && IsChaseRun());
    }

    bool IsChaseRun()
    {
        if (_anim == null || _enemy == null)
            return false;

        if (_anim.GetInteger(_enemy.AnimStateHash) == 2)
            return true;

        AnimatorStateInfo info = _anim.GetCurrentAnimatorStateInfo(0);
        return info.fullPathHash == _runHash || info.IsName("Run") || info.IsName("Run 0");
    }

    void StopDoorAttackSounds()
    {
        if (tauntScream != null && tauntScream.isPlaying)
            tauntScream.Stop();
        if (footstepSource != null && footstepSource.isPlaying)
            footstepSource.Stop();
    }

    bool InDoorAttack()
    {
        if (_anim == null || _enemy == null || _taunting || !_enemy.RuntimeState.IsBusy)
            return false;

        AnimatorStateInfo info = _anim.GetCurrentAnimatorStateInfo(0);
        if (info.fullPathHash == _attackHash || info.IsName("Attack"))
            return true;

        return _anim.GetInteger(_enemy.AnimStateHash) != 2;
    }

    void PoseJaw(bool open)
    {
        if (_jaw == null)
            return;

        float target = open ? chaseJawOpen : 0f;
        _jawOpen = Mathf.MoveTowards(_jawOpen, target, jawOpenSpeed * Time.deltaTime);
        if (_jawOpen <= 0.01f)
            return;

        _jaw.localRotation *= Quaternion.AngleAxis(_jawOpen, Vector3.forward);
    }

    void TickChaseScream(bool chasing, bool cut)
    {
        if (_chaseVoice == null || _chaseVoiceB == null || chaseScream == null)
            return;

        AudioSource lead = _chaseUsingB ? _chaseVoiceB : _chaseVoice;
        AudioSource follow = _chaseUsingB ? _chaseVoice : _chaseVoiceB;

        if (!chasing)
        {
            _chaseOverlapStarted = false;
            FadeChaseSource(lead, cut);
            FadeChaseSource(follow, cut);
            return;
        }

        if (!lead.isPlaying)
        {
            if (follow.isPlaying)
            {
                _chaseUsingB = !_chaseUsingB;
                _chaseOverlapStarted = false;
                lead = _chaseUsingB ? _chaseVoiceB : _chaseVoice;
                follow = _chaseUsingB ? _chaseVoice : _chaseVoiceB;
            }
            else
            {
                PlayChaseVoice(lead);
                _chaseOverlapStarted = false;
            }
        }

        float length = Mathf.Max(0.05f, lead.clip.length);
        float overlap = Mathf.Min(chaseScreamFadeOut, length * 0.42f);
        if (!_chaseOverlapStarted && length - lead.time <= overlap)
        {
            PlayChaseVoice(follow);
            _chaseOverlapStarted = true;
        }

        lead.volume = _screamVolume * ChaseEnvelope(lead, true, overlap);
        if (follow.isPlaying)
            follow.volume = _screamVolume * ChaseEnvelope(follow, false, overlap);

        if (_chaseOverlapStarted && (!lead.isPlaying || lead.time >= length - 0.02f))
        {
            lead.Stop();
            lead.volume = 0f;
            _chaseUsingB = !_chaseUsingB;
            _chaseOverlapStarted = false;
        }
    }

    void FadeChaseSource(AudioSource source, bool cut)
    {
        if (source == null || !source.isPlaying)
            return;

        if (cut)
        {
            source.Stop();
            source.volume = 0f;
            return;
        }

        float outTime = Mathf.Max(0.05f, chaseScreamFadeOut);
        source.volume = Mathf.MoveTowards(source.volume, 0f, Time.deltaTime * (_screamVolume / outTime));
        if (source.volume <= 0.01f)
        {
            source.Stop();
            source.volume = 0f;
        }
    }

    void PlayChaseVoice(AudioSource source)
    {
        _chasePitch = NextChasePitch();
        source.clip = chaseScream;
        source.loop = false;
        source.pitch = _chasePitch;
        source.time = 0f;
        source.volume = 0f;
        source.Play();
    }

    float ChaseEnvelope(AudioSource source, bool lead, float overlap)
    {
        float length = Mathf.Max(0.05f, source.clip.length);
        float time = source.time;
        float fadeIn = lead ? Mathf.Min(chaseScreamFadeIn, length * 0.3f) : overlap;
        float level = time < fadeIn ? time / Mathf.Max(0.05f, fadeIn) : 1f;
        if (lead && _chaseOverlapStarted && time > length - overlap)
            level = Mathf.Min(level, Mathf.Clamp01((length - time) / Mathf.Max(0.05f, overlap)));
        return Mathf.Clamp01(level);
    }

    float NextChasePitch()
    {
        float min = Mathf.Min(chaseScreamPitchMin, chaseScreamPitchMax);
        float max = Mathf.Max(chaseScreamPitchMin, chaseScreamPitchMax);
        float mid = (min + max) * 0.5f;
        bool goHigh = _chasePitch <= mid;
        return Random.Range(goHigh ? mid : min, goHigh ? max : mid);
    }

    void TickFootsteps()
    {
        if (footstepSource == null || footstepSource.clip == null || _anim == null)
            return;

        int move = _anim.GetInteger(_enemy.AnimStateHash);
        bool walk = move == 1;
        bool run = move == 2;
        if (!walk && !run)
        {
            _stepTime = -1f;
            return;
        }

        float now = Mathf.Repeat(_anim.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
        if (_stepTime >= 0f)
        {
            for (int i = 0; i < stepMarks.Length; i++)
            {
                if (!Crossed(_stepTime, now, stepMarks[i]))
                    continue;

                footstepSource.pitch = walk ? walkPitch : runPitch;
                footstepSource.PlayOneShot(footstepSource.clip, walk ? walkVolume : runVolume);
                break;
            }
        }

        _stepTime = now;
    }

    static bool Crossed(float from, float to, float mark)
    {
        return from < to ? from < mark && mark <= to : mark <= to || mark > from;
    }

    bool HeardOnly()
    {
        EnemyState state = _enemy.RuntimeState;
        return state.LastHeardPlayer && !state.LastSawPlayer;
    }

    void BeginTaunt()
    {
        if (_anim == null || !_anim.HasState(0, _tauntHash))
        {
            // Taunt 동작이 없으면 바로 달려드는 것으로 봄
            _roaredThisChase = true;
            return;
        }

        _taunting = true;
        _tauntTimer = 0f;
        _enemy.RuntimeState.IsBusy = true;
        _enemy.RuntimeState.LockAnimator = true;
        StopAgent();
        _anim.SetInteger(_enemy.AnimStateHash, -1);
        _anim.CrossFadeInFixedTime(_tauntHash, tauntBlend, 0, 0f);
        _anim.Update(0f);
        if (tauntScream != null && tauntScream.clip != null)
        {
            _screamVolume = 1f;
            tauntScream.volume = _screamVolume;
            tauntScream.Stop();
            tauntScream.time = 0f;
            tauntScream.Play();
        }
    }

    void HoldTaunt()
    {
        _tauntTimer += Time.deltaTime;
        _enemy.RuntimeState.IsBusy = true;
        _enemy.RuntimeState.LockAnimator = true;
        StopAgent();
        FaceSound();
        FadeScreamOut();
    }

    void FadeScreamOut()
    {
        if (tauntScream == null || !tauntScream.isPlaying || _anim == null)
            return;

        AnimatorStateInfo info = _anim.GetCurrentAnimatorStateInfo(0);
        if (info.fullPathHash != _tauntHash || info.length <= 0.01f)
            return;

        float remain = Mathf.Max(0f, (1f - info.normalizedTime) * info.length);
        tauntScream.volume = _screamVolume * Mathf.Clamp01(remain / Mathf.Max(0.05f, screamFadeTime));
    }

    bool TauntFinished()
    {
        if (_tauntTimer > 8f)
            return true;

        AnimatorStateInfo info = _anim.GetCurrentAnimatorStateInfo(0);
        if (info.fullPathHash != _tauntHash && !_anim.IsInTransition(0))
            return _tauntTimer > 0.4f;

        return info.fullPathHash == _tauntHash &&
               !_anim.IsInTransition(0) &&
               info.normalizedTime >= tauntEndNormalized;
    }

    void FinishTaunt()
    {
        if (tauntScream != null)
        {
            tauntScream.Stop();
            tauntScream.volume = _screamVolume;
        }

        int run = _anim.HasState(0, _runHash)
            ? _runHash
            : Animator.StringToHash(runFallbackState);

        _anim.SetInteger(_enemy.AnimStateHash, 2);
        if (_anim.HasState(0, run))
        {
            _anim.CrossFadeInFixedTime(run, runBlend, 0, 0f);
            _anim.Update(0f);
        }

        _enemy.RuntimeState.LockAnimator = false;
        _enemy.RuntimeState.IsBusy = false;
        _enemy.RuntimeState.NextChaseRepathTime = 0f;
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.updateRotation = true;
        }

        _taunting = false;
        _roaredThisChase = true;
        _lastState = EnemyBase.State.Chase;
    }

    void StopAgent()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
            return;

        _agent.isStopped = true;
        _agent.ResetPath();
        _agent.velocity = Vector3.zero;
        _agent.updateRotation = false;
    }

    void FaceSound()
    {
        Vector3 target = _enemy.Player != null
            ? _enemy.Player.position
            : _enemy.RuntimeState.LastKnownPosition;
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
            return;

        Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, look, faceTurnSpeed * Time.deltaTime);
    }

    Quaternion _deathJawBase;
    Vector3 _snoutLocal = Vector3.left;
    bool _deathPoseReady;

    void PresentDeathFace()
    {
        Camera cam = Camera.main;
        if (cam == null || _head == null)
            return;

        CaptureDeathPose();
        ApplyDeathPose();
        MeetFaceToFace(cam);

        Vector3 facePoint = Vector3.Lerp(MouthPoint(), _head.position, 0.2f);
        Vector3 desired = cam.transform.position + cam.transform.forward * deathFaceDistance;
        transform.position += desired - facePoint;
    }

    void CaptureDeathPose()
    {
        if (_deathPoseReady)
            return;

        _deathPoseReady = true;
        if (_jaw != null)
            _deathJawBase = _jaw.localRotation;

        Vector3 snout = _tongue != null ? _tongue.position - _head.position : _head.forward;
        if (snout.sqrMagnitude > 0.0001f)
            _snoutLocal = _head.InverseTransformDirection(snout.normalized);
    }

    void ApplyDeathPose()
    {
        if (_jaw != null)
            _jaw.localRotation = _deathJawBase * Quaternion.AngleAxis(Mathf.Max(0f, chaseJawOpen), Vector3.forward);
    }

    void MeetFaceToFace(Camera cam)
    {
        Vector3 snout = _head.TransformDirection(_snoutLocal);
        Vector3 flatSnout = snout;
        flatSnout.y = 0f;
        Vector3 want = -cam.transform.forward;
        want.y = 0f;
        if (flatSnout.sqrMagnitude > 0.0001f && want.sqrMagnitude > 0.0001f)
            transform.Rotate(0f, Vector3.SignedAngle(flatSnout, want, Vector3.up), 0f, Space.World);

        Vector3 axis = _head.TransformDirection(Vector3.forward);
        if (axis.sqrMagnitude < 0.0001f)
            return;

        Vector3 face = Vector3.ProjectOnPlane(_head.TransformDirection(_snoutLocal), axis);
        Vector3 look = Vector3.ProjectOnPlane(-cam.transform.forward, axis);
        if (face.sqrMagnitude < 0.0001f || look.sqrMagnitude < 0.0001f)
            return;

        float pitch = Mathf.Clamp(Vector3.SignedAngle(face, look, axis), -12f, 48f);
        _head.localRotation *= Quaternion.AngleAxis(pitch, Vector3.forward);
    }

    Vector3 MouthPoint()
    {
        if (_jaw != null && _tongue != null)
            return Vector3.Lerp(_jaw.position, _tongue.position, 0.55f);
        if (_jaw != null)
            return _jaw.position;
        return _head.position;
    }

    void CacheBones()
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        _head = FindNamed(all, headBoneName);
        if (_head == null)
            _head = FindNamed(all, "Head");
        _jaw = FindNamed(all, "Xenosaurid_ Jaw");
        _tongue = FindNamed(all, "Xenosaurid_ Tongue1");
    }

    static Transform FindNamed(Transform[] all, string boneName)
    {
        if (string.IsNullOrEmpty(boneName))
            return null;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == boneName)
                return all[i];
        }

        return null;
    }
}

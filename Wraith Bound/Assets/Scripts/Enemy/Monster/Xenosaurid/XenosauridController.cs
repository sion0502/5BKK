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
    float _tauntTimer;
    EnemyBase.State _lastState = EnemyBase.State.Patrol;
    int _tauntHash;
    int _runHash;
    float _stepTime = -1f;
    float _screamVolume = 1f;

    void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponentInChildren<Animator>();
        _tauntHash = Animator.StringToHash(tauntState);
        _runHash = Animator.StringToHash(runState);
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
            StraightenForJumpscare();
            return;
        }

        if (_taunting)
        {
            HoldTaunt();
            if (TauntFinished())
                FinishTaunt();
            return;
        }

        EnemyBase.State state = _enemy.RuntimeState.CurrentState;
        if (state == EnemyBase.State.Chase && _lastState != EnemyBase.State.Chase && HeardOnly())
            BeginTaunt();

        _lastState = _enemy.RuntimeState.CurrentState;
        TickFootsteps();
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
            return;

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

    void StraightenForJumpscare()
    {
        if (_head == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 desired = cam.transform.position + cam.transform.forward * deathFaceDistance;
        transform.position += desired - MouthPoint();
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

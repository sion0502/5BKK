using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 크리처 전용 점프스케어(CreatureJumpscare)가 쓰는 무대 도구. EnemyJumpscare가 만들어 넘김.
/// 전부 실제 시간(timeScale=0) 기준. 포즈는 Animator 상태를 정규화 시간으로 고정(Pose)하고,
/// 그 위에 뼈 회전·크기를 LateApply(Animator 평가 후)에서 덧씌움. Cleanup이 원래대로 돌려놓음.
/// 카메라 위치·FOV·크리처 위치 복구는 EnemyJumpscare가 함.
/// </summary>
public sealed class JumpscareStage
{
    public readonly Camera View;
    public readonly Transform Monster;
    public readonly EnemyBase Enemy;
    public readonly Animator Anim;
    public readonly Transform Face;
    public readonly Vector3 Eye;
    public readonly Quaternion BaseRotation;
    public readonly Vector3 Forward;
    public readonly float FloorY;
    public readonly float FaceDistance;
    public readonly float OriginalFov;
    public readonly Light FaceLight;

    private readonly EnemyJumpscare host;
    private readonly Quaternion facing;
    private readonly AudioSource audio;
    private readonly Dictionary<Transform, Quaternion> worldOffsets = new Dictionary<Transform, Quaternion>();
    private readonly Dictionary<Transform, Quaternion> localOffsets = new Dictionary<Transform, Quaternion>();
    private readonly Dictionary<Transform, Vector3> scales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();
    private readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();
    private readonly float originalAnimSpeed;
    private readonly AnimatorCullingMode originalCulling;
    private readonly bool originalAnimEnabled;
    private Vector3 cameraOffset;
    private float poseYaw; // 크리처 루트 정면 대비 얼굴(몸통)이 돌아간 각도 — Pose 때 갱신

    /// <summary>얼굴 기준점 (비우면 Face 뼈) — 주둥이가 긴 크리처는 입 위치로.</summary>
    public System.Func<Vector3> FaceAnchor;
    /// <summary>얼굴이 향하는 월드 방향 (비우면 휴머노이드 몸통 정면, 그것도 없으면 루트 정면).</summary>
    public System.Func<Vector3> FaceForward;

    public JumpscareStage(EnemyJumpscare host, Camera view, EnemyBase enemy, Transform face,
        Vector3 eye, Quaternion baseRotation, float floorY, float faceDistance, Light faceLight)
    {
        this.host = host;
        View = view;
        Enemy = enemy;
        Monster = enemy.transform;
        Anim = enemy.Anim != null ? enemy.Anim : enemy.GetComponentInChildren<Animator>(true);
        Face = face;
        Eye = eye;
        BaseRotation = baseRotation;
        Forward = baseRotation * Vector3.forward;
        FloorY = floorY;
        FaceDistance = faceDistance;
        OriginalFov = view.fieldOfView;
        FaceLight = faceLight;
        facing = Monster.rotation; // EnemyJumpscare가 카메라를 보게 돌려 둔 방향

        audio = host.gameObject.AddComponent<AudioSource>();
        audio.spatialBlend = 0f;
        audio.ignoreListenerPause = true;

        if (Anim != null)
        {
            originalAnimEnabled = Anim.enabled;
            originalAnimSpeed = Anim.speed;
            originalCulling = Anim.cullingMode;
            Anim.enabled = true;
            Anim.speed = 0f; // 포즈는 Pose()로만 바꿈
            Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // 발 붙이기·Xenosaurid 사망 얼굴 등 뼈/위치를 건드리는 크리처 스크립트는 연출 동안 멈춤
        foreach (CreatureFootGrounding g in Monster.GetComponentsInChildren<CreatureFootGrounding>())
            Pause(g);
        foreach (CreatureLocomotionSync s in Monster.GetComponentsInChildren<CreatureLocomotionSync>())
            Pause(s);
    }

    private void Pause(Behaviour b)
    {
        if (b != null && b.enabled)
        {
            b.enabled = false;
            pausedBehaviours.Add(b);
        }
    }

    // ───────── 포즈 ─────────

    /// <summary>Animator 상태를 해당 정규화 시간(0~1) 포즈로 고정. 상태가 없으면 무시.</summary>
    public bool Pose(string state, float normalizedTime)
    {
        if (Anim == null || Anim.runtimeAnimatorController == null)
            return false;
        int hash = Animator.StringToHash(state);
        if (!Anim.HasState(0, hash))
        {
            hash = Animator.StringToHash("Base Layer." + state);
            if (!Anim.HasState(0, hash))
                return false;
        }

        Anim.Play(hash, 0, normalizedTime);
        Anim.Update(0f);
        MeasurePoseYaw();
        return true;
    }

    /// <summary>춤·도발처럼 몸이 돌아가는 포즈에서도 얼굴이 카메라를 보도록, 루트 대비 얼굴 방향 각도를 잼.</summary>
    public void MeasurePoseYaw()
    {
        Vector3 face = Vector3.zero;
        if (FaceForward != null)
            face = FaceForward();
        else if (Anim != null && Anim.isHuman)
            face = HumanBodyForward(); // Animator.bodyRotation은 OnAnimatorIK 밖에서 읽으면 오류 로그
        face.y = 0f;
        Vector3 root = Monster.forward;
        root.y = 0f;
        poseYaw = face.sqrMagnitude > 0.0001f && root.sqrMagnitude > 0.0001f
            ? Vector3.SignedAngle(root, face, Vector3.up) : 0f;
    }

    /// <summary>좌→우 허벅지·어깨 벡터와 위쪽의 외적 = 몸통 정면.</summary>
    private Vector3 HumanBodyForward()
    {
        Vector3 right = Vector3.zero;
        Transform lLeg = Anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform rLeg = Anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform lArm = Anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform rArm = Anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (lLeg != null && rLeg != null)
            right += rLeg.position - lLeg.position;
        if (lArm != null && rArm != null)
            right += rArm.position - lArm.position;
        right.y = 0f;
        return right.sqrMagnitude > 0.0001f ? Vector3.Cross(right.normalized, Vector3.up) : Monster.forward;
    }

    public Vector3 AnchorPoint => FaceAnchor != null ? FaceAnchor()
        : Face != null ? Face.position : Monster.position + Vector3.up * 1.6f;

    /// <summary>얼굴이 카메라를 보게 루트를 돌림 (루트 위치 기준 Y축).</summary>
    private void TurnToCamera()
    {
        Vector3 want = CurrentEye - Monster.position;
        want.y = 0f;
        if (want.sqrMagnitude < 0.0001f)
        {
            Monster.rotation = facing;
            return;
        }
        Monster.rotation = Quaternion.LookRotation(want.normalized, Vector3.up) * Quaternion.Euler(0f, -poseYaw, 0f);
    }

    public Transform Bone(HumanBodyBones bone, params string[] fallbackNames)
    {
        if (Anim != null && Anim.isHuman)
        {
            Transform t = Anim.GetBoneTransform(bone);
            if (t != null)
                return t;
        }

        if (fallbackNames.Length == 0)
            return null;
        foreach (Transform t in Monster.GetComponentsInChildren<Transform>(true))
            foreach (string n in fallbackNames)
                if (t.name == n)
                    return t;
        return null;
    }

    /// <summary>크리처 기준 축으로 뼈를 돌림 (리그마다 다른 로컬 축 대신). roll=좌우 갸웃, pitch=+면 숙임.</summary>
    public void SetBoneTilt(Transform bone, float roll, float pitch, float yaw = 0f)
    {
        if (bone == null)
            return;
        worldOffsets[bone] =
            Quaternion.AngleAxis(yaw, Monster.up) *
            Quaternion.AngleAxis(roll, Monster.forward) *
            Quaternion.AngleAxis(pitch, Monster.right);
    }

    /// <summary>뼈 로컬 축 기준 회전 (팀원이 맞춰 둔 축을 그대로 쓸 때 — 예: Xenosaurid 턱 로컬 Z).</summary>
    public void SetBoneLocal(Transform bone, Quaternion localDelta)
    {
        if (bone != null)
            localOffsets[bone] = localDelta;
    }

    public void SetBoneScale(Transform bone, Vector3 scale)
    {
        if (bone == null)
            return;
        if (!originalScales.ContainsKey(bone))
            originalScales[bone] = bone.localScale;
        scales[bone] = scale;
    }

    /// <summary>LateApply 마지막에 호출 — 모든 포즈·꺾임이 적용된 뒤 결과를 보고 맞출 때(예: 탈이 카메라를 똑바로 보게).</summary>
    public System.Action LateHook;

    /// <summary>EnemyJumpscare.LateUpdate가 호출 — Animator가 포즈를 쓴 뒤라 덮어쓰이지 않음.</summary>
    public void LateApply()
    {
        // 클립에 커브가 없는 뼈(예: Xenosaurid Taunt의 턱)는 Animator가 다시 쓰지 않음 →
        // 지난 프레임에 얹은 값 그대로면 얹기 전 값으로 되돌린 뒤 다시 얹어야 누적되지 않음
        foreach (KeyValuePair<Transform, Quaternion> kv in localOffsets)
            if (kv.Key != null)
            {
                Quaternion before = Unapplied(kv.Key);
                kv.Key.localRotation = before * kv.Value;
                Remember(kv.Key, before);
            }
        foreach (KeyValuePair<Transform, Quaternion> kv in worldOffsets)
            if (kv.Key != null && !localOffsets.ContainsKey(kv.Key))
            {
                Quaternion before = Unapplied(kv.Key);
                kv.Key.localRotation = before;
                kv.Key.rotation = kv.Value * kv.Key.rotation;
                Remember(kv.Key, before);
            }
        foreach (KeyValuePair<Transform, Vector3> kv in scales)
            if (kv.Key != null && originalScales.TryGetValue(kv.Key, out Vector3 baseScale))
                kv.Key.localScale = Vector3.Scale(baseScale, kv.Value);
        LateHook?.Invoke();
    }

    private readonly Dictionary<Transform, (Quaternion applied, Quaternion before)> appliedRotations =
        new Dictionary<Transform, (Quaternion applied, Quaternion before)>();

    private Quaternion Unapplied(Transform bone)
    {
        return appliedRotations.TryGetValue(bone, out var last) && bone.localRotation == last.applied
            ? last.before : bone.localRotation;
    }

    private void Remember(Transform bone, Quaternion before)
    {
        appliedRotations[bone] = (bone.localRotation, before);
    }

    // ───────── 배치 ─────────

    /// <summary>벽에 박히지 않는 최대 거리로 줄임.</summary>
    public float ClampDistance(float distance)
    {
        if (Physics.Raycast(Eye, FlatForward, out RaycastHit hit, distance + 0.6f, ~0, QueryTriggerInteraction.Ignore)
            && hit.transform != null && !hit.transform.IsChildOf(Monster))
            return Mathf.Max(FaceDistance, Mathf.Min(distance, hit.distance - 0.6f));
        return distance;
    }

    public Vector3 FlatForward
    {
        get
        {
            Vector3 f = Forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }
    }

    /// <summary>눈앞 distance(수평) 지점 바닥에 세움, 방향은 카메라를 보게.</summary>
    public void PlaceGrounded(float distance, float side = 0f)
    {
        Vector3 right = Vector3.Cross(Vector3.up, FlatForward);
        Vector3 p = Eye + FlatForward * distance + right * side;
        p.y = FloorY;
        Monster.position = p;
        TurnToCamera();
    }

    /// <summary>얼굴 기준점이 눈앞 distance에 오도록 몸 전체를 옮김 (마지막 덮침). alongView면 지금 카메라가 보는 방향으로.</summary>
    public void PlaceFace(float distance, float up = 0f, bool alongView = false)
    {
        TurnToCamera();
        Vector3 dir = alongView && View != null ? View.transform.forward : Forward;
        Vector3 want = CurrentEye + dir * distance + Vector3.up * up;
        Monster.position += want - AnchorPoint;
    }

    // ───────── 카메라 ─────────

    public Vector3 CurrentEye => Eye + cameraOffset;

    /// <summary>쓰러짐 등으로 눈 위치를 옮김 (복구는 EnemyJumpscare).</summary>
    public void SetCameraOffset(Vector3 worldOffset)
    {
        cameraOffset = worldOffset;
    }

    /// <summary>weight만큼 target을 바라보고(0=처음 방향), euler(흔들림·기울임)를 더함.</summary>
    public void Look(Vector3 target, float weight, Vector3 euler, float fov)
    {
        if (View == null)
            return;
        Vector3 dir = target - CurrentEye;
        Quaternion look = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : BaseRotation;
        View.transform.SetPositionAndRotation(CurrentEye,
            Quaternion.Slerp(BaseRotation, look, Mathf.Clamp01(weight)) * Quaternion.Euler(euler));
        View.fieldOfView = fov;
        EnemyJumpscare.ApplyFaceLight(FaceLight, CurrentEye, AnchorPoint);
    }

    /// <summary>EnemyJumpscare 기본 연출과 같은 성질의 흔들림 (세기 0~1).</summary>
    public static Vector3 Shake(float time, float strength)
    {
        return new Vector3(
            Mathf.Sin(time * 83f) * 1.2f,
            Mathf.Sin(time * 67f) * 1.2f,
            Mathf.Sin(time * 57f) * 1.8f) * strength;
    }

    public float Blackout
    {
        set => host.SetBlackout(value);
    }

    // ───────── 소리 ─────────

    /// <summary>점프스케어 소리 (전용 클립 → 없으면 EnemyBase의 jumpscareSound).</summary>
    public void PlayScare(AudioClip clip, float volume = 0.8f)
    {
        AudioClip use = clip != null ? clip : Enemy.jumpscareSound;
        if (use != null)
            audio.PlayOneShot(use, volume);
    }

    public void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip != null)
            audio.PlayOneShot(clip, volume);
    }

    // ───────── 정리 ─────────

    public void Cleanup()
    {
        foreach (KeyValuePair<Transform, Vector3> kv in originalScales)
            if (kv.Key != null)
                kv.Key.localScale = kv.Value;
        foreach (var kv in appliedRotations)
            if (kv.Key != null && kv.Key.localRotation == kv.Value.applied)
                kv.Key.localRotation = kv.Value.before; // Animator가 다시 쓰지 않는 뼈는 원래대로
        appliedRotations.Clear();
        LateHook = null;
        worldOffsets.Clear();
        localOffsets.Clear();
        scales.Clear();
        originalScales.Clear();

        if (Anim != null)
        {
            Anim.speed = originalAnimSpeed;
            Anim.cullingMode = originalCulling;
            Anim.enabled = originalAnimEnabled;
        }

        foreach (Behaviour b in pausedBehaviours)
            if (b != null)
                b.enabled = true;
        pausedBehaviours.Clear();
        cameraOffset = Vector3.zero;
        host.SetBlackout(0f);
    }
}

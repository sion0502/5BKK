using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 프롤로그 마지막 컷씬 (비상구 문에 E → 로딩 없이 바로, 플레이어 시점 그대로).
///  ① 문을 급하게 덜컹 — 안 열림, 등 뒤에서 다가오는 소리
///  ② 뒤돌아보면 복도 저편 형광등 밑에 간호사가 가만히 서 있음
///  ③ 간호사 쪽 형광등부터 하나씩 '팍' 꺼짐 → 암흑
///  ④ 불이 깜빡일 때마다 간호사가 더 가까이, 관절이 더 기괴하게 꺾인 채 서 있음 (첫 깜빡임은 그 자리 그대로)
///  ⑤ 완전 암전(초록 비상구 표시등까지) → 그 표시등이 빨갛게 켜지며 간호사를 비춤 — 정적
///  ⑥ 아주 빠르게 돌진 → 암전 → 허브 (게임오버 화면 없음)
/// 기괴한 자세 = CreatureJointSnap(탈춤귀신 추격용)을 간호사에 잠깐 붙여, 어둠 속에서 새 각도로 꺾은 뒤 굳힘.
/// </summary>
public class PrologueFinaleCutscene : MonoBehaviour
{
    [Header("Actors")]
    [SerializeField] private PrologueNurse nurse;
    [Tooltip("간호사 쪽(먼 쪽)부터 순서대로 — 이 순서로 꺼짐")]
    [SerializeField] private PrologueCeilingLight[] corridorLights = { };
    [Tooltip("비상구 문 위 비상구 표시등(초록) — 완전 암전 때 꺼짐. 붉은 등(redLight)은 같은 표시등을 빨갛게 켬")]
    [SerializeField] private PrologueCeilingLight exitSign;
    [SerializeField] private PrologueCeilingLight redLight;
    [Tooltip("완전 암전 때 같이 꺼지는 조명 (초록 비상구 표시등 등)")]
    [SerializeField] private Light[] extraLightsOff = { };
    [Tooltip("컷씬 동안 숨길 HUD 등")]
    [SerializeField] private GameObject[] hideDuringCutscene = { };

    [Header("Points")]
    [Tooltip("문을 열려고 서는 자리 (바닥)")]
    [SerializeField] private Transform standPoint;
    [Tooltip("문 손잡이 쪽 바라볼 점")]
    [SerializeField] private Transform doorLookPoint;
    [Tooltip("처음 서 있는 자리 (가장 먼 형광등 밑)")]
    [SerializeField] private Transform nurseStart;
    [Tooltip("깜빡일 때마다 옮겨 갈 자리 — 순서대로 점점 가까이 (첫 깜빡임은 nurseStart 그대로)")]
    [SerializeField] private Transform[] blinkPoints = { };
    [Tooltip("붉은 등에 비치는 마지막 자리")]
    [SerializeField] private Transform finalPoint;

    [Header("Timing (초)")]
    [SerializeField] private float rattleSeconds = 1.8f;
    [SerializeField] private float behindSoundPause = 1.0f;
    [SerializeField] private float turnSeconds = 1.3f;
    [SerializeField] private float stareSeconds = 1.8f;
    [Tooltip("뒤돌아본 뒤 간호사 쪽으로 줌인하는 FOV 감소량 — 이후 깜빡임 동안 유지, 붉은 등에서 원래대로")]
    [SerializeField] private float stareZoom = 26f;
    [SerializeField] private float lightOffInterval = 0.9f;
    [SerializeField] private float darkAfterLightsOff = 1.6f;
    [SerializeField] private float blinkDarkSeconds = 1.0f;
    [SerializeField] private Vector2 blinkOnSeconds = new Vector2(0.35f, 0.55f);
    [SerializeField] private float blackoutBeforeRed = 1.3f;
    [SerializeField] private float redHoldSeconds = 1.8f;
    [SerializeField] private float dashSeconds = 0.32f;
    [Tooltip("돌진 끝 — 간호사 몸과 카메라 사이 수평 거리")]
    [SerializeField] private float dashEndDistance = 1.1f;
    [SerializeField] private float blackHoldSeconds = 1.4f;

    [Header("Darkness")]
    [Tooltip("형광등이 다 꺼졌을 때 남는 주변광 비율 — 0에 가까울수록 어둠 속 간호사가 안 보임(어둠 속에서 자리를 옮기는 연출)")]
    [SerializeField, Range(0f, 1f)] private float darkAmbient = 0.04f;
    [Tooltip("컷씬 시작 때 주변광 비율 — 낮을수록 형광등 밑 간호사의 그림자가 진함")]
    [SerializeField, Range(0f, 1f)] private float startAmbient = 0.3f;

    [Header("Pose")]
    [Tooltip("깜빡일 때마다 관절 꺾임 세기 (blinkPoints와 같은 순서, 첫 값은 nurseStart에서의 깜빡임)")]
    [SerializeField] private float[] blinkContortion = { 0f, 0.4f, 0.75f, 1.05f };
    [Tooltip("붉은 등에 비칠 때 관절 꺾기 세기 — 0이면 꺾임 없이 똑바로 선 채(사용자 결정: 돌진 전엔 그냥 서 있음)")]
    [SerializeField] private float finalContortion = 0f;
    [SerializeField] private float dashAnimSpeed = 2.6f;
    [Tooltip("처음 서 있을 때 고개 꺾기 (x=옆으로 갸웃, y=아래로 숙임)")]
    [SerializeField] private Vector2 standHeadTilt = new Vector2(28f, 18f);

    [Header("Text")]
    [SerializeField] private string rattleLine = "안 돼… 왜 안 열려…! 열려…!";

    [Header("Scene")]
    [SerializeField] private string hubSceneName = "Hub";

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rattleClip;
    [Tooltip("등 뒤에서 다가오는 소리 (간호사 쪽에서 재생)")]
    [SerializeField] private AudioClip approachClip;
    [SerializeField] private AudioClip blackoutClip;
    [SerializeField] private AudioClip dashClip;

    public bool IsRunning { get; private set; }

    // 시선 대상 = 실제 머리 뼈 (관절을 꺾으면 머리가 고정 EyePoint에서 벗어남)
    private Vector3 NurseHead
    {
        get
        {
            Transform head = nurse.Animator != null && nurse.Animator.isHuman ? nurse.Animator.GetBoneTransform(HumanBodyBones.Head) : null;
            return head != null ? head.position : nurse.EyePoint.position;
        }
    }

    private GameObject player;
    private Camera cam;
    private Light[] playerLights = { };
    private float yaw;
    private float pitch;
    private float baseFov;
    private bool tiltHead;
    private Color ambientSky;
    private Color ambientEquator;
    private Color ambientGround;
    private float ambientIntensity;
    private float reflectionIntensity;

    public void Begin()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        StartCoroutine(Run());
    }

    private static void Log(string message)
    {
        Debug.Log("[Prologue] " + message);
    }

    private void LateUpdate()
    {
        // 손전등 등 플레이어 쪽 조명은 컷씬 내내 끔 (켜져 있으면 어둠 연출이 무너짐 — 손전등 스크립트가 매 프레임 다시 켤 수 있어 여기서 계속 끔)
        if (!IsRunning)
        {
            return;
        }

        if (tiltHead && nurse != null && nurse.Animator != null && nurse.Animator.isHuman)
        {
            Transform head = nurse.Animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) // Animator·관절 꺾기가 포즈를 쓴 뒤라 매 프레임 덧씌워도 누적 안 됨
            {
                Transform n = nurse.transform;
                head.rotation = Quaternion.AngleAxis(standHeadTilt.x, n.forward) * Quaternion.AngleAxis(standHeadTilt.y, n.right) * head.rotation;
            }
        }

        foreach (Light l in playerLights)
        {
            if (l != null)
            {
                l.enabled = false;
            }
        }
    }

    private IEnumerator Run()
    {
        Log("마지막 컷씬 시작");
        player = GameObject.FindWithTag("Player");
        cam = player.GetComponentInChildren<Camera>();
        PlayerControlLock.Lock(player); // 씬을 떠나므로 복구 안 함
        playerLights = player.GetComponentsInChildren<Light>(true);
        CamcorderController camcorder = player.GetComponentInChildren<CamcorderController>();
        if (camcorder != null && camcorder.IsViewfinderActive)
        {
            camcorder.SetActive(false); // 뷰파인더로 보던 중이면 내림
        }

        // 손에 든 장비(캠코더·손전등)는 화면에서 치움 — 두 손으로 문을 잡아당기는 장면
        foreach (Renderer r in cam.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = false;
        }

        PlayerChaseEffectController chase = player.GetComponent<PlayerChaseEffectController>();
        if (chase != null)
        {
            chase.SetBeingChased(false);
        }

        foreach (GameObject go in hideDuringCutscene)
        {
            if (go != null)
            {
                go.SetActive(false);
            }
        }

        yaw = player.transform.eulerAngles.y;
        pitch = Mathf.DeltaAngle(0f, cam.transform.localEulerAngles.x);
        CaptureAmbient();
        SetAmbient(startAmbient); // 처음부터 명암을 진하게 — 형광등 빛을 받은 곳과 그림자 차이가 커야 서 있는 간호사가 섬뜩함

        // 간호사는 처음부터 그 자리에 세워 둠 (플레이어는 문을 보고 있어 아직 못 봄) — 고개만 살짝 꺾어 둠(멀뚱히 선 대기 자세 대신)
        JointPoser poser = new JointPoser(nurse);
        nurse.Pin(nurseStart.position, standPoint.position);
        poser.Freeze();
        tiltHead = true;

        // ① 문 앞으로 다가서며 문을 봄 → 덜컹
        yield return MoveAndLook(standPoint.position, doorLookPoint.position, 0.35f);
        Play(rattleClip);
        SubtitleUI.ShowForDuration("나", rattleLine, rattleSeconds + 0.4f);
        for (float t = 0f; t < rattleSeconds; t += Time.deltaTime)
        {
            float k = 1f - t / rattleSeconds * 0.5f;
            Apply(yaw + Mathf.Sin(t * 41f) * 0.9f * k, pitch + Mathf.Sin(t * 29f + 1f) * 0.6f * k, Mathf.Sin(t * 23f) * 0.6f * k);
            yield return null;
        }

        Apply(yaw, pitch, 0f);

        // 등 뒤에서 무언가 다가오는 소리 — 문을 본 채 멈칫
        PlayAt(approachClip, nurseStart.position);
        yield return new WaitForSeconds(behindSoundPause);

        // ② 천천히 뒤돌아봄 → 저편에 가만히 서 있음
        yield return LookAt(NurseHead, turnSeconds);
        // 멀리 서 있는 간호사에게 시선이 빨려 들어가듯 천천히 줌인
        baseFov = cam.fieldOfView;
        for (float t = 0f; t < stareSeconds; t += Time.deltaTime)
        {
            cam.fieldOfView = Mathf.Lerp(baseFov, baseFov - stareZoom, Mathf.SmoothStep(0f, 1f, t / stareSeconds));
            yield return null;
        }

        // ③ 간호사 쪽 형광등부터 하나씩 꺼짐 — 꺼질 때마다 주변광도 줄어 마지막엔 거의 암흑
        for (int i = 0; i < corridorLights.Length; i++)
        {
            StartCoroutine(corridorLights[i].PopOff());
            SetAmbient(Mathf.Lerp(startAmbient, darkAmbient, (i + 1f) / corridorLights.Length));
            yield return new WaitForSeconds(lightOffInterval);
        }

        yield return new WaitForSeconds(darkAfterLightsOff);

        // ④ 깜빡일 때마다 더 가까이, 더 기괴하게
        // 어둠 속에서 먼저 옮기고 자세를 바꾼 뒤 기다렸다가 켬 — 바로 켜면 발 붙이기(최근 0.25초 최저 발 높이로 골반을 맞춤)가
        // 아직 새 자세에 맞춰지는 중이라 몸이 살짝 떴다 내려앉는 게 보였음
        for (int i = 0; i < blinkContortion.Length; i++)
        {
            if (i > 0)
            {
                tiltHead = false; // 꺾인 자세부터는 관절 꺾기가 고개도 맡음
                nurse.Pin(blinkPoints[Mathf.Min(i - 1, blinkPoints.Length - 1)].position, player.transform.position);
                yield return poser.Contort(blinkContortion[i]);
                yield return new WaitForSeconds(Mathf.Max(0.9f, blinkDarkSeconds * (1f - i * 0.1f))); // 발 붙이기가 새 자세에 다 맞춰지는 데 약 0.65초
            }

            yield return Blink(Random.Range(blinkOnSeconds.x, blinkOnSeconds.y), i);
        }

        yield return new WaitForSeconds(blinkDarkSeconds * 0.6f);

        // ⑤ 완전 암전 → 붉은 등
        Play(blackoutClip);
        if (exitSign != null)
        {
            exitSign.Set(false);
        }

        foreach (Light l in extraLightsOff)
        {
            if (l != null)
            {
                l.enabled = false;
            }
        }

        nurse.Pin(finalPoint.position, player.transform.position);
        yield return poser.Contort(finalContortion, faceForward: true);
        cam.fieldOfView = baseFov; // 가까이 온 마지막은 넓은 화각으로
        yield return LookAt(NurseHead, 0.2f);
        yield return new WaitForSeconds(blackoutBeforeRed);
        if (redLight != null)
        {
            redLight.SwitchOn();
        }

        yield return new WaitForSeconds(redHoldSeconds);

        // ⑥ 돌진
        Log("돌진");
        Play(dashClip);
        nurse.SetAnimatorState(2);
        nurse.Animator.speed = dashAnimSpeed;
        poser.Frenzy();
        Vector3 from = finalPoint.position;
        Vector3 toPlayer = player.transform.position - from;
        toPlayer.y = 0f;
        Vector3 to = player.transform.position - toPlayer.normalized * dashEndDistance;
        to.y = from.y;
        float startFov = cam.fieldOfView;
        for (float t = 0f; t < dashSeconds; t += Time.deltaTime)
        {
            float k = t / dashSeconds;
            nurse.transform.position = Vector3.Lerp(from, to, k * k);
            Vector3 look = NurseHead - cam.transform.position;
            Quaternion q = Quaternion.LookRotation(look);
            yaw = q.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0f, q.eulerAngles.x);
            Apply(yaw + Random.Range(-1f, 1f) * k, pitch + Random.Range(-1f, 1f) * k, Random.Range(-2f, 2f) * k);
            cam.fieldOfView = Mathf.Lerp(startFov, startFov - 14f, k);
            yield return null;
        }

        CutsceneBlackScreen.CreateRuntime(5100, 1f); // 닿는 순간 암전
        yield return new WaitForSecondsRealtime(blackHoldSeconds);
        TraitProgress.Add(TraitCatalog.Deaths); // 프롤로그 사망도 누적 사망 수에 (기 해금)
        HubArrival.FromPrologue = true; // 허브에서 바닥에 쓰러져 있다 깨어나는 연출
        SceneManager.LoadScene(hubSceneName);
    }

    // 모든 형광등이 함께 지직 켜졌다 꺼짐
    private IEnumerator Blink(float onSeconds, int index)
    {
        if (corridorLights.Length > 0)
        {
            corridorLights[Mathf.Min(index, corridorLights.Length - 1)].PlayFlickerSound();
        }

        SetCorridor(true);
        yield return new WaitForSeconds(0.07f);
        SetCorridor(false);
        yield return new WaitForSeconds(0.06f);
        SetCorridor(true);
        // 켜진 동안 시선이 간호사에게 끌려감
        Vector3 eye = NurseHead;
        float startYaw = yaw, startPitch = pitch;
        Quaternion q = Quaternion.LookRotation(eye - cam.transform.position);
        for (float t = 0f; t < onSeconds; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / onSeconds);
            Apply(Mathf.LerpAngle(startYaw, q.eulerAngles.y, k), Mathf.Lerp(startPitch, Mathf.DeltaAngle(0f, q.eulerAngles.x), k), 0f);
            yield return null;
        }

        yaw = q.eulerAngles.y;
        pitch = Mathf.DeltaAngle(0f, q.eulerAngles.x);
        SetCorridor(false);
    }

    private void CaptureAmbient()
    {
        ambientSky = RenderSettings.ambientSkyColor;
        ambientEquator = RenderSettings.ambientEquatorColor;
        ambientGround = RenderSettings.ambientGroundColor;
        ambientIntensity = RenderSettings.ambientIntensity;
        reflectionIntensity = RenderSettings.reflectionIntensity;
    }

    // 씬을 떠나므로 되돌리지 않음
    private void SetAmbient(float k)
    {
        RenderSettings.ambientSkyColor = ambientSky * k;
        RenderSettings.ambientEquatorColor = ambientEquator * k;
        RenderSettings.ambientGroundColor = ambientGround * k;
        RenderSettings.ambientIntensity = ambientIntensity * k;
        RenderSettings.reflectionIntensity = reflectionIntensity * k;
    }

    private void SetCorridor(bool on)
    {
        foreach (PrologueCeilingLight l in corridorLights)
        {
            l.Set(on);
        }
    }

    private IEnumerator MoveAndLook(Vector3 position, Vector3 lookPoint, float duration)
    {
        Vector3 start = player.transform.position;
        position.y = start.y;
        CharacterController cc = player.GetComponent<CharacterController>();
        bool ccWasEnabled = cc != null && cc.enabled;
        if (cc != null)
        {
            cc.enabled = false; // 직접 옮기는 동안 충돌 보정이 끼어들지 않게
        }

        float startYaw = yaw, startPitch = pitch;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            player.transform.position = Vector3.Lerp(start, position, k);
            Quaternion q = Quaternion.LookRotation(lookPoint - cam.transform.position);
            Apply(Mathf.LerpAngle(startYaw, q.eulerAngles.y, k), Mathf.Lerp(startPitch, Mathf.DeltaAngle(0f, q.eulerAngles.x), k), 0f);
            yield return null;
        }

        player.transform.position = position;
        Quaternion end = Quaternion.LookRotation(lookPoint - cam.transform.position);
        yaw = end.eulerAngles.y;
        pitch = Mathf.DeltaAngle(0f, end.eulerAngles.x);
        Apply(yaw, pitch, 0f);
        if (cc != null)
        {
            cc.enabled = ccWasEnabled;
        }
    }

    private IEnumerator LookAt(Vector3 point, float duration)
    {
        Quaternion q = Quaternion.LookRotation(point - cam.transform.position);
        float startYaw = yaw, startPitch = pitch;
        float endYaw = q.eulerAngles.y, endPitch = Mathf.DeltaAngle(0f, q.eulerAngles.x);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            Apply(Mathf.LerpAngle(startYaw, endYaw, k), Mathf.Lerp(startPitch, endPitch, k), 0f);
            yield return null;
        }

        yaw = endYaw;
        pitch = endPitch;
        Apply(yaw, pitch, 0f);
    }

    private void Apply(float y, float p, float roll)
    {
        player.transform.rotation = Quaternion.Euler(0f, y, 0f);
        cam.transform.localRotation = Quaternion.Euler(p, 0f, roll);
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private static void PlayAt(AudioClip clip, Vector3 position)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, position);
        }
    }

    /// <summary>간호사 자세 — 평소 애니메이션을 멈춰 굳히고, 관절 꺾기를 원하는 세기로 '뚝' 바꾼 뒤 다시 굳힘.</summary>
    private sealed class JointPoser
    {
        private readonly PrologueNurse nurse;
        private readonly CreatureJointSnap snap;

        public JointPoser(PrologueNurse nurse)
        {
            this.nurse = nurse;
            snap = nurse.GetComponent<CreatureJointSnap>();
            if (snap == null)
            {
                snap = nurse.gameObject.AddComponent<CreatureJointSnap>();
            }
        }

        public void Freeze()
        {
            nurse.Animator.speed = 0f; // 대기 자세 그대로 정지 (숨 쉬듯 움직이지도 않게)
            snap.SetForced(true, 0f);
        }

        /// <summary>어둠 속에서 호출 — 짧게 새 각도로 꺾은 뒤 그대로 굳힘. 세기 0이면 꺾임 없이 원래 자세.</summary>
        public IEnumerator Contort(float amount, bool faceForward = false)
        {
            nurse.Animator.speed = 0f;
            if (amount <= 0f)
            {
                snap.ResetBones(AllBones);
                snap.SetForced(true, 0f);
                yield break;
            }

            snap.SetForced(true, amount);
            yield return new WaitForSeconds(0.12f);
            if (faceForward)
            {
                snap.ResetBones(HumanBodyBones.Head, HumanBodyBones.Neck); // 마지막엔 고개를 세워 플레이어를 똑바로 봄
            }

            snap.SetForced(true, 0f);
        }

        public void Frenzy()
        {
            snap.SetForced(true, 0.8f);
        }

        private static readonly HumanBodyBones[] AllBones =
        {
            HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.UpperChest, HumanBodyBones.Chest, HumanBodyBones.Spine,
            HumanBodyBones.Hips, HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder, HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
        };
    }
}

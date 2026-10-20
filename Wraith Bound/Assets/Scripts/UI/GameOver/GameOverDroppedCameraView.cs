using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 게임오버 배경: 플레이어가 떨어뜨린 캠코더가 바닥에 옆으로 누운 채 찍는 화면.
/// 죽은 자리 바닥에 카메라를 두고 괴물(점프스케어 후 멈춘 위치)을 올려다보게 한 뒤, 나이트비전 색감 + 적외선 조명으로 RenderTexture에 렌더.
/// 색감·IR 값은 View_Camcorder.prefab의 CamcorderController에서 가져와 실제 뷰파인더와 같게 맞춘다.
/// </summary>
public class GameOverDroppedCameraView : MonoBehaviour
{
    private const string CamcorderItemPath = "ItemDatas/Equipment/Camcorder";
    private const float LensHeight = 0.09f;      // 바닥에 누운 캠코더 렌즈 높이
    private const float PullBack = 0.45f;        // 죽은 자리에서 괴물 반대쪽으로 굴러간 거리
    private const float RollAngle = 74f;         // 옆으로 쓰러진 기울기
    private const float YawOffset = 14f;         // 괴물이 정중앙에 오지 않게 살짝 비낌
    private const float FieldOfView = 68f;
    private const float MaxPitchUp = 32f;        // 최대 올려다보는 각도
    private const float MinMonsterDistance = 2.2f; // 괴물과 떨어질 최소/최대 거리 (머리 높이에 비례)
    private const float MaxMonsterDistance = 4.5f;
    private const float MaxPullBack = 3f;
    private const float NearSurfaceBrightnessCap = 0.06f; // 캠코더 IR(0.1)보다 낮게 — 위에 글자가 얹히므로 화면이 너무 밝지 않게
    private const int TextureWidth = 1280;
    private const float PatrolReleaseDelay = 2.5f; // 괴물이 쓰러진 캠코더 앞에 서 있다가 순찰을 떠나기까지 (실제 시간)

    private Camera lensCamera;
    private RenderTexture renderTexture;
    private VolumeProfile profile;

    public RenderTexture Texture => renderTexture;
    /// <summary>색감·뷰파인더 스프라이트를 가져온 캠코더 설정(View_Camcorder.prefab). 못 찾으면 null.</summary>
    public CamcorderController Camcorder { get; private set; }

    /// <summary>죽은 위치 기준으로 떨어진 캠코더 시점을 만든다. 메인 카메라가 없으면 null.</summary>
    public static GameOverDroppedCameraView Create(Transform parent)
    {
        Camera eye = Camera.main;
        if (eye == null)
            return null;

        var view = new GameObject("DroppedCamcorderView").AddComponent<GameOverDroppedCameraView>();
        view.transform.SetParent(parent, false);
        view.Build(eye);
        return view;
    }

    private void Build(Camera eye)
    {
        CamcorderController camcorder = LoadCamcorderSettings();
        Camcorder = camcorder;
        int nightVisionLayer = LayerMask.NameToLayer(camcorder != null ? camcorder.NightVisionLayerName : "NightVision");

        EnemyBase monster = FindNearestEnemy(eye.transform.position);
        StartCoroutine(ReleaseToPatrol(monster));
        PlaceOnFloor(eye.transform.position, eye.transform.forward, monster, out Vector3 position, out Quaternion rotation);

        int height = Mathf.Max(1, Mathf.RoundToInt(TextureWidth * (float)Screen.height / Mathf.Max(1, Screen.width)));
        renderTexture = new RenderTexture(TextureWidth, height, 24) { name = "GameOver_DroppedCamcorder_RT" };
        renderTexture.Create();

        var camGo = new GameObject("DroppedCamcorderLens");
        camGo.transform.SetPositionAndRotation(position, rotation);
        camGo.transform.SetParent(transform, true);
        lensCamera = camGo.AddComponent<Camera>();
        lensCamera.CopyFrom(eye);
        lensCamera.transform.SetPositionAndRotation(position, rotation);
        lensCamera.targetTexture = renderTexture;
        lensCamera.fieldOfView = FieldOfView;
        lensCamera.nearClipPlane = 0.02f;
        lensCamera.clearFlags = CameraClearFlags.SolidColor;
        lensCamera.backgroundColor = Color.black;
        lensCamera.cullingMask = ~(LayerBit("PickupItem") | LayerBit("RaderIcon") | (nightVisionLayer >= 0 ? 1 << nightVisionLayer : 0));

        UniversalAdditionalCameraData eyeData = eye.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData lensData = lensCamera.GetUniversalAdditionalCameraData();
        lensData.renderType = CameraRenderType.Base;
        lensData.renderPostProcessing = true;
        int baseVolumeMask = eyeData != null ? eyeData.volumeLayerMask : 1;
        lensData.volumeLayerMask = nightVisionLayer >= 0 ? baseVolumeMask | (1 << nightVisionLayer) : baseVolumeMask;
        if (eyeData != null && nightVisionLayer >= 0)
            eyeData.volumeLayerMask &= ~(1 << nightVisionLayer);

        // 화면은 게임오버 UI가 전부 덮으므로 눈 카메라는 아무것도 그리지 않게 (렌더 비용 절약)
        eye.cullingMask = 0;
        HidePlayerBody();

        if (camcorder != null && nightVisionLayer >= 0)
            BuildNightVisionVolume(camcorder, nightVisionLayer);
        BuildIrLight(camcorder, monster);
    }

    // 플레이어 캡슐 등 몸 렌더러가 떨어진 캠코더 앞을 가리지 않게 숨김 (어차피 씬을 떠나며 재로드됨)
    private static void HidePlayerBody()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;
        foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
            r.enabled = false;
    }

    private static CamcorderController LoadCamcorderSettings()
    {
        Items camcorderItem = Resources.Load<Items>(CamcorderItemPath);
        if (camcorderItem == null || camcorderItem.itemPrefab == null)
            return null;
        return camcorderItem.itemPrefab.GetComponentInChildren<CamcorderController>(true);
    }

    /// <summary>
    /// 죽은 자리 바닥 → 괴물 반대쪽으로 조금 굴러간 위치에서 괴물 상체를 올려다보고, 옆으로 쓰러진 만큼 롤.
    /// 괴물이 없으면 바라보던 방향 바닥을 비춤.
    /// </summary>
    private static void PlaceOnFloor(Vector3 eyePosition, Vector3 eyeForward, EnemyBase monster, out Vector3 position, out Quaternion rotation)
    {
        Vector3 floor = Physics.Raycast(eyePosition, Vector3.down, out RaycastHit floorHit, 4f, ~0, QueryTriggerInteraction.Ignore)
            ? floorHit.point
            : eyePosition - Vector3.up * 1.6f;

        Vector3 lookTarget;
        if (monster != null)
        {
            // 발밑과 머리 사이(머리 쪽)를 겨눠 몸통과 얼굴이 함께 걸리게
            Vector3 root = monster.transform.position;
            lookTarget = Vector3.Lerp(root + Vector3.up * 0.5f, GetHeadPosition(monster), 0.65f);
        }
        else
        {
            lookTarget = floor + Flat(eyeForward, Vector3.forward) * 3f + Vector3.up * 0.8f;
        }

        Vector3 toTarget = Flat(lookTarget - floor, eyeForward);
        Vector3 lens = floor + Vector3.up * LensHeight;

        // 뒤로 굴러간 자리 (벽에 막히면 그 앞까지). 큰 괴물일수록 더 멀리 — 가까우면 배만 화면을 채움
        float pull = PullBack;
        if (monster != null)
        {
            float headHeight = GetHeadPosition(monster).y - floor.y;
            float desired = Mathf.Clamp(headHeight * 0.9f, MinMonsterDistance, MaxMonsterDistance);
            float current = Vector3.Distance(Flat3(lens), Flat3(monster.transform.position));
            pull = Mathf.Clamp(desired - current, PullBack, MaxPullBack);
        }
        if (Physics.Raycast(lens, -toTarget, out RaycastHit wallHit, pull + 0.1f, ~0, QueryTriggerInteraction.Ignore))
            pull = Mathf.Max(0f, wallHit.distance - 0.1f);
        position = lens - toTarget * pull;

        Vector3 look = Quaternion.AngleAxis(YawOffset, Vector3.up) * (lookTarget - position);
        // 너무 올려다보면 천장만 찍히므로 제한
        float horizontal = new Vector2(look.x, look.z).magnitude;
        float maxRise = horizontal * Mathf.Tan(MaxPitchUp * Mathf.Deg2Rad);
        if (look.y > maxRise)
            look.y = maxRise;
        rotation = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(0f, 0f, RollAngle);
    }

    /// <summary>
    /// 떨어진 캠코더 앞에서 괴물이 잠깐 서 있다가 다시 순찰을 떠나게 한다.
    /// 점프스케어가 3초 뒤 ReleaseEnemiesToPatrol을 부르지만, 게임오버 화면이 점프스케어를 즉시 지워 그 호출이 사라지므로 여기서 대신 부른다.
    /// </summary>
    private IEnumerator ReleaseToPatrol(EnemyBase monster)
    {
        Animator anim = monster != null ? (monster.Anim != null ? monster.Anim : monster.GetComponentInChildren<Animator>(true)) : null;
        AnimatorUpdateMode originalUpdateMode = anim != null ? anim.updateMode : AnimatorUpdateMode.Normal;
        bool originalRootMotion = anim != null && anim.applyRootMotion;
        AnimateInPlace(monster, anim);

        yield return new WaitForSecondsRealtime(PatrolReleaseDelay);

        if (anim != null)
        {
            anim.updateMode = originalUpdateMode;
            anim.applyRootMotion = originalRootMotion;
        }
        PlayerDeathDebug.ReleaseEnemiesToPatrol();
    }

    /// <summary>
    /// 사망 처리(SnapToPatrolAnimation(hold))가 애니메이터 속도를 0으로 묶어 괴물이 굳어 있으므로,
    /// 순찰을 떠나기 전까지 제자리 대기 동작을 재생해 떨어진 캠코더 화면 속에서 숨 쉬듯 움직이게 한다.
    /// 제자리를 벗어나지 않게 루트 모션은 끄고, timeScale=0이어도 돌도록 unscaled 시간으로 갱신.
    /// </summary>
    private static void AnimateInPlace(EnemyBase monster, Animator anim)
    {
        if (monster == null || anim == null || anim.runtimeAnimatorController == null)
            return;

        anim.enabled = true;
        anim.applyRootMotion = false;
        anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        anim.speed = 1f;
        anim.SetInteger(monster.AnimStateHash, 0);

        // Idle이 없는 괴물(TheMimic 등)은 걷기 동작을 제자리에서 재생
        string[] names = { "Base Layer.Idle", "Idle", "Base Layer.Patrol", "Patrol", "Base Layer.Walk", "Walk", "Armature|Walk" };
        foreach (string name in names)
        {
            int hash = Animator.StringToHash(name);
            if (!anim.HasState(0, hash))
                continue;
            anim.Play(hash, 0, Random.value);
            anim.Update(0f);
            return;
        }
    }

    private static EnemyBase FindNearestEnemy(Vector3 from)
    {
        EnemyBase nearest = null;
        float best = float.MaxValue;
        // 사망 시 적 컴포넌트는 꺼지지만 오브젝트는 남아 있음
        foreach (EnemyBase enemy in FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
        {
            float d = (enemy.transform.position - from).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = enemy;
            }
        }
        return nearest;
    }

    // 점프스케어와 같은 순서로 얼굴 위치를 찾음: 지정 얼굴 지점 → 휴머노이드 머리 뼈 → 눈 위치 → 루트 + 1.6m
    private static Vector3 GetHeadPosition(EnemyBase enemy)
    {
        if (enemy.jumpscareFacePoint != null)
            return enemy.jumpscareFacePoint.position;

        Animator anim = enemy.Anim != null ? enemy.Anim : enemy.GetComponentInChildren<Animator>(true);
        if (anim != null && anim.isHuman)
        {
            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
                return head.position;
        }

        if (enemy.EyePoint != null && enemy.EyePoint != enemy.transform)
            return enemy.EyePoint.position;

        return enemy.transform.position + Vector3.up * 1.6f;
    }

    private void BuildNightVisionVolume(CamcorderController camcorder, int nightVisionLayer)
    {
        var volGo = new GameObject("DroppedCamcorderNightVision") { layer = nightVisionLayer };
        volGo.transform.SetParent(transform, false);
        Volume volume = volGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 101f; // 들고 있던 캠코더 볼륨보다 우선
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.sharedProfile = profile;
        camcorder.CreateNightVisionLook().ApplyTo(profile);
    }

    private void BuildIrLight(CamcorderController camcorder, EnemyBase monster)
    {
        var lightGo = new GameObject("DroppedCamcorderIR");
        lightGo.transform.SetParent(lensCamera.transform, false);
        Light ir = lightGo.AddComponent<Light>();
        ir.type = LightType.Spot;
        ir.range = camcorder != null ? camcorder.IrRange : 22f;
        ir.spotAngle = camcorder != null ? camcorder.IrSpotAngle : 75f;
        ir.innerSpotAngle = ir.spotAngle * 0.4f;
        ir.shadows = LightShadows.None;
        ir.cullingMask = ~LayerBit("PickupItem");

        float baseIntensity = camcorder != null ? camcorder.IrIntensity : 6f;
        // 괴물 콜라이더는 보통 몸보다 작아 레이가 빗나가므로, 구운 메시로 잠깐 콜라이더를 만들어 실제 표면 거리를 잼
        GameObject[] proxies = CreateMeshColliderProxies(monster);
        float factor = AverageSurfaceFactor(lensCamera.transform, ir);
        foreach (GameObject proxy in proxies)
        {
            Destroy(proxy.GetComponent<MeshCollider>().sharedMesh);
            Destroy(proxy);
        }
        ir.intensity = Mathf.Min(baseIntensity, NearSurfaceBrightnessCap / factor);
    }

    private static GameObject[] CreateMeshColliderProxies(EnemyBase monster)
    {
        if (monster == null)
            return new GameObject[0];

        var proxies = new System.Collections.Generic.List<GameObject>();
        foreach (SkinnedMeshRenderer skin in monster.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (skin.sharedMesh == null)
                continue;
            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            var proxy = new GameObject("IrDistanceProxy");
            proxy.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
            proxy.AddComponent<MeshCollider>().sharedMesh = baked;
            proxies.Add(proxy);
        }
        Physics.SyncTransforms();
        return proxies.ToArray();
    }

    /// <summary>
    /// 빛 원뿔 안 여러 방향으로 레이를 쏴 표면 밝기 계수(입사각 cos / 거리²) 평균을 구함.
    /// 캠코더는 움직이지 않으므로 처음 괴물이 앞에 서 있을 때 한 번만 계산 (SpotLightProximityDimmer와 같은 기준, 그쪽은 deltaTime 기반이라 정지 중 동작 안 함).
    /// </summary>
    private static float AverageSurfaceFactor(Transform lens, Light ir)
    {
        float halfInner = ir.innerSpotAngle * 0.5f;
        float sum = 0f;
        int count = 0;
        for (int ring = 0; ring <= 1; ring++)
        {
            int samples = ring == 0 ? 1 : 8;
            for (int i = 0; i < samples; i++)
            {
                Quaternion offset = Quaternion.AngleAxis(i * 45f, Vector3.forward) * Quaternion.AngleAxis(ring * halfInner, Vector3.up);
                Vector3 dir = lens.rotation * (offset * Vector3.forward);
                if (Physics.Raycast(lens.position, dir, out RaycastHit hit, ir.range, ~0, QueryTriggerInteraction.Ignore))
                {
                    float d = Mathf.Max(0.15f, hit.distance);
                    sum += Mathf.Max(0.1f, Vector3.Dot(-dir, hit.normal)) / (d * d);
                }
                count++;
            }
        }
        return Mathf.Max(0.0001f, sum / count);
    }

    private static Vector3 Flat3(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f)
        {
            v = fallback;
            v.y = 0f;
        }
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }

    private static int LayerBit(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 ? 1 << layer : 0;
    }

    private void OnDestroy()
    {
        if (lensCamera != null)
            lensCamera.targetTexture = null;
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
        if (profile != null)
            Destroy(profile);
    }
}

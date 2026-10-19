using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 스포트라이트(손전등, 캠코더 적외선 조명)를 켠 채 벽·물체에 가까이 붙으면 빛이 하얗게 날아가는 것을 막는다.
/// 빛 원뿔 안 여러 방향(중앙 + 두 링 × 8)으로 레이를 쏴 표면 밝기(intensity × 입사각 cos / 거리²)의 평균이
/// maxSurfaceBrightness를 넘으면 그만큼 intensity를 줄인다.
/// 중앙 한 점만 보면 벽을 비스듬히 볼 때 값이 크게 출렁여(가장자리는 바로 앞 벽에 닿는데 중앙은 멀어짐) 깜빡이고,
/// 최댓값을 쓰면 바닥 아래쪽 가장자리 때문에 평소 바닥까지 어두워지므로 원뿔 전체 평균을 쓴다.
/// 평소처럼 바닥을 비추면 안 줄고, 가까운 면을 비출 때만 줄어든다.
/// 어두워질 땐 빠르게(하얗게 번쩍임 방지), 밝아질 땐 천천히(깜빡임 방지) 따라간다.
/// FlashlightEnergyController 등이 매 프레임 intensity를 덮어쓸 수 있으므로, 렌더링하는 동안에만 감쇠를 적용하고
/// 끝나면 원래 값으로 되돌린다 (게임 로직은 항상 원래 intensity를 봄).
/// 상한은 화면에 보이는 밝기 기준이라, 후처리 노출을 올리는 카메라(캠코더 나이트비전)용 빛은 Configure로 낮춰 준다.
/// </summary>
public class SpotLightProximityDimmer : MonoBehaviour
{
    private const int RayCountPerRing = 8;
    private static readonly float[] RingAngleRatios = { 0.5f, 1f };

    [Tooltip("원뿔 안 평균 표면 밝기 상한 (intensity × cos / 거리²). 낮출수록 가까운 벽이 어두워짐")]
    [SerializeField] private float maxSurfaceBrightness = 1f;
    [Tooltip("이 거리 안의 표면만 검사")]
    [SerializeField] private float probeDistance = 8f;
    [Tooltip("아무리 가까워도 이 비율 아래로는 줄이지 않음")]
    [SerializeField] private float minIntensityRatio = 0.0005f;
    [Tooltip("어두워지는 속도 (클수록 즉시)")]
    [SerializeField] private float dimSpeed = 20f;
    [Tooltip("다시 밝아지는 속도 (작을수록 천천히)")]
    [SerializeField] private float brightenSpeed = 3f;

    private Light spotLight;
    private int probeMask;
    private float currentRatio = 1f;
    private bool isDimmed;
    private float restoreIntensity;

    /// <summary>AddComponent 직후 상한을 지정할 때 사용.</summary>
    public void Configure(float surfaceBrightnessCap)
    {
        maxSurfaceBrightness = surfaceBrightnessCap;
    }

    private void Awake()
    {
        spotLight = GetComponentInChildren<Light>(true);
        probeMask = ~LayerMask.GetMask("Player", "PickupItem", "Ignore Raycast");
    }

    private void OnEnable()
    {
        currentRatio = 1f;
        RenderPipelineManager.beginContextRendering += ApplyDim;
        RenderPipelineManager.endContextRendering += RestoreIntensity;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginContextRendering -= ApplyDim;
        RenderPipelineManager.endContextRendering -= RestoreIntensity;
        RestoreIntensity(default, null);
    }

    private void LateUpdate()
    {
        // 저배터리 깜빡임으로 잠깐 꺼질 때 비율을 초기화하면 다시 켜질 때 번쩍이므로 유지
        if (spotLight == null || !spotLight.enabled)
        {
            return;
        }

        float target = GetTargetRatio();
        float speed = target < currentRatio ? dimSpeed : brightenSpeed;
        currentRatio = Mathf.Lerp(currentRatio, target, Time.deltaTime * speed);
    }

    private float GetTargetRatio()
    {
        Transform beam = spotLight.transform;
        float totalBrightness = GetSurfaceBrightness(beam.position, beam.forward);
        int rayCount = 1;

        float innerHalfAngle = spotLight.innerSpotAngle * 0.5f;
        foreach (float ringRatio in RingAngleRatios)
        {
            Quaternion tilt = Quaternion.AngleAxis(innerHalfAngle * ringRatio, beam.right);
            for (int i = 0; i < RayCountPerRing; i++)
            {
                Quaternion spin = Quaternion.AngleAxis(360f / RayCountPerRing * i, beam.forward);
                Vector3 dir = spin * (tilt * beam.forward);
                totalBrightness += GetSurfaceBrightness(beam.position, dir);
                rayCount++;
            }
        }

        float averageBrightness = totalBrightness / rayCount;
        if (averageBrightness <= maxSurfaceBrightness)
        {
            return 1f;
        }

        return Mathf.Clamp(maxSurfaceBrightness / averageBrightness, minIntensityRatio, 1f);
    }

    private float GetSurfaceBrightness(Vector3 origin, Vector3 dir)
    {
        if (!Physics.Raycast(origin, dir, out RaycastHit hit, probeDistance, probeMask, QueryTriggerInteraction.Ignore))
        {
            return 0f;
        }

        float distance = Mathf.Max(hit.distance, 0.05f);
        float facing = Mathf.Max(Vector3.Dot(hit.normal, -dir), 0.1f);
        return spotLight.intensity * facing / (distance * distance);
    }

    private void ApplyDim(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (spotLight == null || !spotLight.enabled || currentRatio >= 0.999f)
        {
            return;
        }

        restoreIntensity = spotLight.intensity;
        spotLight.intensity = restoreIntensity * currentRatio;
        isDimmed = true;
    }

    private void RestoreIntensity(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (!isDimmed)
        {
            return;
        }

        isDimmed = false;
        if (spotLight != null)
        {
            spotLight.intensity = restoreIntensity;
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 캠코더(나이트비전) 들기 뷰에 붙는 컨트롤러.
/// 좌클릭 시 [뷰파인더 펼치기 → 앵커/Ortho로 뷰파인더가 화면 가득 → LCD에 피드 켜기] 순서로 연출합니다.
///
///   • RT 카메라: 플레이어 눈(Main Camera) 시야와 동기화 (+ NightVision Volume / IR)
///   • LCD 쿼드: Display 자식. Unlit + RT. HUD는 쿼드 위 World Space(ItemViewCamera)
///   • 들어올리기: ItemViewAnchor + RaisePivot + Ortho로 통째 이동·확대 (Display 경첩은 회전만)
///   • RaisePivot 대기 자세: 프리팹 Transform 캐시(기본). 종료/비활성 후에도 인스펙터 튜닝 유지.
///   • 들어올리기 전환: 카메라 pullback + bounds keep-out + soft RaisePivot Z (관통 클리핑 방지).
///   • 전환 애니: 통째 이동. 사용 중(피드 ON)에만 본체 숨김 + Display eye 레이아웃 스냅
/// </summary>
public class CamcorderController : MonoBehaviour
{
    [Header("모델 참조 (비우면 자동 탐색)")]
    [Tooltip("들어올리기 연출을 적용할 피벗. 비우면 첫 번째 자식을 사용합니다.")]
    [SerializeField] private Transform raisePivot;
    [Tooltip("펼치는 뷰파인더 패널(경첩). 비우면 이름으로 자동 탐색합니다.")]
    [SerializeField] private Transform displayPanel;
    [SerializeField] private string displayPanelName = "Camcorder_Display";

    [Header("펼치기 (Display 경첩 회전)")]
    [Tooltip("접힘 기준에서 펼칠 때 더할 로컬 회전(도). 데모 기준 Y +90도.")]
    [SerializeField] private Vector3 unfoldEulerOffset = new Vector3(0f, 90f, 0f);
    [SerializeField] private float unfoldDuration = 0.4f;

    [Header("들어올리기 — RaisePivot (미세 자세)")]
    [Tooltip("켜면 아래 Lowered 값을 대기 자세로 씁니다. 끄면 Start 시 RaisePivot Transform(프리팹)을 대기 자세로 캐시합니다.")]
    [SerializeField] private bool useSerializedLoweredPose;
    [Tooltip("useSerializedLoweredPose일 때만: 내린(대기) RaisePivot 로컬 오프셋")]
    [SerializeField] private Vector3 loweredLocalPosition = new Vector3(0f, -0.04f, 0.02f);
    [SerializeField] private Vector3 loweredLocalEuler = Vector3.zero;
    [Tooltip("들어올린 상태 RaisePivot 로컬 오프셋")]
    [SerializeField] private Vector3 raisedLocalPosition = new Vector3(0f, 0f, -0.18f);
    [SerializeField] private Vector3 raisedLocalEuler = new Vector3(-10f, 2f, 0f);
    [SerializeField] private float raiseDuration = 0.5f;

    [Header("들어올리기 — ItemViewAnchor (시야 앞으로)")]
    [Tooltip("손에 든 아이템 앵커를 메인 카메라 앞 중앙·가까이로 이동")]
    [SerializeField] private bool animateItemAnchor = true;
    [Tooltip("체크 시 씬 ItemViewAnchor 대기 위치에서 눈앞 좌표를 자동 계산")]
    [SerializeField] private bool autoComputeAnchorEye = true;
    [Tooltip("눈앞(사용) ItemViewAnchor 로컬 위치")]
    [SerializeField] private Vector3 anchorEyeLocalPosition = new Vector3(0.06f, 0.02f, 0.13f);
    [SerializeField] private Vector3 anchorEyeLocalEuler = new Vector3(-3f, 0f, 0f);

    [Header("들어올리기 — ItemViewCamera (캠코더만 확대)")]
    [Tooltip("캠코더 사용 중 ItemViewCamera Orthographic Size 보간")]
    [SerializeField] private bool animateItemViewOrtho = true;
    [Tooltip("뷰파인더가 화면에 가득 찰 때 Orthographic Size (작을수록 큼)")]
    [SerializeField] private float itemViewEyeOrthographicSize = 0.062f;

    [Header("들어올리기 — ItemView 카메라 관통(클리핑) 방지")]
    [Tooltip("모델 bounds가 ItemViewCamera near 앞으로 들어오면 앵커를 뒤로 밀어 관통을 막습니다.")]
    [SerializeField] private bool preventMeshCameraPenetration = true;
    [Tooltip("카메라 local Z 기준, near + 이 여유보다 앞에 bounds가 오도록")]
    [SerializeField] private float meshClearanceBeyondNear = 0.04f;
    [Tooltip("들어올리기/내리기 중 ItemViewCamera를 local -Z로 뒤로 빼는 양")]
    [SerializeField] private float itemViewCameraPullbackLocalZ = 0.06f;
    [Tooltip("전환 중 RaisePivot Z는 완화, 애니 끝에만 Raised Local Position 적용")]
    [SerializeField] private bool useSoftRaisePivotDuringTransition = true;
    [Tooltip("전환 중 RaisePivot 목표 Z (최종 raised Z보다 카메라에서 덜 당김)")]
    [SerializeField] private float transitionRaisedLocalPositionZ = -0.06f;
    [Tooltip("Ortho 확대/축소를 raise 애니 후반에만 적용")]
    [SerializeField] private bool staggerOrthoDuringRaise = true;
    [Range(0f, 0.95f)]
    [SerializeField] private float orthoBlendStart = 0.45f;

    [Header("화면 켜짐 페이드")]
    [SerializeField] private float showDuration = 0.25f;

    [Header("화면 쿼드 (Display 자식, 데모값)")]
    [Tooltip("Display 기준 로컬 위치")]
    [SerializeField] private Vector3 screenLocalPosition = new Vector3(0.0124f, 0.0021f, -0.0759f);
    [Tooltip("Display 기준 로컬 회전(도)")]
    [SerializeField] private Vector3 screenLocalEuler = new Vector3(0f, -90f, 0f);
    [Tooltip("Display rest scale=1 기준. 사용 중 Display 확대 시 역보정")]
    [SerializeField] private Vector3 screenLocalScale = new Vector3(0.2f, 0.1125f, 0.0125f);

    [Header("사용 중 — Display 레이아웃 (피드 ON·본체 숨김 때만, 전환 애니 제외)")]
    [SerializeField] private Vector3 displayActiveLocalPosition = new Vector3(0.0599f, -0.0751f, 0.3089f);
    [Tooltip("체크 시 screenLocalScale/데모 비율로 사용 중 Display 스케일 자동")]
    [SerializeField] private bool autoComputeDisplayActiveScale = true;
    [SerializeField] private Vector3 displayActiveLocalScale = new Vector3(1.968674f, 1.968674f, 1.968674f);

    [Header("Display 베zel 반사")]
    [SerializeField] private bool reduceDisplayBezelReflection = true;
    [Range(0f, 1f)]
    [SerializeField] private float displayBezelSmoothness = 0.08f;

    [Header("RT 카메라 (플레이어 시야 동기화)")]
    [SerializeField] private int renderTextureWidth = 1024;
    [SerializeField] private int renderTextureHeight = 768;
    [SerializeField] private float defaultFov = 55f;
    [SerializeField] private float minFov = 22f;
    [SerializeField] private float maxFov = 60f;
    [SerializeField] private float zoomStep = 4f;

    [Header("NightVision Volume")]
    [SerializeField] private string nightVisionLayerName = "NightVision";
    [Tooltip("노출 부스트(EV). 너무 높으면 화면 중앙이 하얗게 날아갑니다.")]
    [SerializeField] private float postExposure = 0.6f;
    [Tooltip("초록 색조 틴트")]
    [SerializeField] private Color colorFilter = new Color(0.55f, 1f, 0.6f, 1f);
    [Tooltip("탈색(−100이면 완전 흑백 후 초록 틴트 = 전형적 나이트비전)")]
    [Range(-100f, 0f)]
    [SerializeField] private float saturation = -65f;
    [Tooltip("밝은 부위만 약하게 번지도록")]
    [SerializeField] private float bloomIntensity = 0.7f;
    [SerializeField] private float bloomThreshold = 0.9f;
    [SerializeField] private float vignetteIntensity = 0.45f;
    [Tooltip("필름 그레인(거친 입자) 강도")]
    [Range(0f, 1f)]
    [SerializeField] private float filmGrainIntensity = 0.35f;

    [Header("IR Spot Light (펼친 동안 ON)")]
    [Tooltip("적외선 조명 세기. 어두운 곳을 비추는 정도. 너무 높으면 가까운 면이 날아갑니다.")]
    [SerializeField] private float irIntensity = 1.3f;
    [SerializeField] private float irRange = 22f;
    [SerializeField] private float irSpotAngle = 75f;

    [Header("뷰파인더 UI 스프라이트 (선택)")]
    [SerializeField] private Sprite frameSprite;        // Center_Frame / Full_Frame
    [SerializeField] private Sprite recordingDotSprite; // Recording_Dot
    [SerializeField] private Sprite batteryEmptySprite;
    [SerializeField] private Sprite[] batteryLevelSprites = new Sprite[4]; // Battery_1 ~ 4

    private Camera eyeCamera;
    private Camera lensCamera;
    private RenderTexture renderTexture;
    private Light irSpot;
    private Volume nightVisionVolume;
    private VolumeProfile nightVisionProfile;
    private int nightVisionLayer = -1;

    private Transform screenTransform;
    private MeshRenderer screenRenderer;
    private Material screenMaterial;
    private Vector3 screenLocalScaleBase;
    private Vector3 displayRestLocalPosition;
    private Vector3 displayRestLocalScale = Vector3.one;
    private Vector3 displayActiveLocalScaleResolved = Vector3.one;
    private MaterialPropertyBlock displayBezelPropertyBlock;
    private Canvas viewfinderCanvas;
    private Image recDot;
    private Image batteryImage;
    private CamcorderEnergyController camcorderEnergy;

    private Quaternion displayClosedRotation;
    private Quaternion displayOpenRotation;

    private Transform itemViewAnchor;
    private Vector3 anchorRestLocalPosition;
    private Quaternion anchorRestLocalRotation;
    private Camera itemViewCamera;
    private Vector3 itemViewCameraRestLocalPosition;
    private float itemViewRestOrthographicSize;
    private HeldItemSway heldItemSway;
    private Renderer[] bodyRenderers;
    private Renderer[] displayBackgroundRenderers;

    private Vector3 loweredRestLocalPosition;
    private Quaternion loweredRestLocalRotation = Quaternion.identity;

    private bool isHeldView;
    private bool active;

    /// <summary>뷰파인더·야간투시가 켜진 상태(배터리 소모 중).</summary>
    public bool IsViewfinderActive => isHeldView && active;
    private float targetFov;
    private float screenLevel; // 0 = 꺼짐(검정), 1 = 완전 표시
    private Coroutine sequenceRoutine;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly Vector3 DemoScreenLocalScale = new Vector3(0.101591215f, 0.057145067f, 0.006349451f);
    private void Start()
    {
        // itemPrefab은 월드 드롭에도 쓰이므로, 손에 든 뷰일 때만 기능을 켭니다.
        isHeldView = GetComponentInParent<EquipmentViewController>() != null;
        if (!isHeldView)
        {
            enabled = false;
            return;
        }

        ResolveReferences();
        CacheRaisePivotRest();
        CacheDisplayRest();
        CacheItemViewAnchor();
        CacheItemViewCamera();
        heldItemSway = GetComponent<HeldItemSway>();
        ResolveNightVisionLayer();
        BuildLensCameraAndRig();
        BuildScreenQuad();
        CacheBodyRenderers();
        BuildViewfinderCanvas();
        ResolveCamcorderEnergy();
        Camera.onPreCull += OnCameraPreCull;
        ApplyImmediate(false);
    }

    private void OnEnable()
    {
        if (isHeldView)
        {
            active = false;
            StopSequence();
            ApplyImmediate(false);
        }
    }

    private void OnDisable()
    {
        UnregisterFromEnergy();
        Camera.onPreCull -= OnCameraPreCull;

        if (isHeldView)
        {
            active = false;
            ApplyImmediate(false);
        }
    }

    private void OnDestroy()
    {
        Camera.onPreCull -= OnCameraPreCull;

        if (lensCamera != null) lensCamera.targetTexture = null;
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
        if (lensCamera != null) Destroy(lensCamera.gameObject);
        if (nightVisionVolume != null) Destroy(nightVisionVolume.gameObject);
        if (nightVisionProfile != null) Destroy(nightVisionProfile);
        if (screenMaterial != null) Destroy(screenMaterial);
        RestoreItemViewOrthographicSize();
        RestoreItemViewCameraLocalPosition();
    }

    /// <summary>좌클릭 시 SelectedItemUseController가 호출. 펼치기/접기 토글.</summary>
    public void ToggleRaise()
    {
        if (!isHeldView) return;

        if (!active && !CanOpenViewfinder())
        {
            Debug.LogWarning("[Camcorder] 배터리가 방전되어 뷰파인더를 켤 수 없습니다.");
            return;
        }

        SetActive(!active);
    }

    public void SetActive(bool value)
    {
        if (value && !CanOpenViewfinder())
            return;

        if (active == value) return;
        active = value;

        if (value)
            RegisterWithEnergy();
        else
            UnregisterFromEnergy();

        StopSequence();
        sequenceRoutine = StartCoroutine(value ? ActivateSequence() : DeactivateSequence());
    }

    private void StopSequence()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        if (heldItemSway != null && !active)
            heldItemSway.enabled = true;

        RestoreItemViewOrthographicSize();
        RestoreItemViewCameraLocalPosition();
        RestoreDisplayRest();
        SetBodyMeshesVisible(true);
        if (!active)
            SetViewfinderFeedActive(false);
    }

    private void OnCameraPreCull(Camera cam)
    {
        if (!isHeldView || !active || cam != lensCamera || lensCamera == null)
            return;

        if (eyeCamera == null)
            eyeCamera = Camera.main;

        if (eyeCamera == null)
            return;

        SyncLensCameraToEye();
    }

    private void Update()
    {
        if (!isHeldView || !active || lensCamera == null) return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetFov = Mathf.Clamp(targetFov - scroll * zoomStep, minFov, maxFov);
        }
        lensCamera.fieldOfView = Mathf.Lerp(lensCamera.fieldOfView, targetFov, Time.deltaTime * 10f);

        if (recDot != null)
        {
            float a = Mathf.PingPong(Time.time * 1.5f, 1f);
            Color c = recDot.color;
            c.a = a;
            recDot.color = c;
        }

        RefreshBatteryHud();
    }

    private IEnumerator ActivateSequence()
    {
        // Phase 1: 뷰파인더(Display) 펼치기
        yield return AnimateDisplay(displayClosedRotation, displayOpenRotation, unfoldDuration);

        // Phase 2: 통째로 눈앞 이동 (본체+Display 함께)
        yield return AnimateBringToEye(true);

        // Phase 3: 본체 숨김 + 사용 중 Display 레이아웃(스냅), 피드 ON
        SetBodyMeshesVisible(false);
        ApplyActiveDisplayLayout();

        targetFov = eyeCamera != null ? eyeCamera.fieldOfView : defaultFov;
        if (lensCamera != null)
            lensCamera.fieldOfView = targetFov;

        SetViewfinderFeedActive(true);
        if (lensCamera != null && eyeCamera != null)
            SyncLensCameraToEye();

        yield return AnimateScreen(0f, 1f, showDuration);

        sequenceRoutine = null;
    }

    private IEnumerator DeactivateSequence()
    {
        // RT·HUD·IR 먼저 OFF — 내리기/접기 중 LCD에 피드가 붙어 움직이지 않게
        SetViewfinderFeedActive(false);

        SetBodyMeshesVisible(true);
        RestoreDisplayRest();

        yield return AnimateBringToEye(false);

        yield return AnimateDisplay(displayPanel != null ? displayPanel.localRotation : displayOpenRotation,
                                    displayClosedRotation, unfoldDuration);

        sequenceRoutine = null;
    }

    private IEnumerator AnimateDisplay(Quaternion from, Quaternion to, float duration)
    {
        if (displayPanel == null) yield break;

        float t = 0f;
        float d = Mathf.Max(0.01f, duration);
        while (t < d)
        {
            t += Time.deltaTime;
            float k = Smooth(Mathf.Clamp01(t / d));
            displayPanel.localRotation = Quaternion.Slerp(from, to, k);
            EnforceMeshClearanceInViewCameraSpace();
            yield return null;
        }
        displayPanel.localRotation = to;
        EnforceMeshClearanceInViewCameraSpace();
    }

    /// <summary>ItemViewAnchor + RaisePivot 보간. 카메라 관통 방지( pullback / bounds / soft pivot ).</summary>
    private IEnumerator AnimateBringToEye(bool toEye)
    {
        if (heldItemSway != null)
            heldItemSway.enabled = !toEye;

        Vector3 pivotFromPos = raisePivot != null ? raisePivot.localPosition : GetLoweredLocalPosition();
        Quaternion pivotFromRot = raisePivot != null ? raisePivot.localRotation : GetLoweredLocalRotation();
        Vector3 pivotToPosTransition = GetRaisePivotTargetPosition(toEye, endPose: false);
        Vector3 pivotToPosFinal = GetRaisePivotTargetPosition(toEye, endPose: true);
        Quaternion pivotToRot = toEye ? Quaternion.Euler(raisedLocalEuler) : GetLoweredLocalRotation();

        Vector3 anchorFromPos = itemViewAnchor != null ? itemViewAnchor.localPosition : anchorRestLocalPosition;
        Quaternion anchorFromRot = itemViewAnchor != null ? itemViewAnchor.localRotation : anchorRestLocalRotation;
        Vector3 anchorToPos = toEye ? anchorEyeLocalPosition : anchorRestLocalPosition;
        Quaternion anchorToRot = toEye ? Quaternion.Euler(anchorEyeLocalEuler) : anchorRestLocalRotation;

        float orthoFrom = itemViewRestOrthographicSize;
        float orthoTo = toEye ? itemViewEyeOrthographicSize : itemViewRestOrthographicSize;

        float t = 0f;
        float d = Mathf.Max(0.01f, raiseDuration);
        while (t < d)
        {
            t += Time.deltaTime;
            float k = Smooth(Mathf.Clamp01(t / d));

            ApplyItemViewCameraPullback(toEye ? k : 1f - k);

            if (raisePivot != null)
            {
                raisePivot.localPosition = Vector3.Lerp(pivotFromPos, pivotToPosTransition, k);
                raisePivot.localRotation = Quaternion.Slerp(pivotFromRot, pivotToRot, k);
            }

            if (animateItemAnchor && itemViewAnchor != null)
            {
                itemViewAnchor.localPosition = Vector3.Lerp(anchorFromPos, anchorToPos, k);
                itemViewAnchor.localRotation = Quaternion.Slerp(anchorFromRot, anchorToRot, k);
            }

            if (animateItemViewOrtho && itemViewCamera != null)
            {
                float orthoK = staggerOrthoDuringRaise ? OrthoBlendT(k, orthoBlendStart) : k;
                itemViewCamera.orthographicSize = Mathf.Lerp(orthoFrom, orthoTo, Smooth(orthoK));
            }

            EnforceMeshClearanceInViewCameraSpace();
            yield return null;
        }

        RestoreItemViewCameraLocalPosition();

        if (raisePivot != null)
        {
            raisePivot.localPosition = pivotToPosFinal;
            raisePivot.localRotation = pivotToRot;
        }

        if (animateItemAnchor && itemViewAnchor != null)
        {
            itemViewAnchor.localPosition = anchorToPos;
            itemViewAnchor.localRotation = anchorToRot;
        }

        if (animateItemViewOrtho && itemViewCamera != null)
            itemViewCamera.orthographicSize = orthoTo;

        EnforceMeshClearanceInViewCameraSpace();

        if (!toEye)
            RestoreDisplayRest();

        if (heldItemSway != null && !toEye)
            heldItemSway.enabled = true;
    }

    private IEnumerator AnimateScreen(float from, float to, float duration)
    {
        float t = 0f;
        float d = Mathf.Max(0.01f, duration);
        while (t < d)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            SetScreenLevel(Mathf.Lerp(from, to, k));
            yield return null;
        }
        SetScreenLevel(to);
    }

    private void SetScreenLevel(float level)
    {
        screenLevel = Mathf.Clamp01(level);
        if (screenMaterial != null)
        {
            Color c = new Color(screenLevel, screenLevel, screenLevel, 1f);
            screenMaterial.SetColor(BaseColorId, c);
            screenMaterial.color = c;
        }
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    /// <summary>0~blendStart 구간에서는 0, 이후 0~1로 진행 (Ortho를 앵커 이동 뒤에 맞춤).</summary>
    private static float OrthoBlendT(float k, float blendStart)
    {
        blendStart = Mathf.Clamp(blendStart, 0f, 0.95f);
        if (k <= blendStart)
            return 0f;

        return (k - blendStart) / (1f - blendStart);
    }

    private void ApplyImmediate(bool value)
    {
        if (displayPanel != null)
            displayPanel.localRotation = value ? displayOpenRotation : displayClosedRotation;

        SetDisplayBezelMatte(value && reduceDisplayBezelReflection);

        if (raisePivot != null)
        {
            raisePivot.localPosition = value ? raisedLocalPosition : GetLoweredLocalPosition();
            raisePivot.localRotation = value ? Quaternion.Euler(raisedLocalEuler) : GetLoweredLocalRotation();
        }

        if (animateItemAnchor && itemViewAnchor != null)
        {
            itemViewAnchor.localPosition = value ? anchorEyeLocalPosition : anchorRestLocalPosition;
            itemViewAnchor.localRotation = value
                ? Quaternion.Euler(anchorEyeLocalEuler)
                : anchorRestLocalRotation;
        }

        if (heldItemSway != null)
            heldItemSway.enabled = !value;

        if (animateItemViewOrtho && itemViewCamera != null)
            itemViewCamera.orthographicSize = value ? itemViewEyeOrthographicSize : itemViewRestOrthographicSize;

        RestoreItemViewCameraLocalPosition();
        if (value)
            EnforceMeshClearanceInViewCameraSpace();

        SetBodyMeshesVisible(!value);

        if (value)
            ApplyActiveDisplayLayout();
        else
            RestoreDisplayRest();

        SetViewfinderFeedActive(value);

        if (value && lensCamera != null && eyeCamera != null)
            SyncLensCameraToEye();
    }

    /// <summary>RT 렌즈·IR·LCD·HUD 피드 일괄 ON/OFF.</summary>
    private void SetViewfinderFeedActive(bool value)
    {
        if (irSpot != null)
            irSpot.enabled = value;

        if (lensCamera != null)
            lensCamera.enabled = value;

        if (screenRenderer != null)
            screenRenderer.enabled = value;

        SetViewfinderHudVisible(value);
        SetScreenLevel(value ? 1f : 0f);
    }

    private void SetViewfinderHudVisible(bool visible)
    {
        if (viewfinderCanvas != null)
            viewfinderCanvas.gameObject.SetActive(visible);

        // 켤 때(visible) 최신 배터리 반영, 끌 때는 Empty를 잠깐 표시한 뒤 캔버스 숨김
        RefreshBatteryHud();
    }

    private void ResolveCamcorderEnergy()
    {
        EquipmentViewController viewController = GetComponentInParent<EquipmentViewController>();
        if (viewController != null)
            camcorderEnergy = viewController.GetComponent<CamcorderEnergyController>();

        if (camcorderEnergy == null)
            camcorderEnergy = FindFirstObjectByType<CamcorderEnergyController>();
    }

    private void RegisterWithEnergy()
    {
        if (camcorderEnergy == null)
            ResolveCamcorderEnergy();

        camcorderEnergy?.RegisterActiveViewfinder(this);
    }

    private void UnregisterFromEnergy()
    {
        if (camcorderEnergy == null)
            ResolveCamcorderEnergy();

        camcorderEnergy?.UnregisterActiveViewfinder(this);
    }

    private bool CanOpenViewfinder()
    {
        if (camcorderEnergy == null)
            ResolveCamcorderEnergy();

        return camcorderEnergy == null || camcorderEnergy.CanOpenViewfinder;
    }

    /// <summary>CamcorderEnergyController에서 소모 프레임마다 직접 호출해 HUD를 즉시 갱신합니다.</summary>
    public void RequestBatteryHudRefresh() => RefreshBatteryHud();

    private void RefreshBatteryHud()
    {
        if (batteryImage == null)
            return;

        if (camcorderEnergy == null)
            ResolveCamcorderEnergy();

        int level = camcorderEnergy != null ? camcorderEnergy.GetBatteryLevelIndex() : 4;
        Sprite sprite = ResolveBatterySprite(level);
        if (sprite == null)
            return;

        batteryImage.sprite = sprite;
        batteryImage.enabled = true;
    }

    private Sprite ResolveBatterySprite(int levelIndex)
    {
        if (levelIndex <= 0)
            return batteryEmptySprite;

        if (batteryLevelSprites == null || batteryLevelSprites.Length == 0)
            return null;

        int idx = Mathf.Clamp(levelIndex, 1, batteryLevelSprites.Length) - 1;
        return batteryLevelSprites[idx];
    }

    private void CacheDisplayRest()
    {
        screenLocalScaleBase = screenLocalScale;

        if (displayPanel != null)
        {
            displayRestLocalPosition = displayPanel.localPosition;
            displayRestLocalScale = displayPanel.localScale;
        }
        else
        {
            displayRestLocalPosition = Vector3.zero;
            displayRestLocalScale = Vector3.one;
        }

        if (autoComputeDisplayActiveScale)
        {
            displayActiveLocalScaleResolved = Vector3.Scale(displayRestLocalScale, new Vector3(
                screenLocalScale.x / DemoScreenLocalScale.x,
                screenLocalScale.y / DemoScreenLocalScale.y,
                screenLocalScale.z / DemoScreenLocalScale.z));
        }
        else
        {
            displayActiveLocalScaleResolved = displayActiveLocalScale;
        }
    }

    /// <summary>피드 사용 중 LCD·베zel 정렬(전환 애니 없이 즉시 적용).</summary>
    private void ApplyActiveDisplayLayout()
    {
        ApplyDisplayLayout(displayActiveLocalPosition, displayActiveLocalScaleResolved);
    }

    private void ApplyDisplayLayout(Vector3 displayLocalPosition, Vector3 displayScale)
    {
        if (displayPanel != null)
        {
            displayPanel.localPosition = displayLocalPosition;
            displayPanel.localScale = displayScale;
        }

        if (screenTransform == null)
            return;

        screenTransform.localScale = new Vector3(
            screenLocalScaleBase.x * SafeScaleRatio(displayRestLocalScale.x, displayScale.x),
            screenLocalScaleBase.y * SafeScaleRatio(displayRestLocalScale.y, displayScale.y),
            screenLocalScaleBase.z * SafeScaleRatio(displayRestLocalScale.z, displayScale.z));
    }

    private void RestoreDisplayRest()
    {
        ApplyDisplayLayout(displayRestLocalPosition, displayRestLocalScale);
    }

    private static float SafeScaleRatio(float rest, float current)
    {
        float divisor = Mathf.Abs(current) > 0.0001f ? current : 1f;
        return rest / divisor;
    }

    private void SetDisplayBezelMatte(bool matte)
    {
        if (displayBackgroundRenderers == null)
            return;

        if (!matte)
        {
            foreach (Renderer renderer in displayBackgroundRenderers)
            {
                if (renderer != null)
                    renderer.SetPropertyBlock(null);
            }
            return;
        }

        if (displayBezelPropertyBlock == null)
            displayBezelPropertyBlock = new MaterialPropertyBlock();

        displayBezelPropertyBlock.SetFloat(SmoothnessId, displayBezelSmoothness);
        displayBezelPropertyBlock.SetFloat(MetallicId, 0f);

        foreach (Renderer renderer in displayBackgroundRenderers)
        {
            if (renderer != null)
                renderer.SetPropertyBlock(displayBezelPropertyBlock);
        }
    }

    /// <summary>
    /// 대기(내린) RaisePivot 자세. 기본은 Start 직후 프리팹 Transform을 캐시(ApplyImmediate 전에 호출).
    /// </summary>
    private void CacheRaisePivotRest()
    {
        if (useSerializedLoweredPose || raisePivot == null)
        {
            loweredRestLocalPosition = loweredLocalPosition;
            loweredRestLocalRotation = Quaternion.Euler(loweredLocalEuler);
            return;
        }

        loweredRestLocalPosition = raisePivot.localPosition;
        loweredRestLocalRotation = raisePivot.localRotation;
    }

    private Vector3 GetLoweredLocalPosition() => loweredRestLocalPosition;

    private Quaternion GetLoweredLocalRotation() => loweredRestLocalRotation;

    private void CacheItemViewAnchor()
    {
        // EquipmentViewController가 Instantiate(..., itemViewAnchor) 하므로 부모가 앵커
        itemViewAnchor = transform.parent;
        if (itemViewAnchor == null)
            return;

        anchorRestLocalPosition = itemViewAnchor.localPosition;
        anchorRestLocalRotation = itemViewAnchor.localRotation;

        if (autoComputeAnchorEye)
        {
            // 대기(보통 오른쪽 아래) → 화면 중앙·약간 위·가까이
            anchorEyeLocalPosition = new Vector3(
                Mathf.Lerp(anchorRestLocalPosition.x, 0.06f, 0.9f),
                Mathf.Lerp(anchorRestLocalPosition.y, 0.04f, 0.82f),
                Mathf.Max(0.1f, anchorRestLocalPosition.z - 0.26f));
        }
    }

    private void CacheItemViewCamera()
    {
        EquipmentViewController equipmentView = GetComponentInParent<EquipmentViewController>();
        itemViewCamera = equipmentView != null ? equipmentView.ItemViewCamera : null;
        if (itemViewCamera != null)
        {
            itemViewRestOrthographicSize = itemViewCamera.orthographicSize;
            itemViewCameraRestLocalPosition = itemViewCamera.transform.localPosition;
        }
    }

    private void RestoreItemViewOrthographicSize()
    {
        if (itemViewCamera != null && itemViewRestOrthographicSize > 0f)
            itemViewCamera.orthographicSize = itemViewRestOrthographicSize;
    }

    private void RestoreItemViewCameraLocalPosition()
    {
        if (itemViewCamera != null)
            itemViewCamera.transform.localPosition = itemViewCameraRestLocalPosition;
    }

    private void ApplyItemViewCameraPullback(float blend01)
    {
        if (itemViewCamera == null || itemViewCameraPullbackLocalZ <= 0f)
            return;

        float t = Mathf.Clamp01(blend01);
        itemViewCamera.transform.localPosition = itemViewCameraRestLocalPosition
            + new Vector3(0f, 0f, -itemViewCameraPullbackLocalZ * t);
    }

    private Vector3 GetRaisePivotTargetPosition(bool toEye, bool endPose)
    {
        if (!toEye)
            return GetLoweredLocalPosition();

        if (!useSoftRaisePivotDuringTransition || endPose)
            return raisedLocalPosition;

        return new Vector3(
            raisedLocalPosition.x,
            raisedLocalPosition.y,
            transitionRaisedLocalPositionZ);
    }

    /// <summary>
    /// 활성 Renderer bounds가 ItemViewCamera near 앞으로 들어오지 않게 앵커를 +Z(앞)으로 밉니다.
    /// </summary>
    private void EnforceMeshClearanceInViewCameraSpace()
    {
        if (!preventMeshCameraPenetration || itemViewCamera == null || itemViewAnchor == null)
            return;

        Transform cameraTransform = itemViewCamera.transform;
        float requiredMinZ = itemViewCamera.nearClipPlane + meshClearanceBeyondNear;
        float minBoundsZ = float.PositiveInfinity;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = renderer.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(sx, sy, sz));
                        float localZ = cameraTransform.InverseTransformPoint(corner).z;
                        if (localZ < minBoundsZ)
                            minBoundsZ = localZ;
                    }
                }
            }
        }

        if (float.IsPositiveInfinity(minBoundsZ))
            return;

        float pushForward = requiredMinZ - minBoundsZ;
        if (pushForward <= 0f)
            return;

        Vector3 localPos = itemViewAnchor.localPosition;
        localPos.z += pushForward;
        itemViewAnchor.localPosition = localPos;
    }

    private void CacheBodyRenderers()
    {
        var bodyList = new System.Collections.Generic.List<Renderer>();
        var displayList = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == screenRenderer)
                continue;

            if (IsUnderDisplayPanel(renderer.transform))
            {
                renderer.sortingOrder = 0;
                displayList.Add(renderer);
                continue;
            }

            bodyList.Add(renderer);
        }

        bodyRenderers = bodyList.ToArray();
        displayBackgroundRenderers = displayList.ToArray();
    }

    private bool IsUnderDisplayPanel(Transform target)
    {
        if (displayPanel == null || target == null)
            return false;
        return target == displayPanel || target.IsChildOf(displayPanel);
    }

    private void SetBodyMeshesVisible(bool visible)
    {
        if (bodyRenderers != null)
        {
            foreach (Renderer renderer in bodyRenderers)
            {
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }

        if (!visible && displayBackgroundRenderers != null)
        {
            foreach (Renderer renderer in displayBackgroundRenderers)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }

            SetDisplayBezelMatte(reduceDisplayBezelReflection);
        }
        else if (visible)
        {
            SetDisplayBezelMatte(false);
        }
    }

    private void SyncLensCameraToEye()
    {
        if (lensCamera == null || eyeCamera == null) return;

        lensCamera.transform.SetPositionAndRotation(eyeCamera.transform.position, eyeCamera.transform.rotation);
        lensCamera.CopyFrom(eyeCamera);
        lensCamera.targetTexture = renderTexture;
        lensCamera.cullingMask = BuildLensCullingMask();
        lensCamera.clearFlags = CameraClearFlags.SolidColor;
        lensCamera.backgroundColor = Color.black;
        lensCamera.depth = eyeCamera.depth - 1f;

        UniversalAdditionalCameraData eyeData = eyeCamera.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData lensData = lensCamera.GetUniversalAdditionalCameraData();
        if (lensData != null)
        {
            int baseVolumeMask = eyeData != null ? eyeData.volumeLayerMask : 1;
            lensData.renderPostProcessing = true;
            lensData.volumeLayerMask = nightVisionLayer >= 0
                ? (baseVolumeMask | (1 << nightVisionLayer))
                : baseVolumeMask;
        }
    }

    private void ResolveReferences()
    {
        if (raisePivot == null && transform.childCount > 0)
            raisePivot = transform.GetChild(0);

        if (displayPanel == null)
            displayPanel = FindChildByName(transform, displayPanelName);

        // 폴백: 정확한 이름이 없으면 "Display"가 포함된 자식을 찾는다.
        if (displayPanel == null)
            displayPanel = FindChildContaining(transform, "Display");

        if (displayPanel == null)
            Debug.LogWarning($"[Camcorder] '{displayPanelName}'(또는 'Display' 포함) 자식을 찾지 못했습니다. 펼치기 연출이 비활성화됩니다.");

        displayClosedRotation = displayPanel != null ? displayPanel.localRotation : Quaternion.identity;
        displayOpenRotation = displayClosedRotation * Quaternion.Euler(unfoldEulerOffset);
    }

    private void ResolveNightVisionLayer()
    {
        nightVisionLayer = LayerMask.NameToLayer(nightVisionLayerName);
        if (nightVisionLayer < 0)
        {
            Debug.LogWarning($"[Camcorder] '{nightVisionLayerName}' 레이어가 없습니다. " +
                             "Project Settings > Tags and Layers에 추가하면 야간투시가 캠코더 화면에만 적용됩니다.");
        }
    }

    private void BuildLensCameraAndRig()
    {
        eyeCamera = Camera.main;
        if (eyeCamera == null) eyeCamera = FindFirstObjectByType<Camera>();

        renderTexture = new RenderTexture(renderTextureWidth, renderTextureHeight, 24)
        {
            name = "Camcorder_RT"
        };
        renderTexture.Create();

        // RT 카메라: 플레이어 눈(Main Camera)과 동일 시점 — LateUpdate에서 동기화
        Transform lensParent = eyeCamera != null ? eyeCamera.transform : transform;
        GameObject lensGo = new GameObject("Camcorder_ViewCamera");
        lensGo.transform.SetParent(lensParent, false);
        lensGo.transform.localPosition = Vector3.zero;
        lensGo.transform.localRotation = Quaternion.identity;

        lensCamera = lensGo.AddComponent<Camera>();
        lensCamera.targetTexture = renderTexture;
        lensCamera.enabled = false;

        if (eyeCamera != null)
        {
            lensCamera.CopyFrom(eyeCamera);
            targetFov = eyeCamera.fieldOfView;
        }
        else
        {
            lensCamera.fieldOfView = defaultFov;
            targetFov = defaultFov;
        }

        lensCamera.nearClipPlane = 0.03f;
        lensCamera.farClipPlane = eyeCamera != null ? eyeCamera.farClipPlane : 1000f;
        lensCamera.cullingMask = BuildLensCullingMask();
        lensCamera.clearFlags = CameraClearFlags.SolidColor;
        lensCamera.backgroundColor = Color.black;

        UniversalAdditionalCameraData eyeData = eyeCamera != null ? eyeCamera.GetUniversalAdditionalCameraData() : null;

        UniversalAdditionalCameraData lensData = lensCamera.GetUniversalAdditionalCameraData();
        if (lensData != null)
        {
            lensData.renderType = CameraRenderType.Base;
            int baseVolumeMask = eyeData != null ? eyeData.volumeLayerMask : 1;
            lensData.renderPostProcessing = true;
            lensData.volumeLayerMask = nightVisionLayer >= 0
                ? (baseVolumeMask | (1 << nightVisionLayer))
                : baseVolumeMask;
        }

        // 메인(눈) 카메라는 NightVision Volume 제외 → 맨눈은 어둡게 유지
        if (eyeData != null && nightVisionLayer >= 0)
        {
            eyeData.volumeLayerMask &= ~(1 << nightVisionLayer);
        }

        BuildNightVisionVolume();
        BuildIrSpot();
    }

    private Transform GetModelRoot()
    {
        // RaisePivot 아래 첫 모델. 없으면 RaisePivot 자체, 그것도 없으면 this.
        if (raisePivot != null && raisePivot.childCount > 0)
            return raisePivot.GetChild(0);
        if (raisePivot != null)
            return raisePivot;
        return transform;
    }

    private int BuildLensCullingMask()
    {
        int mask = ~0; // Everything
        mask &= ~LayerToBit("PickupItem"); // 손에 든 캠코더 모델/화면은 렌즈에 안 잡히게(피드백 방지)
        mask &= ~LayerToBit("RaderIcon");
        if (nightVisionLayer >= 0) mask &= ~(1 << nightVisionLayer);
        return mask;
    }

    private static int LayerToBit(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 ? (1 << layer) : 0;
    }

    private void BuildNightVisionVolume()
    {
        GameObject volGo = new GameObject("Camcorder_NightVisionVolume");
        if (nightVisionLayer >= 0) volGo.layer = nightVisionLayer;

        nightVisionVolume = volGo.AddComponent<Volume>();
        nightVisionVolume.isGlobal = true;
        nightVisionVolume.priority = 100f;

        nightVisionProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        nightVisionVolume.sharedProfile = nightVisionProfile;

        ColorAdjustments color = nightVisionProfile.Add<ColorAdjustments>(true);
        color.postExposure.overrideState = true;
        color.postExposure.value = postExposure;
        color.saturation.overrideState = true;
        color.saturation.value = saturation;
        color.colorFilter.overrideState = true;
        color.colorFilter.value = colorFilter;

        Bloom bloom = nightVisionProfile.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = bloomIntensity;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = bloomThreshold;

        Vignette vignette = nightVisionProfile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = vignetteIntensity;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.5f;

        FilmGrain grain = nightVisionProfile.Add<FilmGrain>(true);
        grain.type.overrideState = true;
        grain.type.value = FilmGrainLookup.Medium2;
        grain.intensity.overrideState = true;
        grain.intensity.value = filmGrainIntensity;
        grain.response.overrideState = true;
        grain.response.value = 0.8f;
    }

    private void BuildIrSpot()
    {
        // 렌즈 카메라에 붙여 시야와 항상 정렬 → 캠코더가 보는 방향이 환해짐(적외선).
        GameObject spotGo = new GameObject("Camcorder_IRSpot");
        Transform parent = lensCamera != null ? lensCamera.transform : (eyeCamera != null ? eyeCamera.transform : transform);
        spotGo.transform.SetParent(parent, false);
        spotGo.transform.localPosition = Vector3.zero;
        spotGo.transform.localRotation = Quaternion.identity;

        irSpot = spotGo.AddComponent<Light>();
        irSpot.type = LightType.Spot;
        irSpot.intensity = irIntensity;
        irSpot.range = irRange;
        irSpot.spotAngle = irSpotAngle;
        irSpot.innerSpotAngle = irSpotAngle * 0.4f;
        irSpot.color = Color.white;
        irSpot.shadows = LightShadows.None;
        irSpot.renderingLayerMask = ~0;
        irSpot.cullingMask = BuildIrSpotCullingMask();
        irSpot.enabled = false;
    }

    private int BuildIrSpotCullingMask()
    {
        int mask = ~0;
        mask &= ~LayerToBit("PickupItem");
        return mask;
    }

    private void BuildScreenQuad()
    {
        Transform screenParent = displayPanel != null ? displayPanel : GetModelRoot();

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Camcorder_Screen";
        Collider quadCollider = quad.GetComponent<Collider>();
        if (quadCollider != null) Destroy(quadCollider);

        quad.transform.SetParent(screenParent, false);
        quad.transform.localPosition = screenLocalPosition;
        quad.transform.localRotation = Quaternion.Euler(screenLocalEuler);
        screenTransform = quad.transform;
        screenLocalScaleBase = screenLocalScale;
        RestoreDisplayRest();
        // 손에 든 모델과 같은 레이어로 두어 동일 카메라(아이템 뷰 카메라)가 위에 그리도록
        quad.layer = screenParent.gameObject.layer;

        screenRenderer = quad.GetComponent<MeshRenderer>();
        screenRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        screenRenderer.receiveShadows = false;
        screenRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        screenRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        screenRenderer.sortingOrder = 1;

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) unlit = Shader.Find("Unlit/Texture");
        screenMaterial = new Material(unlit) { name = "Camcorder_ScreenMat" };
        screenMaterial.SetTexture(BaseMapId, renderTexture);
        screenMaterial.mainTexture = renderTexture;
        screenRenderer.sharedMaterial = screenMaterial;
    }

    private void BuildViewfinderCanvas()
    {
        if (screenTransform == null)
            return;

        // RT 밖: LCD 쿼드 위 World Space UI → ItemViewCamera와 같이 그려져 이동 시 깜빡임 방지.
        GameObject canvasGo = new GameObject("Camcorder_ViewfinderCanvas");
        Transform hudParent = screenTransform;
        canvasGo.transform.SetParent(hudParent, false);
        canvasGo.transform.localPosition = new Vector3(0f, 0f, -0.002f);
        canvasGo.transform.localRotation = Quaternion.identity;

        viewfinderCanvas = canvasGo.AddComponent<Canvas>();
        viewfinderCanvas.renderMode = RenderMode.WorldSpace;
        viewfinderCanvas.worldCamera = itemViewCamera;

        const float refW = 960f;
        const float refH = 540f;
        RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(refW, refH);
        canvasRect.localScale = new Vector3(1f / refW, 1f / refH, 1f / refW);

        int hudLayer = hudParent.gameObject.layer;
        SetLayerRecursively(canvasGo, hudLayer);

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;
        scaler.referencePixelsPerUnit = 100f;

        if (frameSprite != null)
        {
            Image frame = CreateStretchedChild<Image>(canvasGo.transform, "Frame");
            frame.sprite = frameSprite;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = false;
            frame.raycastTarget = false;
        }

        if (recordingDotSprite != null)
        {
            recDot = CreateAnchoredChild<Image>(canvasGo.transform, "RecDot",
                new Vector2(0f, 1f), new Vector2(60f, -45f), new Vector2(24f, 24f));
            recDot.sprite = recordingDotSprite;
            recDot.color = new Color(1f, 0.15f, 0.15f, 1f);
            recDot.raycastTarget = false;
        }

        if (batteryEmptySprite != null || HasAnyBatteryLevelSprite())
        {
            batteryImage = CreateAnchoredChild<Image>(canvasGo.transform, "Battery",
                new Vector2(1f, 1f), new Vector2(-70f, -45f), new Vector2(80f, 36f));
            batteryImage.preserveAspect = true;
            batteryImage.raycastTarget = false;
            RefreshBatteryHud();
        }

        canvasGo.SetActive(false);
    }

    private static T CreateStretchedChild<T>(Transform parent, string name) where T : Graphic
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go.AddComponent<T>();
    }

    private static T CreateAnchoredChild<T>(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, Vector2 size) where T : Graphic
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        return go.AddComponent<T>();
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (string.IsNullOrEmpty(targetName)) return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root && child.name == targetName)
                return child;
        }
        return null;
    }

    private bool HasAnyBatteryLevelSprite()
    {
        if (batteryLevelSprites == null)
            return false;

        foreach (Sprite sprite in batteryLevelSprites)
        {
            if (sprite != null)
                return true;
        }

        return false;
    }

    private static Transform FindChildContaining(Transform root, string token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root && child.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return child;
        }
        return null;
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 캠코더 줌 중 아날로그 지직거림. 나이트비전 Volume 값을 잠깐 흔들었다가 원래대로 되돌린다.
/// 그레인·색수차 급증 + 렌즈 왜곡 떨림 + 밝기 깜빡임 + LCD 영상 가로 튐(트래킹 어긋남).
/// 줌 입력이 들어올 때마다 강도가 1로 차고 duration 동안 줄어든다.
/// CamcorderController가 소유하며 Update에서 Trigger/Tick, 뷰파인더를 닫을 때 Reset을 호출한다.
/// LCD 머티리얼은 Volume보다 늦게 만들어지므로 Tick 때 넘겨받는다.
/// </summary>
public class CamcorderZoomGlitch
{
    private readonly FilmGrain grain;
    private readonly ChromaticAberration aberration;
    private readonly ColorAdjustments color;
    private readonly LensDistortion distortion;

    private readonly float baseGrain;
    private readonly float baseAberration;
    private readonly float baseExposure;

    private readonly float duration;
    private readonly float extraGrain;
    private readonly float extraAberration;
    private readonly float distortionJitter;
    private readonly float exposureFlicker;
    private readonly float screenTearOffset;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    private float amount;
    private bool isDirty;
    private Material dirtyScreen;

    public CamcorderZoomGlitch(VolumeProfile profile, float duration, float extraGrain, float extraAberration,
        float distortionJitter, float exposureFlicker, float screenTearOffset)
    {
        profile.TryGet(out grain);
        profile.TryGet(out aberration);
        profile.TryGet(out color);
        if (!profile.TryGet(out distortion))
        {
            distortion = profile.Add<LensDistortion>(true);
        }

        distortion.intensity.overrideState = true;
        distortion.intensity.value = 0f;

        baseGrain = grain != null ? grain.intensity.value : 0f;
        baseAberration = aberration != null ? aberration.intensity.value : 0f;
        baseExposure = color != null ? color.postExposure.value : 0f;

        this.duration = Mathf.Max(duration, 0.01f);
        this.extraGrain = extraGrain;
        this.extraAberration = extraAberration;
        this.distortionJitter = distortionJitter;
        this.exposureFlicker = exposureFlicker;
        this.screenTearOffset = screenTearOffset;
    }

    public void Trigger()
    {
        amount = 1f;
    }

    public void Tick(float deltaTime, Material screen)
    {
        if (amount <= 0f)
        {
            Reset();
            return;
        }

        amount = Mathf.Max(0f, amount - deltaTime / duration);
        isDirty = true;

        // 매 프레임 무작위로 튀어야 지직거림처럼 보임
        float jitter = Random.value;
        if (grain != null)
        {
            grain.intensity.value = Mathf.Clamp01(baseGrain + extraGrain * amount * (0.5f + 0.5f * jitter));
        }

        if (aberration != null)
        {
            aberration.intensity.value = Mathf.Clamp01(baseAberration + extraAberration * amount * jitter);
        }

        distortion.intensity.value = Random.Range(-distortionJitter, distortionJitter) * amount;

        if (color != null)
        {
            color.postExposure.value = baseExposure + Random.Range(-exposureFlicker, exposureFlicker * 0.5f) * amount;
        }

        // 가끔씩만 가로로 튀어야 트래킹 어긋남처럼 보임
        if (screen != null && screen.HasProperty(BaseMapId))
        {
            Vector2 offset = Random.value < 0.5f * amount
                ? new Vector2(Random.Range(-screenTearOffset, screenTearOffset) * amount, 0f)
                : Vector2.zero;
            screen.SetTextureOffset(BaseMapId, offset);
            dirtyScreen = screen;
        }
    }

    public void Reset()
    {
        amount = 0f;
        if (!isDirty)
        {
            return;
        }

        isDirty = false;
        if (grain != null) grain.intensity.value = baseGrain;
        if (aberration != null) aberration.intensity.value = baseAberration;
        if (color != null) color.postExposure.value = baseExposure;
        distortion.intensity.value = 0f;

        if (dirtyScreen != null)
        {
            dirtyScreen.SetTextureOffset(BaseMapId, Vector2.zero);
            dirtyScreen = null;
        }
    }
}

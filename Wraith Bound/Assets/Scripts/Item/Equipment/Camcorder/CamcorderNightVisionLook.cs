using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 캠코더 나이트비전 색감(Volume 프로파일 구성). CamcorderController와 게임오버의 떨어진 캠코더 화면이 같이 사용.
/// 값은 CamcorderController 인스펙터(View_Camcorder.prefab)에서 채워 넘김.
/// </summary>
public class CamcorderNightVisionLook
{
    public float postExposure;
    public float contrast;
    public Color monochromeTint;
    public Vector4 shadowsTint;
    public Vector4 highlightsTint;
    public float highlightsStart;
    public float highlightsEnd;
    public float bloomIntensity;
    public float bloomThreshold;
    public float bloomScatter;
    public float vignetteIntensity;
    public float chromaticAberration;
    public float filmGrainIntensity;
    public float filmGrainResponse;
    public FilmGrainLookup filmGrainType;

    /// <summary>프로파일에 나이트비전 효과들을 추가. 실사 캠코더 나이트샷: 휘도만 남긴 초록 단색 + 밝은 곳은 하얗게 + 높은 대비 + 센서 노이즈</summary>
    public void ApplyTo(VolumeProfile profile)
    {
        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.overrideState = true;
        color.postExposure.value = postExposure;
        color.contrast.overrideState = true;
        color.contrast.value = contrast;

        // 채도(Saturation)는 색 보정 마지막 단계라 −100이면 초록 틴트까지 지워짐 → Channel Mixer로 단색화
        ChannelMixer mixer = profile.Add<ChannelMixer>(true);
        SetMonochromeRow(mixer.redOutRedIn, mixer.redOutGreenIn, mixer.redOutBlueIn, monochromeTint.r);
        SetMonochromeRow(mixer.greenOutRedIn, mixer.greenOutGreenIn, mixer.greenOutBlueIn, monochromeTint.g);
        SetMonochromeRow(mixer.blueOutRedIn, mixer.blueOutGreenIn, mixer.blueOutBlueIn, monochromeTint.b);

        ShadowsMidtonesHighlights tones = profile.Add<ShadowsMidtonesHighlights>(true);
        tones.shadows.overrideState = true;
        tones.shadows.value = shadowsTint;
        tones.highlights.overrideState = true;
        tones.highlights.value = highlightsTint;
        tones.highlightsStart.overrideState = true;
        tones.highlightsStart.value = highlightsStart;
        tones.highlightsEnd.overrideState = true;
        tones.highlightsEnd.value = highlightsEnd;

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = bloomIntensity;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = bloomThreshold;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = bloomScatter;

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = vignetteIntensity;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.5f;

        ChromaticAberration aberration = profile.Add<ChromaticAberration>(true);
        aberration.intensity.overrideState = true;
        aberration.intensity.value = chromaticAberration;

        // URP FilmGrain은 매 프레임 오프셋이 바뀌어 자글거림
        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.type.overrideState = true;
        grain.type.value = filmGrainType;
        grain.intensity.overrideState = true;
        grain.intensity.value = filmGrainIntensity;
        grain.response.overrideState = true;
        grain.response.value = filmGrainResponse;
    }

    /// <summary>출력 채널 하나를 휘도(Rec.601 가중치) × tint로 설정. Channel Mixer 값은 % 단위.</summary>
    private static void SetMonochromeRow(ClampedFloatParameter fromRed, ClampedFloatParameter fromGreen, ClampedFloatParameter fromBlue, float tint)
    {
        fromRed.overrideState = true;
        fromRed.value = 30f * tint;
        fromGreen.overrideState = true;
        fromGreen.value = 59f * tint;
        fromBlue.overrideState = true;
        fromBlue.value = 11f * tint;
    }
}

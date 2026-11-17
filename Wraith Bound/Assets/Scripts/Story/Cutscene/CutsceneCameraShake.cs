using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// 컷씬 카메라 흔들림 (Cutscene Blend 트랙 대상, 카메라에 붙임). Blend = 세기(0~1).
/// 문이 쾅 닫힐 때처럼 짧은 충격용 — 클립 Ease Out으로 잦아들게 씀. LookAt보다 나중에 적용.
/// </summary>
[DefaultExecutionOrder(110)]
public class CutsceneCameraShake : CutsceneBlendTarget
{
    [SerializeField] private float maxAngle = 2.2f;
    [SerializeField] private float maxOffset = 0.03f;
    [SerializeField] private float frequency = 22f;
    [SerializeField, Range(0f, 1f)] private float blend;

    public override float Blend
    {
        get => blend;
        set => blend = Mathf.Clamp01(value);
    }

    private void LateUpdate()
    {
        if (blend <= 0f)
        {
            return;
        }

        float t = Time.unscaledTime * frequency;
        float Noise(float seed) => (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f;

        transform.rotation *= Quaternion.Euler(Noise(1.3f) * maxAngle * blend, Noise(4.7f) * maxAngle * blend, Noise(8.1f) * maxAngle * 0.6f * blend);
        transform.position += transform.rotation * new Vector3(Noise(2.9f), Noise(6.2f), 0f) * maxOffset * blend;
    }

    public override void GatherPreviewProperties(IPropertyCollector driver)
    {
        driver.AddFromName<CutsceneCameraShake>(gameObject, "blend");
    }
}

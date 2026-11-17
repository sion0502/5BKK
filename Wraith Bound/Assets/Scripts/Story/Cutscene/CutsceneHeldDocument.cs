using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Rendering;

/// <summary>
/// 카메라 앞에 손으로 든 것처럼 문서(종이)를 들어 올려 보여줍니다. 인트로의 기획서용.
/// Blend 0 = 화면 아래로 내림(숨김), 1 = 눈앞에 들고 있음. Timeline Cutscene Blend 트랙이 움직입니다.
/// 같은 값으로 배경 블러 Volume(피사계 심도) 가중치와 종이를 비추는 조명 세기도 같이 올립니다.
/// 종이·글자 오브젝트는 이 컴포넌트의 자식(paper)으로 씬에 배치돼 있어 위치·문구를 에디터에서 직접 수정 가능.
/// </summary>
public class CutsceneHeldDocument : CutsceneBlendTarget
{
    [SerializeField] private Transform paper;

    [Header("Pose (카메라 기준 로컬)")]
    [SerializeField] private Vector3 raisedPosition = new Vector3(0.02f, 0.005f, 0.46f);
    [SerializeField] private Vector3 raisedEuler = new Vector3(14f, -3f, -4f);
    [SerializeField] private Vector3 loweredPosition = new Vector3(0.08f, -0.42f, 0.34f);
    [SerializeField] private Vector3 loweredEuler = new Vector3(65f, -8f, 12f);

    [Header("Hand Sway")]
    [SerializeField] private float swayPosition = 0.004f;
    [SerializeField] private float swayAngle = 1.2f;
    [SerializeField] private float swayFrequency = 0.6f;

    [Header("Linked")]
    [Tooltip("배경 블러(Depth of Field) Volume — Blend만큼 weight")]
    [SerializeField] private Volume blurVolume;
    [Tooltip("종이를 비추는 조명 — Blend × lightIntensity")]
    [SerializeField] private Light paperLight;
    [SerializeField] private float lightIntensity = 0.12f;

    private float blend;

    public override float Blend
    {
        get => blend;
        set
        {
            blend = Mathf.Clamp01(value);
            Apply();
        }
    }

    private void Awake()
    {
        Apply();
    }

    private void LateUpdate()
    {
        // 들고 있는 동안 손떨림이 계속 보이도록 매 프레임 갱신
        if (blend > 0f)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (paper != null)
        {
            bool visible = blend > 0.001f;
            if (paper.gameObject.activeSelf != visible)
            {
                paper.gameObject.SetActive(visible);
            }

            // 들어 올릴 땐 빠르게 시작해 부드럽게 멈춤
            float t = 1f - (1f - blend) * (1f - blend);
            Vector3 position = Vector3.LerpUnclamped(loweredPosition, raisedPosition, t);
            Quaternion rotation = Quaternion.SlerpUnclamped(Quaternion.Euler(loweredEuler), Quaternion.Euler(raisedEuler), t);

            float time = Application.isPlaying ? Time.unscaledTime * swayFrequency : 0f;
            float nx = Mathf.PerlinNoise(time, 0.3f) - 0.5f;
            float ny = Mathf.PerlinNoise(0.6f, time) - 0.5f;
            position += new Vector3(nx, ny, 0f) * (2f * swayPosition * t);
            rotation *= Quaternion.Euler(ny * swayAngle * 2f * t, 0f, nx * swayAngle * 2f * t);

            paper.localPosition = position;
            paper.localRotation = rotation;
        }

        if (blurVolume != null)
        {
            blurVolume.weight = blend;
        }

        if (paperLight != null)
        {
            paperLight.intensity = blend * lightIntensity;
            paperLight.enabled = blend > 0.001f;
        }
    }

    public override void GatherPreviewProperties(IPropertyCollector driver)
    {
        if (paper != null)
        {
            driver.AddFromName<Transform>(paper.gameObject, "m_LocalPosition");
            driver.AddFromName<Transform>(paper.gameObject, "m_LocalRotation");
            driver.AddFromName(paper.gameObject, "m_IsActive");
        }

        if (blurVolume != null)
        {
            driver.AddFromName<Volume>(blurVolume.gameObject, "weight");
        }

        if (paperLight != null)
        {
            driver.AddFromName<Light>(paperLight.gameObject, "m_Intensity");
            driver.AddFromName<Light>(paperLight.gameObject, "m_Enabled");
        }
    }
}

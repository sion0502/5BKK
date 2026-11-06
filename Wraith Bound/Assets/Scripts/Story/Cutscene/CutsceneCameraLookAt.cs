using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// 컷씬 카메라 시선을 대상 쪽으로 돌림 (Cutscene Blend 트랙 대상, 카메라에 붙임).
/// CutsceneCameraPath가 매 프레임 정한 회전 위에 LateUpdate에서 덮어씀 → Blend 0 = 경로 시선, 1 = 대상 응시.
/// 경로 시선을 매 프레임 새로 받아야 하므로, 이 구간에도 Camera Path 트랙에 (멈춘) 클립이 있어야 함.
/// </summary>
[DefaultExecutionOrder(100)]
public class CutsceneCameraLookAt : CutsceneBlendTarget
{
    [SerializeField] private Transform target;
    [SerializeField, Range(0f, 1f)] private float blend;

    public override float Blend
    {
        get => blend;
        set => blend = Mathf.Clamp01(value);
    }

    private void LateUpdate()
    {
        if (target == null || blend <= 0f)
        {
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion look = Quaternion.LookRotation(toTarget, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Mathf.SmoothStep(0f, 1f, blend));
    }

    public override void GatherPreviewProperties(IPropertyCollector driver)
    {
        driver.AddFromName<CutsceneCameraLookAt>(gameObject, "blend");
    }
}

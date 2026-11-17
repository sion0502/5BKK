using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 컷씬 카메라를 웨이포인트(바닥 위치) 경로를 따라 걷게 합니다. 손에 든 캠코더처럼 걸음 흔들림·손떨림 포함.
/// 진행도는 Timeline의 Camera Path 클립이 SetProgress(0~1)로 넣습니다.
/// 경로는 씬의 웨이포인트 오브젝트를 옮겨서 수정합니다(Catmull-Rom 곡선으로 부드럽게 연결).
/// </summary>
public class CutsceneCameraPath : MonoBehaviour
{
    [Header("Path")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float eyeHeight = 1.55f;
    [Tooltip("진행 방향을 볼 때 몇 m 앞을 볼지")]
    [SerializeField] private float lookAhead = 3f;
    [SerializeField] private float lookDownPitch = 4f;

    [Header("Look Target (끝부분에 특정 대상을 올려다보기)")]
    [SerializeField] private Transform lookTarget;
    [Tooltip("이 진행도부터 lookTarget 쪽으로 시선이 옮겨감")]
    [SerializeField, Range(0f, 1f)] private float lookTargetStart = 0.7f;

    [Header("Handheld")]
    [SerializeField] private float stepLength = 0.75f;
    [SerializeField] private float bobHeight = 0.035f;
    [SerializeField] private float bobRoll = 0.8f;
    [SerializeField] private float shakeAngle = 0.9f;
    [SerializeField] private float shakeFrequency = 0.35f;

    private const int SamplesPerSegment = 24;

    private readonly List<Vector3> samples = new List<Vector3>();
    private readonly List<float> distances = new List<float>();
    private float totalLength;

    public float TotalLength
    {
        get
        {
            BuildTable();
            return totalLength;
        }
    }

    public void SetProgress(float progress01)
    {
        BuildTable();
        if (samples.Count < 2)
        {
            return;
        }

        float distance = Mathf.Clamp01(progress01) * totalLength;
        Vector3 ground = SampleAt(distance);
        Vector3 ahead = SampleAt(Mathf.Min(totalLength, distance + lookAhead));
        if ((ahead - ground).sqrMagnitude < 0.0001f)
        {
            ahead = ground + (samples[samples.Count - 1] - samples[samples.Count - 2]).normalized;
        }

        Vector3 eye = ground + Vector3.up * eyeHeight;

        // 걸음 흔들림 (이동 거리 기준이라 멈추면 같이 멈춤)
        float stepPhase = distance / Mathf.Max(0.05f, stepLength) * Mathf.PI;
        eye.y += Mathf.Abs(Mathf.Sin(stepPhase)) * bobHeight - bobHeight * 0.5f;

        Vector3 aheadEye = ahead + Vector3.up * (eyeHeight - Mathf.Tan(lookDownPitch * Mathf.Deg2Rad) * lookAhead);
        Quaternion pathLook = Quaternion.LookRotation(aheadEye - eye, Vector3.up);

        if (lookTarget != null && progress01 > lookTargetStart)
        {
            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lookTargetStart, 1f, progress01));
            Quaternion targetLook = Quaternion.LookRotation(lookTarget.position - eye, Vector3.up);
            pathLook = Quaternion.Slerp(pathLook, targetLook, w);
        }

        // 손떨림 (시간 기준 펄린 노이즈)
        float t = Time.unscaledTime * shakeFrequency;
        float shakePitch = (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * shakeAngle;
        float shakeYaw = (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * shakeAngle;
        float roll = Mathf.Sin(stepPhase * 0.5f) * bobRoll;

        transform.SetPositionAndRotation(eye, pathLook * Quaternion.Euler(shakePitch, shakeYaw, roll));
    }

    private Vector3 SampleAt(float distance)
    {
        int index = distances.BinarySearch(distance);
        if (index >= 0)
        {
            return samples[index];
        }

        index = ~index;
        if (index <= 0)
        {
            return samples[0];
        }

        if (index >= samples.Count)
        {
            return samples[samples.Count - 1];
        }

        float d0 = distances[index - 1];
        float d1 = distances[index];
        return Vector3.Lerp(samples[index - 1], samples[index], Mathf.InverseLerp(d0, d1, distance));
    }

    private void BuildTable()
    {
        // 에디터에서 웨이포인트를 옮겨도 반영되도록 매번 다시 계산 (점 수가 적어 비용 무시 가능)
        samples.Clear();
        distances.Clear();
        totalLength = 0f;

        if (waypoints == null)
        {
            return;
        }

        List<Vector3> points = new List<Vector3>();
        foreach (Transform waypoint in waypoints)
        {
            if (waypoint != null)
            {
                points.Add(waypoint.position);
            }
        }

        if (points.Count < 2)
        {
            return;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(0, i - 1)];
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 p3 = points[Mathf.Min(points.Count - 1, i + 2)];

            for (int s = 0; s < SamplesPerSegment; s++)
            {
                AddSample(CatmullRom(p0, p1, p2, p3, s / (float)SamplesPerSegment));
            }
        }

        AddSample(points[points.Count - 1]);
    }

    private void AddSample(Vector3 point)
    {
        if (samples.Count > 0)
        {
            totalLength += Vector3.Distance(samples[samples.Count - 1], point);
        }

        samples.Add(point);
        distances.Add(totalLength);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    private void OnDrawGizmos()
    {
        BuildTable();
        Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.9f);
        for (int i = 1; i < samples.Count; i++)
        {
            Gizmos.DrawLine(samples[i - 1] + Vector3.up * 0.05f, samples[i] + Vector3.up * 0.05f);
        }
    }
}

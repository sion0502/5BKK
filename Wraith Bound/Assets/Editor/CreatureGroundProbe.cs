using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 크리처가 바닥에서 얼마나 뜨는지/파묻히는지 Play 모드에서 실제 Animator로 측정.
/// 메뉴 실행 → 프리팹마다 AI를 뗀 시각용 복제본을 만들어 State 0/1/2·Attack을 차례로 재생하며
/// 매 프레임 "가장 낮은 정점 높이"(루트 기준)를 모아 백분위로 콘솔에 출력. 결과는 LastReport에도 남음.
/// </summary>
public static class CreatureGroundProbe
{
    public static readonly string[] DefaultPrefabs =
    {
        "Assets/Prefab/Enemy/Monster/Xenosaurid.prefab",
        "Assets/Prefab/Enemy/Monster/NurseGhost.prefab",
        "Assets/Prefab/Enemy/Monster/MaskGhost.prefab",
    };

    const float SecondsPerState = 3f;
    static readonly int StateHash = Animator.StringToHash("State");
    static readonly int AttackHash = Animator.StringToHash("Attack");

    public static string LastReport { get; private set; } = "";
    /// <summary>false면 CreatureFootGrounding도 떼고 애니메이션 원래 높이를 잼.</summary>
    public static bool WithGrounding = true;
    public static bool IsRunning => probes.Count > 0;

    class Probe
    {
        public string name;
        public GameObject go;
        public Animator anim;
        public SkinnedMeshRenderer[] renderers;
        public int phase = -1;
        public float phaseStart;
        public readonly List<float>[] samples = { new List<float>(), new List<float>(), new List<float>(), new List<float>() };
        public readonly List<float>[] stride = { new List<float>(), new List<float>(), new List<float>(), new List<float>() };
        public readonly List<float>[] yaw = { new List<float>(), new List<float>(), new List<float>(), new List<float>() };
        public List<Transform> feet;
        public int lastPlanted = -1;
        public Vector3 lastPlantedLocal;
    }

    static readonly List<Probe> probes = new List<Probe>();
    static readonly string[] PhaseNames = { "State0(Idle)", "State1(Patrol)", "State2(Chase)", "Attack" };

    [MenuItem("Tools/Creatures/Measure Ground Offset (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[GroundProbe] Play 모드에서 실행하세요.");
            return;
        }

        Stop();
        float x = 2000f;
        foreach (string path in DefaultPrefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            var holder = new GameObject("GroundProbeHolder");
            holder.SetActive(false);
            GameObject go = Object.Instantiate(prefab, new Vector3(x, 0f, 2000f), Quaternion.identity, holder.transform);
            x += 20f;
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(mb is CreatureFootGrounding) || !WithGrounding)
                    Object.DestroyImmediate(mb);
            foreach (var agent in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
                Object.DestroyImmediate(agent);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                rb.isKinematic = true;
            foreach (var src in go.GetComponentsInChildren<AudioSource>(true))
                src.enabled = false;
            go.transform.SetParent(null, true);
            go.SetActive(true); // 귀신 프리팹은 루트가 비활성으로 저장돼 있음
            Object.Destroy(holder);

            var probe = new Probe
            {
                name = prefab.name,
                go = go,
                anim = go.GetComponentInChildren<Animator>(),
                renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(),
            };
            probe.feet = CreatureGroundingSetup.FindFeet(go.transform, probe.anim);
            if (probe.anim != null)
            {
                probe.anim.applyRootMotion = false;
                probe.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; // 카메라 밖이라 멈추지 않게
            }
            foreach (SkinnedMeshRenderer smr in probe.renderers)
                smr.updateWhenOffscreen = true;
            probes.Add(probe);
        }

        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Debug.Log("[GroundProbe] 측정 시작 (" + probes.Count + "종, 약 " + (SecondsPerState * 4) + "초)");
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode)
            Stop();
    }

    static void Stop()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        foreach (Probe p in probes)
            if (p.go != null)
                Object.Destroy(p.go);
        probes.Clear();
    }

    static void Tick()
    {
        if (!Application.isPlaying)
        {
            Stop();
            return;
        }

        bool allDone = true;
        foreach (Probe p in probes)
        {
            if (p.go == null || p.anim == null)
                continue;

            float now = Time.time;
            if (p.phase < 0 || now - p.phaseStart >= SecondsPerState)
            {
                p.phase++;
                p.phaseStart = now;
                if (p.phase < 3)
                    p.anim.SetInteger(StateHash, p.phase);
                else if (p.phase == 3)
                {
                    p.anim.SetInteger(StateHash, 0);
                    p.anim.SetTrigger(AttackHash);
                }
            }

            if (p.phase > 3)
                continue;
            allDone = false;

            // 전환 블렌드가 끝난 뒤부터 측정
            if (now - p.phaseStart < 0.5f)
                continue;
            p.samples[p.phase].Add(LowestVertexY(p));
            SampleStride(p);
            SampleBodyYaw(p);
        }

        if (allDone)
        {
            Report();
            Stop();
        }
    }

    /// <summary>디딘 발(더 낮은 발)이 몸 기준으로 뒤로 밀리는 속도 = 발이 안 미끄러지는 이동 속도.</summary>
    static void SampleStride(Probe p)
    {
        if (p.feet == null || p.feet.Count < 2 || Time.deltaTime <= 0f)
            return;
        int planted = p.feet[0].position.y <= p.feet[1].position.y ? 0 : 1;
        Vector3 local = p.go.transform.InverseTransformPoint(p.feet[planted].position);
        if (planted == p.lastPlanted)
        {
            Vector3 d = Vector3.Scale(local - p.lastPlantedLocal, p.go.transform.lossyScale);
            d.y = 0f;
            p.stride[p.phase].Add(d.magnitude / Time.deltaTime);
        }
        p.lastPlanted = planted;
        p.lastPlantedLocal = local;
    }

    /// <summary>휴머노이드 몸통 정면(허벅지·어깨 좌→우 × 위)이 루트 정면에서 돌아간 각도 — 옆을 보고 걷는지 확인.</summary>
    static void SampleBodyYaw(Probe p)
    {
        if (p.anim == null || !p.anim.isHuman)
            return;
        Vector3 r = p.anim.GetBoneTransform(HumanBodyBones.RightUpperLeg).position - p.anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position
                  + p.anim.GetBoneTransform(HumanBodyBones.RightUpperArm).position - p.anim.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
        r.y = 0f;
        if (r.sqrMagnitude < 0.0001f)
            return;
        Vector3 body = Vector3.Cross(r.normalized, Vector3.up);
        p.yaw[p.phase].Add(Vector3.SignedAngle(p.go.transform.forward, body, Vector3.up));
    }

    static readonly List<Vector3> verts = new List<Vector3>();
    static Mesh bakeMesh;

    static float LowestVertexY(Probe p)
    {
        if (bakeMesh == null)
            bakeMesh = new Mesh();
        float low = float.MaxValue;
        foreach (SkinnedMeshRenderer smr in p.renderers)
        {
            if (smr == null || smr.sharedMesh == null)
                continue;
            smr.BakeMesh(bakeMesh, true);
            bakeMesh.GetVertices(verts);
            Matrix4x4 m = smr.transform.localToWorldMatrix;
            int step = Mathf.Max(1, verts.Count / 6000);
            for (int i = 0; i < verts.Count; i += step)
            {
                float y = m.MultiplyPoint3x4(verts[i]).y;
                if (y < low)
                    low = y;
            }
        }
        return low - p.go.transform.position.y;
    }

    static void Report()
    {
        var sb = new StringBuilder();
        foreach (Probe p in probes)
        {
            sb.AppendLine("== " + p.name);
            for (int i = 0; i < 4; i++)
            {
                List<float> s = p.samples[i];
                if (s.Count == 0)
                    continue;
                s.Sort();
                List<float> st = p.stride[i];
                st.Sort();
                float yawAvg = 0f;
                foreach (float y in p.yaw[i])
                    yawAvg += y;
                yawAvg = p.yaw[i].Count > 0 ? yawAvg / p.yaw[i].Count : 0f;
                sb.AppendLine(string.Format("  {0,-15} min {1,6:F3}  p10 {2,6:F3}  p50 {3,6:F3}  p90 {4,6:F3}  max {5,6:F3}  (n={6})  stride p50 {7:F2} m/s  bodyYaw avg {8:F1}°",
                    PhaseNames[i], s[0], Pct(s, 0.1f), Pct(s, 0.5f), Pct(s, 0.9f), s[s.Count - 1], s.Count, st.Count > 0 ? Pct(st, 0.5f) : 0f, yawAvg));
            }
        }
        LastReport = sb.ToString();
        Debug.Log("[GroundProbe] 가장 낮은 정점 높이(m, +면 뜸 / −면 파묻힘)\n" + LastReport);
    }

    static float Pct(List<float> sorted, float t)
    {
        return sorted[Mathf.Clamp(Mathf.RoundToInt((sorted.Count - 1) * t), 0, sorted.Count - 1)];
    }
}

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 길잡이 혼불 (시연 전용 기, 2026-11-19). DemoGuideRoute의 현재 단계 경로 위에서 플레이어보다 lead만큼 앞 지점에 떠 있음.
/// 경로 위 거리로만 움직여 벽을 안 뚫음(예전엔 목표점으로 직선 이동해 모퉁이에서 벽을 통과), 속도는 플레이어 속도 ×1.5+1.5 이상
/// → 플레이어가 늦으면 그 자리에서 기다리고, 목표에 닿으면 그 위에서 맴돎. 앞에 닫힌 문이 있으면 문 앞에 멈춰 깜빡임('여기 열어').
/// 색: 평소 옅은 초록빛 도는 흰빛, 괴물(EnemyBase)이 dangerRadius 안에 있으면 붉게 + 불안하게 깜빡임 (사용자 요청).
/// 모양은 전부 코드로: 빛나는 구(Unlit HDR) + 꼬리(TrailRenderer) + 점광.
/// 플레이어가 루트에서 벗어나면(1초 넘게 12m 안에 보이는 루트 구간이 없음 — 부적을 순서와 다르게 태워 단계가 먼 경로로 바뀌었을 때 등)
/// 실행 중 NavMesh로 플레이어 → 루트의 가장 가까운 점까지 임시 경로를 만들어 그걸 따라 안내, 루트에 다시 닿으면 원래 경로로 (2026-11-19 '부적 태우니 혼불이 사라짐').
/// </summary>
public class DemoGuideWisp : MonoBehaviour
{
    [SerializeField] private float lead = 5f;
    [SerializeField] private float hoverHeight = 1.9f;
    [SerializeField] private float maxSpeed = 7f;
    [SerializeField] private float dangerRadius = 18f;
    [SerializeField] private Color calmColor = new Color(0.82f, 1f, 0.86f);
    [SerializeField] private Color dangerColor = new Color(1f, 0.12f, 0.08f);

    private DemoGuideRoute route;
    private Transform player;
    private Material coreMat;
    private TrailRenderer trail;
    private Light glow;
    private float danger;
    private float nextEnemyScan;
    private bool nearEnemy;
    private bool waitingAtDoor;
    private readonly List<EnemyBase> enemies = new List<EnemyBase>();

    public void Init(DemoGuideRoute owner)
    {
        route = owner;
    }

    private void Start()
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        coreMat = new Material(unlit);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(core.GetComponent<Collider>());
        core.name = "Core";
        core.transform.SetParent(transform, false);
        core.transform.localScale = Vector3.one * 0.18f;
        var mr = core.GetComponent<MeshRenderer>();
        mr.sharedMaterial = coreMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.startWidth = 0.1f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Shader particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material trailMat = new Material(particle != null ? particle : unlit);
        trailMat.SetFloat("_Surface", 1f);
        trailMat.SetFloat("_Blend", 2f); // 가산
        trailMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        trailMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        trailMat.SetInt("_ZWrite", 0);
        trailMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        trailMat.renderQueue = 3000;
        trail.sharedMaterial = trailMat;

        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 7f;
        glow.intensity = 2.5f;
        glow.shadows = LightShadows.None;

        GameObject p = GameObject.FindWithTag("Player");
        player = p != null ? p.transform : null;
        if (player != null)
        {
            shown = player.position + Vector3.up * hoverHeight + player.forward * 2f;
            transform.position = shown;
            lastPlayerPos = player.position;
        }

        ApplyColor(calmColor, 1f);
    }

    private void Update()
    {
        if (player == null || route == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            player = p != null ? p.transform : null;
            if (player != null)
            {
                lastPlayerPos = player.position;
            }

            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float measured = Vector3.Distance(player.position, lastPlayerPos) / dt;
        lastPlayerPos = player.position;
        playerSpeed = Mathf.Lerp(playerSpeed, Mathf.Min(measured, 20f), 1f - Mathf.Exp(-4f * dt));

        DemoGuideRoute.Step step = route.CurrentStep;
        Vector3 pathPos;
        waitingAtDoor = false;
        if (step != null && step.path != null && step.path.Length > 1)
        {
            if (step.path != stepPath)
            {
                stepPath = step.path;
                onDetour = false;
                offRouteTime = 0f;
                SetPath(stepPath);
            }

            CheckOffRoute();

            float sPlayer = PlayerParam();
            float goal = Mathf.Min(sPlayer + lead, total);
            // 무리한 질주 등으로 플레이어가 빨라도 앞서 가게
            float speed = Mathf.Max(maxSpeed, playerSpeed * 1.5f + 1.5f);
            bool behindPlayer = sPlayer > sWisp + 1.5f; // 플레이어가 이미 앞(문을 지나 닫았거나 뛰어감) → 문 무시, 빨리 따라잡음
            if (behindPlayer)
            {
                speed += (sPlayer - sWisp) * 1.5f;
            }

            if (!behindPlayer && goal > sWisp && ClosedDoorAhead())
            {
                waitingAtDoor = true;
            }
            else
            {
                sWisp = Mathf.MoveTowards(sWisp, goal, speed * dt);
            }

            pathPos = PointAt(sWisp) + Vector3.up * hoverHeight;
            // 경로 위에 붙은 뒤로는 경로점 그대로(직선으로 쫓으면 빠르게 돌 때 모퉁이에서 벽을 가로질렀음).
            // 단계가 바뀌어 경로에서 멀면(4m+) 그 자리로 옮겨 나타남, 가까우면 잠깐 따라붙음
            float off = Vector3.Distance(shown, pathPos);
            if (onPath || off > 4f)
            {
                shown = pathPos;
                onPath = true;
            }
            else
            {
                shown = Vector3.MoveTowards(shown, pathPos, Mathf.Max(speed * 1.6f, off * 3f) * dt);
                onPath = off < 0.2f;
            }
        }
        else
        {
            pathPos = player.position + Vector3.up * hoverHeight + player.forward * 2f;
            shown = Vector3.MoveTowards(shown, pathPos, maxSpeed * dt);
        }

        float t = Time.time;
        transform.position = shown + new Vector3(Mathf.Sin(t * 1.3f) * 0.12f, Mathf.Sin(t * 2.1f) * 0.15f, Mathf.Cos(t * 1.7f) * 0.12f);

        UpdateDanger();
        float pulse = waitingAtDoor ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 5f)) : 0.85f + 0.15f * Mathf.Sin(t * 2.4f);
        if (danger > 0.5f)
        {
            pulse *= Random.value < 0.08f ? 0.35f : 1f; // 불안한 깜빡임
        }

        ApplyColor(Color.Lerp(calmColor, dangerColor, danger), pulse);
    }

    // ── 경로(1m 간격, 실제 바닥 높이 — 에디터가 구움) 위 거리 s로 움직임 ──

    private Vector3[] path;
    private float[] cum;
    private float total;
    private float sWisp;
    private Vector3 shown;
    private Vector3 lastPlayerPos;
    private float playerSpeed;

    private void SetPath(Vector3[] p)
    {
        path = p;
        cum = new float[p.Length];
        for (int i = 1; i < p.Length; i++)
        {
            cum[i] = cum[i - 1] + Vector3.Distance(p[i - 1], p[i]);
        }

        total = cum[p.Length - 1];
        sWisp = ClosestParamVisible(shown - Vector3.up * hoverHeight);
        sPlayerPrev = player != null ? ClosestParamVisible(player.position) : 0f;
        onPath = false;
    }

    private float sPlayerPrev;
    private bool onPath;

    // ── 루트 이탈 시 임시 경로 ──
    private Vector3[] stepPath;
    private bool onDetour;
    private float offRouteTime;
    private float nextRouteCheck;
    private float nextDetourCalc;

    private void CheckOffRoute()
    {
        if (Time.time < nextRouteCheck)
        {
            return;
        }

        nextRouteCheck = Time.time + 0.5f;
        if (NearVisible(stepPath, player.position, 12f))
        {
            offRouteTime = 0f;
            if (onDetour)
            {
                onDetour = false;
                SetPath(stepPath);
            }

            return;
        }

        offRouteTime += 0.5f;
        if (offRouteTime >= 1f && (!onDetour || Time.time >= nextDetourCalc))
        {
            BuildDetour();
        }
    }

    // 플레이어 → 루트에서 가장 가까운 점, 실행 중 NavMesh(닫힌 문은 깎여 있어 그 앞에서 끊기면 혼불이 문 앞에서 기다림)
    private void BuildDetour()
    {
        nextDetourCalc = Time.time + 2f;
        Vector3 target = stepPath[0];
        float best = float.MaxValue;
        for (int i = 0; i < stepPath.Length - 1; i++)
        {
            Vector3 q = ClosestOnSegment(stepPath[i], stepPath[i + 1], player.position, out float d2);
            if (d2 < best)
            {
                best = d2;
                target = q;
            }
        }

        if (!UnityEngine.AI.NavMesh.SamplePosition(player.position, out UnityEngine.AI.NavMeshHit a, 3f, UnityEngine.AI.NavMesh.AllAreas) ||
            !UnityEngine.AI.NavMesh.SamplePosition(target, out UnityEngine.AI.NavMeshHit b, 3f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return;
        }

        var nav = new UnityEngine.AI.NavMeshPath();
        if (!UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, UnityEngine.AI.NavMesh.AllAreas, nav) || nav.corners.Length < 2)
        {
            return;
        }

        var pts = new List<Vector3> { nav.corners[0] };
        for (int i = 1; i < nav.corners.Length; i++)
        {
            Vector3 from = nav.corners[i - 1], to = nav.corners[i];
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to)));
            for (int k = 1; k <= n; k++)
            {
                pts.Add(Vector3.Lerp(from, to, k / (float)n));
            }
        }

        if (!onDetour)
        {
            Debug.Log("[Demo] 혼불: 루트에서 벗어남 → 임시 경로 " + pts.Count + "점 (" + nav.status + ")");
        }

        onDetour = true;
        SetPath(pts.ToArray());
    }

    // pts 경로 중 pos에서 maxDist 안이면서 벽에 안 가려 보이는 구간이 있는지
    private bool NearVisible(Vector3[] pts, Vector3 pos, float maxDist)
    {
        nearCands.Clear();
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 q = ClosestOnSegment(pts[i], pts[i + 1], pos, out float d2);
            if (d2 <= maxDist * maxDist)
            {
                nearCands.Add(new KeyValuePair<float, Vector3>(d2, q));
            }
        }

        nearCands.Sort((x, y) => x.Key.CompareTo(y.Key));
        Vector3 eye = pos + Vector3.up * 1.2f;
        for (int k = 0; k < nearCands.Count && k < 8; k++)
        {
            Vector3 q = nearCands[k].Value + Vector3.up * 1.2f;
            if (!Physics.Linecast(eye, q, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<DoorClick>() != null
                || hit.collider.GetComponentInParent<CharacterController>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private readonly List<KeyValuePair<float, Vector3>> nearCands = new List<KeyValuePair<float, Vector3>>();

    private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 pos, out float d2)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        float u = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(pos - a, ab) / len2) : 0f;
        Vector3 q = a + ab * u;
        Vector3 d = pos - q;
        d.y *= 4f;
        d2 = d.sqrMagnitude;
        return q;
    }

    // 플레이어 위치 = 지난 프레임 위치 근처(경로 거리 −12~+20m)에서만 찾음 — 전체에서 찾으면 벽 너머 복도 구간이
    // 직선거리로 더 가까워 위치가 벽 너머로 튀었음(혼불이 벽 속으로 들어감). 창 안이 너무 멀면(경로를 벗어남) 보이는 구간 중에서 다시 찾음
    private float PlayerParam()
    {
        float s = ClosestParamVisible(player.position, sPlayerPrev - 12f, sPlayerPrev + 20f, out float d2);
        if (d2 > 10f * 10f)
        {
            s = ClosestParamVisible(player.position);
        }

        sPlayerPrev = s;
        return s;
    }

    // 전체에서 가까운 순으로, 그 지점이 pos에서 벽에 안 가려 보이는 첫 구간
    private float ClosestParamVisible(Vector3 pos)
    {
        return ClosestParamVisible(pos, float.MinValue, float.MaxValue, out _);
    }

    private readonly List<KeyValuePair<float, float>> cands = new List<KeyValuePair<float, float>>(); // (거리², s)

    private float ClosestParamVisible(Vector3 pos, float sMin, float sMax, out float bestD2)
    {
        cands.Clear();
        for (int i = 0; i < path.Length - 1; i++)
        {
            if (cum[i + 1] < sMin || cum[i] > sMax)
            {
                continue;
            }

            float s = SegmentClosest(i, pos, out float d2);
            cands.Add(new KeyValuePair<float, float>(d2, s));
        }

        bestD2 = float.MaxValue;
        if (cands.Count == 0)
        {
            return Mathf.Clamp(sMin, 0f, total);
        }

        cands.Sort((a, b) => a.Key.CompareTo(b.Key));
        Vector3 eye = pos + Vector3.up * 1.2f;
        for (int k = 0; k < cands.Count && k < 12; k++)
        {
            Vector3 q = PointAt(cands[k].Value) + Vector3.up * 1.2f;
            if (!Physics.Linecast(eye, q, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<DoorClick>() != null
                || hit.collider.GetComponentInParent<CharacterController>() != null)
            {
                bestD2 = cands[k].Key;
                return cands[k].Value;
            }
        }

        bestD2 = cands[0].Key;
        return cands[0].Value;
    }

    private Vector3 PointAt(float s)
    {
        int i = System.Array.BinarySearch(cum, s);
        if (i >= 0)
        {
            return path[i];
        }

        i = ~i;
        if (i <= 0)
        {
            return path[0];
        }

        if (i >= path.Length)
        {
            return path[path.Length - 1];
        }

        float seg = cum[i] - cum[i - 1];
        return Vector3.Lerp(path[i - 1], path[i], seg > 1e-4f ? (s - cum[i - 1]) / seg : 0f);
    }

    private float SegmentClosest(int i, Vector3 pos, out float d2)
    {
        Vector3 a = path[i];
        Vector3 ab = path[i + 1] - a;
        float len2 = ab.sqrMagnitude;
        float u = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(pos - a, ab) / len2) : 0f;
        Vector3 d = pos - (a + ab * u);
        d.y *= 4f;
        d2 = d.sqrMagnitude;
        return cum[i] + (cum[i + 1] - cum[i]) * u;
    }

    // 경로를 따라 바로 앞(1.2m)에 닫힌 문이 있으면 그 앞에서 기다림('여기 열어')
    private bool ClosedDoorAhead()
    {
        Vector3 a = PointAt(sWisp) + Vector3.up * 1.2f;
        Vector3 b = PointAt(Mathf.Min(sWisp + 1.2f, total)) + Vector3.up * 1.2f;
        Vector3 dir = b - a;
        float dist = dir.magnitude;
        if (dist < 0.01f)
        {
            return false;
        }

        foreach (RaycastHit hit in Physics.RaycastAll(a, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            DoorClick door = hit.collider.GetComponentInParent<DoorClick>();
            if (door != null && !door.IsPassageReady())
            {
                return true;
            }

            if (route.IsGateDoor(hit.collider.transform)) // 봉인이 남은 출구 대문
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateDanger()
    {
        if (Time.time >= nextEnemyScan)
        {
            nextEnemyScan = Time.time + 0.4f;
            enemies.Clear();
            enemies.AddRange(FindObjectsByType<EnemyBase>(FindObjectsSortMode.None));
            nearEnemy = false;
            foreach (EnemyBase e in enemies)
            {
                if (e != null && e.isActiveAndEnabled && Vector3.Distance(e.transform.position, player.position) <= dangerRadius)
                {
                    nearEnemy = true;
                    break;
                }
            }
        }

        danger = Mathf.MoveTowards(danger, nearEnemy ? 1f : 0f, Time.deltaTime * (nearEnemy ? 2.5f : 0.8f));
    }

    private void ApplyColor(Color c, float pulse)
    {
        Color hdr = c * (3.5f * pulse);
        hdr.a = 1f;
        coreMat.SetColor("_BaseColor", hdr);
        glow.color = c;
        glow.intensity = 2.5f * pulse;
        Color tc = c * (2f * pulse);
        tc.a = 0.8f;
        trail.startColor = tc;
        tc.a = 0f;
        trail.endColor = tc;
    }
}

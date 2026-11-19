using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 시연 연출 공용 — 괴물이 '어디서 나타나고', 예고 소리가 '어디서부터 다가올지' 정함.
///  · 등장 지점: 정해 둔 자리(spawn)에서 플레이어 쪽으로 NavMesh 길을 따라오다, 플레이어와 approachDistance만큼 떨어진 곳.
///    단, 플레이어 눈(카메라)에서 보이는 곳이면 길을 거슬러 올라가 안 보이는 곳으로. (눈앞에서 '뿅' 나타나지 않게)
///  · 예고 소리 길: 같은 길에서 플레이어와 cueStartDistance 떨어진 곳 → 등장 지점.
///    길이 그만큼 길지 않으면(정해 둔 자리가 가까우면) 플레이어 반대쪽으로 늘려서 벽 너머 먼 곳에서 시작.
/// </summary>
public static class DemoCreatureStaging
{
    private const float VisibleCheckRange = 40f;
    private static readonly float[] EyeHeights = { 0.4f, 1.3f, 2.2f };
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    /// <summary>플레이어 눈에서 이 바닥 지점에 선 괴물이 보이는지 (벽·문에 가리면 false).</summary>
    public static bool IsVisible(Vector3 ground, Transform player, Transform creature)
    {
        Camera cam = player != null ? player.GetComponentInChildren<Camera>() : null;
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam == null)
        {
            return false;
        }

        Vector3 eye = cam.transform.position;
        if (Vector3.Distance(eye, ground) > VisibleCheckRange)
        {
            return false;
        }

        foreach (float h in EyeHeights)
        {
            if (ClearLine(eye, ground + Vector3.up * h, player, creature))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ClearLine(Vector3 from, Vector3 to, Transform player, Transform creature)
    {
        Vector3 diff = to - from;
        float len = diff.magnitude;
        if (len < 0.01f)
        {
            return true;
        }

        int count = Physics.RaycastNonAlloc(from, diff / len, Hits, len, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform t = Hits[i].collider.transform;
            if ((player != null && t.IsChildOf(player.root)) || (creature != null && t.IsChildOf(creature)))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>등장 지점 + 예고 소리 길을 계산.</summary>
    /// <param name="spawn">씬에 정해 둔 등장 자리</param>
    /// <param name="approachDistance">이보다 플레이어에게 가까이에서 나타나지 않음 (정해 둔 자리가 더 가까우면 그 자리)</param>
    /// <param name="cueStartDistance">예고 소리가 시작하는 플레이어와의 거리</param>
    public static Vector3 Plan(Vector3 spawn, Transform player, Transform creature, float approachDistance, float cueStartDistance,
        List<Vector3> cuePath)
    {
        cuePath.Clear();
        if (player == null)
        {
            cuePath.Add(spawn);
            return spawn;
        }

        Vector3 target = player.position;
        List<Vector3> route = Route(spawn, target); // spawn → 플레이어

        // 1) 등장 지점: 길을 따라오다 approachDistance 안으로 들어오기 직전 지점
        int appearIndex = 0;
        Vector3 appear = route[0];
        float step = 0.75f;
        List<Vector3> samples = Resample(route, step);
        for (int i = 0; i < samples.Count; i++)
        {
            if (FlatDistance(samples[i], target) < approachDistance)
            {
                break;
            }

            appearIndex = i;
            appear = samples[i];
        }

        // 2) 보이는 곳이면 길을 거슬러(먼 쪽으로) 올라가며 안 보이는 곳을 찾음
        int hiddenIndex = appearIndex;
        while (hiddenIndex > 0 && IsVisible(samples[hiddenIndex], player, creature))
        {
            hiddenIndex--;
        }

        if (!IsVisible(samples[hiddenIndex], player, creature))
        {
            appearIndex = hiddenIndex;
            appear = samples[hiddenIndex];
        }
        else if (TryFindHiddenPoint(target, approachDistance, player, creature, out Vector3 alt))
        {
            // 정해 둔 길이 전부 보이면(트인 복도 등) 근처의 안 보이는 다른 곳에서
            appear = alt;
            List<Vector3> altRoute = Resample(Route(alt, target), step);
            samples = altRoute;
            appearIndex = 0;
        }

        // 3) 예고 소리 길: 등장 지점보다 먼 쪽(길의 앞부분)에서 cueStartDistance 떨어진 곳부터
        int startIndex = appearIndex;
        while (startIndex > 0 && FlatDistance(samples[startIndex], target) < cueStartDistance)
        {
            startIndex--;
        }

        float startDist = FlatDistance(samples[startIndex], target);
        if (startDist < cueStartDistance - 0.5f)
        {
            // 길이 짧으면 플레이어 반대쪽으로 늘려서 더 먼 곳에서 시작 (소리만 — 벽 너머로 들림)
            Vector3 away = samples[startIndex] - target;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -player.forward;
            cuePath.Add(samples[startIndex] + away * (cueStartDistance - startDist));
        }

        for (int i = startIndex; i <= appearIndex; i++)
        {
            cuePath.Add(samples[i]);
        }

        if (cuePath.Count == 0)
        {
            cuePath.Add(appear);
        }

        return appear;
    }

    /// <summary>
    /// '멀리서 한 번 보여주기'용 등장 지점 — 플레이어 눈에 보이고(벽에 안 가림) minDistance~maxDistance 떨어진 NavMesh 위.
    /// 지금 보고 있는 방향(화면 안)을 가장 우선, 그다음 preferredDistance에 가까운 곳. 걸어서 플레이어에게 올 수 있는 곳만.
    /// 정해 둔 자리(spawn)에서 플레이어로 오는 길 위의 지점도 후보 — 원래 오던 방향에서 보이면 그쪽을 씀.
    /// </summary>
    public static bool TryPickRevealPoint(Vector3 spawn, Transform player, Transform creature,
        float minDistance, float maxDistance, float preferredDistance, out Vector3 point)
    {
        point = spawn;
        if (player == null)
        {
            return false;
        }

        Camera cam = player.GetComponentInChildren<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
        }

        Vector3 target = player.position;
        List<Vector3> candidates = new List<Vector3>(Resample(Route(spawn, target), 1f));
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI * 2f / 32f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            foreach (float r in new[] { minDistance, preferredDistance, (preferredDistance + maxDistance) * 0.5f, maxDistance })
            {
                candidates.Add(target + dir * r);
            }
        }

        float best = float.MaxValue;
        bool found = false;
        foreach (Vector3 c in candidates)
        {
            if (!NavMesh.SamplePosition(c, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 p = hit.position;
            float d = FlatDistance(p, target);
            if (d < minDistance || d > maxDistance || Mathf.Abs(p.y - target.y) > 2.5f || !IsVisible(p, player, creature))
            {
                continue;
            }

            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(p, target, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            // 화면 안(보고 있는 방향)이면 크게 우선, 길이 곧을수록(돌아오지 않고 바로 달려옴) 좋음
            float angle = 180f;
            if (cam != null)
            {
                Vector3 fwd = cam.transform.forward;
                fwd.y = 0f;
                Vector3 to = p - cam.transform.position;
                to.y = 0f;
                angle = Vector3.Angle(fwd, to);
            }

            float detour = Length(path.corners) / Mathf.Max(1f, d);
            float score = (angle <= 40f ? 0f : angle <= 70f ? 50f : 200f) + Mathf.Abs(d - preferredDistance) * 2f + (detour - 1f) * 20f;
            if (score < best)
            {
                best = score;
                point = p;
                found = true;
            }
        }

        return found;
    }

    /// <summary>이 지점의 크리처가 지금 플레이어 화면 안에 보이는지 (벽에 안 가리고 화면 범위 안).</summary>
    public static bool IsOnScreen(Vector3 ground, Transform player, Transform creature)
    {
        Camera cam = player != null ? player.GetComponentInChildren<Camera>() : null;
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam == null)
        {
            return false;
        }

        Vector3 vp = cam.WorldToViewportPoint(ground + Vector3.up * 1.3f);
        return vp.z > 0f && vp.x > 0.1f && vp.x < 0.9f && vp.y > 0.05f && vp.y < 0.95f && IsVisible(ground, player, creature);
    }

    /// <summary>
    /// 크리처가 '다른 곳으로 떠날' 목적지 — from(크리처)에서 걸어서 갈 수 있고 minDistance~maxDistance 떨어진 NavMesh 위,
    /// avoid(플레이어·숨은 곳)에서 될수록 먼 곳. 방 밖 복도 쪽이 고르기 쉽도록 여러 거리·방향을 훑음.
    /// </summary>
    public static bool TryPickLeavePoint(Vector3 from, Vector3 avoid, float minDistance, float maxDistance, out Vector3 point)
    {
        point = from;
        float best = float.MinValue;
        bool found = false;
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int ring = 0; ring < 3; ring++)
            {
                float r = Mathf.Lerp(minDistance, maxDistance, ring / 2f);
                if (!NavMesh.SamplePosition(from + dir * r, out NavMeshHit hit, 3f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - from.y) > 2.5f)
                {
                    continue;
                }

                NavMeshPath path = new NavMeshPath();
                if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float len = Length(path.corners);
                if (len < minDistance || len > maxDistance * 1.8f)
                {
                    continue;
                }

                float score = FlatDistance(hit.position, avoid);
                if (score > best)
                {
                    best = score;
                    point = hit.position;
                    found = true;
                }
            }
        }

        return found;
    }

    /// <summary>
    /// AI를 잠깐 멈추고 target까지 걸어가게 함(돌아가는 동안엔 잡지 않음). 도착하면 AI를 다시 켜 그 자리부터 평소처럼 순찰.
    /// 도중에 숨는 곳에서 나온 플레이어가 reengageDistance 안에서 보이면 AI를 켜고 done(true) — 호출한 쪽이 다시 쫓게 함.
    /// </summary>
    public static IEnumerator WalkAway(EnemyBase creature, Vector3 target, Transform player, float walkSpeed, float reengageDistance,
        System.Action<bool> done)
    {
        EnemyState s = creature.RuntimeState;
        NavMeshAgent agent = creature.Agent;
        creature.Combat.ChangeState(EnemyBase.State.Patrol); // 추격 소리·연출 끔
        s.PlayerDeadLogged = true; // 떠나는 동안엔 몸에 닿아도 잡지 않음 (EnemySense가 잡기 전에 확인하는 값)
        creature.enabled = false;  // AI가 숨은 곳으로 되돌아오지 않게 직접 걷게 함
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.updateRotation = true;
            agent.autoBraking = true;
            agent.speed = walkSpeed > 0f ? walkSpeed : (creature.Data != null && creature.Data.moveSpeed > 0f ? creature.Data.moveSpeed : 1.5f);
            agent.SetDestination(target);
        }

        creature.SnapToPatrolAnimation(false);

        bool reengage = false;
        float start = Time.time;
        while (Time.time - start < 45f)
        {
            if (PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead || creature == null)
            {
                break;
            }

            PlayerHidingController hiding = player != null ? player.GetComponent<PlayerHidingController>() : null;
            if (player != null && (hiding == null || !hiding.isHiding)
                && Vector3.Distance(player.position, creature.transform.position) < reengageDistance
                && IsVisible(creature.transform.position, player, creature.transform))
            {
                reengage = true;
                break;
            }

            if (agent == null || !agent.isOnNavMesh || agent.pathStatus == NavMeshPathStatus.PathInvalid
                || (!agent.pathPending && agent.remainingDistance <= 0.8f))
            {
                break;
            }

            yield return null;
        }

        if (creature != null)
        {
            s.PlayerDeadLogged = false;
            if (creature.Anim != null)
            {
                creature.Anim.speed = 1f;
            }

            creature.enabled = true; // 이 자리부터 다시 평소 AI (순찰)
        }

        done(reengage);
    }

    private static bool TryFindHiddenPoint(Vector3 target, float distance, Transform player, Transform creature, out Vector3 point)
    {
        point = target;
        float best = float.MaxValue;
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24f;
            for (int ring = 0; ring < 3; ring++)
            {
                float r = distance + ring * 4f;
                Vector3 c = target + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (!NavMesh.SamplePosition(c, out NavMeshHit hit, 2f, NavMesh.AllAreas) || IsVisible(hit.position, player, creature))
                {
                    continue;
                }

                NavMeshPath p = new NavMeshPath();
                if (!NavMesh.CalculatePath(hit.position, target, NavMesh.AllAreas, p) || p.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float len = Length(p.corners);
                float score = Mathf.Abs(len - distance * 1.3f); // 너무 멀지도, 너무 가깝지도 않게
                if (score < best)
                {
                    best = score;
                    point = hit.position;
                }
            }
        }

        return best < float.MaxValue;
    }

    private static List<Vector3> Route(Vector3 from, Vector3 to)
    {
        List<Vector3> route = new List<Vector3>();
        Vector3 a = NavMesh.SamplePosition(from, out NavMeshHit ha, 3f, NavMesh.AllAreas) ? ha.position : from;
        Vector3 b = NavMesh.SamplePosition(to, out NavMeshHit hb, 3f, NavMesh.AllAreas) ? hb.position : to;
        NavMeshPath path = new NavMeshPath();
        // 닫힌 문 때문에 끊긴 길(Partial)이어도 갈 수 있는 데까지 씀
        if (NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) && path.corners.Length > 0)
        {
            route.AddRange(path.corners);
        }
        else
        {
            route.Add(a);
        }

        if (route.Count == 1)
        {
            route.Add(b);
        }

        return route;
    }

    private static List<Vector3> Resample(List<Vector3> route, float step)
    {
        List<Vector3> result = new List<Vector3> { route[0] };
        for (int i = 1; i < route.Count; i++)
        {
            Vector3 a = route[i - 1];
            Vector3 b = route[i];
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
            for (int k = 1; k <= n; k++)
            {
                result.Add(Vector3.Lerp(a, b, k / (float)n));
            }
        }

        return result;
    }

    private static float Length(Vector3[] corners)
    {
        float len = 0f;
        for (int i = 1; i < corners.Length; i++)
        {
            len += Vector3.Distance(corners[i - 1], corners[i]);
        }

        return len;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}

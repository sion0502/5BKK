using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 점프스케어 확인용 (Play 모드). 이름이 맞는 적을 플레이어 앞 3m에 세우고 PlayerDeathDebug.BeginJumpscare로 사망 연출을 시작,
/// captureFolder를 주면 0.1초마다 플레이어 카메라 화면을 PNG로 저장(OnGUI 암전은 안 찍힘).
/// 메뉴는 선택한 적(Hierarchy)으로 바로 실행.
/// </summary>
public static class CreatureJumpscarePreview
{
    static string folder;
    static Camera cam;
    static float startTime;
    static float nextShot;
    static int shot;
    const float Interval = 0.1f;
    const float Duration = 5f;

    [MenuItem("Tools/Creatures/Preview Jumpscare On Selected Enemy (Play Mode)")]
    static void PreviewSelected()
    {
        EnemyBase enemy = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<EnemyBase>() : null;
        if (enemy == null)
        {
            Debug.LogWarning("[JumpscarePreview] Hierarchy에서 적을 선택하세요.");
            return;
        }
        Start(enemy, null, true);
    }

    public static string Start(string enemyNamePrefix, string captureFolder, bool chaseFirst = true)
    {
        foreach (EnemyBase e in Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (e.name.StartsWith(enemyNamePrefix))
                return Start(e, captureFolder, chaseFirst);
        return "적 없음: " + enemyNamePrefix;
    }

    static EnemyBase pendingEnemy;
    static float pendingAt;

    /// <summary>
    /// chaseFirst: 실제 게임처럼 추격 상태(관절 꺾기·몸 늘어남·겹친 몸 등 추격 전용 연출이 켜짐)로 7m 앞에서 달려오게 한 뒤
    /// 잡히면 자연스럽게 사망 연출, 2.5초 안에 못 잡으면 강제로 시작. false면 3m 앞에서 바로 시작.
    /// </summary>
    static string Start(EnemyBase enemy, string captureFolder, bool chaseFirst)
    {
        if (!Application.isPlaying)
            return "Play 모드에서만";
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return "플레이어 없음";
        cam = player.GetComponentInChildren<Camera>();
        if (cam == null)
            return "플레이어 카메라 없음";

        // 다른 적은 꺼서 끼어들지 않게
        foreach (EnemyBase e in Object.FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (e != enemy)
                e.gameObject.SetActive(false);

        enemy.gameObject.SetActive(true);
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        fwd.Normalize();
        Vector3 pos = player.transform.position + fwd * (chaseFirst ? 7f : 3f);
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            pos = hit.position;
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.Warp(pos);
        else
            enemy.transform.position = pos;
        enemy.transform.rotation = Quaternion.LookRotation(-fwd, Vector3.up);

        if (chaseFirst && ForceChase(enemy))
        {
            pendingEnemy = enemy;
            pendingAt = Time.realtimeSinceStartup + 2.5f;
            EditorApplication.update -= PendingTick;
            EditorApplication.update += PendingTick;
        }
        else
        {
            PlayerDeathDebug.BeginJumpscare(enemy);
        }

        folder = captureFolder;
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
            startTime = Time.realtimeSinceStartup;
            nextShot = 0f;
            shot = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        return "시작: " + enemy.name + " (" + (enemy.GetComponent<CreatureJumpscare>() is CreatureJumpscare c ? c.GetType().Name : "기본 연출") + ")";
    }

    /// <summary>EnemyCombat.ChangeState(Chase)를 리플렉션으로 (internal).</summary>
    static bool ForceChase(EnemyBase enemy)
    {
        const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        object combat = typeof(EnemyBase).GetProperty("Combat", bf)?.GetValue(enemy);
        var change = combat?.GetType().GetMethod("ChangeState");
        if (change == null)
            return false;
        change.Invoke(combat, new object[] { EnemyBase.State.Chase });
        return true;
    }

    static void PendingTick()
    {
        if (!Application.isPlaying || pendingEnemy == null || PlayerDeathDebug.IsDying || PlayerDeathDebug.IsDead)
        {
            EditorApplication.update -= PendingTick;
            pendingEnemy = null;
            return;
        }
        if (Time.realtimeSinceStartup >= pendingAt)
        {
            EditorApplication.update -= PendingTick;
            PlayerDeathDebug.BeginJumpscare(pendingEnemy);
            pendingEnemy = null;
        }
    }

    /// <summary>캡처한 프레임(최대 24장)을 6열 콘택트 시트 한 장으로.</summary>
    public static string BuildSheet(string captureFolder, string outPath)
    {
        string[] files = Directory.GetFiles(captureFolder, "*.png");
        System.Array.Sort(files);
        const int cols = 6, w = 320, h = 180;
        int n = Mathf.Min(files.Length, 24), rows = (n + cols - 1) / cols;
        if (n == 0)
            return "프레임 없음";
        var sheet = new Texture2D(w * cols, h * rows, TextureFormat.RGB24, false);
        for (int i = 0; i < n; i++)
        {
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(files[i]));
            RenderTexture rt = RenderTexture.GetTemporary(w, h);
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var small = new Texture2D(w, h, TextureFormat.RGB24, false);
            small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            sheet.SetPixels((i % cols) * w, (rows - 1 - i / cols) * h, w, h, small.GetPixels());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(small);
        }
        sheet.Apply();
        File.WriteAllBytes(outPath, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        return n + "/" + files.Length + " frames";
    }

    static void Tick()
    {
        float t = Time.realtimeSinceStartup - startTime;
        if (!Application.isPlaying || cam == null || t > Duration)
        {
            EditorApplication.update -= Tick;
            return;
        }
        if (t < nextShot)
            return;
        nextShot += Interval;

        var rt = RenderTexture.GetTemporary(640, 360, 24);
        RenderTexture prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        RenderTexture.active = rt;
        var tex = new Texture2D(640, 360, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(Path.Combine(folder, string.Format("f{0:00}_{1:0.0}s.png", shot++, t)), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}

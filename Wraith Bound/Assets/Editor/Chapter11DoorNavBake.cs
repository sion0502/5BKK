#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class Chapter11DoorNavBake
{
    const string Flag = "Assets/Editor/Chapter11DoorNavBake.once";
    const string ScenePath = "Assets/Scenes/Chapter/Chapter1 1.unity";
    const string SceneName = "Chapter1 1";
    const string ReportPath = "Temp/door-nav-report.txt";

    static bool _started;
    static bool _opened;
    static bool _carveCleared;
    static bool _reported;
    static double _giveUpAt;
    static double _testAfter;
    static NavMeshSurface _surface;
    static readonly List<NavMeshObstacle> _pausedObstacles = new List<NavMeshObstacle>();

    static Chapter11DoorNavBake()
    {
        if (!File.Exists(Flag))
            return;

        _giveUpAt = EditorApplication.timeSinceStartup + 7200;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        if (_started)
        {
            if (EditorApplication.isPlaying)
                return;
            if (_surface != null && NavMeshAssetManager.instance.IsSurfaceBaking(_surface))
                return;

            if (!_carveCleared)
            {
                PauseObstacles();
                _carveCleared = true;
                _testAfter = EditorApplication.timeSinceStartup + 2.0;
                return;
            }

            if (EditorApplication.timeSinceStartup < _testAfter)
                return;
            if (!_reported)
            {
                _reported = true;
                Finish();
            }
            return;
        }

        if (EditorApplication.isPlaying)
        {
            _giveUpAt = EditorApplication.timeSinceStartup + 7200;
            return;
        }

        if (EditorApplication.timeSinceStartup > _giveUpAt)
        {
            WriteReport("gave up before bake");
            Cleanup();
            return;
        }

        Scene active = EditorSceneManager.GetActiveScene();
        if (!active.IsValid() || active.name != SceneName)
        {
            if (active.IsValid() && active.isDirty)
            {
                WriteReport("active scene is dirty, did not switch: " + active.name);
                Cleanup();
                return;
            }

            if (_opened)
                return;

            _opened = true;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return;
        }

        _surface = UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();
        if (_surface == null)
        {
            WriteReport("NavMeshSurface missing");
            Cleanup();
            return;
        }

        _surface.collectObjects = CollectObjects.All;
        _surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        _surface.layerMask = 171970559;
        _surface.ignoreNavMeshObstacle = true;
        EditorUtility.SetDirty(_surface);

        List<BoxCollider> leaves = FindDoorLeaves();
        List<GameObject> spans = new List<GameObject>();
        var disabled = new List<Collider>();
        try
        {
            var used = new bool[leaves.Count];
            for (int i = 0; i < leaves.Count; i++)
            {
                if (used[i] || leaves[i] == null)
                    continue;

                var group = new List<BoxCollider>();
                Vector3 origin = leaves[i].transform.TransformPoint(leaves[i].center);
                for (int j = i; j < leaves.Count; j++)
                {
                    if (used[j] || leaves[j] == null)
                        continue;
                    Vector3 point = leaves[j].transform.TransformPoint(leaves[j].center);
                    if (j != i && Vector3.Distance(origin, point) > 2.2f)
                        continue;
                    used[j] = true;
                    group.Add(leaves[j]);
                    DisableDoorColliders(leaves[j], disabled);
                }

                spans.Add(CreateWidePassage(group));
            }

            NavMeshAssetManager.instance.StartBakingSurfaces(new UnityEngine.Object[] { _surface });
            _started = true;
        }
        finally
        {
            for (int i = 0; i < disabled.Count; i++)
            {
                if (disabled[i] != null)
                    disabled[i].enabled = true;
            }

            for (int i = 0; i < spans.Count; i++)
            {
                if (spans[i] != null)
                    UnityEngine.Object.DestroyImmediate(spans[i]);
            }
        }
    }

    static void Finish()
    {
        string report = ReportDoors();
        RestoreObstacles();
        WriteReport(report);

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.IsValid() && scene.name == SceneName)
            EditorSceneManager.SaveScene(scene);

        Cleanup();
    }

    static List<BoxCollider> FindDoorLeaves()
    {
        var leaves = new List<BoxCollider>();
        BoxCollider[] boxes = UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < boxes.Length; i++)
        {
            BoxCollider box = boxes[i];
            if (!IsDoorLeaf(box))
                continue;
            leaves.Add(box);
        }

        return leaves;
    }

    static bool IsDoorLeaf(BoxCollider box)
    {
        if (box == null || box.isTrigger)
            return false;

        bool named = false;
        Transform t = box.transform;
        while (t != null)
        {
            if (t.name.IndexOf("Cabinet", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (t.name.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                named = true;
                break;
            }
            t = t.parent;
        }

        if (!named)
            return false;

        Vector3 world = WorldSize(box);
        float thin = Mathf.Min(world.x, world.z);
        float wide = Mathf.Max(world.x, world.z);
        return world.y > 1.2f && thin < 0.9f && thin > 0.02f && wide > 0.7f;
    }

    static void DisableDoorColliders(BoxCollider leaf, List<Collider> disabled)
    {
        Transform doorRoot = leaf.transform;
        Transform cursor = leaf.transform;
        while (cursor != null)
        {
            if (cursor.name.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0)
                doorRoot = cursor;
            cursor = cursor.parent;
        }

        Collider[] cols = doorRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider col = cols[i];
            if (col == null || !col.enabled || col.isTrigger || disabled.Contains(col))
                continue;
            col.enabled = false;
            disabled.Add(col);
        }
    }

    static GameObject CreateWidePassage(List<BoxCollider> group)
    {
        BoxCollider leaf = group[0];
        Vector3 world = WorldSize(leaf);
        bool thinIsX = world.x < world.z;
        Vector3 thinAxis = leaf.transform.TransformDirection(thinIsX ? Vector3.right : Vector3.forward);
        thinAxis.y = 0f;
        if (thinAxis.sqrMagnitude < 0.0001f)
            thinAxis = Vector3.forward;
        thinAxis.Normalize();
        Vector3 wideAxis = Vector3.Cross(Vector3.up, thinAxis);

        float thick = thinIsX ? world.x : world.z;
        float floorY = leaf.transform.position.y;
        Vector3 center = Vector3.zero;
        float minAlong = float.MaxValue;
        float maxAlong = float.MinValue;
        for (int i = 0; i < group.Count; i++)
        {
            Vector3 point = group[i].transform.TransformPoint(group[i].center);
            center += point;
            float along = Vector3.Dot(point, wideAxis);
            Vector3 size = WorldSize(group[i]);
            float halfWide = Mathf.Max(size.x, size.z) * 0.5f;
            minAlong = Mathf.Min(minAlong, along - halfWide);
            maxAlong = Mathf.Max(maxAlong, along + halfWide);
        }

        center /= group.Count;
        float width = Mathf.Max(2.8f, (maxAlong - minAlong) + 1.4f);
        var root = new GameObject("NavBakeSpan");
        root.layer = 10;
        root.transform.SetPositionAndRotation(
            new Vector3(center.x, floorY + 0.03f, center.z),
            Quaternion.LookRotation(thinAxis, Vector3.up));

        BoxCollider span = root.AddComponent<BoxCollider>();
        span.size = new Vector3(width, 0.08f, thick + 3.2f);

        CreateWallBlocker(root.transform, wideAxis, center, floorY, width * 0.5f + 1.6f, 1);
        CreateWallBlocker(root.transform, wideAxis, center, floorY, width * 0.5f + 1.6f, -1);
        return root;
    }

    static void CreateWallBlocker(Transform parent, Vector3 wideAxis, Vector3 center, float floorY, float along, int sign)
    {
        var blocker = new GameObject("NavBakeWall");
        blocker.layer = 0;
        blocker.transform.SetParent(parent, false);
        Vector3 pos = center + wideAxis * (along * sign);
        pos.y = floorY + 1.1f;
        blocker.transform.SetPositionAndRotation(pos, parent.rotation);
        BoxCollider box = blocker.AddComponent<BoxCollider>();
        box.size = new Vector3(3.2f, 2.2f, 0.45f);
    }

    static void PauseObstacles()
    {
        _pausedObstacles.Clear();
        NavMeshObstacle[] obstacles = UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < obstacles.Length; i++)
        {
            if (obstacles[i] == null || !obstacles[i].enabled)
                continue;
            obstacles[i].enabled = false;
            _pausedObstacles.Add(obstacles[i]);
        }
    }

    static void RestoreObstacles()
    {
        for (int i = 0; i < _pausedObstacles.Count; i++)
        {
            if (_pausedObstacles[i] != null)
                _pausedObstacles[i].enabled = true;
        }

        _pausedObstacles.Clear();
    }

    static string ReportDoors()
    {
        var sb = new StringBuilder();
        List<BoxCollider> leaves = FindDoorLeaves();
        int connected = 0;
        sb.AppendLine("leaves=" + leaves.Count);
        for (int i = 0; i < leaves.Count; i++)
        {
            BoxCollider leaf = leaves[i];
            bool ok = PathConnects(leaf, out string detail);
            if (ok)
                connected++;
            sb.AppendLine((ok ? "OK " : "FAIL ") + leaf.transform.root.name + " / " + leaf.name + " " + detail);
        }

        sb.AppendLine("connected=" + connected + "/" + leaves.Count);
        Debug.Log(sb.ToString());
        return sb.ToString();
    }

    static bool PathConnects(BoxCollider leaf, out string detail)
    {
        Vector3 world = WorldSize(leaf);
        bool thinIsX = world.x < world.z;
        Vector3 axis = leaf.transform.TransformDirection(thinIsX ? Vector3.right : Vector3.forward);
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f)
        {
            detail = "no axis";
            return false;
        }

        axis.Normalize();
        Vector3 center = leaf.transform.TransformPoint(leaf.center);
        center.y = leaf.transform.position.y + 0.2f;
        Vector3 a = center - axis * 1.4f;
        Vector3 b = center + axis * 1.4f;
        bool sideA = NavMesh.SamplePosition(a, out NavMeshHit hitA, 2f, NavMesh.AllAreas);
        bool sideB = NavMesh.SamplePosition(b, out NavMeshHit hitB, 2f, NavMesh.AllAreas);
        bool connected = false;
        if (sideA && sideB)
        {
            var path = new NavMeshPath();
            connected = NavMesh.CalculatePath(hitA.position, hitB.position, NavMesh.AllAreas, path) &&
                        path.status == NavMeshPathStatus.PathComplete;
        }

        detail = "sample=" + sideA + "/" + sideB + " connected=" + connected;
        return connected;
    }

    static Vector3 WorldSize(BoxCollider box)
    {
        Vector3 v = Vector3.Scale(box.size, box.transform.lossyScale);
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    static void WriteReport(string text)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, text ?? "");
        Debug.Log("DoorNav report\n" + text);
    }

    static void Cleanup()
    {
        EditorApplication.update -= Tick;
        if (File.Exists(Flag))
            File.Delete(Flag);
    }
}
#endif

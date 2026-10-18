#if UNITY_EDITOR
using System;
using System.IO;
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
    const string SceneName = "Chapter1 1";

    static bool _started;
    static bool _reported;
    static double _giveUpAt;
    static NavMeshSurface _surface;

    static Chapter11DoorNavBake()
    {
        if (!File.Exists(Flag))
            return;

        _giveUpAt = EditorApplication.timeSinceStartup + 600;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (_started)
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (_surface != null && NavMeshAssetManager.instance.IsSurfaceBaking(_surface))
                return;
            if (!_reported)
            {
                _reported = true;
                ReportDoors();
                Scene scene = EditorSceneManager.GetActiveScene();
                if (scene.IsValid() && scene.name == SceneName)
                    EditorSceneManager.SaveScene(scene);
                if (File.Exists(Flag))
                    File.Delete(Flag);
                EditorApplication.update -= Tick;
            }
            return;
        }

        if (EditorApplication.timeSinceStartup > _giveUpAt)
        {
            EditorApplication.update -= Tick;
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            return;

        Scene active = EditorSceneManager.GetActiveScene();
        if (!active.IsValid() || active.name != SceneName)
            return;

        _surface = UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>();
        if (_surface == null)
            return;

        _surface.collectObjects = CollectObjects.All;
        _surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        _surface.layerMask = 171970559;
        _surface.ignoreNavMeshObstacle = true;
        EditorUtility.SetDirty(_surface);

        _started = true;
        NavMeshAssetManager.instance.StartBakingSurfaces(new UnityEngine.Object[] { _surface });
    }

    static void ReportDoors()
    {
        BoxCollider[] boxes = UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < boxes.Length; i++)
        {
            BoxCollider box = boxes[i];
            if (box == null || box.isTrigger || !IsDoorSlab(box))
                continue;

            Vector3 world = Abs(Vector3.Scale(box.size, box.transform.lossyScale));
            int thin = world.x < world.z ? 0 : 2;
            Vector3 axis = box.transform.TransformDirection(thin == 0 ? Vector3.right : Vector3.forward);
            axis.y = 0f;
            if (axis.sqrMagnitude < 0.0001f)
                continue;
            axis.Normalize();

            Vector3 center = box.transform.TransformPoint(box.center);
            center.y = 0.2f;
            Vector3 a = center - axis * 1.2f;
            Vector3 b = center + axis * 1.2f;
            bool sideA = NavMesh.SamplePosition(a, out NavMeshHit hitA, 1.5f, NavMesh.AllAreas);
            bool sideB = NavMesh.SamplePosition(b, out NavMeshHit hitB, 1.5f, NavMesh.AllAreas);
            bool connected = false;
            if (sideA && sideB)
            {
                var path = new NavMeshPath();
                connected = NavMesh.CalculatePath(hitA.position, hitB.position, NavMesh.AllAreas, path) &&
                            path.status == NavMeshPathStatus.PathComplete;
            }

            Debug.Log("DoorNav " + box.transform.root.name + " connected=" + connected);
        }
    }

    static bool IsDoorSlab(BoxCollider box)
    {
        Transform t = box.transform;
        bool named = false;
        while (t != null)
        {
            if (t.name.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                named = true;
                break;
            }
            t = t.parent;
        }
        if (!named)
            return false;

        Vector3 world = Abs(Vector3.Scale(box.size, box.transform.lossyScale));
        return world.y > 1.2f && Mathf.Min(world.x, world.z) < 0.8f;
    }

    static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
#endif

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class InteractionRegressionChecks
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            foreach (string name in new[] { "UnlockDoor", "LockedDoor", "LockedDoor (Lever)" })
            {
                GameObject wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefab/Object/Chapter1/" + name + ".prefab");
                MeshFilter visibleWall = wallPrefab.transform.Find("HH_Hospital_PartitionWall_Door_01_LOD0")
                    .GetComponent<MeshFilter>();
                MeshCollider wallCollider = visibleWall.GetComponent<MeshCollider>();
                Require(wallCollider != null && wallCollider.sharedMesh == visibleWall.sharedMesh,
                    name + ": occlusion geometry must match the visible wall and its transform");
            }

            // A trigger can be a target, but must never make a solid wall transparent.
            GameObject candidate = new GameObject("Trigger interaction regression");
            candidate.transform.position = new Vector3(20, 1, 2);
            DoorClick candidateDoor = candidate.AddComponent<DoorClick>();
            candidateDoor.interactDistance = 3f;
            candidate.AddComponent<BoxCollider>().isTrigger = true;
            Physics.SyncTransforms();
            Ray triggerRay = new Ray(new Vector3(20, 1, 0), Vector3.forward);
            Require(PlayerInteractor.TryGetInteractionHit(triggerRay, 3, null, out RaycastHit triggerHit)
                && ReferenceEquals(PlayerInteractor.ResolveTarget(triggerHit.collider), candidateDoor),
                "Interaction triggers must be selectable");
            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.transform.position = new Vector3(20, 1, 1);
            Physics.SyncTransforms();
            Require(PlayerInteractor.TryGetInteractionHit(triggerRay, 3, null, out RaycastHit barrierHit)
                && barrierHit.collider.gameObject == barrier,
                "Candidate selection must not bypass a real wall");
            UnityEngine.Object.DestroyImmediate(barrier);
            UnityEngine.Object.DestroyImmediate(candidate);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Object/Chapter1/Cabinet (ItemCreate).prefab");
            GameObject cabinet = UnityEngine.Object.Instantiate(prefab);
            cabinet.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            cabinet.transform.localScale = Vector3.one;
            Transform shell = cabinet.transform.Find("HH_Wall_Cabinet_01");
            Require(shell.GetComponents<BoxCollider>().Length == 5,
                "Cabinet shell must use five hollow box colliders");
            Require(shell.GetComponent<MeshCollider>() == null,
                "Cabinet interaction must not depend on mesh cooking/read access");
            Physics.SyncTransforms();
            foreach (float z in new[] { -0.35f, 0.35f })
            {
                Require(PlayerInteractor.TryGetInteractionHit(new Ray(new Vector3(2, 0.6f, z), Vector3.left),
                    4, null, out RaycastHit hit), "Cabinet must be hit");
                Require(PlayerInteractor.ResolveTarget(hit.collider) is DoorClick,
                    "Cabinet front must select a door, not " + hit.collider.name);
            }
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Regression wall on Ignore Raycast layer";
            wall.layer = LayerMask.NameToLayer("Ignore Raycast");
            wall.transform.position = new Vector3(1.2f, 0.6f, 0);
            wall.transform.localScale = new Vector3(0.1f, 2, 2);
            Physics.SyncTransforms();
            Require(PlayerInteractor.TryGetInteractionHit(new Ray(new Vector3(2, 0.6f, 0.35f), Vector3.left),
                4, null, out RaycastHit blocked) && blocked.collider.gameObject == wall,
                "Wall must block a cabinet even on Ignore Raycast layer");
            Require(!PlayerInteractor.HasClearPath(new Vector3(2, 0.6f, 0),
                new Vector3(0.5f, 0.6f, 0), cabinet.transform, null), "Wall must block switch anchor visibility");
            UnityEngine.Object.DestroyImmediate(wall);
            Require(PlayerInteractor.TryGetInteractionHit(new Ray(new Vector3(-1, 0.6f, 0.35f), Vector3.right),
                4, null, out RaycastHit rear) && PlayerInteractor.ResolveTarget(rear.collider) == null,
                "Cabinet back must remain solid");
            Debug.Log("INTERACTION_REGRESSION_PASS: both cabinet doors, rear blocking, wall layers, anchor occlusion");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
            return;
        }
        EditorApplication.Exit(0);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

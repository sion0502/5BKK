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
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Object/Chapter1/Cabinet (ItemCreate).prefab");
            GameObject cabinet = UnityEngine.Object.Instantiate(prefab);
            cabinet.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            cabinet.transform.localScale = Vector3.one;
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

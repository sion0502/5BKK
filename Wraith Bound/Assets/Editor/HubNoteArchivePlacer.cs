using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 허브 쪽지 게시판 자리 오브젝트를 열린 Hub 씬에 놓음 (2026-11-19 '플레이 아닐 때 씬 뷰에서 위치 수정 가능하게').
/// 이미 있으면 선택만 함. 위치는 씬 뷰에서 옮기면 되고(로컬 +z = 방 쪽), 게시판 내용은 실행 중 HubNoteArchive가 만듦.
/// </summary>
public static class HubNoteArchivePlacer
{
    [MenuItem("Tools/Story/Place Hub Note Archive")]
    public static void Place()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Hub")
        {
            Debug.LogError("[HubNoteArchive] Hub 씬을 열고 실행");
            return;
        }

        HubNoteArchive existing = Object.FindFirstObjectByType<HubNoteArchive>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        var go = new GameObject("HubNoteArchive");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetPositionAndRotation(HubNoteArchive.DefaultPosition, Quaternion.identity);
        go.AddComponent<HubNoteArchive>();
        Undo.RegisterCreatedObjectUndo(go, "Place Hub Note Archive");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;
        Debug.Log("[HubNoteArchive] 게시판 자리 배치 — 씬 뷰에서 옮긴 뒤 씬 저장");
    }
}

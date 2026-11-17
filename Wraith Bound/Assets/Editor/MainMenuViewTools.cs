using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 첫 플레이 메인 메뉴용 에디터 도구.
/// - Create Menu View Anchors: 열린 씬(Intro)에 "MenuViews" 루트와 MenuView_* 앵커를 기본 구도로 만듦.
///   앵커를 선택하고 Scene 뷰에서 구도를 잡은 뒤 GameObject > Align With View (Ctrl+Shift+F) 로 맞추면
///   메인 메뉴가 그 위치·회전을 그대로 씀. 이미 있는 앵커는 건드리지 않음.
/// - Reset Game Progress: 진행 기록을 지워 다음 실행 때 첫 플레이 메뉴가 뜨게 함.
/// </summary>
public static class MainMenuViewTools
{
    [MenuItem("Tools/Story/Create Menu View Anchors")]
    public static void CreateAnchors()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.Equals("Intro") &&
            !EditorUtility.DisplayDialog("메뉴 시점 앵커",
                $"지금 활성 씬은 '{scene.name}'입니다. 첫 플레이 메뉴 배경은 Intro 씬이에요. 그래도 여기에 만들까요?",
                "만들기", "취소"))
        {
            return;
        }

        GameObject root = null;
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == "MenuViews")
            {
                root = go;
                break;
            }
        }

        if (root == null)
        {
            root = new GameObject("MenuViews");
            Undo.RegisterCreatedObjectUndo(root, "Create Menu Views");
        }

        int created = 0;
        foreach (MainMenuViews.Id id in System.Enum.GetValues(typeof(MainMenuViews.Id)))
        {
            string anchorName = MainMenuViews.AnchorName(id);
            if (root.transform.Find(anchorName) != null)
            {
                continue;
            }

            MainMenuViews.View view = MainMenuViews.Default(id);
            GameObject anchor = new GameObject(anchorName);
            Undo.RegisterCreatedObjectUndo(anchor, "Create Menu View Anchor");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.SetPositionAndRotation(view.Position, view.Rotation);
            created++;
        }

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[MainMenuViewTools] 앵커 {created}개 생성. 각 앵커를 선택 → Scene 뷰에서 구도 잡기 → Ctrl+Shift+F, 그리고 씬 저장.");
    }

    [MenuItem("Tools/Story/Reset Game Progress")]
    public static void ResetProgress()
    {
        GameProgress.Clear();
        Debug.Log("[MainMenuViewTools] 진행 기록 삭제 — 다음 실행 때 첫 플레이 메뉴(병원 배경)가 뜹니다.");
    }

    [MenuItem("Tools/Story/Mark Game Progress (이어서 하기 메뉴 테스트)")]
    public static void MarkProgress()
    {
        GameProgress.MarkHubReached();
        Debug.Log("[MainMenuViewTools] 진행 기록 생성 — 다음 실행 때 허브 배경 메뉴(이어서 하기)가 뜹니다.");
    }
}

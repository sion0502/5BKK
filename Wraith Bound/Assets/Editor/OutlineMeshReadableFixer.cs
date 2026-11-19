using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// QuickOutline(Outline.cs)은 Awake에서 메시의 정점·노멀을 읽고 UV3·서브메시를 고쳐 씀.
/// 에디터에서는 Read/Write가 꺼진 메시도 읽을 수 있지만, 빌드에서는 막혀서
/// 외곽선이 켜지는 순간(가까이 가서 조준할 때) 캐비닛 문·트롤리 상판 등이 투명하게 보이는 버그가 생김.
///
/// ExternalAssets 폴더는 git에 올라가지 않아 FBX 설정(.meta)을 고쳐도 팀원/빌드 PC에 전달되지 않으므로,
/// 빌드 직전에 Outline이 붙은 프리팹이 쓰는 모델의 Read/Write를 자동으로 켬. (메뉴로 직접 실행도 가능)
/// 실행 중 강조(TutorialHighlight)로 외곽선이 붙는 빌드 씬의 스토리 서류(StoryDocumentPickup) 프리팹 모델도 함께.
/// </summary>
public class OutlineMeshReadableFixer : IPreprocessBuildWithReport
{
    private const string OutlineScriptGuid = "5fea29bb7c508c244a1f805a5fd3fc4d";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Fix(false);
    }

    [MenuItem("Tools/Wraith Bound/Outline 메시 Read-Write 켜기")]
    private static void FixFromMenu()
    {
        Fix(true);
    }

    private static void Fix(bool verbose)
    {
        HashSet<string> modelPaths = new HashSet<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!FileMentionsOutline(prefabPath))
            {
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                continue;
            }

            foreach (Outline outline in prefab.GetComponentsInChildren<Outline>(true))
            {
                CollectMeshModels(outline.gameObject, modelPaths);
            }
        }

        CollectRuntimeHighlightModels(modelPaths);

        List<string> changed = new List<string>();
        foreach (string modelPath in modelPaths)
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null || importer.isReadable)
            {
                continue;
            }

            importer.isReadable = true;
            importer.SaveAndReimport();
            changed.Add(modelPath);
        }

        if (changed.Count > 0)
        {
            Debug.Log("[OutlineMeshReadableFixer] Read/Write 켬 (" + changed.Count + "개):\n" + string.Join("\n", changed));
        }
        else if (verbose)
        {
            Debug.Log("[OutlineMeshReadableFixer] Outline 대상 모델 " + modelPaths.Count + "개 모두 이미 Read/Write가 켜져 있음.");
        }
    }

    // 빌드 씬에 놓인 스토리 서류(StoryDocumentPickup)는 실행 중 강조 외곽선이 붙음 → 그 프리팹 모델도 Read/Write 필요
    private static readonly string[] RuntimeHighlightComponents = { "Assembly-CSharp::StoryDocumentPickup" };

    private static void CollectRuntimeHighlightModels(HashSet<string> modelPaths)
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled || !File.Exists(scene.path))
            {
                continue;
            }

            string text = File.ReadAllText(scene.path);
            foreach (string component in RuntimeHighlightComponents)
            {
                int index = 0;
                while ((index = text.IndexOf(component, index, System.StringComparison.Ordinal)) >= 0)
                {
                    string prefabGuid = FindSourcePrefabGuid(text, index);
                    index += component.Length;
                    if (string.IsNullOrEmpty(prefabGuid))
                    {
                        continue;
                    }

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(prefabGuid));
                    if (prefab != null)
                    {
                        CollectMeshModels(prefab, modelPaths);
                    }
                }
            }
        }
    }

    // 컴포넌트 블록의 m_GameObject → 그 GameObject가 프리팹에서 온 것(stripped)이면 원본 프리팹 guid
    private static string FindSourcePrefabGuid(string sceneText, int componentIndex)
    {
        int blockStart = sceneText.LastIndexOf("\n--- !u!", componentIndex, System.StringComparison.Ordinal);
        string block = sceneText.Substring(blockStart < 0 ? 0 : blockStart, componentIndex - (blockStart < 0 ? 0 : blockStart));
        System.Text.RegularExpressions.Match go = System.Text.RegularExpressions.Regex.Match(block, @"m_GameObject: \{fileID: (\d+)\}");
        if (!go.Success)
        {
            return null;
        }

        System.Text.RegularExpressions.Match header = System.Text.RegularExpressions.Regex.Match(sceneText,
            @"\n--- !u!1 &" + go.Groups[1].Value + @" stripped\r?\nGameObject:\r?\n  m_CorrespondingSourceObject: \{fileID: -?\d+, guid: ([0-9a-f]+)");
        return header.Success ? header.Groups[1].Value : null;
    }

    private static bool FileMentionsOutline(string assetPath)
    {
        try
        {
            return File.ReadAllText(assetPath).Contains(OutlineScriptGuid);
        }
        catch (IOException)
        {
            return false;
        }
    }

    // Outline.cs가 GetComponentsInChildren으로 메시를 모으는 것과 같은 범위
    private static void CollectMeshModels(GameObject root, HashSet<string> modelPaths)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            AddMesh(filter.sharedMesh, modelPaths);
        }

        foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            AddMesh(skinned.sharedMesh, modelPaths);
        }
    }

    private static void AddMesh(Mesh mesh, HashSet<string> modelPaths)
    {
        if (mesh == null)
        {
            return;
        }

        string path = AssetDatabase.GetAssetPath(mesh);
        if (!string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is ModelImporter)
        {
            modelPaths.Add(path);
        }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Read-only audit after the URP asset repair. Does not save scenes or gameplay assets.
[InitializeOnLoad]
public static class HospitalShaderAudit
{
    const string Key = "HospitalShaderAudit.20261005.v1";
    static double readyAt;
    static HospitalShaderAudit()
    {
        if (SessionState.GetBool(Key, false)) return;
        readyAt = EditorApplication.timeSinceStartup + 5;
        EditorApplication.update += WaitForImport;
    }
    static void WaitForImport()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { readyAt = EditorApplication.timeSinceStartup + 5; return; }
        if (EditorApplication.timeSinceStartup < readyAt) return;
        EditorApplication.update -= WaitForImport;
        SessionState.SetBool(Key, true);
        Run();
    }
    [Serializable] public class Entry
    {
        public string path, shader;
        public bool supported;
        public int passes;
        public string[] errors;
    }
    [Serializable] public class Report
    {
        public string project, unityVersion, pipeline;
        public int materialCount;
        public List<Entry> problems = new List<Entry>();
        public List<Entry> shaders = new List<Entry>();
    }
    [MenuItem("Tools/Hospital Assets/Check URP Shaders")]
    public static void Run()
    {
        var report = new Report {
            project = Directory.GetCurrentDirectory(), unityVersion = Application.unityVersion,
            pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? "Built-in" : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().FullName
        };
        var seen = new HashSet<Shader>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] {"Assets/ExternalAssets"}))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) continue;
            report.materialCount++;
            var shader = mat.shader;
            if (shader && seen.Add(shader))
            {
                try { for (int pass = 0; pass < mat.passCount; pass++) ShaderUtil.CompilePass(mat, pass, true); }
                catch (Exception e) { Debug.LogWarning("Hospital shader compile check: " + e.Message); }
                report.shaders.Add(Inspect(path, mat));
            }
            var entry = Inspect(path, mat);
            if (!entry.supported || entry.errors.Length != 0) report.problems.Add(entry);
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/HospitalShaderAudit.json", JsonUtility.ToJson(report, true));
        Debug.Log("[HospitalShaderAudit] Checked " + report.materialCount + " materials; problems: " + report.problems.Count + ". Report: Logs/HospitalShaderAudit.json");
    }
    static Entry Inspect(string path, Material mat)
    {
        var shader = mat.shader;
        return new Entry {
            path = path, shader = shader ? shader.name : "MISSING",
            supported = shader && shader.isSupported && shader.name != "Hidden/InternalErrorShader",
            passes = mat.passCount,
            errors = shader ? ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString() == "Error").Select(m => m.message + " (" + m.file + ":" + m.line + ")").Distinct().ToArray() : new[] {"Missing shader"}
        };
    }
}
#endif

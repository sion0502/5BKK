#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MaskGhostSound))]
public class MaskGhostSoundEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (target is not MaskGhostSound sound)
            return;

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "발소리 맞추기: Play 실행 → Walk/Run Step Interval 슬라이더 조절.\n" +
            "애니와 정확히 맞추려면 Footstep Sync = AnimationEvent 후\n" +
            "Walk 클립에 OnWalkFootstep, Run 클립에 OnRunFootstep 추가.",
            MessageType.Info);

        if (!Application.isPlaying)
            return;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Play 중", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("현재 속도", $"{sound.PlaySpeed:F2} m/s");
        EditorGUILayout.LabelField("다음 발소리까지", $"{sound.NextFootstepIn:F2} 초");
        EditorGUILayout.LabelField("발소리 종류", sound.IsRunFootstep ? "Run (추격)" : "Walk");
        EditorGUILayout.LabelField("다음 추격 울음까지", $"{sound.NextChaseVocalIn:F2} 초");
        Repaint();
    }
}
#endif

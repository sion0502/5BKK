using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 크리처 3종 Animator 다듬기 (Tools > Creatures > Setup Animator Transitions).
/// ① 상태 전환이 전부 0초라 동작이 뚝뚝 끊기던 것 → 이동 상태끼리 0.25초, 공격 진입 0.1초 크로스페이드
/// ② 걷기(State 1)·달리기(State 2) 상태가 MoveSpeedMul 파라미터를 속도 배수로 쓰게 + 프리팹에 CreatureLocomotionSync
///    (보폭 속도는 Tools > Creatures > Measure Ground Offset의 stride 측정값)
/// 다시 실행해도 결과 같음.
/// </summary>
public static class CreatureAnimatorSetup
{
    struct Spec
    {
        public string prefab;
        public float walkStride, runStride, minMul, maxMul;
    }

    static readonly Spec[] Specs =
    {
        // Xenosaurid 달리기 클립은 보폭이 커서(10.3m/s) 추격 4.1m/s면 0.4배 — 너무 느리면 슬로모션이라 0.55에서 멈춤
        new Spec { prefab = "Assets/Prefab/Enemy/Monster/Xenosaurid.prefab", walkStride = 3.31f, runStride = 10.3f, minMul = 0.55f, maxMul = 1.4f },
        new Spec { prefab = "Assets/Prefab/Enemy/Monster/NurseGhost.prefab", walkStride = 1.88f, runStride = 3.41f, minMul = 0.6f, maxMul = 1.5f },
        // 탈춤귀신 순찰은 재즈댄스(춤추며 이동) — 보폭 개념이 없어 맞추지 않음.
        // 추격 = Blender로 만든 TalChase(실측 보폭 7.15m/s) — 발광하듯 다리가 정신없이 돌게 일부러 3.2 기준(추격 4m/s에서 1.25배, 미끄러짐은 관절 꺾임에 묻힘)
        new Spec { prefab = "Assets/Prefab/Enemy/Monster/MaskGhost.prefab", walkStride = 0f, runStride = 3.2f, minMul = 0.6f, maxMul = 1.5f },
        // 같은 크리처의 다른 경로 사본 (Prologue·EnemyScene·Chapter1 2가 씀)
        new Spec { prefab = "Assets/Model/Enemy/Xenosaruid/Xenosaurid/Prefab/Xenosaurid.prefab", walkStride = 3.31f, runStride = 10.3f, minMul = 0.55f, maxMul = 1.4f },
        new Spec { prefab = "Assets/Model/Enemy/MaskGhost/Prefab/Xenosaurid.prefab", walkStride = 3.31f, runStride = 10.3f, minMul = 0.55f, maxMul = 1.4f },
        new Spec { prefab = "Assets/Model/Enemy/MaskGhost/Prefab/NurseGhost.prefab", walkStride = 1.88f, runStride = 3.41f, minMul = 0.6f, maxMul = 1.5f },
        new Spec { prefab = "Assets/Model/Enemy/MaskGhost/Prefab/MaskGhost.prefab", walkStride = 0f, runStride = 3.2f, minMul = 0.6f, maxMul = 1.5f },
    };

    const float MoveBlend = 0.25f;
    const float AttackBlend = 0.1f;

    [MenuItem("Tools/Creatures/Setup Animator Transitions")]
    public static void SetupAll()
    {
        var sb = new StringBuilder();
        foreach (Spec spec in Specs)
            sb.AppendLine(Setup(spec));
        AssetDatabase.SaveAssets();
        Debug.Log("[AnimatorSetup]\n" + sb);
    }

    static string Setup(Spec spec)
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefab);
        if (prefabAsset == null)
            return spec.prefab + " — 없음";
        var controller = prefabAsset.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
        if (controller == null)
            return spec.prefab + " — AnimatorController 없음";

        if (System.Array.FindIndex(controller.parameters, p => p.name == CreatureLocomotionSync.SpeedParameter) < 0)
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = CreatureLocomotionSync.SpeedParameter,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            });

        int blended = 0, synced = 0;
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in sm.states)
        {
            foreach (AnimatorStateTransition t in child.state.transitions)
                blended += FixTransition(t, child.state) ? 1 : 0;
        }
        foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            blended += FixTransition(t, null) ? 1 : 0;

        // 걷기·달리기 상태 = State 1/2 조건으로 들어가는 상태
        foreach (ChildAnimatorState child in sm.states)
        {
            int moveState = MoveStateIndex(sm, child.state);
            if (moveState == 0)
                continue;
            float stride = moveState == 1 ? spec.walkStride : spec.runStride;
            child.state.speedParameterActive = stride > 0f;
            child.state.speedParameter = CreatureLocomotionSync.SpeedParameter;
            synced += stride > 0f ? 1 : 0;
        }
        EditorUtility.SetDirty(controller);

        GameObject root = PrefabUtility.LoadPrefabContents(spec.prefab);
        try
        {
            CreatureLocomotionSync sync = root.GetComponent<CreatureLocomotionSync>();
            if (sync == null)
                sync = root.AddComponent<CreatureLocomotionSync>();
            sync.Configure(spec.walkStride, spec.runStride, spec.minMul, spec.maxMul);
            PrefabUtility.SaveAsPrefabAsset(root, spec.prefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return spec.prefab + " 전환 " + blended + "개 블렌드, 속도 맞춤 상태 " + synced + "개";
    }

    /// <summary>이 상태로 들어오는 전환 조건이 State==1이면 1, State==2면 2, 아니면 0.</summary>
    static int MoveStateIndex(AnimatorStateMachine sm, AnimatorState target)
    {
        foreach (ChildAnimatorState child in sm.states)
            foreach (AnimatorStateTransition t in child.state.transitions)
            {
                if (t.destinationState != target)
                    continue;
                foreach (AnimatorCondition c in t.conditions)
                    if (c.parameter == "State" && c.mode == AnimatorConditionMode.Equals && (c.threshold == 1f || c.threshold == 2f))
                        return (int)c.threshold;
            }
        return 0;
    }

    static bool FixTransition(AnimatorStateTransition t, AnimatorState from)
    {
        if (t.destinationState == null)
            return false;
        bool toAttack = t.destinationState.name.Contains("Attack");
        bool fromAttack = from != null && from.name.Contains("Attack");
        float want = toAttack ? AttackBlend : MoveBlend;
        if (fromAttack && t.duration > 0f)
            return false; // 공격 → 복귀는 원래 값(0.25) 유지
        if (Mathf.Approximately(t.duration, want) && t.hasFixedDuration)
            return false;
        t.hasFixedDuration = true;
        t.duration = want;
        t.offset = 0f;
        return true;
    }
}

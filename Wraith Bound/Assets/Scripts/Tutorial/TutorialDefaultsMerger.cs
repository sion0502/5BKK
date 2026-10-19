using System.Collections.Generic;

/// <summary>
/// 프리팹·씬에 직렬화된 단계/팁 목록에 새로 추가된 기본 항목을 채워 넣습니다.
/// 이미 있는 항목(조건/계기 기준)은 문구를 포함해 그대로 두고, 빠진 기본 항목만 추가합니다.
/// </summary>
public static class TutorialDefaultsMerger
{
    /// <summary>빠진 기본 단계를 기본 순서상 바로 앞 단계 뒤에 끼워 넣습니다.</summary>
    public static void MergeMissingSteps(List<TutorialStep> steps)
    {
        List<TutorialStep> defaults = TutorialDefaults.CreateSteps();

        for (int d = 0; d < defaults.Count; d++)
        {
            if (IndexOfCondition(steps, defaults[d].condition) >= 0)
            {
                continue;
            }

            int insertAt = 0;
            for (int prev = d - 1; prev >= 0; prev--)
            {
                int existing = IndexOfCondition(steps, defaults[prev].condition);
                if (existing >= 0)
                {
                    insertAt = existing + 1;
                    break;
                }
            }

            steps.Insert(insertAt, defaults[d]);
        }
    }

    /// <summary>빠진 기본 팁을 목록 끝에 추가합니다.</summary>
    public static void MergeMissingTips(List<TutorialTip> tips)
    {
        foreach (TutorialTip tip in TutorialDefaults.CreateTips())
        {
            if (!tips.Exists(t => t.trigger == tip.trigger))
            {
                tips.Add(tip);
            }
        }
    }

    private static int IndexOfCondition(List<TutorialStep> steps, TutorialCondition condition)
    {
        return steps.FindLastIndex(s => s.condition == condition);
    }
}

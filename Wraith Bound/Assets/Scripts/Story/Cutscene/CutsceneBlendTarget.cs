using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Timeline의 Cutscene Blend 트랙이 0~1 값(Blend)을 넣어 주는 대상의 공통 부모.
/// 오버레이(알파), 손에 든 문서(들어 올림 정도) 등이 상속합니다.
/// </summary>
public abstract class CutsceneBlendTarget : MonoBehaviour
{
    public abstract float Blend { get; set; }

    /// <summary>에디터 미리보기 후 원래 값으로 되돌릴 속성을 등록 (Timeline GatherProperties).</summary>
    public virtual void GatherPreviewProperties(IPropertyCollector driver)
    {
    }
}

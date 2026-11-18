using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// NonFaceGhost 추격 애니메이션(팔 뻗고 상체 숙임).
/// GhostEnemy는 Animator를 꺼 두므로, 추격 중에만 Animator를 켜서 클립을 재생하고
/// 추격이 끝나면 기본 포즈로 되돌린다. GhostEnemy는 수정하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class NonFaceGhostChaseAnim : MonoBehaviour
{
    [SerializeField] AnimationClip chaseClip;
    [SerializeField] float speed = 1f;

    EnemyBase enemy;
    Animator animator;
    PlayableGraph graph;
    AnimationClipPlayable clipPlayable;
    bool playing;
    float t;

    void Awake()
    {
        enemy = GetComponentInParent<EnemyBase>();
        animator = GetComponentInChildren<Animator>(true);
    }

    void LateUpdate()
    {
        if (enemy == null || animator == null || chaseClip == null)
            return;

        bool chasing = enemy.RuntimeState.CurrentState == EnemyBase.State.Chase;

        if (chasing && !playing)
            StartPlay();
        else if (!chasing && playing)
            StopPlay();

        if (!playing)
            return;

        t += Time.deltaTime * speed;
        float len = Mathf.Max(0.01f, chaseClip.length);
        clipPlayable.SetTime(t % len);
        graph.Evaluate(0f);
    }

    void StartPlay()
    {
        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = null;
        animator.enabled = true;

        graph = PlayableGraph.Create("NonFaceGhostChase");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
        clipPlayable = AnimationClipPlayable.Create(graph, chaseClip);
        output.SetSourcePlayable(clipPlayable);
        t = 0f;
        playing = true;
    }

    void StopPlay()
    {
        playing = false;
        if (graph.IsValid())
            graph.Destroy();
        if (animator != null)
        {
            animator.Rebind();
            animator.enabled = false;
        }
    }

    void OnDisable()
    {
        if (playing)
            StopPlay();
    }

    void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}

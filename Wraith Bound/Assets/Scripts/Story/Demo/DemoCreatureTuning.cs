using System.Reflection;
using UnityEngine;

/// <summary>
/// 시연 씬 괴물 이동 속도 (2026-11-19 사용자 결정: '너무 느리다 — 플레이어 달리기 속도와 같게, 스태미나가 떨어지면 잡히게, 탈춤귀신은 살짝 더 빠르게').
/// 추격 속도 = 플레이어 달리기 속도(PlayerController.runSpeed) × speedFactor, 탈춤귀신은 × maskGhostFactor. 순찰은 팀원 AI가 그 절반으로 씀.
/// 속도는 괴물 데이터 에셋(Monsters.moveSpeed — 모든 씬 공용)에서 오므로 **에셋은 그대로 두고 이 씬의 괴물마다 복제본을 끼워 넣음**(EnemyBase 수정 없음, 리플렉션).
/// 또 괴물 Animator를 화면 밖에서도 갱신(AlwaysAnimate)으로 — 아래 Apply 주석 참고.
/// 반드시 괴물 Start 전에 — 연출 괴물은 비활성으로 숨겨 둬서 이 컴포넌트 Awake가 먼저, 배회 괴물은 DemoRoamingSpawner가 켜기 전에 Apply.
/// </summary>
[DefaultExecutionOrder(-100)]
public class DemoCreatureTuning : MonoBehaviour
{
    [SerializeField] private float speedFactor = 1f;
    [SerializeField] private float maskGhostFactor = 1.1f;

    private static readonly FieldInfo DataField = typeof(EnemyBase).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic);
    private static DemoCreatureTuning instance;

    private void Awake()
    {
        instance = this;
        foreach (EnemyBase e in FindObjectsByType<EnemyBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Apply(e);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>이 씬 규칙대로 속도를 맞춤(시연 씬이 아니면 아무것도 안 함).</summary>
    public static void Apply(EnemyBase enemy)
    {
        if (instance == null || enemy == null || DataField == null)
        {
            return;
        }

        var data = DataField.GetValue(enemy) as Monsters;
        if (data == null)
        {
            return;
        }

        // 화면 밖에서도 애니메이션 갱신 — 기본(화면 밖이면 멈춤)이면 그동안 몸 보정(문 접기·발 붙이기)이 골반을 매 프레임 덧밀어
        // 몸이 바닥 아래/위 몇 m로 쌓여 가고, 그러면 렌더러가 '화면 밖'으로 판정돼 영영 안 보였음(배회 괴물이 투명해 보이던 원인, 2026-11-19)
        foreach (Animator an in enemy.GetComponentsInChildren<Animator>(true))
        {
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        float run = PlayerRunSpeed();
        if (run <= 0f)
        {
            return;
        }

        bool mask = enemy.GetComponentInChildren<CreatureTalDance>(true) != null;
        float speed = run * instance.speedFactor * (mask ? instance.maskGhostFactor : 1f);
        if (!data.name.EndsWith("(Demo)"))
        {
            data = Instantiate(data);
            data.name = data.name.Replace("(Clone)", string.Empty) + " (Demo)";
            DataField.SetValue(enemy, data);
        }

        data.moveSpeed = speed;
    }

    private static float PlayerRunSpeed()
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        return pc != null ? pc.runSpeed : 0f;
    }
}

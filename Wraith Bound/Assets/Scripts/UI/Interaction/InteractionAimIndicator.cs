using UnityEngine;

/// <summary>
/// 조준 중인 상호작용 대상을 모아 크로스헤어에 원 + 행동 문구로 표시 (플레이어에 붙음, PlayerInteractor가 자동 추가).
///  1) PlayerInteractor가 잡은 IInteractable (아이템·야광봉·스위치·봉인된 문 등)
///  2) 그 외 각자 E키를 처리하는 오브젝트 (문·잠긴 문·숨는 곳·옷장 문·레버) — InteractionAimResolver
/// 조작이 꺼져 있으면(컷씬·튜토리얼 팁 중 PlayerInteractor 비활성) 표시하지 않음.
/// </summary>
[DefaultExecutionOrder(50)]
public class InteractionAimIndicator : MonoBehaviour
{
    private PlayerInteractor interactor;
    private InteractionCrosshairUI crosshair;

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        crosshair = GetComponent<InteractionCrosshairUI>();
    }

    private void LateUpdate()
    {
        if (crosshair == null)
        {
            crosshair = GetComponent<InteractionCrosshairUI>();
            if (crosshair == null)
            {
                return;
            }
        }

        crosshair.SetPrompt(ResolvePrompt());
    }

    private InteractionPrompt ResolvePrompt()
    {
        if (interactor == null || !interactor.isActiveAndEnabled)
        {
            return InteractionPrompt.None;
        }

        // 문·옷장 문은 팀원 작업(2026-11-09)으로 IInteractable이 됐지만 프롬프트가 "Open [E]"뿐이라
        // 잠김·열기/닫기를 아는 기존 판정기로 문구를 만듦
        if (interactor.CurrentTarget is DoorClick || interactor.CurrentTarget is WardrobeDoorInteract)
        {
            InteractionPrompt door = InteractionAimResolver.Resolve(interactor.ViewCamera, gameObject);
            if (door.IsValid)
            {
                return door;
            }
        }

        if (interactor.CurrentTarget != null)
        {
            string label = interactor.CurrentLabel;
            if (string.IsNullOrEmpty(label))
            {
                return InteractionPrompt.None; // 이미 쓴 스위치 등 — 할 수 있는 행동 없음
            }

            return label.Contains("[")
                ? InteractionPrompt.FromLegacyPrompt(label)
                : InteractionPrompt.E(label + " 줍기"); // 프롬프트 없이 아이템 이름만 온 경우
        }

        return InteractionAimResolver.Resolve(interactor.ViewCamera, gameObject);
    }
}

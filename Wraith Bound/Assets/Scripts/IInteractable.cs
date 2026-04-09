public interface IInteractable
{
    // 이 기능을 가진 물체는 반드시 '플레이어 정보를 받는 Interact 함수'를 가져야 함
    void Interact(PlayerController player);
}
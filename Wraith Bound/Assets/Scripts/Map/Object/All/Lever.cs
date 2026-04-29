using UnityEngine;

public class Lever : MonoBehaviour
{
    [SerializeField] private LockDoor targetDoor;

    private bool isActivated = false;

    private void OnTriggerStay(Collider other)
    {
        // 플레이어가 레버 근처에서 E키를 누르면 (수정 필요)
        if (!isActivated && Input.GetKeyDown(KeyCode.E))
        {
            ActivateLever();
        }
    }

    private void ActivateLever()
    {
        isActivated = true;
        Debug.Log(gameObject.name + " 작동됨!");

        // 연결된 특정 문만 잠금 해제!
        if (targetDoor != null)
        {
            targetDoor.UnlockDoor();
        }
    }
}
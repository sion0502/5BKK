using UnityEngine;

public class LockDoor : MonoBehaviour
{
    public bool isLocked = true;

    public void UnlockDoor()
    {
        isLocked = false;
        // 잠금이 풀리면 문 색깔을 초록색으로 변경 (테스트용)
        GetComponent<Renderer>().material.color = Color.green;
        Debug.Log(gameObject.name + " 잠금 해제됨!");
    }
}
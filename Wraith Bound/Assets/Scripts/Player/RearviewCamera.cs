using UnityEngine;

public class RearviewCamera : MonoBehaviour
{
    void LateUpdate()
    {
        // 스페이스바를 누르고 있는 동안 적용
        if (Input.GetKey(KeyCode.Space))
        {
            // 1인칭 스크립트가 계산해 놓은 현재 카메라 각도에서 Y축만 180도 더해줌
            Vector3 currentRotation = transform.eulerAngles;
            transform.eulerAngles = new Vector3(currentRotation.x, currentRotation.y + 180f, currentRotation.z);
        }
    }
}

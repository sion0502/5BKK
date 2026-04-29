using UnityEngine;

public class RearviewCamera : MonoBehaviour
{
    void LateUpdate()
    {
        // GetKeyDown(누르는 순간)이 아니라 GetKey(누르고 있는 동안 매 프레임)를 사용합니다.
        if (Input.GetKey(KeyCode.Space))
        {
            // 1인칭 스크립트가 계산해 놓은 현재 카메라 각도에서 Y축만 180도 더해줍니다.
            Vector3 currentRotation = transform.eulerAngles;
            transform.eulerAngles = new Vector3(currentRotation.x, currentRotation.y + 180f, currentRotation.z);
        }
    }
}

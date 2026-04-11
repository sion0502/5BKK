using UnityEngine;

public class CompassLogic : MonoBehaviour
{
    private Transform exitTarget;
    public Transform needle; // 나침반 바늘 (인스펙터에서 할당)
    public float rotationSpeed = 8f; // 바늘이 돌아가는 속도

    void OnEnable() // 활성화될 때마다 타겟 확인
    {
        if (exitTarget == null)
        {
            GameObject exitObj = GameObject.FindGameObjectWithTag("Exit");
            if (exitObj != null) exitTarget = exitObj.transform;
        }
    }

    void Update()
    {
        if (exitTarget == null || needle == null) return;

        // 1. 출구의 위치를 '나침반(this)' 기준의 상대적 좌표로 변환합니다. (이게 핵심!)
        Vector3 localTargetPos = transform.InverseTransformPoint(exitTarget.position);

        // 2. 나침반 판 위에서의 방향만 필요하므로 높이(y) 차이를 무시합니다.
        localTargetPos.y = 0;

        if (localTargetPos != Vector3.zero)
        {
            // 3. 로컬 좌표계 기준으로 바늘이 가야 할 방향(회전값)을 계산합니다.

            Quaternion targetLocalRot = Quaternion.LookRotation(localTargetPos);

            // 4. localRotation을 사용하여 부모가 기울어져도 바늘은 판 위에서만 돕니다.
            needle.localRotation = Quaternion.Slerp(needle.localRotation, targetLocalRot, Time.deltaTime * rotationSpeed);
        }
    }
}
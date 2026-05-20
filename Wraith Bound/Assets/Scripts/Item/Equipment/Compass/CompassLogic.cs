using UnityEngine;

public class CompassLogic : MonoBehaviour
{
    private Transform exitTarget;
    public Transform needle; // 나침반 바늘 (인스펙터에서 할당)
    public float rotationSpeed = 8f; // 바늘이 돌아가는 속도
    [SerializeField] private Transform directionReference;
    [SerializeField] private float needleYawOffset = 180f;

    private Quaternion initialNeedleLocalRotation;

    void Awake()
    {
        if (needle != null)
        {
            initialNeedleLocalRotation = needle.localRotation;
        }
    }

    void OnEnable() // 활성화될 때마다 타겟 확인
    {
        if (exitTarget == null)
        {
            GameObject exitObj = GameObject.FindGameObjectWithTag("Exit");
            if (exitObj != null) exitTarget = exitObj.transform;
        }

        if (directionReference == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                directionReference = playerObj.transform;
            }
            else if (Camera.main != null)
            {
                directionReference = Camera.main.transform;
            }
        }
    }

    void Update()
    {
        if (exitTarget == null || needle == null || directionReference == null) return;

        Vector3 toExit = exitTarget.position - directionReference.position;
        toExit.y = 0f;

        Vector3 forward = directionReference.forward;
        forward.y = 0f;

        if (toExit.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float signedAngle = Vector3.SignedAngle(forward.normalized, toExit.normalized, Vector3.up);
        Quaternion targetLocalRot = initialNeedleLocalRotation * Quaternion.Euler(0f, signedAngle + needleYawOffset, 0f);
        needle.localRotation = Quaternion.Slerp(needle.localRotation, targetLocalRot, Time.deltaTime * rotationSpeed);
    }
}
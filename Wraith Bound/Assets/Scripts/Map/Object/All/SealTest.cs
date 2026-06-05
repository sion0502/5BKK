using System.Collections;
using UnityEngine;

public class SealTest : MonoBehaviour
{
    [Header("Seals")]
    public SealOrb[] seals;

    [Header("Doors")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Door Settings")]
    public float doorOpenAngle = 90f;
    public float doorOpenSpeed = 2f;

    private bool opened = false;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.G) && !opened)
        {
            opened = true;

            // 봉인핵 전부 파괴
            foreach (var seal in seals)
            {
                if (seal != null)
                    seal.BreakSeal();
            }

            // 문 열기
            StartCoroutine(OpenDoors());
        }
    }

    private IEnumerator OpenDoors()
    {
        Quaternion leftStartRot = leftDoor.rotation;
        Quaternion rightStartRot = rightDoor.rotation;

        Quaternion leftTargetRot =
            leftStartRot * Quaternion.Euler(0f, doorOpenAngle, 0f);

        Quaternion rightTargetRot =
            rightStartRot * Quaternion.Euler(0f, -doorOpenAngle, 0f);

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * doorOpenSpeed;

            leftDoor.rotation =
                Quaternion.Slerp(leftStartRot, leftTargetRot, t);

            rightDoor.rotation =
                Quaternion.Slerp(rightStartRot, rightTargetRot, t);

            yield return null;
        }
    }
}
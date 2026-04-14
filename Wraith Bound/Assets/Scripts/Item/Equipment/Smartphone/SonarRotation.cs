using UnityEngine;

public class SonarRotation : MonoBehaviour
{
    void Update()
    {
        transform.Rotate(0, 0, -150f * Time.deltaTime); // -값이 시계방향
    }
}
using UnityEngine;

public class WardrobeDoorInteract : MonoBehaviour
{
    public float openAngle = 90f;
    public float interactDistance = 5f;

    private bool isOpen = false;

    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            Ray ray = cam.ScreenPointToRay(
                new Vector3(Screen.width / 2f, Screen.height / 2f)
            );

            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, interactDistance))
            {
                Debug.Log(hit.transform.name);

                // 문 이름 포함하면 열기
                if (hit.transform.name.Contains("Door"))
                {
                    isOpen = !isOpen;

                    if (isOpen)
                    {
                        transform.Rotate(0, openAngle, 0);
                    }
                    else
                    {
                        transform.Rotate(0, -openAngle, 0);
                    }
                }
            }
        }
    }
}
using UnityEngine;

[RequireComponent(typeof(Outline))]
public class SpotOutline : MonoBehaviour
{
    public float maxDistance = 3f;

    private Outline outline;
    private Camera mainCam;

    void Start()
    {
        outline = GetComponent<Outline>();
        outline.enabled = false;

        mainCam = Camera.main;
    }

    void Update()
    {
        Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);

        bool shouldShow = false;

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
        {
            if (hit.collider.transform == transform)
            {
                shouldShow = true;
            }
        }

        outline.enabled = shouldShow;
    }
}
using UnityEngine;

public class DoorClick : MonoBehaviour
{
    private bool open;
    private bool isMousePressed;

    [Header("Door Settings")]
    public float smooth = 2.0f;
    public float DoorOpenAngle = 87.0f;

    private Quaternion defaultRot;
    private Quaternion openRot;

    private Vector3 defaultLocalPos;
    private Vector3 targetLocalSlidePos;

    [Header("Door Type")]
    public bool isSlidingDoor = false;
    public Vector3 slideOffset = new Vector3(1, 0, 0);

    [Header("Interaction Settings")]
    public float interactDistance = 3f;

    [Header("GUI Settings")]
    public string openMessage = "Open Mouse Left";
    public string closeMessage = "Close Mouse Left";

    public Font messageFont;
    public int fontSize = 24;
    public Color fontColor = Color.white;
    public Vector2 messagePosition = new Vector2(0.5f, 0.5f);

    private string doorMessage = "";

    [Header("Audio Settings")]
    public AudioClip openSound;
    public AudioClip closeSound;

    private AudioSource audioSource;

    private Camera cam;

    private void Start()
    {
        defaultRot = transform.rotation;

        openRot = Quaternion.Euler(
            transform.eulerAngles.x,
            transform.eulerAngles.y + DoorOpenAngle,
            transform.eulerAngles.z
        );

        defaultLocalPos = transform.localPosition;
        targetLocalSlidePos = defaultLocalPos + slideOffset;

        audioSource = gameObject.AddComponent<AudioSource>();

        cam = Camera.main;
    }

    private void Update()
    {
        // Door movement
        if (isSlidingDoor)
        {
            Vector3 targetPos = open ? targetLocalSlidePos : defaultLocalPos;

            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                targetPos,
                Time.deltaTime * smooth
            );
        }
        else
        {
            Quaternion targetRot = open ? openRot : defaultRot;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * smooth
            );
        }

        // Mouse Left Click
        if (Input.GetMouseButtonDown(0) && !isMousePressed)
        {
            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);

                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, interactDistance))
                {
                    if (hit.transform == transform)
                    {
                        open = !open;
                        isMousePressed = true;

                        PlayDoorSound();
                    }
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            isMousePressed = false;
        }

        // UI message
        UpdateDoorMessage();
    }

    private void UpdateDoorMessage()
    {
        if (cam == null)
        {
            doorMessage = "";
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            if (hit.transform == transform)
            {
                doorMessage = open ? closeMessage : openMessage;
                return;
            }
        }

        doorMessage = "";
    }

    private void OnGUI()
    {
        if (!string.IsNullOrEmpty(doorMessage))
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                normal = { textColor = fontColor }
            };

            if (messageFont != null)
            {
                style.font = messageFont;
            }

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            Vector2 labelSize = style.CalcSize(
                new GUIContent(doorMessage)
            );

            float labelX =
                screenWidth * messagePosition.x - labelSize.x / 2;

            float labelY =
                screenHeight * messagePosition.y - labelSize.y / 2;

            GUI.Label(
                new Rect(labelX, labelY, labelSize.x, labelSize.y),
                doorMessage,
                style
            );
        }
    }

    private void PlayDoorSound()
    {
        if (audioSource != null)
        {
            if (open && openSound != null)
            {
                audioSource.clip = openSound;
                audioSource.Play();
            }
            else if (!open && closeSound != null)
            {
                audioSource.clip = closeSound;
                audioSource.Play();
            }
        }
    }
}
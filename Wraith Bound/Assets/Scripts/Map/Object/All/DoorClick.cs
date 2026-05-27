using UnityEngine;

public class DoorClick : MonoBehaviour
{
    private bool open;

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

    [Header("Swing Door Settings")]
    public bool autoDirection = true;

    [Header("Interaction Settings")]
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("GUI Settings")]
    public string openMessage = "Open [E]";
    public string closeMessage = "Close [E]";

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

        // E Key Interaction
        if (Input.GetKeyDown(interactKey))
        {
            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);

                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, interactDistance))
                {
                    if (hit.transform == transform)
                    {
                        if (!open)
                        {
                            SetDoorDirection();
                        }

                        open = !open;

                        PlayDoorSound();
                    }
                }
            }
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

    private void SetDoorDirection()
    {
        if (!autoDirection)
            return;

        Vector3 playerDir =
            cam.transform.position - transform.position;

        float dot = Vector3.Dot(
            transform.right,
            playerDir
        );

        float angle =
            dot > 0
            ? DoorOpenAngle
            : -DoorOpenAngle;

        openRot = Quaternion.Euler(
            transform.eulerAngles.x,
            transform.eulerAngles.y + angle,
            transform.eulerAngles.z
        );
    }
}
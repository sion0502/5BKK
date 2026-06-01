using UnityEngine;
using UnityEngine.AI;

public class DoorClick : MonoBehaviour
{
    private bool open;
    private bool isMousePressed;
    private bool broken;

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

    [Header("Door Block Settings")]
    [SerializeField] private Collider blockCollider;
    [SerializeField] private Collider interactCollider;
    [SerializeField] private NavMeshObstacle navObstacle;

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

    public bool IsOpen()
    {
        return open;
    }

    public bool IsClosed()
    {
        return !open && !broken;
    }

    public bool IsBroken()
    {
        return broken;
    }

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

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        cam = Camera.main;

        if (blockCollider == null)
            blockCollider = GetComponent<Collider>();

        if (navObstacle == null)
            navObstacle = GetComponent<NavMeshObstacle>();

        if (interactCollider != null)
            interactCollider.isTrigger = true;

        ApplyDoorBlockState();
    }

    private void Update()
    {
        if (broken)
        {
            doorMessage = "";
            return;
        }

        MoveDoor();

        if (Input.GetMouseButtonDown(0) && !isMousePressed)
            TryClickDoor();

        if (Input.GetMouseButtonUp(0))
            isMousePressed = false;

        UpdateDoorMessage();
    }

    private void MoveDoor()
    {
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
    }

    private void TryClickDoor()
    {
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, ~0, QueryTriggerInteraction.Collide))
            return;

        DoorClick door = hit.collider.GetComponentInParent<DoorClick>();

        if (door != this)
            return;

        if (!open)
            SetDoorDirection();

        open = !open;
        isMousePressed = true;

        ApplyDoorBlockState();
        PlayDoorSound();
    }

    private void UpdateDoorMessage()
    {
        if (cam == null || broken)
        {
            doorMessage = "";
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, ~0, QueryTriggerInteraction.Collide))
        {
            DoorClick door = hit.collider.GetComponentInParent<DoorClick>();

            if (door == this)
            {
                doorMessage = open ? closeMessage : openMessage;
                return;
            }
        }

        doorMessage = "";
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(doorMessage))
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            normal = { textColor = fontColor }
        };

        if (messageFont != null)
            style.font = messageFont;

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        Vector2 labelSize = style.CalcSize(new GUIContent(doorMessage));

        float labelX = screenWidth * messagePosition.x - labelSize.x / 2;
        float labelY = screenHeight * messagePosition.y - labelSize.y / 2;

        GUI.Label(
            new Rect(labelX, labelY, labelSize.x, labelSize.y),
            doorMessage,
            style
        );
    }

    private void PlayDoorSound()
    {
        if (audioSource == null)
            return;

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

    private void SetDoorDirection()
    {
        if (!autoDirection || cam == null)
            return;

        Vector3 playerDir = cam.transform.position - transform.position;

        float dot = Vector3.Dot(
            transform.right,
            playerDir
        );

        float angle = dot > 0 ? DoorOpenAngle : -DoorOpenAngle;

        openRot = Quaternion.Euler(
            transform.eulerAngles.x,
            transform.eulerAngles.y + angle,
            transform.eulerAngles.z
        );
    }

    private void ApplyDoorBlockState()
    {
        if (broken)
        {
            if (blockCollider != null)
                blockCollider.enabled = false;

            if (navObstacle != null)
                navObstacle.enabled = false;

            if (interactCollider != null)
                interactCollider.enabled = false;

            return;
        }

        bool shouldBlock = !open;

        if (blockCollider != null)
            blockCollider.enabled = shouldBlock;

        if (navObstacle != null)
            navObstacle.enabled = shouldBlock;

        if (interactCollider != null)
            interactCollider.enabled = true;
    }

    public void SetBroken()
    {
        broken = true;
        open = true;
        ApplyDoorBlockState();
    }

    public void ForceOpen()
    {
        if (broken)
            return;

        open = true;
        ApplyDoorBlockState();
    }

    public void ForceClose()
    {
        if (broken)
            return;

        open = false;
        ApplyDoorBlockState();
    }
}

using UnityEngine;

public class DoorClick : MonoBehaviour
{
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

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
    [SerializeField] private Camera viewCamera;
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
    [Tooltip("재생마다 랜덤 피치 범위 (낮을수록 굵고, 높을수록 가늘게 들림)")]
    public float pitchMin = 0.88f;
    public float pitchMax = 1.12f;

    private AudioSource audioSource;

    public bool IsOpen()
    {
        return open;
    }

    public bool IsBroken()
    {
        DoorBrokenTest brokenDoor = GetComponent<DoorBrokenTest>();
        return brokenDoor != null && brokenDoor.IsBroken();
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

        audioSource = gameObject.AddComponent<AudioSource>();

        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }

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

        if (WasInteractPressed() && IsAimedAtDoor())
        {
            if (!open)
            {
                SetDoorDirection();
            }

            open = !open;
            PlayDoorSound();
        }

        UpdateDoorMessage();
    }

    private bool WasInteractPressed()
    {
        return Input.GetButtonDown("Interact") || Input.GetKeyDown(interactKey);
    }

    private bool IsAimedAtDoor()
    {
        if (viewCamera == null)
        {
            return false;
        }

        Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
        int hitCount = Physics.RaycastNonAlloc(ray, HitBuffer, interactDistance);

        for (int i = 0; i < hitCount; i++)
        {
            if (IsDoorCollider(HitBuffer[i].collider))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsDoorCollider(Collider col)
    {
        if (col == null)
        {
            return false;
        }

        Transform hitTransform = col.transform;
        if (hitTransform == transform)
        {
            return true;
        }

        return hitTransform.IsChildOf(transform) || transform.IsChildOf(hitTransform);
    }

    private void UpdateDoorMessage()
    {
        if (IsAimedAtDoor())
        {
            doorMessage = open ? closeMessage : openMessage;
            return;
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
        if (audioSource == null)
        {
            return;
        }

        AudioClip clip = open ? openSound : closeSound;
        if (clip == null)
        {
            return;
        }

        float minPitch = Mathf.Min(pitchMin, pitchMax);
        float maxPitch = Mathf.Max(pitchMin, pitchMax);
        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.clip = clip;
        audioSource.Play();
    }

    private void SetDoorDirection()
    {
        if (!autoDirection || viewCamera == null)
        {
            return;
        }

        Vector3 playerDir =
            viewCamera.transform.position - transform.position;

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

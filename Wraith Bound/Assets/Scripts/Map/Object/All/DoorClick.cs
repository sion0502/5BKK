using UnityEngine;
using UnityEngine.AI;

public class DoorClick : MonoBehaviour, IInteractable
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

    [Header("Audio Settings")]
    public AudioClip openSound;
    public AudioClip closeSound;
    [Tooltip("재생마다 랜덤 피치 범위 (낮을수록 굵고, 높을수록 가늘게 들림)")]
    public float pitchMin = 0.88f;
    public float pitchMax = 1.12f;

    private AudioSource audioSource;
    private bool lastNavBlocked = true;

    public bool IsOpen()
    {
        return open;
    }

    public bool IsBroken()
    {
        DoorBrokenTest brokenDoor = GetComponent<DoorBrokenTest>();
        return brokenDoor != null && brokenDoor.IsBroken();
    }

    public bool IsPassageReady()
    {
        if (IsBroken()) return true;
        if (!open) return false;
        return isSlidingDoor
            ? Vector3.Distance(transform.localPosition, targetLocalSlidePos) <= 0.1f
            : Quaternion.Angle(transform.rotation, openRot) <= 10f;
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
            viewCamera = Camera.main;

        DoorNavMeshUtility.EnsureCarveObstacle(transform);
        SyncNavMeshObstacle(true);
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


        SyncNavMeshObstacle(false);
    }

    private void SyncNavMeshObstacle(bool force)
    {
        if (GetComponent<DoorNavMesh>() != null)
            return;

        bool blocked = !IsPassageReady();
        if (!force && blocked == lastNavBlocked)
            return;

        lastNavBlocked = blocked;
        DoorNavMeshUtility.SetNavMeshBlocked(transform, blocked);
    }

    public void Interact(GameObject interactor)
    {
        if (!isActiveAndEnabled) return;
        if (!PlayerInteractor.CanInteractWith(interactor, this, interactDistance)) return;
        LockedDoor lockedDoor = GetComponent<LockedDoor>();
        if (lockedDoor != null && lockedDoor.IsLocked) return;
        Camera camera = interactor.GetComponentInChildren<Camera>();
        if (camera != null) viewCamera = camera;
        if (!open) SetDoorDirection();
        open = !open;
        PlayDoorSound();
        SyncNavMeshObstacle(true);
    }

    public string GetInteractPrompt()
    {
        return open ? closeMessage : openMessage;
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

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 컷씬 → 플레이 전환. 씬 시작부터 플레이어를 startPoint에 세워 두되 조작·카메라를 꺼 두고,
/// 컷씬이 끝나면(CutsceneDirector.onFinished → Handoff) 컷씬 카메라와 같은 자리·시선에서 플레이어 카메라로 바꿔 조작을 켭니다.
/// ESC 건너뛰기로 끝나도 같은 결과가 되도록 finalState 대상(문 닫힘 등)의 Blend를 강제로 맞춤.
/// 플레이어 스크립트는 수정하지 않고 enabled만 끄고 켬 (MouseLook의 상하 각도만 리플렉션으로 맞춤).
/// </summary>
public class CutscenePlayerHandoff : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject player;
    [SerializeField] private Camera playerCamera;
    [Tooltip("플레이 시작 위치·시선 (발 위치, Y회전=좌우, X회전=상하)")]
    [SerializeField] private Transform startPoint;

    [Header("Cutscene")]
    [SerializeField] private Camera cutsceneCamera;
    [Tooltip("전환 직후 강제로 맞출 Blend 값 (문 닫힘=1, 캠코더 화면=0 등)")]
    [SerializeField] private List<CutsceneBlendTarget> finalStateTargets = new List<CutsceneBlendTarget>();
    [SerializeField] private List<float> finalStateValues = new List<float>();

    [Header("Activate on handoff (튜토리얼·HUD·일시정지 메뉴 등)")]
    [SerializeField] private List<GameObject> activateOnHandoff = new List<GameObject>();

    [Header("Fade")]
    [SerializeField] private CutsceneBlackScreen blackScreen;
    [SerializeField] private float fadeInDuration = 0.6f;

    // 컷씬 동안 끌 플레이어 컴포넌트 (타입 이름으로 찾아 의존성 최소화)
    private static readonly string[] InputComponentNames =
    {
        "MouseLook", "PlayerController", "PlayerInteractor", "InventoryManager", "SelectedItemUseController",
        "PlayerHidingController", "SmartPhoneHolderToggle",
    };

    private readonly List<Behaviour> disabled = new List<Behaviour>();
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private bool handedOff;

    private void Awake()
    {
        if (player == null)
        {
            return;
        }

        PlacePlayer();

        foreach (Behaviour behaviour in player.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour.enabled && System.Array.IndexOf(InputComponentNames, behaviour.GetType().Name) >= 0)
            {
                behaviour.enabled = false;
                disabled.Add(behaviour);
            }
        }

        // 플레이어는 컷씬 카메라가 걸어오는 끝 지점에 서 있으므로 몸체가 화면에 보이지 않게 숨김
        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.enabled)
            {
                renderer.enabled = false;
                hiddenRenderers.Add(renderer);
            }
        }

        SetPlayerCameraActive(false);
        foreach (GameObject go in activateOnHandoff)
        {
            if (go != null)
            {
                go.SetActive(false);
            }
        }
    }

    public void Handoff()
    {
        if (handedOff)
        {
            return;
        }

        handedOff = true;

        for (int i = 0; i < finalStateTargets.Count; i++)
        {
            if (finalStateTargets[i] != null)
            {
                finalStateTargets[i].Blend = i < finalStateValues.Count ? finalStateValues[i] : 0f;
            }
        }

        PlacePlayer();
        if (cutsceneCamera != null)
        {
            cutsceneCamera.gameObject.SetActive(false);
        }

        SetPlayerCameraActive(true);
        foreach (Renderer renderer in hiddenRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        foreach (Behaviour behaviour in disabled)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        foreach (GameObject go in activateOnHandoff)
        {
            if (go != null)
            {
                go.SetActive(true);
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (blackScreen != null && blackScreen.Alpha > 0f)
        {
            StartCoroutine(blackScreen.FadeTo(0f, fadeInDuration));
        }
    }

    private void PlacePlayer()
    {
        if (startPoint == null)
        {
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null)
        {
            controller.enabled = false;
        }

        float yaw = startPoint.eulerAngles.y;
        float pitch = Mathf.DeltaAngle(0f, startPoint.eulerAngles.x);
        player.transform.SetPositionAndRotation(startPoint.position, Quaternion.Euler(0f, yaw, 0f));

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            MonoBehaviour look = playerCamera.GetComponent("MouseLook") as MonoBehaviour;
            if (look != null)
            {
                var field = look.GetType().GetField("xRotation",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                field?.SetValue(look, pitch);
            }
        }

        if (controller != null)
        {
            controller.enabled = wasEnabled;
        }
    }

    private void SetPlayerCameraActive(bool active)
    {
        if (playerCamera == null)
        {
            return;
        }

        playerCamera.enabled = active;
        AudioListener listener = playerCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = active;
        }
    }
}

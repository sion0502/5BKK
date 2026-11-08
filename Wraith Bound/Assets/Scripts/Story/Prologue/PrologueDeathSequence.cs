using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 프롤로그 고정 사망. 일반 사망 흐름(PlayerDeathDebug·EnemyJumpscare → 게임오버 화면)을 쓰지 않고 바로 허브로.
///  deadEnd=true(비상구 문 앞): 조작 잠김 → 문 덜컹 + "안 돼, 열려…!" → 뒤돌아봄 → 크리처가 덮침
///  deadEnd=false(도중에 잡힘): 바로 크리처 쪽으로 시선이 돌아가며 덮침
/// → 화면 튐·암전 → hubSceneName 로드.
/// </summary>
public class PrologueDeathSequence : MonoBehaviour
{
    [SerializeField] private string hubSceneName = "Hub";
    [SerializeField] private string deadEndLine = "안 돼, 열려… 열리라고…!";
    [SerializeField] private float rattleDuration = 1.5f;
    [SerializeField] private float turnDuration = 0.45f;
    [SerializeField] private float lungeDistance = 1.05f;

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rattleClip;
    [SerializeField] private AudioClip jumpscareClip;

    public bool IsRunning { get; private set; }

    public void Begin(PrologueNurse nurse, bool deadEnd, Vector3 deadEndDoorPoint)
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        StartCoroutine(Run(nurse, deadEnd, deadEndDoorPoint));
    }

    private IEnumerator Run(PrologueNurse nurse, bool deadEnd, Vector3 doorPoint)
    {
        GameObject player = GameObject.FindWithTag("Player");
        Camera cam = player.GetComponentInChildren<Camera>();
        PlayerControlLock.Lock(player); // 씬을 떠나므로 복구 안 함

        PlayerChaseEffectController chase = player.GetComponent<PlayerChaseEffectController>();

        if (deadEnd)
        {
            // 문 쪽을 보며 손잡이를 잡아당김 — 크리처는 뒤에서 천천히 다가옴
            nurse.SetChaseSpeed(1.6f);
            yield return TurnTo(player, cam, doorPoint, 0.25f);
            SubtitleUI.ShowForDuration("나", deadEndLine, rattleDuration + 0.6f);
            if (audioSource != null && rattleClip != null)
            {
                audioSource.PlayOneShot(rattleClip);
            }

            float t = 0f;
            Quaternion baseRot = cam.transform.localRotation;
            while (t < rattleDuration)
            {
                t += Time.deltaTime;
                float shake = Mathf.Sin(t * 38f) * 0.8f * (1f - t / rattleDuration * 0.5f);
                cam.transform.localRotation = baseRot * Quaternion.Euler(shake * 0.4f, shake, 0f);
                yield return null;
            }

            cam.transform.localRotation = baseRot;
        }

        // 바로 등 뒤에 붙여 덮치게
        Vector3 toPlayer = player.transform.position - nurse.transform.position;
        toPlayer.y = 0f;
        Vector3 behind = player.transform.position - (deadEnd ? (doorPoint - player.transform.position).normalized : toPlayer.normalized) * lungeDistance;
        behind.y = player.transform.position.y;
        nurse.Appear(behind, Quaternion.identity);
        nurse.FaceTowards(player.transform.position);

        SubtitleUI.Clear();
        yield return TurnTo(player, cam, nurse.EyePoint.position, turnDuration);
        nurse.PlayAttack();
        if (chase != null)
        {
            chase.PlayDetectionBurst();
        }

        if (audioSource != null && jumpscareClip != null)
        {
            audioSource.PlayOneShot(jumpscareClip);
        }

        // 덮치는 순간: 시야 확 좁힘 + 흔들림
        float startFov = cam.fieldOfView;
        for (float t = 0f; t < 0.55f; t += Time.deltaTime)
        {
            float k = t / 0.55f;
            cam.fieldOfView = Mathf.Lerp(startFov, startFov - 18f, k * k);
            cam.transform.LookAt(nurse.EyePoint.position + Random.insideUnitSphere * 0.04f * k);
            yield return null;
        }

        CutsceneBlackScreen.CreateRuntime(5100, 1f); // 순간 암전
        yield return new WaitForSecondsRealtime(1.4f);
        TraitProgress.Add(TraitCatalog.Deaths); // 프롤로그 사망도 누적 사망 수에 (기 해금)
        HubArrival.FromPrologue = true; // 허브에서 바닥에 쓰러져 있다 깨어나는 연출
        SceneManager.LoadScene(hubSceneName);
    }

    private static IEnumerator TurnTo(GameObject player, Camera cam, Vector3 point, float duration)
    {
        Vector3 dir = point - cam.transform.position;
        Quaternion look = Quaternion.LookRotation(dir);
        float targetYaw = look.eulerAngles.y;
        float targetPitch = Mathf.DeltaAngle(0f, look.eulerAngles.x);

        float startYaw = player.transform.eulerAngles.y;
        float startPitch = Mathf.DeltaAngle(0f, cam.transform.localEulerAngles.x);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            player.transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(startYaw, targetYaw, k), 0f);
            cam.transform.localRotation = Quaternion.Euler(Mathf.Lerp(startPitch, targetPitch, k), 0f, 0f);
            yield return null;
        }

        player.transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
        cam.transform.localRotation = Quaternion.Euler(targetPitch, 0f, 0f);
    }
}

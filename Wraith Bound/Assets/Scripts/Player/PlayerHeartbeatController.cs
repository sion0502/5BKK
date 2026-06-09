using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class PlayerHeartbeatController : MonoBehaviour
{
    [Header("Audio Components")]
    [SerializeField] private AudioSource heartbeatSource;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioClip heartbeatClip;

    private const string MIXER_PITCH_PARAM = "ReverbAmount";

    [Header("Stamina Settings")]
    private PlayerConditions playerConditions;

    [Header("Heartbeat Interval Mapping")]
    [SerializeField] private float minInterval = 0.35f;  // 스태미나 100일 때 속도 (느림)
    [SerializeField] private float maxInterval = 1.8f;  // 스태미나 0일 때 속도 (매우 빠름)

    [Header("Double Beat Micro-timing")]
    [SerializeField] private float doubleBeatDelay = 0.15f; // "쿵"과 다음 "쿵" 사이의 내부 지연 시간
    [Range(0.5f, 1.0f)][SerializeField] private float secondBeatVolumeScale = 0.7f; // 두 번째 박동의 감쇄 비율
    [Range(0.8f, 1.0f)][SerializeField] private float secondBeatPitchScale = 0.9f;  // 두 번째 박동의 피치 비율 (더 낮게)

    [Header("Heartbeat Volume Mapping")]
    [SerializeField] private float minVolume = 0.0f; // 스태미나 100일 때 볼륨 (안 들림)
    [SerializeField] private float maxVolume = 1.0f; // 스태미나 0일 때 볼륨 (가장 큼)

    private float timer;

    private void Start()
    {
        // 오디오 소스 초기 설정
        heartbeatSource.loop = false;
        heartbeatSource.pitch = 1.0f;
        // 스태미나 가져오기
        playerConditions = GetComponent<PlayerConditions>();
        // 타이머 초기 설정
        timer = 0f;
    }

    private void Update()
    {
        // 1. 스태미나 비율 계산 (0.0 = 탈진, 1.0 = 가득 참)
        float staminaRatio = Mathf.Clamp01(playerConditions.currentStamina / playerConditions.maxStamina);

        // 2. 스태미나가 낮을수록 역으로 높은 값(속도/볼륨)을 가지도록 뒤집음
        float urgency = 1.0f - staminaRatio;

        // 3. 스태미나가 낮아질수록 소리가 나는 간격은 줄어들고, 볼륨은 커짐
        float currentInterval = Mathf.Lerp(minInterval, maxInterval, urgency);
        float currentVolume = Mathf.Lerp(minVolume, maxVolume, urgency);

        // 스태미나가 80% 이상일 때는 심장 소리가 아예 안 나도록 컷오프 처리
        if (staminaRatio > 0.8f)
        {
            timer = 0f;
            return;
        }

        // 타이머 계산 및 발진
        timer += Time.deltaTime;
        if (timer >= currentInterval)
        {
            // 한 세트의 박동 시퀀스를 코루틴으로 구동
            StartCoroutine(PlayDoubleBeatSequence(currentVolume));
            timer = 0f;
        }
    }

  
    // 단발성 클립 하나를 이용해 시간 차를 두고 "쿵-쿵" 소리를 재현
    private IEnumerator PlayDoubleBeatSequence(float volume)
    {
        // 1. 첫 번째 박동 (강하게) 
        heartbeatSource.pitch = 1.0f; // 기준 피치
        heartbeatSource.PlayOneShot(heartbeatClip, volume);

        // 정해진 미세 지연 시간만큼 대기
        yield return new WaitForSeconds(doubleBeatDelay);

        // 2. 두 번째 박동 (약간 작고 무겁게) 
        // 일시적으로 피치와 볼륨을 조정하여 자연스러운 수축기/확장기 음색 구현
        heartbeatSource.pitch = secondBeatPitchScale;
        heartbeatSource.PlayOneShot(heartbeatClip, volume * secondBeatVolumeScale);
    }
}
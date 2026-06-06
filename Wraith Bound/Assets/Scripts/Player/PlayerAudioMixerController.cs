using UnityEngine;
using UnityEngine.Audio;

public class PlayerAudioMixerController : MonoBehaviour
{
    public enum MovementState { Crouch, Walk, Run }

    [System.Serializable]
    public struct FootstepData
    {
        public MovementState state;
        public AudioClip[] clips;       // 사운드가 질리지 않도록 랜덤 재생용 배열
        public float stepInterval;      // 발소리 재생 주기 (시간 간격)
        [Range(0f, 1f)] public float volume; // 상태별 볼륨 크기
        [Range(0.5f, 2f)] public float playbackSpeed; // 상태별 재생 배속
    }

    [Header("Audio Components")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private FootstepData[] footstepSettings;

    private const string MIXER_PITCH_PARAM = "FootstepPitch";

    [Header("Player Physics & States (References)")]
    [SerializeField] private CharacterController characterController;

    private MovementState currentState = MovementState.Walk;
    private FootstepData currentData;
    private float stepTimer;

    private void Start()
    {
        UpdateMovementState(MovementState.Walk);
    }

    private void Update()
    {
        // 1. 플레이어 상태 검사 (실제 환경에선 FSM 또는 Movement 스크립트에서 이 이벤트를 호출해 주는 것이 성능상 좋습니다)
        EvaluateMovementState();

        // 2. 캐릭터가 지면에 닿아있고 실제로 이동 중일 때만 타이머 계산
        if (characterController.isGrounded && characterController.velocity.sqrMagnitude > 0.1f)
        {
            stepTimer += Time.deltaTime;

            if (stepTimer >= currentData.stepInterval)
            {
                PlayFootstepSound();
                stepTimer = 0f;
            }
        }
        else
        {
            // 정지 상태일 때는 타이머 초기화 (움직이기 시작하면 즉시 발소리가 나도록)
            stepTimer = currentData.stepInterval;
        }
    }

   
    // 입력값이나 피지컬 속도에 기초하여 현재 이동 상태 판정
    private void EvaluateMovementState()
    {
        // 예시용 판정 로직 (실제 프로젝트의 입력/상태 변수에 맞춰 매핑)
        if (Input.GetKey(KeyCode.LeftControl)) // 웅크리기 키
        {
            UpdateMovementState(MovementState.Crouch);
        }
        else if (Input.GetKey(KeyCode.LeftShift)) // 달리기 키
        {
            UpdateMovementState(MovementState.Run);
        }
        else
        {
            UpdateMovementState(MovementState.Walk);
        }
    }


    // 상태가 변경될 때 재생 속도 및 피치 보정값 갱신
    private void UpdateMovementState(MovementState newState)
    {
        if (currentState == newState && currentData.clips != null) return;

        currentState = newState;

        // 구조체 배열에서 매칭되는 세팅값 검색
        foreach (var data in footstepSettings)
        {
            if (data.state == currentState)
            {
                currentData = data;
                break;
            }
        }

        // 1. AudioSource의 재생 속도 조절
        audioSource.pitch = currentData.playbackSpeed;

        // 2. Audio Mixer의 Pitch Shifter를 제어하여 원래의 음고를 유지
        float targetMixerPitch = 1.0f / currentData.playbackSpeed;
        audioMixer.SetFloat(MIXER_PITCH_PARAM, targetMixerPitch);
    }


    // PlayOneShot을 사용하여 발소리를 중첩 재생
    private void PlayFootstepSound()
    {
        if (currentData.clips == null || currentData.clips.Length == 0) return;

        // 오디오 클립 배열에서 무작위 사운드 선택 (반복 피로감 해소)
        int randomIndex = Random.Range(0, currentData.clips.Length);
        AudioClip clip = currentData.clips[randomIndex];

        // 자연스러운 발소리를 위해 피치와 볼륨에 미세한 랜덤 노이즈 추가 (Micro-variation)
        float volumeOffset = Random.Range(-0.05f, 0.05f);
        float speedOffset = Random.Range(-0.05f, 0.05f);

        // 일시적인 속도 왜곡 적용
        audioSource.pitch = currentData.playbackSpeed + speedOffset;
        audioMixer.SetFloat(MIXER_PITCH_PARAM, 1.0f / (currentData.playbackSpeed + speedOffset));

        // 최종 플레이원샷 호출
        float finalVolume = Mathf.Clamp01(currentData.volume + volumeOffset);
        audioSource.PlayOneShot(clip, finalVolume);
    }
}

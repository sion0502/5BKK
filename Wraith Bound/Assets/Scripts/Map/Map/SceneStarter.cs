using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SceneStarter : MonoBehaviour
{
    [Header("참조 객체")]
    public GameObject player;
    public Transform targetCamera; // 메인 카메라 할당
    public Image fadeImage;        // 검은색 UI 이미지 할당

    [Header("추락 연출 설정")]
    public float fallHeight = 20f;     // 시작 높이 (높을수록 속도감 상승)
    public float gravity = 60f;        // 중력 값 (클수록 팍! 꽂힘)
    public float startWaitTime = 1.0f; // 암전 상태 대기 시간

    public AudioClip impactSound; // 재생할 사운드 파일

    void Start()
    {
        // 카메라를 할당 안 했을 경우 자동 찾기
        if (targetCamera == null) targetCamera = Camera.main.transform;
        
        StartCoroutine(StartSequence());
    }

    IEnumerator StartSequence()
    {
        fadeImage = ScreenFader.Prepare(fadeImage);

        // 1. 컴포넌트 참조 및 비활성화
        PlayerController pc = player.GetComponent<PlayerController>();
        CharacterController cc = player.GetComponent<CharacterController>();
        MeshRenderer mr = player.GetComponent<MeshRenderer>();
        
        // MouseLook 스크립트를 이름으로 찾아서 가져오기
        MonoBehaviour ml = targetCamera.GetComponent("MouseLook") as MonoBehaviour;

        // 연출 동안 모든 기능 정지
        if (pc != null) pc.enabled = false;
        if (cc != null) cc.enabled = false;
        if (mr != null) mr.enabled = false; // 큐브 숨기기
        if (ml != null) ml.enabled = false; // 마우스 조작 강제 차단

        // 초기 위치/시선 세팅
        player.transform.position += Vector3.up * fallHeight;
        fadeImage.color = Color.black;
        targetCamera.localRotation = Quaternion.Euler(85f, 0, 0); // 수직으로 바닥 보기

        yield return new WaitForSeconds(startWaitTime); // 툭 멈춘 정적

        // 2. 가속도 추락 로직
        float verticalVelocity = 0f;
        float currentHeight = fallHeight;
        Vector3 startPos = player.transform.position;
        Vector3 landPos = startPos - (Vector3.up * fallHeight);

        while (currentHeight > 0.1f)
        {
            // 중력 가속도 계산
            verticalVelocity += gravity * Time.deltaTime;
            float fallAmount = verticalVelocity * Time.deltaTime;
            
            // 실제 하강
            player.transform.position += Vector3.down * fallAmount;
            currentHeight -= fallAmount;

            // 시선 처리: 바닥에 가까워질수록 급격하게 정면 보기 (제곱 가속)
            float progress = Mathf.Clamp01(1f - (currentHeight / fallHeight));
            float cameraCurve = Mathf.Pow(progress, 2); 
            targetCamera.localRotation = Quaternion.Slerp(Quaternion.Euler(85f, 0, 0), Quaternion.identity, cameraCurve);

            // 페이드 효과: 추락 중간부터 서서히 밝아짐
            fadeImage.color = new Color(0, 0, 0, 1f - (progress * 1.2f));

            yield return null;
        }

        // 바닥 위치 보정
        player.transform.position = landPos;

        // 3. 강력한 착지 충격 (카메라 덜컥거림)
        yield return StartCoroutine(LandingImpactStrong());

        // 4. 모든 기능 복구
        if (mr != null) mr.enabled = true; // 큐브 보이기
        if (cc != null) cc.enabled = true;
        if (pc != null) pc.enabled = true;
        
        // 마우스 조작 복구 전 각도 정렬
        targetCamera.localRotation = Quaternion.identity;
        if (ml != null) ml.enabled = true;
        
        fadeImage.color = Color.clear;
    }

    IEnumerator LandingImpactStrong()
    {
        float t = 0;
        Vector3 originPos = targetCamera.localPosition; // 원래 서 있을 때의 카메라 높이
        Quaternion originRot = Quaternion.identity; 

        // [1단계] 철퍽! 바닥에 주저앉음 (시야를 대폭 낮춤)
        // -1.2f 정도로 확 낮추면 거의 바닥에 기어가는 높이가 됩니다.
        Vector3 crouchPos = originPos + new Vector3(0, -1.2f, 0); 
        Quaternion faceDownRot = Quaternion.Euler(50f, 5f, -10f); // 고개도 푹 숙이고 옆으로 살짝 삐딱하게

        GameObject soundObj = new GameObject("TempAudio");
        AudioSource audioSource = soundObj.AddComponent<AudioSource>();

        // 클립 할당 및 사운드 재생
        audioSource.clip = impactSound;
        audioSource.Play();

        // 재생이 끝난 후 게임 오브젝트 자동 삭제 (오디오 길이 기준)
        Destroy(soundObj, impactSound.length);

        while (t < 0.08f) // 팍! 하고 쓰러지는 속도
        {
            t += Time.deltaTime;
            float rate = t / 0.08f;
            targetCamera.localPosition = Vector3.Lerp(originPos, crouchPos, rate);
            targetCamera.localRotation = Quaternion.Slerp(originRot, faceDownRot, rate);
            yield return null;
        }

        // [2단계] 정신 혼미 (바닥에 쓰러져서 0.4초간 멍하니 있음)
        yield return new WaitForSeconds(0.4f);

        // [3단계] 영차... 하고 몸을 일으킴 (시야 복구)
        t = 0;
        float riseDuration = 1.5f; // 몸을 일으키는 시간 (길게 잡을수록 더 힘겨워 보임)
        while (t < riseDuration)
        {
            t += Time.deltaTime;
            float rate = t / riseDuration;

            // 3제곱 가속: 처음엔 아주 천천히 일어나다가 마지막에 똑바로 섬
            float heavyRate = Mathf.Pow(rate, 3); 

            targetCamera.localPosition = Vector3.Lerp(crouchPos, originPos, heavyRate);
            targetCamera.localRotation = Quaternion.Slerp(faceDownRot, originRot, heavyRate);
            yield return null;
        }

        // 최종 위치 고정
        targetCamera.localPosition = originPos;
        targetCamera.localRotation = originRot;
    }
}
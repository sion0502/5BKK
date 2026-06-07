using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using UnityEngine.InputSystem;
using NUnit.Framework;
using System;
using JetBrains.Annotations;
using UnityEngine.Audio;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed; // 걷기 속도
    public float runSpeed; // 달리기 속도
    public float staminaDrainRate; // 달리기 시 소비할 스태미나
    public float gravity = -10.0f; // 중력
    public bool isRun = false; // 달리기 상태
    public bool grounded = true; // 지면에 있는 지
    
    [Header("Falling Settings")]
    public float damagePerMeter = 3f; // 1미터 당 3 데미지
    public float minFallHeight = 20f; // 20m 미만은 데미지 없음
    public int baseFallingDamage = 20; // 기본 낙하 데미지 20
    public float minDamageVelocity = -10f; // 짧은 낙하 제거
    private float verticalVelocity; // 수직 낙하 속도
    private float minVelocityReached; // 낙하 판정 필터링
    private bool isFalling; // 낙하 중인 지

    [Header("Crouch Settings")]
    public float crouchSpeed; // 웅크리기 속도
    public float normalHeight = 1.8f;
    public float crouchHeight = 0.9f;
    private float normalCameraHeight; // 캐릭터 중심 기준 카메라 로컬 Y 위치
    private float crouchCameraHeight;
    public float crouchTransitionSpeed = 10f; // 웅크리기 전환 속도
    public bool isCrouching = false; // 웅크리기 상태

    // 숨기 관련 변수
    private float targetHeight;
    private float targetCameraHeight;

    // 지면 검사 거리
    private float groundCheckRadius = 0.35f;
    private float groundCheckDistance = 0.2f; // CharacterController는 항상 지면에 딱 붙어있지 않기 때문에 이를 보정할 변수 생성
    private float groundCheckStartOffset = 0.1f;
    private float maxGroundAngle = 45f;
    private RaycastHit groundHit;

    [Header("References")]
    private PlayerConditions conditions; // PlayerConditions 스크립트 가져오기
    private CharacterController controller; // CharacterController 컴포넌트 가져오기
    public Transform cameraTransform; // 1인칭 카메라 할당
    private AudioSource playerAudio;
    private Vector3 velocity;
    private Vector3 horizontalVelocity;
    public LayerMask ceilingCheckLayer; // 천장 충돌을 체크할 레이어
    public LayerMask groundMask; // 지면 검사를 체크할 레이어

    void Start()
    {
        conditions = GetComponent<PlayerConditions>();
        controller = GetComponent<CharacterController>();
        playerAudio = GetComponent<AudioSource>();

        normalCameraHeight = cameraTransform.localPosition.y;
        // 웅크렸을 때의 카메라 높이는 기본 눈높이에서 일정 값(예: 1.0f)을 뺀 값으로 자동 설정
        crouchCameraHeight = normalCameraHeight - 1.0f;

        targetHeight = normalHeight;
        targetCameraHeight = normalCameraHeight;
    }

    void Update()
    {
        HandleGrounded();
        HandleMovement();
        HandleGravity();

        Vector3 finalMovement = horizontalVelocity + velocity;
        controller.Move(finalMovement * Time.deltaTime);

        HandleFallingDamage();
    }

    private void HandleGrounded()
    {
        /* Vector3 origin = transform.position + controller.center;
        origin.y -= controller.height / 2 - controller.radius;
        float radius = controller.radius;

        grounded = Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, controller.height / 2 - radius + groundCheckDistance, groundMask);

        if (grounded)
        {
            controller.stepOffset = 0.3f; // 지상에서는 기본 stepOffset 값으로 적용함
        }
        else
        {
            controller.stepOffset = 0f; // 공중에서는 stepOffset을 0으로 설정하여 벽에 걸리는 것을 방지함
        } */

        Vector3 origin = transform.position + Vector3.up * groundCheckDistance;
        float groundCheckRadius = controller.radius * 0.85f;

        // SphereCast는 구체를 방향으로 쏘면서 충돌을 찾는데, '이미 접속 중인 지면'에 대한 검사에서 오류가 발생할 수 있음
        // 따라서 CheckSphere를 사용함
        grounded = Physics.CheckSphere(
            origin,
            groundCheckRadius,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        controller.stepOffset = grounded ? 0.3f : 0f; // 지상에서는 기본 stepOffset 값으로, 공중에서는 stepOffset 값을 0으로 설정하여 벽에 걸리는 것을 방지
        

        /* Vector3 origin = transform.position + Vector3.up * groundCheckStartOffset;
        float distance = groundCheckStartOffset + groundCheckDistance;
        float minGroundNormalY = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);

        grounded = Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out groundHit,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        ) && groundHit.normal.y >= minGroundNormalY;

        controller.stepOffset = grounded ? 0.3f : 0f; */

    }

    // 움직임 처리
    private void HandleMovement()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        
        Vector3 move = transform.right * x + transform.forward * z;

        // 수평 속도(X, Z 축)만 추출하여 실제 이동 속도 계산 (추락/상승 속도 제외)
        Vector3 horizontalVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
        bool isActuallyMoving = horizontalVelocity.sqrMagnitude > 0.1f;

        if (Input.GetKey(KeyCode.LeftShift) && conditions.CanSprint() && isCrouching == false && z > 0.1f && isActuallyMoving)
        {
            if (grounded)
            {
                isRun = true;
                conditions.ConsumeStamina(staminaDrainRate);
            }
        }

        else if (Input.GetKeyUp(KeyCode.LeftShift) || conditions.GetCurrentStamina() <= 0f || (isRun && z < 0f))
        {
            isRun = false;
            conditions.StartStaminaRegen();
            /*디버그 로그(Debug.Log)로 매 프레임마다 깎이는 스태미나 수치를 확인 중이시라면, 0.01씩 깎이는 것이 정상일 수 있습니다.
            원리: 초당 10을 소비한다는 것은 1초에 걸쳐 총 10이 깎인다는 의미입니다. 만약 게임이 1000프레임(FPS)으로 돌아가고 있다면, 1프레임당 10 / 1000 = 0.01씩 깎이게 됩니다.
            확인 방법: 디버그 로그가 0.01씩 찍히더라도, 스태미나 바 UI가 10초에 걸쳐 서서히 0(최대치 100 기준)으로 줄어든다면 정상적으로 작동하고 있는 것입니다. */
            // Debug.Log("현재 스태미나: " + conditions.GetCurrentStamina());
        }

        // 웅크리기 키 (예: 좌측 컨트롤 키)
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            if (grounded)
            {
                isCrouching = true;
            }
        }
        else if (Input.GetKeyUp(KeyCode.LeftControl))
        {
            // 일어서기 시도
            if (CanStandUp())
            {
                isCrouching = false;
            }
        }


        // 만약 키를 뗐는데 머리 위에 장애물이 있어서 못 일어난 상태라면,
        // 장애물이 없어졌을 때 자동으로 일어서도록 처리 (Toggle 방식이 아닌 Hold 방식일 때)
        if (isCrouching && !Input.GetKey(KeyCode.LeftControl))
        {
            if (CanStandUp())
            {
                isCrouching = false;
            }
            else
            {
                isCrouching = true; // 강제 유지
            }
        }

        if (isCrouching)
        {
            if (grounded) {
            Crouch(move);
            }
        }
        else if (isRun)
        {
            if (grounded) {
            Run(move);
            }
        }
        else
        {
            Move(move);
        }


        if (isCrouching)
        {
            // 목표 높이 설정
            targetHeight = isCrouching ? crouchHeight : normalHeight;
            targetCameraHeight = isCrouching ? crouchCameraHeight : normalCameraHeight;


            // 1. 콜라이더 높이 부드럽게 조절
            float currentHeight = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            controller.height = currentHeight;

            // 콜라이더 중심점(Center)도 높이에 맞춰 조절해야 바닥을 뚫거나 공중에 뜨지 않음
            controller.center = new Vector3(0, currentHeight / 2f, 0);

            // 2. 카메라 높이 부드럽게 조절
            Vector3 newCameraPos = cameraTransform.localPosition;
            newCameraPos.y = Mathf.Lerp(cameraTransform.localPosition.y, targetCameraHeight, Time.deltaTime * crouchTransitionSpeed);
            cameraTransform.localPosition = newCameraPos;
        }

        else
        {
            // isCrouching이 false가 되면, 목표 높이는 원래 키(normalHeight)가 됩니다.
            float targetHeight = isCrouching ? crouchHeight : normalHeight;
            float targetCameraHeight = isCrouching ? crouchCameraHeight : normalCameraHeight;

            // 콜라이더 높이를 원래대로 부드럽게 복구
            float currentHeight = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            controller.height = currentHeight;
            controller.center = new Vector3(0, currentHeight / 2f, 0);

            // 카메라 위치를 원래 눈높이로 부드럽게 복구
            Vector3 newCameraPos = cameraTransform.localPosition;
            newCameraPos.y = Mathf.Lerp(cameraTransform.localPosition.y, targetCameraHeight, Time.deltaTime * crouchTransitionSpeed);
            cameraTransform.localPosition = newCameraPos;
        }
    }

    // 걷기
    private void Move(Vector3 direction)
    {
        horizontalVelocity = direction * moveSpeed;
    }


    // 달리기
    private void Run(Vector3 direction)
    {
        horizontalVelocity = direction * runSpeed;

    }

    // 웅크리기
    private void Crouch(Vector3 direction)
    {
        horizontalVelocity = direction * crouchSpeed;
    }

    // 중력 처리
    private void HandleGravity()
    {
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;

        if (velocity.y < -50f)
        {
            velocity.y = -50f;
        }
    }

    // 머리 위에 장애물이 있는지 확인하여 일어설 수 있는지 체크
    private bool CanStandUp()
    {
        // 벽면 긁힘 등 미세한 오차로 인해 못 일어나는 버그를 방지하기 위해 반경을 90%로 줄임
        float checkRadius = controller.radius * 0.9f;
        // 일어섰을 때 캡슐의 중심
        Vector3 standCenter = transform.position + Vector3.up * (normalHeight * 0.5f);
        // 일어섰을 때 캡슐의 하단 구체 중심 (발바닥에서 반경만큼 위)
        Vector3 bottom = standCenter + Vector3.down * ((normalHeight * 0.5f) - controller.radius);
        // 일어섰을 때 캡슐의 상단 구체 중심 (원래 키에서 반경만큼 아래)
        Vector3 top = standCenter + Vector3.up * ((normalHeight * 0.5f) - controller.radius);
        // Physics.CheckCapsule은 해당 영역에 지정된 레이어의 콜라이더가 겹치면 true를 반환
        bool hitCeiling = Physics.CheckCapsule(bottom, top, checkRadius, ceilingCheckLayer, QueryTriggerInteraction.Ignore);
        return !hitCeiling;
    }

    private float highestY;

    private void HandleFallingDamage()
    {
        // 정확한 낙하 높이 구하기
        // 기존에는 Character Controller의 center를 기준으로 구해서 오차가 있었음
        bool GetGroundY(out float groundY, float rayLength = 100f)
        {
            Vector3 origin = transform.position + Vector3.up * (controller.height * 0.5f);

            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayLength, groundMask, QueryTriggerInteraction.Ignore);

            if (hits.Length == 0)
            {
                groundY = 0f;
                return false;
            }

            float bestY = float.MinValue;

            foreach (var hit in hits)
            {
                if (hit.collider == controller) continue;

                if (hit.point.y > transform.position.y) continue;

                if (hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                }
            }

            if (bestY == float.MinValue)
            {
                groundY = 0f;
                return false;
            }

            groundY = bestY;
            return true;
        }

        float footY = transform.position.y - (controller.height * 0.5f);

        // 공중 상태
        if (!grounded)
        {
            if (!isFalling)
            {
                isFalling = true;
                highestY = footY;
                minVelocityReached = 0f;
            }

            if (footY > highestY)
            {
                highestY = footY;
            }

            verticalVelocity = velocity.y;

            if (verticalVelocity < minVelocityReached)
                minVelocityReached = verticalVelocity;
        }
        else
        {
            // 착지 순간
            if (grounded && isFalling)
            {
                if (GetGroundY(out float groundY))
                {
                   float fallHeight = highestY - groundY; // 낙하한 높이

                // 최소 낙하 높이(20미터)를 충족하고 낙하 데미지를 받기 위한 최소 낙하 속도를 만족했다면
                if (fallHeight >= minFallHeight && minVelocityReached < minDamageVelocity) 
                {
                    // 초과한 낙하 높이만큼의 추가 데미지
                    float extra = fallHeight - minFallHeight;
                    // 낙하 데미지 = 기본 낙하 데미지(20) + (낙하한 높이 * 초과된 미터 당 데미지)
                    int damage = baseFallingDamage + (int)(extra * damagePerMeter);
                    Debug.Log($"낙하한 높이: {fallHeight}, 낙하 데미지: {damage}");
                    // 낙하 데미지를 줌
                    conditions.onDamage(damage);
                    
                }
                }
            }

            // grounded 상태 리셋
            verticalVelocity = -2f;
            // isFalling을 false로 전환
            isFalling = false;
        }
    }
}

using UnityEngine;
using UnityEngine.AI;

public class TheMimicController : MonoBehaviour
{
    Animator anim;
    NavMeshAgent agent;

    bool isRushing;
    int phase;
    float timer;

    [SerializeField] Monsters data;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // 🔥 평상시 문 감지 → 자동 돌진
        if (!isRushing)
        {
            DetectDoorAndStartRush();
            return;
        }

        // 🔥 Rush 동작
        timer -= Time.deltaTime;

        if (phase == 0)
        {
            // 뒤로
            transform.position += -transform.forward * 2f * Time.deltaTime;

            if (timer <= 0f)
            {
                phase = 1;
                timer = 0.6f;

                anim.SetFloat("Speed", 1f);
            }
        }
        else if (phase == 1)
        {
            float rushSpeed = data.moveSpeed * 1.5f;

            transform.position += transform.forward * rushSpeed * Time.deltaTime;

            if (timer <= 0f)
            {
                isRushing = false;

                agent.enabled = true;
                agent.speed = data.moveSpeed;

                anim.SetFloat("Speed", 0.7f);
            }
        }
    }

    void DetectDoorAndStartRush()
    {
        RaycastHit hit;

        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 dir = transform.forward;

        // 🔥 앞에 문 있으면
        if (Physics.Raycast(origin, dir, out hit, 2f))
        {
            if (hit.collider.CompareTag("Door"))
            {
                DoorController door = hit.collider.GetComponentInParent<DoorController>();

                if (door != null && !door.IsBroken())
                {
                    StartRush();

                    // 🔥 여기서 문 파괴까지 처리
                    door.TakeDamage(999);
                }
            }
        }
    }

    void StartRush()
    {
        isRushing = true;
        phase = 0;

        // 🔥 NavMesh 완전 차단
        agent.enabled = false;

        timer = 0.25f;

        anim.SetFloat("Speed", 0.3f);
    }
}
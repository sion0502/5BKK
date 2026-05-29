using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public abstract class EnemyBase : MonoBehaviour
{
    public enum State
    {
        Patrol,
        Chase,
        Investigate
    }

    [Header("References")]
    [SerializeField] protected Monsters data;
    [SerializeField] protected Transform player;
    [SerializeField] protected Transform eyePoint;

    [Header("Patrol")]
    [SerializeField] protected float patrolRadius = 20f;

    [Header("Investigate")]
    [SerializeField] protected float investigateRadius = 3f;
    [SerializeField] protected float investigateDuration = 10f;

    protected NavMeshAgent agent;
    protected Animator anim;

    protected State currentState;

    protected Vector3 lastKnownPosition;

    protected float lastDetectTime;

    protected bool isBusy;

    float investigateTimer;

    protected virtual void Awake()
    {
        agent =
            GetComponent<NavMeshAgent>();

        anim =
            GetComponentInChildren<Animator>();
    }

    protected virtual void Start()
    {
        agent.speed =
            data.moveSpeed;

        currentState =
            State.Patrol;

        SetRandomPatrolPoint();
    }

    protected virtual void Update()
    {
        DetectPlayer();

        switch (currentState)
        {
            case State.Patrol:

                UpdatePatrol();

                break;

            case State.Chase:

                UpdateChase();

                break;

            case State.Investigate:

                UpdateInvestigate();

                break;
        }

        UpdateAnimator();
    }

    void DetectPlayer()
    {
        bool canSee =
            CheckVision();

        bool canHear =
            Vector3.Distance(
                transform.position,
                player.position)
            <= data.hearingRange;

        if (canSee || canHear)
        {
            currentState =
                State.Chase;

            lastDetectTime =
                Time.time;

            lastKnownPosition =
                player.position;
        }
    }

    bool CheckVision()
    {
        Vector3 dir =
            player.position -
            eyePoint.position;

        float dist =
            dir.magnitude;

        if (dist >
            data.detectRange)
            return false;

        float angle =
            Vector3.Angle(
                eyePoint.forward,
                dir);

        if (angle >
            data.viewAngle * 0.5f)
            return false;

        dir.Normalize();

        if (Physics.Raycast(
            eyePoint.position,
            dir,
            out RaycastHit hit,
            dist))
        {
            if (hit.collider.CompareTag(
                "Player"))
            {
                return true;
            }
        }

        return false;
    }

    void UpdatePatrol()
    {
        if (agent.pathPending)
            return;

        if (agent.remainingDistance >
            agent.stoppingDistance)
            return;

        SetRandomPatrolPoint();
    }

    void SetRandomPatrolPoint()
    {
        for (int i = 0; i < 20; i++)
        {
            Vector3 randomPos =
                transform.position +
                Random.insideUnitSphere *
                patrolRadius;

            if (NavMesh.SamplePosition(
                randomPos,
                out NavMeshHit hit,
                patrolRadius,
                NavMesh.AllAreas))
            {
                agent.SetDestination(
                    hit.position);

                return;
            }
        }
    }

    void UpdateChase()
    {
        if (isBusy)
            return;

        agent.SetDestination(
            player.position);

        lastKnownPosition =
            player.position;

        bool visible =
            CheckVision();

        bool hear =
            Vector3.Distance(
                transform.position,
                player.position)
            <= data.hearingRange;

        if (visible || hear)
        {
            lastDetectTime =
                Time.time;
        }

        if (Time.time -
            lastDetectTime <
            15f)
        {
            return;
        }

        lastKnownPosition =
            player.position;

        StartCoroutine(
            StartInvestigate());
    }

    IEnumerator StartInvestigate()
    {
        isBusy = true;

        agent.isStopped = true;

        anim.SetInteger(
            "State",
            0);

        yield return new WaitForSeconds(
            0.5f);

        agent.isStopped = false;

        currentState =
            State.Investigate;

        investigateTimer =
            investigateDuration;

        agent.SetDestination(
            lastKnownPosition);

        isBusy = false;
    }

    void UpdateInvestigate()
    {
        investigateTimer -=
            Time.deltaTime;

        if (investigateTimer <= 0f)
        {
            currentState =
                State.Patrol;

            SetRandomPatrolPoint();

            return;
        }

        if (agent.pathPending)
            return;

        if (agent.remainingDistance >
            agent.stoppingDistance)
            return;

        Vector3 randomPos =
            lastKnownPosition +
            Random.insideUnitSphere *
            investigateRadius;

        if (NavMesh.SamplePosition(
            randomPos,
            out NavMeshHit hit,
            investigateRadius,
            NavMesh.AllAreas))
        {
            agent.SetDestination(
                hit.position);
        }
    }

    void UpdateAnimator()
    {
        switch (currentState)
        {
            case State.Patrol:

                anim.SetInteger(
                    "State",
                    1);

                break;

            case State.Chase:

                anim.SetInteger(
                    "State",
                    2);

                break;

            case State.Investigate:

                anim.SetInteger(
                    "State",
                    3);

                break;
        }
    }

    protected virtual void OnGUI()
    {
        GUI.Label(
            new Rect(
                10,
                10,
                300,
                30),
            $"{name} : {currentState}");
    }
}
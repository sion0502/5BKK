using UnityEngine;

public sealed class EnemyState
{
    public EnemyBase.State CurrentState;
    public Vector3 LastKnownPosition;
    public Vector3 LastChaseDestination;

    public float LastDetectTime = -999f;
    public float InvestigateTimer;
    public float NextSenseTime;
    public float SenseEnableTime;
    public float NextChaseRepathTime;
    public float NextLogTime;
    public float NextSenseLogTime;
    public float LastVisionDetectTime = -999f;
    public float ObstacleStuckTimer;
    public float NextObstacleAvoidTime;

    public bool HasPatDestination;
    public bool IsBusy;
    public bool CanDetectPlayer;
    public bool LockAnimator;
    public bool ReachedLastKnownPosition;
    public bool InvestigateRoutineRunning;
    public bool WasPlayerHiding;
    public bool PlayerDeadLogged;
    public bool HiddenSearchTargetActive;
    public bool HiddenKillTargetActive;
    public bool DoorSpecialAllowed;

    public bool LastSawPlayer;
    public bool LastHeardPlayer;
    public bool TargetLostActive;

    public Vector3 CurrentPatrolDestination;
    public Vector3 LastObstacleCheckPosition;
}

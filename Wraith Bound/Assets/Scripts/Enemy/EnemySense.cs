using UnityEngine;

public sealed class EnemySense
{
    readonly EnemyBase _owner;
    PlayerAudioMixerController _playerFootsteps;
    PlayerController _playerController;
    InventoryManager _inventory;
    EquipmentViewController _equipmentView;
    FlashlightEnergyController _flashlightEnergy;
    Equipment _flashlightEquipment;

    public EnemySense(EnemyBase owner)
    {
        _owner = owner;
    }

    public void Tick()
    {
        EnemyState s = _owner.RuntimeState;

        s.CanDetectPlayer = false;
        s.LastSawPlayer = false;
        s.LastHeardPlayer = false;
        s.LastSawFlashlight = false;

        if (Time.time < s.SenseEnableTime) return;
        if (Time.time < s.NextSenseTime) return;

        s.NextSenseTime = Time.time + Mathf.Max(0.02f, _owner.Data.checkInterval);

        bool playerIsHiding = IsPlayerHiding();
        bool sawPlayer = playerIsHiding ? false : CheckVision();
        bool heardPlayer = playerIsHiding ? false : CheckHearing();
        bool sawFlashlight = playerIsHiding ? false : CheckFlashlight();

        s.LastSawPlayer = sawPlayer;
        s.LastHeardPlayer = heardPlayer;
        s.LastSawFlashlight = sawFlashlight;

        if (sawPlayer)
            s.LastVisionDetectTime = Time.time;

        LogCurrentSenseState(sawPlayer, heardPlayer, sawFlashlight, playerIsHiding);

        if (!sawPlayer && !heardPlayer && !sawFlashlight)
            return;

        s.CanDetectPlayer = true;
        s.TargetLostActive = false;
        s.HiddenSearchTargetActive = false;
        s.DoorSpecialAllowed = sawPlayer || heardPlayer || sawFlashlight;

        if (_owner.Player != null)
            s.LastKnownPosition = _owner.Player.position;
        else
            s.LastKnownPosition = _owner.DetectPlayerPosition();

        s.LastDetectTime = Time.time;

        if (s.CurrentState != EnemyBase.State.Chase)
            _owner.Combat.ChangeState(EnemyBase.State.Chase);
    }

    public void TickHiding()
    {
        EnemyState s = _owner.RuntimeState;
        bool isHiding = IsPlayerHiding();

        if (isHiding && CheckFlashlight())
        {
            MarkPlayerDead();
            s.WasPlayerHiding = isHiding;
            return;
        }

        if (isHiding && !s.WasPlayerHiding)
        {
            bool visibleAtHidingMoment = CheckVision();
            bool wasSeenWhileEntering = visibleAtHidingMoment || s.LastSawPlayer ||
                                        Time.time - s.LastVisionDetectTime <= _owner.HidingSeenMemoryTime;

            if (wasSeenWhileEntering)
            {
                if (_owner.Player != null)
                    s.LastKnownPosition = _owner.Player.position;

                s.HiddenKillTargetActive = true;
                s.HiddenSearchTargetActive = false;
                s.TargetLostActive = false;
                LogHiddenCaught("들켰다");

                if (s.CurrentState != EnemyBase.State.Chase)
                    _owner.Combat.ChangeState(EnemyBase.State.Chase);
            }
            else if (s.CurrentState == EnemyBase.State.Chase && _owner.Player != null)
            {
                s.LastKnownPosition = _owner.Player.position;
                s.HiddenSearchTargetActive = true;
                s.HiddenKillTargetActive = false;
                _owner.LogAI("플레이어가 시야 밖에서 숨음 → 마지막 숨은 위치 추적 후 수색");
            }
        }

        s.WasPlayerHiding = isHiding;
    }

    public void CheckPlayerCatchDistance()
    {
        EnemyState s = _owner.RuntimeState;

        if (s.PlayerDeadLogged) return;
        if (_owner.Player == null) return;

        float distance = GetDistanceToPlayerCollider();

        if (distance <= Mathf.Max(0.01f, _owner.PlayerContactKillDistance))
        {
            _owner.LogAI($"플레이어와 직접 접촉({distance:F2}m) → Death");
            MarkPlayerDead();
            return;
        }

        if (s.CurrentState != EnemyBase.State.Chase) return;

        float catchDistance = Mathf.Max(0.1f, _owner.PlayerCatchDistance);

        if (distance > catchDistance)
            return;

        _owner.LogAI($"플레이어와 접촉 거리 도달({distance:F2}m) → Death");
        MarkPlayerDead();
    }

    public void AutoFindPlayerReferences()
    {
        if (_owner.Player == null)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(_owner.PlayerTag);
            if (taggedPlayer != null)
                _owner.Player = taggedPlayer.transform;
        }

        if (_owner.Player == null)
        {
            Collider[] hits = Physics.OverlapSphere(
                _owner.transform.position,
                200f,
                _owner.Data.playerLayer,
                QueryTriggerInteraction.Ignore
            );

            if (hits.Length > 0)
                _owner.Player = hits[0].transform;
        }

        if (_owner.Player == null)
            return;

        if (_owner.PlayerCharacterController == null)
            _owner.PlayerCharacterController = _owner.Player.GetComponent<CharacterController>();

        if (_owner.PlayerRigidbody == null)
            _owner.PlayerRigidbody = _owner.Player.GetComponent<Rigidbody>();

        if (_playerController == null)
            _playerController = _owner.Player.GetComponent<PlayerController>();

        if (_inventory == null)
            _inventory = _owner.Player.GetComponent<InventoryManager>();

        if (_equipmentView == null)
            _equipmentView = _owner.Player.GetComponent<EquipmentViewController>();

        if (_flashlightEnergy == null)
            _flashlightEnergy = _owner.Player.GetComponent<FlashlightEnergyController>();

        if (_flashlightEquipment == null && _flashlightEnergy != null)
            _flashlightEquipment = _flashlightEnergy.FlashlightEquipment;

        if (_flashlightEquipment == null)
            _flashlightEquipment = Resources.Load<Equipment>("ItemDatas/Equipment/FlashLight");

        if (_owner.PlayerFootstepSource == null)
        {
            _playerFootsteps = _owner.Player.GetComponent<PlayerAudioMixerController>();
            if (_playerFootsteps != null)
                _owner.PlayerFootstepSource = _playerFootsteps.GetComponent<AudioSource>();
            else
                _owner.PlayerFootstepSource = _owner.Player.GetComponentInChildren<AudioSource>();
        }
        else if (_playerFootsteps == null)
        {
            _playerFootsteps = _owner.Player.GetComponent<PlayerAudioMixerController>();
        }

        if (_owner.PlayerHidingController == null)
            _owner.PlayerHidingController = _owner.Player.GetComponent<PlayerHidingController>();
    }

    public bool IsPlayerContact(Collider other)
    {
        if (other == null)
            return false;

        Transform hitTransform = other.transform;

        if (_owner.Player != null &&
            (hitTransform == _owner.Player || hitTransform.IsChildOf(_owner.Player) ||
             _owner.Player.IsChildOf(hitTransform)))
            return true;

        if (other.CompareTag(_owner.PlayerTag))
            return true;

        if (_owner.Data != null && (_owner.Data.playerLayer.value & (1 << other.gameObject.layer)) != 0)
            return true;

        return false;
    }

    bool CheckVision()
    {
        if (_owner.Player == null) return false;
        if (_owner.EyePoint == null) return false;

        Vector3 eyePos = _owner.EyePoint.position;
        Vector3 targetPos = GetBestVisiblePlayerPosition(eyePos, out bool hasCandidate);
        if (!hasCandidate) return false;

        Vector3 toPlayer = targetPos - eyePos;
        float dist = toPlayer.magnitude;
        if (dist > _owner.Data.detectRange) return false;
        if (dist <= 0.01f) return false;

        Vector3 dirToPlayer = toPlayer.normalized;
        float angle = Vector3.Angle(_owner.EyePoint.forward, dirToPlayer);
        if (angle > _owner.ViewAngle * 0.5f)
            return false;

        if (IsVisionBlockedByObstacle(eyePos, dirToPlayer, dist, out RaycastHit blockHit))
        {
            if (_owner.DrawVisionDebug)
                Debug.DrawLine(eyePos, blockHit.point, Color.red, _owner.Data.checkInterval);
            return false;
        }

        if (_owner.DrawVisionDebug)
            Debug.DrawLine(eyePos, targetPos, Color.green, _owner.Data.checkInterval);

        return true;
    }

    bool CheckHearing()
    {
        if (_owner.Player == null) return false;
        if (!IsPlayerActuallyMoving()) return false;

        if (IsCrouchWalking())
            return false;

        float dist = Vector3.Distance(_owner.transform.position, _owner.Player.position);
        if (dist > _owner.Data.hearingRange) return false;

        if (!HasAudibleFootstep())
            return false;

        return true;
    }

    bool CheckFlashlight()
    {
        if (_owner.EyePoint == null) return false;
        if (!TryGetActiveFlashlightPosition(out Vector3 lightPos)) return false;

        Vector3 eyePos = _owner.EyePoint.position;
        Vector3 toLight = lightPos - eyePos;
        float dist = toLight.magnitude;

        if (dist > _owner.Data.detectRange) return false;
        if (dist <= 0.01f) return false;

        float halfArc = _owner.EyeFrontArcAngle * 0.5f;
        if (Vector3.Angle(_owner.EyePoint.forward, toLight.normalized) > halfArc)
            return false;

        if (IsVisionBlockedByObstacle(eyePos, toLight.normalized, dist, out RaycastHit blockHit))
        {
            if (_owner.DrawVisionDebug)
                Debug.DrawLine(eyePos, blockHit.point, Color.magenta, _owner.Data.checkInterval);
            return false;
        }

        if (_owner.DrawVisionDebug)
            Debug.DrawLine(eyePos, lightPos, Color.cyan, _owner.Data.checkInterval);

        return true;
    }

    bool TryGetActiveFlashlightPosition(out Vector3 lightPos)
    {
        lightPos = default;

        if (_inventory == null || _equipmentView == null || _flashlightEquipment == null)
            return false;

        if (_inventory.GetSelectedItem() != _flashlightEquipment)
            return false;

        if (!_equipmentView.TryGetEquipmentLight(_flashlightEquipment, out Light light))
            return false;

        if (light == null || !light.enabled)
            return false;

        lightPos = light.transform.position;
        return true;
    }

    bool IsCrouchWalking()
    {
        if (_playerController == null || !_playerController.isCrouching)
            return false;

        return IsPlayerActuallyMoving();
    }

    bool HasAudibleFootstep()
    {
        if (_owner.PlayerFootstepSource != null && _owner.PlayerFootstepSource.isPlaying)
            return true;

        if (_playerFootsteps != null &&
            Time.time - _playerFootsteps.LastFootstepPlayTime <= _owner.FootstepHearingMemory)
            return true;

        return false;
    }

    Vector3 GetBestVisiblePlayerPosition(Vector3 eyePos, out bool hasCandidate)
    {
        hasCandidate = true;
        Vector3 fallback = GetPlayerAimPosition();
        float bestDistance = Vector3.Distance(eyePos, fallback);
        Vector3 bestPosition = fallback;

        if (_owner.Data == null || _owner.Data.playerLayer.value == 0)
            return bestPosition;

        Collider[] playerColliders = Physics.OverlapSphere(
            eyePos,
            _owner.Data.detectRange,
            _owner.Data.playerLayer,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider col = playerColliders[i];
            if (col == null) continue;

            Vector3 candidate = col.bounds.center;
            float distance = Vector3.Distance(eyePos, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPosition = candidate;
            }
        }

        return bestPosition;
    }

    bool IsVisionBlockedByObstacle(Vector3 eyePos, Vector3 dirToTarget, float distanceToTarget, out RaycastHit blockingHit)
    {
        blockingHit = default;
        int mask = _owner.ObstacleLayer.value | _owner.DoorLayer.value | _owner.Data.playerLayer.value;

        RaycastHit[] hits = Physics.SphereCastAll(
            eyePos,
            _owner.PlayerDetectRadius,
            dirToTarget,
            distanceToTarget,
            mask,
            QueryTriggerInteraction.Ignore
        );

        if (hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null) continue;
            if (IsSelfCollider(hitCollider)) continue;

            if (IsPlayerCollider(hitCollider))
                return false;

            blockingHit = hits[i];
            return true;
        }

        return false;
    }

    bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;

        Transform hitTransform = col.transform;

        if (_owner.Player != null &&
            (hitTransform == _owner.Player || hitTransform.IsChildOf(_owner.Player) ||
             _owner.Player.IsChildOf(hitTransform)))
            return true;

        if (col.CompareTag(_owner.PlayerTag))
            return true;

        return _owner.Data != null && (_owner.Data.playerLayer.value & (1 << col.gameObject.layer)) != 0;
    }

    bool IsSelfCollider(Collider col)
    {
        if (col == null) return false;

        Transform hitTransform = col.transform;
        return hitTransform == _owner.transform || hitTransform.IsChildOf(_owner.transform);
    }

    Vector3 GetPlayerAimPosition()
    {
        if (_owner.PlayerCharacterController != null)
            return _owner.Player.position + Vector3.up *
                Mathf.Max(0.8f, _owner.PlayerCharacterController.height * 0.5f);

        return _owner.Player.position + Vector3.up * 1.0f;
    }

    bool IsPlayerActuallyMoving()
    {
        if (_owner.PlayerCharacterController != null)
        {
            Vector3 v = _owner.PlayerCharacterController.velocity;
            v.y = 0f;
            return v.magnitude > _owner.MinFootstepMoveSpeed;
        }

        if (_owner.PlayerRigidbody != null)
        {
            Vector3 v = _owner.PlayerRigidbody.linearVelocity;
            v.y = 0f;
            return v.magnitude > _owner.MinFootstepMoveSpeed;
        }

        return false;
    }

    bool IsPlayerHiding()
    {
        return _owner.PlayerHidingController != null && _owner.PlayerHidingController.isHiding;
    }

    float GetDistanceToPlayerCollider()
    {
        float bestDistance = Vector3.Distance(_owner.transform.position, _owner.Player.position);

        Collider[] enemyColliders = _owner.GetComponentsInChildren<Collider>();
        Collider[] playerColliders = _owner.Player.GetComponentsInChildren<Collider>();

        if (enemyColliders == null || playerColliders == null || enemyColliders.Length == 0 ||
            playerColliders.Length == 0)
            return bestDistance;

        for (int i = 0; i < enemyColliders.Length; i++)
        {
            Collider enemyCol = enemyColliders[i];
            if (enemyCol == null || !enemyCol.enabled) continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCol = playerColliders[j];
                if (playerCol == null || !playerCol.enabled) continue;

                Vector3 pointOnEnemy = enemyCol.ClosestPoint(playerCol.bounds.center);
                Vector3 pointOnPlayer = playerCol.ClosestPoint(pointOnEnemy);
                float distance = Vector3.Distance(pointOnEnemy, pointOnPlayer);

                if (distance < bestDistance)
                    bestDistance = distance;
            }
        }

        return bestDistance;
    }

    /// <summary>
    /// 플레이어 사망 — Debug "Death"만 출력. 나중에 UI 담당이 이 로그 지점을 게임오버 UI로 교체.
    /// </summary>
    void MarkPlayerDead()
    {
        if (_owner.RuntimeState.PlayerDeadLogged) return;

        _owner.RuntimeState.PlayerDeadLogged = true;
        PlayerDeathDebug.TriggerDeath();
    }

    public void KillPlayerFromHiddenCatch() => MarkPlayerDead();

    public void KillPlayerDirect() => MarkPlayerDead();

    void LogHiddenCaught(string message)
    {
        Debug.Log($"[{_owner.name}] {message}");
        _owner.LogAI(message);
    }

    void LogCurrentSenseState(bool sawPlayer, bool heardPlayer, bool sawFlashlight, bool playerIsHiding)
    {
        string senseState;

        if (sawPlayer && heardPlayer && sawFlashlight)
            senseState = "시야 + 소리 + 손전등 감지";
        else if (sawPlayer && heardPlayer)
            senseState = "시야 + 소리 감지";
        else if (sawPlayer && sawFlashlight)
            senseState = "시야 + 손전등 감지";
        else if (heardPlayer && sawFlashlight)
            senseState = "소리 + 손전등 감지";
        else if (sawPlayer)
            senseState = "시야만 감지";
        else if (heardPlayer)
            senseState = "소리만 감지";
        else if (sawFlashlight)
            senseState = "손전등만 감지";
        else if (playerIsHiding)
            senseState = "플레이어 숨음 상태 → 시야/소리/손전등 감지 X";
        else
            senseState = "시야/소리/손전등 감지 X";

        _owner.LogSenseThrottled($"{senseState} | AI 상태: {_owner.RuntimeState.CurrentState}");
    }
}

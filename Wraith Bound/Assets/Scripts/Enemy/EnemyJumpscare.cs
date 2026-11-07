using System.Collections;
using UnityEngine;

/// <summary>자동 생성되는 사망 연출. 정지된 게임에서도 실제 시간으로 재생합니다.
/// 적에게 CreatureJumpscare(크리처 전용 연출)가 붙어 있으면 기본 돌진 대신 그것을 재생 — 카메라·적 위치 복구는 공통.</summary>
public sealed class EnemyJumpscare : MonoBehaviour
{
    Camera view;
    Transform monster;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    Vector3 monsterPosition;
    Quaternion monsterRotation;
    float originalFov;
    float originalNearClip;
    float blackout;
    bool finished;
    JumpscareStage stage;

    static readonly Color FaceLightColor = new Color(0.78f, 0.84f, 1f);
    const float FaceSurfaceBrightness = 0.3f;

    /// <summary>
    /// 얼굴 비추는 조명 — 예전엔 세기 2.5 고정이라 얼굴이 0.6m까지 오면(밝기 ∝ 1/거리²) 하얗게 빛나 보였음.
    /// 세기를 거리²에 비례시켜 어느 거리에서든 비슷하게 어둑한 밝기로. 위치는 눈 살짝 위.
    /// </summary>
    public static void ApplyFaceLight(Light light, Vector3 eye, Vector3 face)
    {
        if (light == null)
            return;
        float d = Mathf.Max(0.3f, Vector3.Distance(eye, face));
        light.transform.position = eye + Vector3.up * 0.15f;
        light.intensity = Mathf.Clamp(FaceSurfaceBrightness * d * d, 0.03f, 2.5f);
        light.range = d + 2.5f;
    }

    /// <summary>전용 연출의 암전 컷 (0~1, OnGUI 검은 막).</summary>
    public void SetBlackout(float alpha) => blackout = Mathf.Clamp01(alpha);

    public void Play(EnemyBase enemy, Camera camera)
    {
        view = camera;
        monster = enemy.transform;
        cameraPosition = view.transform.position;
        cameraRotation = view.transform.rotation;
        originalFov = view.fieldOfView;
        originalNearClip = view.nearClipPlane;
        monsterPosition = monster.position;
        monsterRotation = monster.rotation;
        StartCoroutine(Sequence(enemy));
    }

    IEnumerator Sequence(EnemyBase enemy)
    {
        if (enemy.Anim != null) enemy.Anim.enabled = false;
        if (enemy.Agent != null) enemy.Agent.enabled = false;

        Transform face = FindFace(enemy);
        Vector3 faceOffset = monster.InverseTransformPoint(
            face != null ? face.position : GetFaceFallback(enemy));

        Vector3 toFace = monster.TransformPoint(faceOffset) - cameraPosition;
        if (toFace.sqrMagnitude < 0.001f)
            toFace = cameraRotation * Vector3.forward;
        toFace.y = 0f;
        if (toFace.sqrMagnitude < 0.001f)
            toFace = cameraRotation * Vector3.forward;
        Quaternion scareRotation = Quaternion.LookRotation(toFace.normalized, Vector3.up);

        float turnTime = 0f;
        while (turnTime < 0.14f && view != null && monster != null)
        {
            view.transform.rotation = Quaternion.Slerp(cameraRotation, scareRotation,
                Mathf.SmoothStep(0f, 1f, turnTime / 0.14f));
            turnTime += Time.unscaledDeltaTime;
            yield return null;
        }
        if (view == null || monster == null)
        {
            finished = true;
            PlayerDeathDebug.TriggerDeath();
            yield break;
        }

        view.transform.rotation = scareRotation;
        Vector3 forward = scareRotation * Vector3.forward;
        FaceCamera(monster, forward);
        Vector3 faceWorld = monster.TransformPoint(faceOffset);
        FaceCameraIfShowingBack(monster, faceWorld, cameraPosition, forward);
        Vector3 rotatedOffset = monster.TransformVector(faceOffset);

        view.nearClipPlane = 0.03f;
        float faceDistance = GetSafeFaceDistance(monster.TransformPoint(faceOffset), forward);
        faceDistance = Mathf.Clamp(faceDistance, 0.55f, 1.05f);

        Light faceLight = gameObject.AddComponent<Light>();
        faceLight.type = LightType.Point;
        faceLight.color = FaceLightColor;
        ApplyFaceLight(faceLight, cameraPosition, monster.TransformPoint(faceOffset));
        transform.position = cameraPosition;

        CreatureJumpscare custom = enemy.GetComponent<CreatureJumpscare>();
        if (custom != null && custom.isActiveAndEnabled)
        {
            // 크리처 전용 연출 — 소리·포즈·배치·카메라를 연출이 직접 다룸
            stage = new JumpscareStage(this, view, enemy, face, cameraPosition, scareRotation,
                monsterPosition.y, faceDistance, faceLight);
            yield return custom.Play(stage);
            stage.Cleanup();
            stage = null;
        }
        else if (enemy.jumpscareSound != null)
        {
            AudioSource sound = gameObject.AddComponent<AudioSource>();
            sound.spatialBlend = 0f;
            sound.ignoreListenerPause = true;
            sound.PlayOneShot(enemy.jumpscareSound, 0.8f);
        }

        float duration = Mathf.Clamp(enemy.jumpscareDuration, 0.3f, 2f);
        float elapsed = custom != null && custom.isActiveAndEnabled ? duration : 0f;
        while (elapsed < duration && view != null && monster != null)
        {
            float lunge = Mathf.Clamp01(elapsed / 0.16f);
            float distance = Mathf.Lerp(faceDistance + 1.15f, faceDistance,
                1f - Mathf.Pow(1f - lunge, 3f));
            monster.position = cameraPosition + forward * distance - rotatedOffset;
            ApplyFaceLight(faceLight, cameraPosition, cameraPosition + forward * distance);
            float shake = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            view.transform.SetPositionAndRotation(cameraPosition,
                scareRotation * Quaternion.Euler(
                    Mathf.Sin(elapsed * 83f) * shake * 1.2f,
                    Mathf.Sin(elapsed * 67f) * shake * 1.2f,
                    Mathf.Sin(elapsed * 57f) * shake * 1.8f));
            view.fieldOfView = Mathf.Lerp(originalFov, 48f, lunge);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (faceLight != null)
            faceLight.enabled = false;
        RestoreScareView();
        if (enemy.Anim != null)
            enemy.Anim.enabled = true;
        if (enemy.Agent != null)
            enemy.Agent.enabled = true;
        finished = true;
        PlayerDeathDebug.KeepViewAfterJumpscare();
        yield return new WaitForSecondsRealtime(3f);
        PlayerDeathDebug.ReleaseEnemiesToPatrol();
        Destroy(gameObject);
    }

    static void FaceCamera(Transform monster, Vector3 cameraForward)
    {
        Vector3 want = -cameraForward;
        want.y = 0f;
        if (want.sqrMagnitude < 0.01f)
            want = -monster.forward;
        want.Normalize();
        monster.rotation = Quaternion.LookRotation(want, Vector3.up);
    }

    static void FaceCameraIfShowingBack(
        Transform monster, Vector3 faceWorld, Vector3 cameraPosition, Vector3 cameraForward)
    {
        Vector3 body = GetRendererCenter(monster);
        float faceAlong = Vector3.Dot(faceWorld - cameraPosition, cameraForward);
        float bodyAlong = Vector3.Dot(body - cameraPosition, cameraForward);
        if (bodyAlong + 0.05f >= faceAlong)
            return;

        monster.Rotate(0f, 180f, 0f, Space.World);
    }

    static Vector3 GetRendererCenter(Transform monster)
    {
        Renderer[] renderers = monster.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return monster.position + Vector3.up;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
                bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center;
    }

    static Transform FindFace(EnemyBase enemy)
    {
        if (enemy.jumpscareFacePoint != null)
            return enemy.jumpscareFacePoint;

        Animator anim = enemy.Anim != null
            ? enemy.Anim
            : enemy.GetComponentInChildren<Animator>(true);
        if (anim != null && anim.isHuman)
        {
            Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
                return head;
        }

        Transform named = FindNamedBone(enemy.transform, "Head", "head", "mixamorig:Head", "HeadTop_End");
        if (named != null)
            return named;

        if (enemy.EyePoint != null && enemy.EyePoint != enemy.transform)
            return enemy.EyePoint;

        return null;
    }

    static Transform FindNamedBone(Transform root, params string[] names)
    {
        Transform[] bones = root.GetComponentsInChildren<Transform>(true);
        for (int n = 0; n < names.Length; n++)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && bones[i].name == names[n])
                    return bones[i];
            }
        }

        return null;
    }

    static Vector3 GetFaceFallback(EnemyBase enemy)
    {
        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return enemy.transform.position + enemy.transform.forward * 0.25f + Vector3.up * 1.5f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
                bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 forward = enemy.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        forward.Normalize();
        return bounds.center + Vector3.up * bounds.extents.y * 0.45f + forward * bounds.extents.z * 0.35f;
    }

    float GetSafeFaceDistance(Vector3 facePosition, Vector3 forward)
    {
        float nearestSurface = 0f;
        const float headRadius = 0.45f;
        const float headRadiusSq = headRadius * headRadius;

        foreach (SkinnedMeshRenderer skin in monster.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.enabled || skin.sharedMesh == null)
                continue;

            Mesh pose = new Mesh();
            try
            {
                skin.BakeMesh(pose);
                Vector3[] vertices = pose.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 world = skin.transform.TransformPoint(vertices[i]);
                    if ((world - facePosition).sqrMagnitude > headRadiusSq)
                        continue;
                    nearestSurface = Mathf.Min(nearestSurface, Vector3.Dot(world - facePosition, forward));
                }
            }
            finally
            {
                Destroy(pose);
            }
        }

        foreach (MeshFilter filter in monster.GetComponentsInChildren<MeshFilter>())
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            if (filter.sharedMesh == null || renderer == null || !renderer.enabled)
                continue;

            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 worldCenter = filter.transform.TransformPoint(bounds.center);
            if ((worldCenter - facePosition).sqrMagnitude > 1.5f)
                continue;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 world = filter.transform.TransformPoint(corner);
                nearestSurface = Mathf.Min(nearestSurface, Vector3.Dot(world - facePosition, forward));
            }
        }

        return Mathf.Max(0.55f, -nearestSurface + view.nearClipPlane + 0.22f);
    }

    void LateUpdate()
    {
        // Animator가 포즈를 쓴 뒤에 전용 연출의 뼈 보정을 덧씌움
        stage?.LateApply();
    }

    void RestoreScareView()
    {
        if (view != null)
        {
            view.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            view.fieldOfView = originalFov;
            view.nearClipPlane = originalNearClip;
        }

        if (monster != null)
            monster.SetPositionAndRotation(monsterPosition, monsterRotation);

        view = null;
        monster = null;
    }

    void OnGUI()
    {
        if (blackout <= 0f)
            return;
        Color previous = GUI.color;
        int previousDepth = GUI.depth;
        GUI.depth = -1000;
        GUI.color = new Color(0f, 0f, 0f, blackout);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
        GUI.depth = previousDepth;
    }

    void OnDestroy()
    {
        if (stage != null)
        {
            stage.Cleanup();
            stage = null;
        }
        if (view != null)
        {
            view.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            view.fieldOfView = originalFov;
            view.nearClipPlane = originalNearClip;
        }
        if (monster != null)
            monster.SetPositionAndRotation(monsterPosition, monsterRotation);
        if (!finished && gameObject.scene.isLoaded && PlayerDeathDebug.IsDying)
            PlayerDeathDebug.TriggerDeath();
    }
}

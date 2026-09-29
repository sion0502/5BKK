using System.Collections;
using UnityEngine;

/// <summary>자동 생성되는 사망 연출. 정지된 게임에서도 실제 시간으로 재생합니다.</summary>
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
        // Freeze the pose so animation/root motion cannot fight the lunge.
        if (enemy.Anim != null) enemy.Anim.enabled = false;
        if (enemy.Agent != null) enemy.Agent.enabled = false;

        Transform face = enemy.jumpscareFacePoint;
        if (face == null && enemy.Anim != null && enemy.Anim.isHuman)
            face = enemy.Anim.GetBoneTransform(HumanBodyBones.Head);
        Vector3 faceOffset = face != null
            ? monster.InverseTransformPoint(face.position)
            : monster.InverseTransformPoint(GetFaceFallback(enemy));

        Vector3 toFace = monster.TransformPoint(faceOffset) - cameraPosition;
        if (toFace.sqrMagnitude < 0.001f) toFace = cameraRotation * Vector3.forward;
        Quaternion scareRotation = Quaternion.LookRotation(toFace.normalized, Vector3.up);
        // Turn toward the attacker's actual position before the lunge.
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
        Vector3 facing = -forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.01f) facing = -monster.forward;
        monster.rotation = Quaternion.LookRotation(facing, Vector3.up);
        Vector3 rotatedOffset = monster.TransformVector(faceOffset);
        view.nearClipPlane = 0.03f;
        float faceDistance = GetSafeFaceDistance(monster.TransformPoint(faceOffset), forward);

        Light faceLight = gameObject.AddComponent<Light>();
        faceLight.type = LightType.Point;
        faceLight.range = 4f;
        faceLight.intensity = 2.5f;
        faceLight.color = new Color(1f, 0.65f, 0.55f);
        transform.position = cameraPosition;
        if (enemy.jumpscareSound != null)
        {
            AudioSource sound = gameObject.AddComponent<AudioSource>();
            sound.spatialBlend = 0f;
            sound.ignoreListenerPause = true;
            sound.PlayOneShot(enemy.jumpscareSound, 0.8f);
        }

        float duration = Mathf.Clamp(enemy.jumpscareDuration, 0.3f, 2f);
        float elapsed = 0f;
        while (elapsed < duration && view != null && monster != null)
        {
            float lunge = Mathf.Clamp01(elapsed / 0.16f);
            float distance = Mathf.Lerp(faceDistance + 1.25f, faceDistance,
                1f - Mathf.Pow(1f - lunge, 3f));
            monster.position = cameraPosition + forward * distance - rotatedOffset;
            float shake = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            view.transform.SetPositionAndRotation(cameraPosition,
                scareRotation * Quaternion.Euler(
                    Mathf.Sin(elapsed * 83f) * shake * 1.2f,
                    Mathf.Sin(elapsed * 67f) * shake * 1.2f,
                    Mathf.Sin(elapsed * 57f) * shake * 1.8f));
            view.fieldOfView = Mathf.Lerp(originalFov, 48f, lunge);
            blackout = Mathf.InverseLerp(duration * 0.78f, duration, elapsed);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        blackout = 1f;
        faceLight.enabled = false;
        finished = true;
        PlayerDeathDebug.TriggerDeath();
    }

    static Vector3 GetFaceFallback(EnemyBase enemy)
    {
        if (enemy.EyePoint != null && enemy.EyePoint != enemy.transform)
            return enemy.EyePoint.position;
        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return enemy.transform.position + Vector3.up * 1.6f;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.12f, bounds.center.z);
    }

    float GetSafeFaceDistance(Vector3 facePosition, Vector3 forward)
    {
        float nearestSurface = 0f;
        // Sample the frozen pose, including scaled/non-humanoid heads and protruding faces.
        foreach (SkinnedMeshRenderer skin in monster.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.enabled || skin.sharedMesh == null) continue;
            Mesh pose = new Mesh();
            try
            {
                skin.BakeMesh(pose);
                foreach (Vector3 vertex in pose.vertices)
                    nearestSurface = Mathf.Min(nearestSurface,
                        Vector3.Dot(skin.transform.TransformPoint(vertex) - facePosition, forward));
            }
            finally { Destroy(pose); }
        }
        foreach (MeshFilter filter in monster.GetComponentsInChildren<MeshFilter>())
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            if (filter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
            // Bounds work even when imported meshes have Read/Write disabled.
            Bounds bounds = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                nearestSurface = Mathf.Min(nearestSurface,
                    Vector3.Dot(filter.transform.TransformPoint(corner) - facePosition, forward));
            }
        }
        return Mathf.Max(0.65f, -nearestSurface + view.nearClipPlane + 0.3f);
    }

    void OnGUI()
    {
        if (blackout <= 0f) return;
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
        if (view != null)
        {
            view.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            view.fieldOfView = originalFov;
            view.nearClipPlane = originalNearClip;
        }
        if (monster != null) monster.SetPositionAndRotation(monsterPosition, monsterRotation);
        if (!finished && gameObject.scene.isLoaded && PlayerDeathDebug.IsDying)
            PlayerDeathDebug.TriggerDeath();
    }
}

using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 캠코더 화면으로만 보이는 형체 (E1). 렌더러는 "CamcorderOnly" 레이어 — 플레이어 눈 카메라 cullingMask에서 빼고
/// 캠코더 렌즈 카메라(거의 모든 레이어를 그림)에는 그대로 잡히게 함 (PrologueSceneBuilder가 눈 카메라 마스크 설정).
/// 뷰파인더를 켠 채 화면 안에 seenSeconds 동안 담기면 Seen → 잠깐 깜빡이다 사라짐.
/// </summary>
public class CamcorderOnlyApparition : MonoBehaviour
{
    public const string LayerName = "CamcorderOnly";

    [SerializeField] private float seenSeconds = 0.9f;
    [SerializeField] private float viewportMargin = 0.12f;
    [SerializeField] private Transform focusPoint;

    public event Action Seen;

    public bool IsVisible => gameObject.activeSelf && !vanishing;

    private Camera eyeCamera;
    private CamcorderController camcorder;
    private float seenTimer;
    private bool vanishing;
    private Renderer[] renderers;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        int layer = LayerMask.NameToLayer(LayerName);
        if (layer >= 0)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }
    }

    public void Show()
    {
        vanishing = false;
        seenTimer = 0f;
        gameObject.SetActive(true);
        SetRenderersVisible(true);
    }

    /// <summary>보지 못했어도 조용히 사라지게 (시간 초과 등).</summary>
    public void HideSilently()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (vanishing || !IsInCamcorderView())
        {
            seenTimer = Mathf.Max(0f, seenTimer - Time.deltaTime);
            return;
        }

        seenTimer += Time.deltaTime;
        if (seenTimer >= seenSeconds)
        {
            StartCoroutine(VanishRoutine());
        }
    }

    private bool IsInCamcorderView()
    {
        if (camcorder == null)
        {
            camcorder = FindFirstObjectByType<CamcorderController>();
        }

        if (camcorder == null || !camcorder.IsViewfinderActive)
        {
            return false;
        }

        if (eyeCamera == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            eyeCamera = player != null ? player.GetComponentInChildren<Camera>() : Camera.main;
        }

        if (eyeCamera == null)
        {
            return false;
        }

        // 렌즈 카메라는 눈 카메라 방향을 따라가므로 눈 카메라 기준으로 판정
        Vector3 focus = focusPoint != null ? focusPoint.position : transform.position + Vector3.up * 1.4f;
        Vector3 vp = eyeCamera.WorldToViewportPoint(focus);
        if (vp.z <= 0f || vp.x < viewportMargin || vp.x > 1f - viewportMargin || vp.y < viewportMargin || vp.y > 1f - viewportMargin)
        {
            return false;
        }

        Vector3 from = eyeCamera.transform.position;
        Vector3 to = focus - from;
        int mask = ~(1 << gameObject.layer) & ~LayerMask.GetMask("Player", "PickupItem", "Ignore Raycast");
        return !Physics.Raycast(from, to.normalized, to.magnitude - 0.3f, mask, QueryTriggerInteraction.Ignore);
    }

    private IEnumerator VanishRoutine()
    {
        vanishing = true;
        Seen?.Invoke();

        // 화면이 튀듯 몇 번 깜빡이다 사라짐
        float[] blinks = { 0.06f, 0.05f, 0.09f, 0.04f, 0.12f };
        for (int i = 0; i < blinks.Length; i++)
        {
            SetRenderersVisible(i % 2 == 1);
            yield return new WaitForSeconds(blinks[i]);
        }

        gameObject.SetActive(false);
    }

    private void SetRenderersVisible(bool visible)
    {
        foreach (Renderer r in renderers)
        {
            if (r != null)
            {
                r.enabled = visible;
            }
        }
    }
}

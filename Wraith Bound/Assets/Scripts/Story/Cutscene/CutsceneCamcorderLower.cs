using System.Collections;
using UnityEngine;

/// <summary>
/// 컷씬 → 플레이 전환 때 캠코더를 본편과 똑같이 내림.
/// 컷씬의 캠코더 화면(CutsceneCamcorderOverlay)은 끝까지 켜 둔 채로, 플레이어 손에 캠코더를 꺼내(F와 같은 처리)
/// 눈앞에 든 자세로 놓고 → 같은 프레임에 오버레이를 끄고 본편 내리기(좌클릭으로 뷰파인더 닫을 때와 같은 애니)를 시작.
/// 내린 뒤엔 캠코더를 손에 든 채로 둠. CutscenePlayerHandoff.Handoff()가 호출.
/// </summary>
public class CutsceneCamcorderLower : MonoBehaviour
{
    [SerializeField] private GameObject player;
    [SerializeField] private CutsceneBlendTarget camcorderOverlay;
    [Tooltip("손에 든 캠코더 뷰가 준비되길 기다리는 최대 시간 (못 찾으면 오버레이만 끔)")]
    [SerializeField] private float readyTimeout = 1f;

    public void Begin()
    {
        if (camcorderOverlay != null)
        {
            camcorderOverlay.Blend = 1f; // 실제 캠코더로 바뀔 때까지 컷씬 화면 유지
        }

        StartCoroutine(LowerRoutine());
    }

    private IEnumerator LowerRoutine()
    {
        InventoryManager inventory = player != null ? player.GetComponent<InventoryManager>() : null;
        float waited = 0f;

        // 컷씬 동안 꺼져 있던 InventoryManager는 다시 켜진 뒤 첫 Start에서야 캠코더를 지급함 → 그때까지 기다림
        while (inventory != null && !inventory.HasCamcorder && waited < readyTimeout)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (inventory != null && inventory.HasCamcorder && !inventory.IsCamcorderHeld())
        {
            inventory.ToggleCamcorderHeld();
        }

        CamcorderController camcorder = null;
        while (waited < readyTimeout)
        {
            camcorder = FindHeldCamcorder();
            if (camcorder != null)
            {
                break;
            }

            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (camcorder != null)
        {
            camcorder.LowerFromEye();
        }
        else
        {
            Debug.LogWarning("[CutsceneCamcorderLower] 손에 든 캠코더를 찾지 못해 화면만 끔");
        }

        if (camcorderOverlay != null)
        {
            camcorderOverlay.Blend = 0f;
        }
    }

    private CamcorderController FindHeldCamcorder()
    {
        if (player == null)
        {
            return null;
        }

        foreach (CamcorderController camcorder in player.GetComponentsInChildren<CamcorderController>())
        {
            if (camcorder.isActiveAndEnabled && camcorder.IsReady)
            {
                return camcorder;
            }
        }

        return null;
    }
}

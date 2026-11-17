using UnityEngine;

/// <summary>
/// 손에 든 아이템 렌더 카메라(ItemViewCamera)에 붙어, 플레이어 크기를 바꿔도 손 아이템·캠코더 LCD/HUD가 예전과 똑같이 보이게 함.
/// ItemViewCamera는 직교 카메라라 orthographicSize가 월드 단위 — 플레이어를 1.3배로 키우면(2026-11, 맵 비율 맞춤) 그 아래 앵커·모델·HUD만
/// 1.3배로 커지고 위치도 밀려 화면에서 어긋났음. 이 오브젝트의 스케일을 부모 배율의 역수로 두어 하위 전체의 월드 크기를 1로 되돌림
/// (카메라 자체는 스케일이 렌더에 영향 없음). 원래 모든 값이 플레이어 크기 1 기준으로 맞춰져 있음.
/// </summary>
[DefaultExecutionOrder(-200)]
public class ItemViewScaleCompensator : MonoBehaviour
{
    private Vector3 lastParentScale = new Vector3(float.NaN, 0f, 0f);

    private void Awake()
    {
        Apply();
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        Transform parent = transform.parent;
        Vector3 s = parent != null ? parent.lossyScale : Vector3.one;
        if (s == lastParentScale)
        {
            return;
        }

        lastParentScale = s;
        transform.localScale = new Vector3(SafeInverse(s.x), SafeInverse(s.y), SafeInverse(s.z));
    }

    private static float SafeInverse(float v)
    {
        return Mathf.Abs(v) > 0.0001f ? 1f / v : 1f;
    }
}

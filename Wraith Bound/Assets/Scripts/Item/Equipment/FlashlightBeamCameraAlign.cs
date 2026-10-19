using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 손전등 SpotLight의 위치를 플레이어 카메라(눈) 위치로 맞춘다.
/// 빛이 손(오른쪽 아래 앞)에서 나오면 그림자가 물체에서 비껴나 허공에 뜬 것처럼 보이므로,
/// 위치만 카메라로 옮기고 방향은 손전등 모델(흔들림 포함)을 그대로 따른다.
/// 빛 원뿔 안에 들어오게 된 손전등 모델은 이 빛을 받지 않고 그림자도 드리우지 않게 한다.
/// 카메라 이동(MouseLook 등) 이후 위치를 맞추도록 실행 순서를 늦춘다.
/// </summary>
[DefaultExecutionOrder(100)]
public class FlashlightBeamCameraAlign : MonoBehaviour
{
    [Tooltip("카메라 위치에서 시선 방향으로 얼마나 앞에 둘지")]
    [SerializeField] private float forwardOffset = 0.05f;
    [SerializeField] private string viewLayerName = "PickupItem";

    private Transform lightPivot;
    private Camera viewCamera;

    private void Awake()
    {
        Light spotLight = GetComponentInChildren<Light>(true);
        if (spotLight == null)
        {
            return;
        }

        lightPivot = spotLight.transform;

        int viewLayer = LayerMask.NameToLayer(viewLayerName);
        if (viewLayer >= 0)
        {
            spotLight.cullingMask &= ~(1 << viewLayer);
        }

        foreach (Renderer viewRenderer in GetComponentsInChildren<Renderer>(true))
        {
            viewRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    private void LateUpdate()
    {
        if (lightPivot == null)
        {
            return;
        }

        if (viewCamera == null)
        {
            viewCamera = Camera.main;
            if (viewCamera == null)
            {
                return;
            }
        }

        Transform eye = viewCamera.transform;
        lightPivot.position = eye.position + eye.forward * forwardOffset;
    }
}

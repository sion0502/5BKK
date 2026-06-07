using UnityEngine;

public class DoorLayerSetter : MonoBehaviour
{
    [SerializeField] private string doorLayerName = "Door";
    [SerializeField] private float delay = 0.5f;

    private void Start()
    {
        Invoke(nameof(ApplyDoorLayer), delay);
    }

    private void ApplyDoorLayer()
    {
        int layer = LayerMask.NameToLayer(doorLayerName);

        if (layer == -1)
        {
            Debug.LogError("Door 레이어가 없습니다. 먼저 Layer를 만들어주세요.");
            return;
        }

        DoorClick[] doors = GetComponentsInChildren<DoorClick>(true);

        for (int i = 0; i < doors.Length; i++)
        {
            SetLayerRecursively(doors[i].gameObject, layer);
        }

        Debug.Log($"Door 레이어 적용 완료: {doors.Length}개");
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}

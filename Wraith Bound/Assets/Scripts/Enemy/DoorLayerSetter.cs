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
            Debug.LogError("Door layer not found. Add a Door layer in TagManager.");
            return;
        }

        DoorClick[] doors = Object.FindObjectsByType<DoorClick>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < doors.Length; i++)
            SetLayerRecursively(doors[i].gameObject, layer);

        Debug.Log($"Door layer applied: {doors.Length}");
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}

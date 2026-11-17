using UnityEngine;

// Only used by Chapter1_TileLineTest. Corner tiles keep their original openings.
public class Chapter1TileTestNavigation : MonoBehaviour
{
    public const string TestSceneName = "Chapter1_TileLineTest";
    [SerializeField] private SceneStarter sceneStarter;
    [SerializeField] private Transform[] tiles;
    [SerializeField, Min(1f)] private float speedMultiplier = 3f;
    [SerializeField, Min(2f)] private float teleportDistance = 8f;
    private int selected = -1;
    private PlayerController boostedPlayer;
    private Vector3 originalSpeeds;

    private void Update()
    {
        if (gameObject.scene.name != TestSceneName ||
            sceneStarter == null || !sceneStarter.IsSequenceFinished || Time.timeScale == 0f) return;

        GameObject player = sceneStarter.player;
        if (player == null || !player.TryGetComponent(out CharacterController controller) ||
            !controller.enabled) return;

        if (Input.GetKeyDown(KeyCode.F10) && player.TryGetComponent(out PlayerController movement))
            ToggleSpeed(movement);

        if (Input.GetKeyDown(KeyCode.R))
        {
            TeleportAlongView(controller);
            return;
        }

        int step = Input.GetKeyDown(KeyCode.PageDown) ? 1 :
            Input.GetKeyDown(KeyCode.F9) || Input.GetKeyDown(KeyCode.PageUp) ? -1 : 0;
        if (step == 0 || tiles == null || tiles.Length == 0) return;

        int next = selected < 0 ? (step > 0 ? 0 : tiles.Length - 1) :
            (selected + step + tiles.Length) % tiles.Length;
        Transform tile = tiles[next];
        if (tile == null || !TryFindFloor(tile, controller, out Vector3 position))
        {
            Debug.LogWarning("[Tile test] No clear spawn point in " + tile, this);
            return;
        }

        MovePlayer(controller, position);
        selected = next;
        Debug.Log($"[Tile test] {selected + 1}/{tiles.Length}: {tile.name}", tile);
    }

    private void TeleportAlongView(CharacterController player)
    {
        Camera camera = Camera.main;
        if (player.TryGetComponent(out PlayerInteractor interactor) && interactor.InteractionCamera != null)
            camera = interactor.InteractionCamera;
        if (camera == null) return;

        // Intentionally ignore walls along the route; validate only the landing.
        int mask = ~(1 << player.gameObject.layer);
        Vector3 direction = camera.transform.forward;
        for (float distance = teleportDistance; distance >= 2f; distance -= 0.5f)
        {
            if (direction.y > 0.15f)
            {
                Vector3 airbornePosition = player.transform.position + direction * distance;
                if (!HasRoomAt(player, airbornePosition, mask)) continue;
                MovePlayer(player, airbornePosition);
                return;
            }

            Vector3 target = camera.transform.position + direction * distance;
            if (!Physics.Raycast(target + Vector3.up * 0.25f, Vector3.down, out RaycastHit floor,
                    4f, mask, QueryTriggerInteraction.Ignore) ||
                floor.normal.y < 0.85f || !TryGetLanding(player, floor.point, out Vector3 position)) continue;

            MovePlayer(player, position);
            return;
        }

        Debug.Log("[Tile test] No clear destination in the look direction", this);
    }

    private static void MovePlayer(CharacterController player, Vector3 position)
    {
        player.enabled = false;
        player.transform.position = position;
        player.enabled = true;
        Physics.SyncTransforms();
    }

    private void ToggleSpeed(PlayerController movement)
    {
        if (boostedPlayer == movement)
        {
            RestoreSpeed();
            Debug.Log("[Tile test] Movement speed restored", this);
            return;
        }

        RestoreSpeed();
        boostedPlayer = movement;
        originalSpeeds = new Vector3(movement.moveSpeed, movement.runSpeed, movement.crouchSpeed);
        float multiplier = Mathf.Max(1f, speedMultiplier);
        movement.moveSpeed *= multiplier;
        movement.runSpeed *= multiplier;
        movement.crouchSpeed *= multiplier;
        Debug.Log($"[Tile test] Movement speed x{multiplier:0.##}", this);
    }

    private void OnDisable() => RestoreSpeed();

    private void RestoreSpeed()
    {
        if (boostedPlayer != null)
        {
            boostedPlayer.moveSpeed = originalSpeeds.x;
            boostedPlayer.runSpeed = originalSpeeds.y;
            boostedPlayer.crouchSpeed = originalSpeeds.z;
        }
        boostedPlayer = null;
    }

    private static bool TryFindFloor(Transform tile, CharacterController player, out Vector3 position)
    {
        // Search the ground floor from the center out, never using a neighboring tile.
        for (int ring = 0; ring <= 8; ring++)
        for (int x = -ring; x <= ring; x++)
        for (int z = -ring; z <= ring; z++)
        {
            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring) continue;
            Vector3 origin = tile.position + new Vector3(x * 5f, 3f, z * 5f);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit floor, 5f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                floor.normal.y < 0.85f || !floor.transform.IsChildOf(tile)) continue;

            if (TryGetLanding(player, floor.point, out position)) return true;
        }

        position = default;
        return false;
    }

    private static bool TryGetLanding(CharacterController player, Vector3 floor, out Vector3 position)
    {
        float halfHeight = Mathf.Max(player.height * 0.5f, player.radius);
        position = floor + Vector3.up * (halfHeight + 0.1f) - player.transform.TransformVector(player.center);
        return HasRoomAt(player, position, ~(1 << player.gameObject.layer));
    }

    private static bool HasRoomAt(CharacterController player, Vector3 position, int mask)
    {
        float radius = player.radius;
        float halfHeight = Mathf.Max(player.height * 0.5f, radius);
        Vector3 center = position + player.transform.TransformVector(player.center);
        Vector3 segment = Vector3.up * (halfHeight - radius);
        return !Physics.CheckCapsule(center - segment, center + segment, radius, mask,
            QueryTriggerInteraction.Ignore);
    }
}

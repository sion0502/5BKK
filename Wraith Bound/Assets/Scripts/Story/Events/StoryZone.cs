using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 연출 이벤트 트리거 구역. 플레이어(Player 태그)가 들어오면 Entered(C#) + onEntered(UnityEvent) 호출.
/// BoxCollider(isTrigger)를 같이 붙여 크기를 정함. once=true면 한 번만.
/// 숨는 중(CharacterController 꺼짐)엔 트리거가 안 불리므로, 상태 확인이 필요하면 IsPlayerInside를 직접 씀.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class StoryZone : MonoBehaviour
{
    [SerializeField] private bool once = true;
    [SerializeField] private UnityEvent onEntered = new UnityEvent();

    public event Action Entered;

    public bool HasFired { get; private set; }
    public bool IsPlayerInside => PlayerInside();

    private BoxCollider box;
    private Transform player;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((once && HasFired) || !other.CompareTag("Player"))
        {
            return;
        }

        HasFired = true;
        Entered?.Invoke();
        onEntered.Invoke();
    }

    /// <summary>트리거 이벤트와 무관하게 플레이어 위치로 판정 (숨어 있을 때 등).</summary>
    private bool PlayerInside()
    {
        if (player == null)
        {
            GameObject go = GameObject.FindWithTag("Player");
            player = go != null ? go.transform : null;
        }

        if (player == null || box == null)
        {
            return false;
        }

        Vector3 local = transform.InverseTransformPoint(player.position + Vector3.up * 0.9f) - box.center;
        Vector3 half = box.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    private void OnDrawGizmos()
    {
        BoxCollider b = GetComponent<BoxCollider>();
        if (b == null)
        {
            return;
        }

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.25f);
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}

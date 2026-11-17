using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Space(뒤돌아보기, 팀원 RearviewCamera — Space를 누르고 있는 동안 시점 180°)와 '창 닫기 Space'가 겹치는 문제 방지.
/// 튜토리얼 팁·일지 등을 Space로 닫으면 그 Space를 계속 누르고 있는 동안 뒤돌아보기가 켜져 화면이 휙 돌았음 →
/// 입력 잠금을 풀 때 뒤돌아보기만 Space를 뗄 때까지 꺼 둠. RearviewCamera는 수정하지 않고 enabled만 끄고 켬.
/// </summary>
public static class RearviewSuppressor
{
    public const string RearviewTypeName = "RearviewCamera";

    public static bool IsRearview(Behaviour behaviour)
    {
        return behaviour != null && behaviour.GetType().Name == RearviewTypeName;
    }

    /// <summary>Space가 눌려 있으면 뗀 뒤에 켜고, 아니면 바로 켬.</summary>
    public static void Enable(Behaviour rearview)
    {
        if (rearview == null)
        {
            return;
        }

        if (!Input.GetKey(KeyCode.Space))
        {
            rearview.enabled = true;
            return;
        }

        rearview.enabled = false;
        Runner.Get().Add(rearview);
    }

    private sealed class Runner : MonoBehaviour
    {
        private static Runner instance;
        private readonly List<Behaviour> pending = new List<Behaviour>();

        public static Runner Get()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("RearviewSuppressor");
                go.hideFlags = HideFlags.HideInHierarchy;
                instance = go.AddComponent<Runner>();
            }

            return instance;
        }

        public void Add(Behaviour behaviour)
        {
            if (!pending.Contains(behaviour))
            {
                pending.Add(behaviour);
            }
        }

        private void Update()
        {
            if (pending.Count == 0 || Input.GetKey(KeyCode.Space))
            {
                return;
            }

            foreach (Behaviour behaviour in pending)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }
            }

            pending.Clear();
        }
    }
}

using UnityEngine;

/// <summary>
/// 실행할 때 지정한 오브젝트들을 숨김 — 팀원이 타일 프리팹에 찍어 둔 배치 표시(노란 공 `Key`)를
/// 프리팹은 건드리지 않고 게임에서만 안 보이게. 에디터에선 그대로 보여 위치 확인용으로 남음.
/// </summary>
public class DemoHideOnPlay : MonoBehaviour
{
    [SerializeField] private GameObject[] targets = { };

    private void Awake()
    {
        foreach (GameObject t in targets)
        {
            if (t != null)
            {
                t.SetActive(false);
            }
        }
    }
}

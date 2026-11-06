using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 씬에 배치해 SubtitleSequence를 재생하는 컴포넌트.
/// Timeline Signal Receiver, 연출 트리거의 UnityEvent 등에서 Play()를 호출하면 됩니다.
/// </summary>
public class SubtitlePlayer : MonoBehaviour
{
    [SerializeField] private SubtitleSequence sequence;
    [SerializeField] private bool playOnStart;
    [Tooltip("한 번만 재생 (트리거에 다시 들어가도 무시)")]
    [SerializeField] private bool playOnce = true;
    [SerializeField] private UnityEvent onFinished = new UnityEvent();

    private bool played;

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        if (sequence == null || (playOnce && played))
        {
            return;
        }

        played = true;
        SubtitleUI.Play(sequence, onFinished.Invoke);
    }

    public void Stop()
    {
        SubtitleUI.Clear();
    }
}

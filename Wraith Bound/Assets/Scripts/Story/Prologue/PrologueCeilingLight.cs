using System.Collections;
using UnityEngine;

/// <summary>
/// 연출용 천장 조명 하나 (복도 형광등, 비상구 위 붉은 등). 실제 Light + 발광 재질을 함께 켜고 끔.
/// 정전된 병원에서 켜져 있는 형광등은 플레이어를 복도 끝으로 끌어당기는 길잡이이자 마지막 컷씬의 연출 도구.
/// 소리 슬롯은 비워 둠(사운드는 팀이 나중에 결정) — 비어 있으면 무음.
/// </summary>
public class PrologueCeilingLight : MonoBehaviour
{
    [SerializeField] private Light lightSource;
    [SerializeField] private Renderer[] renderers = { };
    [SerializeField] private Material onMaterial;
    [SerializeField] private Material offMaterial;
    [SerializeField] private bool startOn = true;

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("꺼질 때 '팍' 소리")]
    [SerializeField] private AudioClip popClip;
    [Tooltip("깜빡일 때 지직 소리")]
    [SerializeField] private AudioClip flickerClip;
    [Tooltip("켜질 때 '철컥' 소리 (붉은 등)")]
    [SerializeField] private AudioClip switchOnClip;

    public bool IsOn { get; private set; }

    private void Awake()
    {
        Set(startOn);
    }

    /// <summary>소리 없이 즉시 켜고 끔.</summary>
    public void Set(bool on)
    {
        IsOn = on;
        if (lightSource != null)
        {
            lightSource.enabled = on;
        }

        Material material = on ? onMaterial : offMaterial;
        if (material == null)
        {
            return;
        }

        foreach (Renderer r in renderers)
        {
            if (r != null)
            {
                r.sharedMaterial = material;
            }
        }
    }

    public void SwitchOn()
    {
        Play(switchOnClip);
        Set(true);
    }

    /// <summary>지직거리다가 '팍' 하고 꺼짐.</summary>
    public IEnumerator PopOff()
    {
        Play(popClip);
        float[] pattern = { 0.05f, 0.04f, 0.07f, 0.03f };
        for (int i = 0; i < pattern.Length; i++)
        {
            Set(i % 2 == 1);
            yield return new WaitForSeconds(pattern[i]);
        }

        Set(false);
    }

    public void PlayFlickerSound()
    {
        Play(flickerClip);
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}

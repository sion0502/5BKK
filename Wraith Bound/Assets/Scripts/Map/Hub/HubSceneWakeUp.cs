using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HubSceneWakeUp : MonoBehaviour
{
    [Header("References")]
    public GameObject player;
    public Image fadeImage;

    [Header("Wake Up")]
    public float wakeDelay = 1.2f;
    public float fadeInDuration = 4.5f;
    public AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private void Start()
    {
        if (!ScreenFader.ShouldWakeUpInHub)
        {
            return;
        }

        ScreenFader.ShouldWakeUpInHub = false;
        StartCoroutine(WakeUpSequence());
    }

    private IEnumerator WakeUpSequence()
    {
        Image fade = ScreenFader.GetPersistedOverlay();
        if (fade == null)
        {
            fade = ScreenFader.Prepare(fadeImage);
        }

        ScreenFader.SetAlpha(fade, 1f);
        SetPlayerControl(false);

        yield return new WaitForSeconds(wakeDelay);
        yield return ScreenFader.FadeFromBlack(fade, fadeInDuration, fadeInCurve);

        SetPlayerControl(true);
        ScreenFader.ClearPersisted();
    }

    private void SetPlayerControl(bool enabled)
    {
        if (player == null)
        {
            return;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = enabled;
        }

        MonoBehaviour[] scripts = player.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != null && script != this)
            {
                script.enabled = enabled;
            }
        }
    }
}

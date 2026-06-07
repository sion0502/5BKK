using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class AbyssSinker : MonoBehaviour
{
    [Header("Scene")]
    public string nextSceneName = "Chapter1";

    [Header("Fall Sequence")]
    public float waitTime = 1.0f;
    public float fallSpeed = 15.0f;
    public float totalDuration = 4.0f;

    [Header("Chapter Intro")]
    public string chapterTitle = "폐병원";
    public Image fadeImage;
    public float fadeToBlackDuration = 1.2f;
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float titleFontSize = 156f;
    public float titleFadeMinAlpha = 0.42f;
    public float titleFadePeriod = 2.8f;
    public float titleExitFadeDuration = 1.2f;

    [Header("Chapter Intro Sound")]
    public AudioClip titleAppearSound;
    [Range(0f, 1f)]
    public float titleAppearVolume = 1f;

    private bool isWorking;

    private void OnTriggerEnter(Collider other)
    {
        if (isWorking || !other.CompareTag("Player"))
        {
            return;
        }

        isWorking = true;
        StartCoroutine(StartFallingSequence(other.gameObject));
    }

    private IEnumerator StartFallingSequence(GameObject player)
    {
        MonoBehaviour[] scripts = player.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != this)
            {
                script.enabled = false;
            }
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        yield return new WaitForSeconds(waitTime);

        float timer = 0f;
        float fallDuration = totalDuration - waitTime;
        while (timer < fallDuration)
        {
            timer += Time.deltaTime;
            player.transform.position += Vector3.down * fallSpeed * Time.deltaTime;
            yield return null;
        }

        ChapterTitleTransitionConfig config = new ChapterTitleTransitionConfig
        {
            Title = chapterTitle,
            SceneName = nextSceneName,
            FadeDuration = fadeToBlackDuration,
            FadeCurve = fadeCurve,
            TitleFontSize = titleFontSize,
            TitleFadeMinAlpha = titleFadeMinAlpha,
            TitleFadePeriod = titleFadePeriod,
            TitleExitFadeDuration = titleExitFadeDuration,
            TitleAppearSound = titleAppearSound,
            TitleAppearVolume = titleAppearVolume
        };

        yield return ChapterTitleTransition.Run(fadeImage, config);
    }
}

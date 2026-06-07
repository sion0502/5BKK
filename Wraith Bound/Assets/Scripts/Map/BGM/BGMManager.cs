using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    private AudioSource audioSource;
    private string ownerSceneName;
    private float defaultVolume;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }

        Instance = this;
        ownerSceneName = gameObject.scene.name;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            defaultVolume = audioSource.volume;
            if (audioSource.clip != null && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == ownerSceneName)
        {
            return;
        }

        StopBGM();

        if (Instance == this)
        {
            Instance = null;
        }

        Destroy(gameObject);
    }

    public void StopBGM()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.volume = defaultVolume;
        }
    }

    public void PlayBGM()
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.volume = defaultVolume;
        audioSource.Play();
    }

    public void FadeOut(float duration, AnimationCurve curve = null)
    {
        if (audioSource == null || !audioSource.isPlaying)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeOutRoutine(duration, curve));
    }

    private IEnumerator FadeOutRoutine(float duration, AnimationCurve curve)
    {
        float startVolume = audioSource.volume;

        if (duration <= 0f)
        {
            audioSource.volume = 0f;
            audioSource.Stop();
            fadeRoutine = null;
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float curveProgress = curve != null && curve.length > 0
                ? curve.Evaluate(progress)
                : progress;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, curveProgress);
            yield return null;
        }

        audioSource.volume = 0f;
        audioSource.Stop();
        fadeRoutine = null;
    }
}

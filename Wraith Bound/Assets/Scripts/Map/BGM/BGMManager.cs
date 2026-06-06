using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    private AudioSource audioSource;
    private string ownerSceneName;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            ownerSceneName = gameObject.scene.name;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
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
        Destroy(gameObject);
    }

    public void StopBGM()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    public void PlayBGM()
    {
        if (audioSource != null)
        {
            audioSource.Play();
        }
    }
}
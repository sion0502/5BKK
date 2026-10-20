using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임오버 화면의 비디오테이프 노이즈 연출: 지직거리는 노이즈·주사선·트래킹 띠·화면 흔들림·테이프 잡음 소리.
/// 사망 시 timeScale=0이므로 모든 연출은 unscaled 시간으로 돈다. 리소스 없이 절차적으로 생성.
/// </summary>
public class GameOverTapeEffects : MonoBehaviour
{
    private const int NoiseWidth = 384;
    private const int NoiseHeight = 216;
    private const float NoiseFrameInterval = 1f / 30f;
    private const int AudioSampleRate = 44100;

    [Tooltip("평소(대기 중) 노이즈 세기")]
    public float idleStrength = 0.07f;
    [Tooltip("최대 소리 크기 (세기 1 기준)")]
    public float maxVolume = 0.32f;

    private RawImage noiseImage;
    private RawImage scanlineImage;
    private RectTransform trackingBand;
    private RectTransform jitterTarget;
    private AudioSource hiss;

    private Texture2D noiseTexture;
    private Texture2D scanlineTexture;
    private Color32[] noisePixels;
    private float noiseTimer;

    private float baseStrength = 1f;
    private float burstStrength;
    private float burstEnd;
    private float nextRandomBurst;
    private bool randomBursts;

    /// <summary>지금 화면에 적용되는 노이즈 세기(0~1).</summary>
    public float Strength => Mathf.Max(baseStrength, Time.unscaledTime < burstEnd ? burstStrength : 0f);

    public void Build(RectTransform overlayParent, RectTransform contentToJitter)
    {
        jitterTarget = contentToJitter;

        noiseTexture = new Texture2D(NoiseWidth, NoiseHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        noisePixels = new Color32[NoiseWidth * NoiseHeight];
        noiseImage = CreateRawImage("TapeNoise", overlayParent, noiseTexture);

        // 4픽셀 주기 가로 줄 (한 줄만 어둡게) — 세로로 반복시켜 주사선
        scanlineTexture = new Texture2D(1, 4, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        scanlineTexture.SetPixels32(new[]
        {
            new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0),
            new Color32(0, 0, 0, 110), new Color32(0, 0, 0, 30)
        });
        scanlineTexture.Apply();
        scanlineImage = CreateRawImage("Scanlines", overlayParent, scanlineTexture);
        scanlineImage.uvRect = new Rect(0f, 0f, 1f, 1080f / 4f);

        var band = new GameObject("TrackingBand", typeof(RectTransform), typeof(Image));
        band.transform.SetParent(overlayParent, false);
        trackingBand = band.GetComponent<RectTransform>();
        trackingBand.anchorMin = new Vector2(0f, 1f);
        trackingBand.anchorMax = new Vector2(1f, 1f);
        trackingBand.pivot = new Vector2(0.5f, 0.5f);
        trackingBand.sizeDelta = new Vector2(0f, 70f);
        var bandImage = band.GetComponent<Image>();
        bandImage.color = new Color(1f, 1f, 1f, 0.05f);
        bandImage.raycastTarget = false;

        hiss = gameObject.AddComponent<AudioSource>();
        hiss.clip = CreateHissClip();
        hiss.loop = true;
        hiss.spatialBlend = 0f;
        hiss.ignoreListenerPause = true;
        hiss.playOnAwake = false;
        hiss.volume = 0f;
        hiss.Play();
    }

    /// <summary>기본 노이즈 세기 설정 (0~1).</summary>
    public void SetBaseStrength(float strength)
    {
        baseStrength = Mathf.Clamp01(strength);
    }

    /// <summary>잠깐 노이즈를 확 키움.</summary>
    public void Burst(float strength, float duration)
    {
        burstStrength = Mathf.Clamp01(strength);
        burstEnd = Time.unscaledTime + duration;
    }

    /// <summary>대기 상태: 약한 노이즈 + 가끔 튀는 지직거림.</summary>
    public void EnterIdle()
    {
        baseStrength = idleStrength;
        randomBursts = true;
        nextRandomBurst = Time.unscaledTime + Random.Range(2f, 4f);
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (randomBursts && now >= nextRandomBurst)
        {
            Burst(Random.Range(0.35f, 0.6f), Random.Range(0.08f, 0.2f));
            nextRandomBurst = now + Random.Range(2.5f, 6f);
        }

        float strength = Strength;

        noiseTimer += Time.unscaledDeltaTime;
        if (noiseTimer >= NoiseFrameInterval)
        {
            noiseTimer = 0f;
            RegenerateNoise(strength);
        }

        scanlineImage.uvRect = new Rect(0f, -now * 0.6f, 1f, 1080f / 4f);

        // 트래킹 띠가 위에서 아래로 천천히 흘러내림
        float bandT = Mathf.Repeat(now * 0.22f, 1.3f) - 0.15f;
        trackingBand.anchoredPosition = new Vector2(0f, -bandT * 1080f);

        if (jitterTarget != null)
        {
            float shake = strength > 0.3f ? (strength - 0.3f) * 40f : 0f;
            jitterTarget.anchoredPosition = new Vector2(
                Random.Range(-shake, shake),
                Random.value < strength * 0.15f ? Random.Range(-6f, 6f) : 0f);
        }

        hiss.volume = maxVolume * Mathf.Lerp(0.15f, 1f, strength) * (strength > 0.01f ? 1f : 0f);
    }

    private void RegenerateNoise(float strength)
    {
        int tearRow = Random.value < strength ? Random.Range(0, NoiseHeight) : -1;
        int tearHeight = Random.Range(2, 8);

        for (int y = 0; y < NoiseHeight; y++)
        {
            bool tear = tearRow >= 0 && y >= tearRow && y < tearRow + tearHeight;
            float rowAlpha = tear ? Mathf.Min(1f, strength * 2.2f) : strength;
            int row = y * NoiseWidth;
            for (int x = 0; x < NoiseWidth; x++)
            {
                byte v = (byte)Random.Range(0, 256);
                byte a = (byte)(Random.value * rowAlpha * 255f);
                noisePixels[row + x] = new Color32(v, v, v, a);
            }
        }

        noiseTexture.SetPixels32(noisePixels);
        noiseTexture.Apply(false);
    }

    private static RawImage CreateRawImage(string name, RectTransform parent, Texture texture)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    // 고역이 강한 테이프 잡음 (화이트 노이즈를 살짝 거르고 낮은 웅웅거림 섞음)
    private static AudioClip CreateHissClip()
    {
        int length = AudioSampleRate * 2;
        var samples = new float[length];
        float smooth = 0f;
        for (int i = 0; i < length; i++)
        {
            float white = Random.Range(-1f, 1f);
            smooth = Mathf.Lerp(smooth, white, 0.55f);
            float hum = Mathf.Sin(2f * Mathf.PI * 60f * i / AudioSampleRate) * 0.08f;
            samples[i] = smooth * 0.5f + hum;
        }

        var clip = AudioClip.Create("GameOverTapeHiss", length, 1, AudioSampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDestroy()
    {
        if (noiseTexture != null) Destroy(noiseTexture);
        if (scanlineTexture != null) Destroy(scanlineTexture);
        if (hiss != null && hiss.clip != null) Destroy(hiss.clip);
    }
}

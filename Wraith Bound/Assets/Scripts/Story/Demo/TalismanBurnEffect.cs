using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 부적이 아래에서 위로 불타 없어지는 연출 (셰이더 추가 없이 URP 기본 셰이더의 알파 클립으로).
///  · 종이(Lit, 알파 클립)와 불씨 띠(Unlit 주황 HDR, 알파 클립)가 같은 '타는 순서' 텍스처(아래일수록 먼저 + 노이즈)를 씀.
///    불씨는 종이 바로 뒤에 있고 컷오프가 종이보다 edgeWidth만큼 앞서 가서, 종이가 막 사라진 가장자리에만 빛나는 띠가 보임.
///  · 글씨(TMP)는 불길이 지나간 글자부터 꺼짐. 불티 파티클·주황 점광이 불길 높이를 따라 올라감.
/// 재질은 에디터에서 알파 클립을 켠 에셋(빌드에 셰이더 변형 포함)을 쓰고, 실행 중 인스턴스로 복제해 값만 바꿈.
/// </summary>
public class TalismanBurnEffect : MonoBehaviour
{
    [SerializeField] private Renderer paper;
    [SerializeField] private Renderer ember;
    [SerializeField] private TMP_Text glyphs;
    [SerializeField] private Light idleGlow;
    [SerializeField] private float duration = 2.2f;
    [SerializeField] private float edgeWidth = 0.13f;
    [SerializeField] private Color fireColor = new Color(1f, 0.55f, 0.18f);

    [Header("Sound (비워 두면 무음)")]
    [SerializeField] private AudioClip burnClip;
    [SerializeField] private float burnVolume = 0.8f;

    public bool IsBurnt { get; private set; }
    public bool IsBurning { get; private set; }

    private static Texture2D burnMask;
    private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    private void Awake()
    {
        if (ember != null)
        {
            ember.enabled = false;
        }
    }

    public void Burn(Action finished = null)
    {
        if (IsBurnt || IsBurning)
        {
            return;
        }

        StartCoroutine(Run(finished));
    }

    /// <summary>연출 없이 바로 타 없어진 상태로 (이미 진행된 상태로 씬을 시작할 때).</summary>
    public void BurnInstant()
    {
        IsBurnt = true;
        SetVisible(false);
    }

    private IEnumerator Run(Action finished)
    {
        IsBurning = true;
        Texture2D mask = BurnMask();
        Material paperMat = paper != null ? paper.material : null;
        Material emberMat = ember != null ? ember.material : null;
        if (paperMat != null)
        {
            paperMat.SetTexture(BaseMapId, mask);
        }

        if (emberMat != null)
        {
            emberMat.SetTexture(BaseMapId, mask);
            ember.enabled = true;
        }

        Vector3 size = PaperSize();
        Light fire = new GameObject("BurnLight").AddComponent<Light>();
        fire.transform.SetParent(transform, false);
        fire.type = LightType.Point;
        fire.color = fireColor;
        fire.range = Mathf.Max(1.5f, size.y * 3.5f);
        fire.shadows = LightShadows.None;
        ParticleSystem sparks = CreateSparks(size);

        AudioSource audio = null;
        if (burnClip != null)
        {
            audio = gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = 1f;
            audio.PlayOneShot(burnClip, burnVolume);
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float b = Mathf.SmoothStep(0f, 1f, t / duration) * (1f + edgeWidth) - edgeWidth * 0.5f;
            if (paperMat != null)
            {
                paperMat.SetFloat(CutoffId, Mathf.Clamp(b + edgeWidth, 0.001f, 1.01f));
            }

            if (emberMat != null)
            {
                emberMat.SetFloat(CutoffId, Mathf.Clamp(b, 0.001f, 1.01f));
            }

            // 마스크 알파 = 0.65·높이 + 0.35·노이즈 → 불길 높이(0~1) ≈ (b − 0.175) / 0.65
            float line = Mathf.Clamp01((b - 0.175f) / 0.65f);
            HideGlyphsBelow(line + 0.06f);
            // 불길 높이의 종이 앞면 위치 (쿼드 로컬: 높이 −0.5~0.5, 앞면 = −z)
            Vector3 linePos = paper != null ? paper.transform.TransformPoint(new Vector3(0f, line - 0.5f, 0f)) : transform.position;
            Vector3 front = paper != null ? -paper.transform.forward : Vector3.up;
            fire.transform.position = linePos + front * Mathf.Max(0.12f, size.x * 0.5f); // 종이 앞면 위로 띄움 — 붙여 두면 가구 속에서 아래만 밝힘
            fire.intensity = (1.2f + Mathf.PerlinNoise(Time.time * 9f, 0.3f) * 1.6f) * Mathf.Clamp01(t * 4f);
            if (sparks != null)
            {
                sparks.transform.position = linePos + front * 0.01f;
            }

            if (idleGlow != null)
            {
                idleGlow.intensity *= 0.95f;
            }

            yield return null;
        }

        SetVisible(false);
        if (sparks != null)
        {
            var emission = sparks.emission;
            emission.enabled = false;
            Destroy(sparks.gameObject, 2f);
        }

        float fade = 0.4f;
        float start = fire.intensity;
        for (float f = 0f; f < fade; f += Time.deltaTime)
        {
            fire.intensity = Mathf.Lerp(start, 0f, f / fade);
            yield return null;
        }

        Destroy(fire.gameObject);
        IsBurning = false;
        IsBurnt = true;
        finished?.Invoke();
    }

    private void SetVisible(bool on)
    {
        if (paper != null)
        {
            paper.enabled = on;
        }

        if (ember != null)
        {
            ember.enabled = false;
        }

        if (glyphs != null)
        {
            glyphs.enabled = on;
        }

        if (idleGlow != null)
        {
            idleGlow.enabled = on;
        }
    }

    // 종이 월드 크기 (x 폭, y 높이)
    private Vector3 PaperSize()
    {
        if (paper == null)
        {
            return new Vector3(0.3f, 0.6f, 0f);
        }

        Vector3 s = paper.transform.lossyScale;
        return new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 0f);
    }

    // 불길이 지나간(종이 아래쪽 기준 높이 < line) 글자를 투명하게
    private void HideGlyphsBelow(float line)
    {
        if (glyphs == null || paper == null)
        {
            return;
        }

        glyphs.ForceMeshUpdate();
        TMP_TextInfo info = glyphs.textInfo;
        bool changed = false;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo c = info.characterInfo[i];
            if (!c.isVisible)
            {
                continue;
            }

            Vector3 mid = (c.bottomLeft + c.topRight) * 0.5f;
            Vector3 world = glyphs.transform.TransformPoint(mid);
            float h = paper.transform.InverseTransformPoint(world).y + 0.5f;
            if (h >= line)
            {
                continue;
            }

            Color32[] colors = info.meshInfo[c.materialReferenceIndex].colors32;
            for (int v = 0; v < 4; v++)
            {
                colors[c.vertexIndex + v].a = 0;
            }

            changed = true;
        }

        if (changed)
        {
            glyphs.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }

    private ParticleSystem CreateSparks(Vector3 size)
    {
        Material mat = Resources.Load<Material>("Roguelite/M_ReviveSpark");
        if (mat == null)
        {
            return null;
        }

        GameObject go = new GameObject("BurnSparks");
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = duration;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x * 0.07f, size.x * 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.35f, 0.08f));
        main.gravityModifier = -0.3f; // 위로 날림
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var emission = ps.emission;
        emission.rateOverTime = 110f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(size.x, 0.02f, 0.02f);
        shape.rotation = paper != null ? (Quaternion.Inverse(transform.rotation) * paper.transform.rotation).eulerAngles : Vector3.zero;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 2f;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
        return ps;
    }

    // 타는 순서 텍스처: 알파 = 0.65·세로 위치 + 0.35·노이즈 (아래부터, 가장자리는 들쭉날쭉)
    private static Texture2D BurnMask()
    {
        if (burnMask != null)
        {
            return burnMask;
        }

        const int w = 64, h = 160;
        burnMask = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "TalismanBurnMask" };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                float n = Mathf.PerlinNoise(u * 4f + 3.1f, v * 9f + 7.7f) * 0.65f + Mathf.PerlinNoise(u * 13f + 1.3f, v * 25f + 0.4f) * 0.35f;
                float a = Mathf.Clamp01(0.65f * v + 0.35f * n);
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        burnMask.SetPixels32(px);
        burnMask.Apply(false, true);
        return burnMask;
    }
}

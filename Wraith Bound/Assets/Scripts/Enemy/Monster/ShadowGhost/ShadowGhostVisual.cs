using UnityEngine;

/// <summary>
/// ShadowGhost 전용 비주얼. 애니메이션 메시 없이 짙은 검은 안개 기운만 만든다.
/// 여러 레이어가 겹쳐 뒤 벽이 거의 안 보일 정도로 짙게 유지한다.
/// </summary>
[DisallowMultipleComponent]
public class ShadowGhostVisual : MonoBehaviour
{
    [Header("Body")]
    [SerializeField] float bodyHeight = 2.55f;
    [SerializeField] float bodyRadius = 1.45f;
    [SerializeField] int coreBillboards = 12;
    [SerializeField] float coreAlpha = 0.82f;
    [SerializeField] float swirlSpeed = 11f;
    [SerializeField] float bobAmount = 0.22f;
    [SerializeField] float bobSpeed = 0.75f;
    [SerializeField] float driftAmount = 0.18f;

    [Header("Particles")]
    [SerializeField] int maxParticles = 140;
    [SerializeField] float particleLife = 3.6f;
    [SerializeField] float particleSize = 2.1f;
    [SerializeField] float emitRate = 48f;

    [Header("Look")]
    [SerializeField] Color fogColor = new Color(0.01f, 0.01f, 0.015f, 0.96f);
    [SerializeField] float density = 1.7f;

    Transform _visualRoot;
    Transform[] _cores;
    Vector3[] _coreLocal;
    Vector3[] _coreScale;
    float[] _corePhase;
    Material _fogMat;
    Texture2D _softTex;
    ParticleSystem _mist;
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int DensityId = Shader.PropertyToID("_Density");

    void Awake()
    {
        Build();
    }

    void OnDestroy()
    {
        if (_fogMat != null)
            Destroy(_fogMat);
        if (_softTex != null)
            Destroy(_softTex);
    }

    void LateUpdate()
    {
        if (_cores == null)
            return;

        Camera cam = Camera.main;
        Quaternion face = cam != null
            ? Quaternion.LookRotation(cam.transform.forward, Vector3.up)
            : Quaternion.identity;
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;

        for (int i = 0; i < _cores.Length; i++)
        {
            Transform core = _cores[i];
            if (core == null)
                continue;

            float spin = Time.time * swirlSpeed + _corePhase[i];
            float breathe = 1f + Mathf.Sin(Time.time * 0.55f + _corePhase[i]) * 0.08f;
            Vector3 offset = _coreLocal[i];
            offset.x += Mathf.Sin(spin * 0.07f + i) * driftAmount;
            offset.y += bob * (0.55f + (i % 3) * 0.18f);
            offset.z += Mathf.Cos(spin * 0.05f + i * 0.7f) * driftAmount;
            core.localPosition = offset;
            core.localScale = _coreScale[i] * breathe;
            core.rotation = face;
        }
    }

    void Build()
    {
        if (_visualRoot != null)
            return;

        _softTex = CreateSoftDisc(128);
        Shader shader = Shader.Find("WraithBound/ShadowGhostFog");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        _fogMat = new Material(shader);
        _fogMat.name = "ShadowGhostFog_Runtime";
        if (_fogMat.HasProperty("_MainTex"))
            _fogMat.SetTexture("_MainTex", _softTex);
        if (_fogMat.HasProperty(ColorId))
            _fogMat.SetColor(ColorId, fogColor);
        if (_fogMat.HasProperty(DensityId))
            _fogMat.SetFloat(DensityId, density);
        if (_fogMat.HasProperty("_NoiseStrength"))
            _fogMat.SetFloat("_NoiseStrength", 0.62f);
        if (_fogMat.HasProperty("_EdgeSoftness"))
            _fogMat.SetFloat("_EdgeSoftness", 0.72f);
        if (_fogMat.HasProperty("_NoiseScale"))
            _fogMat.SetFloat("_NoiseScale", 1.55f);
        if (_fogMat.HasProperty("_Scroll"))
            _fogMat.SetVector("_Scroll", new Vector4(0.045f, 0.028f, -0.035f, 0.04f));
        _fogMat.renderQueue = 3000;

        _visualRoot = new GameObject("ShadowFog").transform;
        _visualRoot.SetParent(transform, false);
        _visualRoot.localPosition = new Vector3(0f, bodyHeight * 0.42f, 0f);

        BuildCoreBillboards();
        BuildMistParticles();
    }

    void BuildCoreBillboards()
    {
        int count = Mathf.Max(6, coreBillboards + 2);
        _cores = new Transform[count];
        _coreLocal = new Vector3[count];
        _coreScale = new Vector3[count];
        _corePhase = new float[count];

        // Dense center mass.
        SpawnCore(0, Vector3.zero, bodyRadius * 3.15f, Mathf.Clamp01(coreAlpha * 1.1f), 0f);
        // Tall eerie column behind/around the mass.
        SpawnCore(1, new Vector3(0f, bodyHeight * 0.08f, 0f), bodyRadius * 2.55f, coreAlpha * 0.9f, 12f);

        for (int i = 2; i < count; i++)
        {
            float t = (i - 2) / (float)Mathf.Max(1, count - 3);
            float height = Mathf.Lerp(-bodyHeight * 0.38f, bodyHeight * 0.48f, t);
            float ring = Mathf.Sin(t * Mathf.PI);
            float angle = i * 2.399963f;
            float radial = bodyRadius * (0.22f + ring * 0.85f);
            Vector3 local = new Vector3(Mathf.Cos(angle) * radial, height, Mathf.Sin(angle) * radial * 0.8f);
            float size = Mathf.Lerp(bodyRadius * 2.1f, bodyRadius * 3.4f, 1f - Mathf.Abs(t - 0.5f) * 1.2f);
            float alpha = coreAlpha * Mathf.Lerp(0.55f, 0.95f, 1f - Mathf.Abs(t - 0.5f));
            SpawnCore(i, local, size, alpha, Random.Range(0f, 100f));
        }
    }

    void SpawnCore(int index, Vector3 local, float size, float alpha, float phase)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "FogCore_" + index;
        Object.Destroy(go.GetComponent<Collider>());

        Transform tr = go.transform;
        tr.SetParent(_visualRoot, false);
        tr.localPosition = local;
        Vector3 scale = Vector3.one * size;
        // Stretch a few outer wisps into tall tendrils.
        if (index > 1 && index % 3 == 0)
            scale = new Vector3(size * 0.85f, size * 1.55f, size);
        tr.localScale = scale;

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = _fogMat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var color = fogColor;
        color.a = alpha;
        var block = new MaterialPropertyBlock();
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);

        _cores[index] = tr;
        _coreLocal[index] = local;
        _coreScale[index] = scale;
        _corePhase[index] = phase;
    }

    void BuildMistParticles()
    {
        var go = new GameObject("FogMist");
        go.transform.SetParent(_visualRoot, false);
        go.transform.localPosition = Vector3.zero;

        _mist = go.AddComponent<ParticleSystem>();
        var main = _mist.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = particleLife;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.28f);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.85f, particleSize * 1.7f);
        main.startColor = new Color(fogColor.r, fogColor.g, fogColor.b, 0.8f);
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = -0.03f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = _mist.emission;
        emission.rateOverTime = emitRate;

        var shape = _mist.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = bodyRadius * 0.95f;
        shape.radiusThickness = 0.7f;

        var colorOverLifetime = _mist.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(fogColor, 0f),
                new GradientColorKey(fogColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.88f, 0.18f),
                new GradientAlphaKey(0.72f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = grad;

        var sizeOverLifetime = _mist.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.7f));

        var velocity = _mist.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.28f, 0.28f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.06f, 0.32f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.28f, 0.28f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _fogMat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = 2f;

        _mist.Play(true);
    }

    static Texture2D CreateSoftDisc(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ShadowGhostSoftDisc",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        float inv = 1f / (size - 1);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = x * inv * 2f - 1f;
                float ny = y * inv * 2f - 1f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                a = Mathf.Pow(a, 1.35f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply(false, true);
        return tex;
    }
}

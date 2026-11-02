using UnityEngine;

/// <summary>
/// ShadowGhost 전용 비주얼. 애니메이션 메시 없이 짙은 검은 안개 기운만 만든다.
/// 여러 레이어가 겹쳐 뒤 벽이 거의 안 보일 정도로 짙게 유지한다.
/// </summary>
[DisallowMultipleComponent]
public class ShadowGhostVisual : MonoBehaviour
{
    [Header("Body")]
    [SerializeField] float bodyHeight = 1.75f;
    [SerializeField] float bodyRadius = 0.72f;
    [SerializeField] int coreBillboards = 7;
    [SerializeField] float coreAlpha = 0.78f;
    [SerializeField] float swirlSpeed = 18f;
    [SerializeField] float bobAmount = 0.08f;
    [SerializeField] float bobSpeed = 1.1f;

    [Header("Particles")]
    [SerializeField] int maxParticles = 70;
    [SerializeField] float particleLife = 2.4f;
    [SerializeField] float particleSize = 1.15f;
    [SerializeField] float emitRate = 28f;

    [Header("Look")]
    [SerializeField] Color fogColor = new Color(0.015f, 0.015f, 0.02f, 0.94f);
    [SerializeField] float density = 1.45f;

    Transform _visualRoot;
    Transform[] _cores;
    Vector3[] _coreLocal;
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
            Vector3 offset = _coreLocal[i];
            offset.x += Mathf.Sin(spin * 0.07f + i) * 0.05f;
            offset.y += bob * (0.55f + (i % 3) * 0.12f);
            offset.z += Mathf.Cos(spin * 0.05f + i * 0.7f) * 0.05f;
            core.localPosition = offset;
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
        _fogMat.renderQueue = 3000;

        _visualRoot = new GameObject("ShadowFog").transform;
        _visualRoot.SetParent(transform, false);
        _visualRoot.localPosition = new Vector3(0f, bodyHeight * 0.48f, 0f);

        BuildCoreBillboards();
        BuildMistParticles();
    }

    void BuildCoreBillboards()
    {
        int count = Mathf.Max(4, coreBillboards + 1);
        _cores = new Transform[count];
        _coreLocal = new Vector3[count];
        _corePhase = new float[count];

        // Extra-dense center slab so walls behind only ghost through faintly.
        SpawnCore(0, Vector3.zero, bodyRadius * 2.6f, Mathf.Clamp01(coreAlpha * 1.08f), 0f);

        for (int i = 1; i < count; i++)
        {
            float t = (i - 1) / (float)Mathf.Max(1, count - 2);
            float height = Mathf.Lerp(-bodyHeight * 0.28f, bodyHeight * 0.34f, t);
            float ring = Mathf.Sin(t * Mathf.PI);
            float angle = i * 2.399963f;
            float radial = bodyRadius * (0.12f + ring * 0.5f);
            Vector3 local = new Vector3(Mathf.Cos(angle) * radial, height, Mathf.Sin(angle) * radial * 0.65f);
            float size = Mathf.Lerp(bodyRadius * 1.7f, bodyRadius * 2.45f, 1f - Mathf.Abs(t - 0.5f) * 1.35f);
            float alpha = coreAlpha * Mathf.Lerp(0.75f, 1f, 1f - Mathf.Abs(t - 0.55f));
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
        tr.localScale = Vector3.one * size;

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
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.7f, particleSize * 1.35f);
        main.startColor = new Color(fogColor.r, fogColor.g, fogColor.b, 0.72f);
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = -0.015f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = _mist.emission;
        emission.rateOverTime = emitRate;

        var shape = _mist.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = bodyRadius * 0.55f;
        shape.radiusThickness = 0.85f;

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
                new GradientAlphaKey(0.82f, 0.2f),
                new GradientAlphaKey(0.78f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = grad;

        var sizeOverLifetime = _mist.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.75f, 1f, 1.35f));

        var velocity = _mist.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.04f, 0.18f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

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

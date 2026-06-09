using UnityEngine;
using UnityEngine.Rendering;

public class CompassLogic : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private Transform exitTarget;
    public Transform needle; // 나침반 바늘 (인스펙터에서 할당)
    public float rotationSpeed = 8f; // 바늘이 돌아가는 속도
    [SerializeField] private Transform directionReference;
    [SerializeField] private float needleYawOffset = 180f;

    [Header("바늘 발광 (어두운 곳 가시성)")]
    [SerializeField] private bool applyNeedleEmission = true;
    [Tooltip("비우면 바늘 Base Map(T_Compass_01_BC)을 Emission Map으로 사용해 방향 표시를 유지합니다.")]
    [SerializeField] private Texture2D needleEmissionMapOverride;
    [SerializeField] private Color needleEmissionColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    private Quaternion initialNeedleLocalRotation;
    private Renderer needleRenderer;
    private MaterialPropertyBlock needleEmissionBlock;

    void Awake()
    {
        if (needle != null)
        {
            initialNeedleLocalRotation = needle.localRotation;
        }

        CacheNeedleRenderer();
        SetupNeedleEmission();
    }

    void OnDisable()
    {
        ClearNeedleEmission();
    }

    void OnEnable() // 활성화될 때마다 타겟 확인
    {
        if (exitTarget == null)
        {
            GameObject exitObj = GameObject.FindGameObjectWithTag("Exit");
            if (exitObj != null) exitTarget = exitObj.transform;
        }

        if (directionReference == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                directionReference = playerObj.transform;
            }
            else if (Camera.main != null)
            {
                directionReference = Camera.main.transform;
            }
        }
    }

    void Update()
    {
        if (exitTarget == null || needle == null || directionReference == null) return;

        Vector3 toExit = exitTarget.position - directionReference.position;
        toExit.y = 0f;

        Vector3 forward = directionReference.forward;
        forward.y = 0f;

        if (toExit.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float signedAngle = Vector3.SignedAngle(forward.normalized, toExit.normalized, Vector3.up);
        Quaternion targetLocalRot = initialNeedleLocalRotation * Quaternion.Euler(0f, signedAngle + needleYawOffset, 0f);
        needle.localRotation = Quaternion.Slerp(needle.localRotation, targetLocalRot, Time.deltaTime * rotationSpeed);
    }

    private void CacheNeedleRenderer()
    {
        if (needle == null)
        {
            return;
        }

        Transform needleMesh = needle.Find("Needle");
        if (needleMesh != null)
        {
            needleRenderer = needleMesh.GetComponent<Renderer>();
            return;
        }

        needleRenderer = needle.GetComponent<Renderer>();
        if (needleRenderer == null)
        {
            needleRenderer = needle.GetComponentInChildren<Renderer>(true);
        }
    }

    private void SetupNeedleEmission()
    {
        if (!applyNeedleEmission || needleRenderer == null)
        {
            return;
        }

        // 이 렌더러에만 머티리얼 인스턴스를 만들어 Emission 키워드를 켭니다. 공유 .mat 에셋 파일은 수정하지 않습니다.
        Material[] instancedMaterials = needleRenderer.materials;
        bool appliedAny = false;

        for (int i = 0; i < instancedMaterials.Length; i++)
        {
            Material mat = instancedMaterials[i];
            Texture emissionMap = ResolveNeedleEmissionMap(mat);
            if (mat == null || emissionMap == null)
            {
                continue;
            }

            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetTexture(EmissionMapId, emissionMap);
            mat.SetColor(EmissionColorId, Color.black);

            needleEmissionBlock ??= new MaterialPropertyBlock();
            needleRenderer.GetPropertyBlock(needleEmissionBlock, i);
            needleEmissionBlock.SetTexture(EmissionMapId, emissionMap);
            needleEmissionBlock.SetColor(EmissionColorId, needleEmissionColor);
            needleRenderer.SetPropertyBlock(needleEmissionBlock, i);
            appliedAny = true;
        }

        if (appliedAny)
        {
            needleRenderer.materials = instancedMaterials;
        }
    }

    private Texture ResolveNeedleEmissionMap(Material mat)
    {
        if (needleEmissionMapOverride != null)
        {
            return needleEmissionMapOverride;
        }

        if (mat == null)
        {
            return null;
        }

        return mat.GetTexture(BaseMapId) ?? mat.GetTexture(MainTexId);
    }

    private void ClearNeedleEmission()
    {
        if (needleRenderer == null)
        {
            return;
        }

        for (int i = 0; i < needleRenderer.sharedMaterials.Length; i++)
        {
            needleRenderer.SetPropertyBlock(null, i);
        }
    }
}
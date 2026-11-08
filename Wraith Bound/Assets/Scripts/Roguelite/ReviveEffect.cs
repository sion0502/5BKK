using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기 '구사일생' 부활 이펙트 — 금빛 화면 번쩍임 + 플레이어 둘레로 금빛 불티가 터져 퍼짐 + 빛 펄스 + 카메라 흔들림.
/// 불티 재질은 Resources/Roguelite/M_ReviveSpark (빌드에서 셰이더가 빠지지 않게 에셋으로 둠). 소리 슬롯 없음(사운드는 나중에).
/// </summary>
public class ReviveEffect : MonoBehaviour
{
    private static readonly Color Gold = new Color(1f, 0.82f, 0.38f);

    public static void Play(GameObject player)
    {
        if (player == null)
        {
            return;
        }

        ReviveEffect host = new GameObject("ReviveEffect").AddComponent<ReviveEffect>();
        host.StartCoroutine(Run(host.gameObject, player));
    }

    private static IEnumerator Run(GameObject host, GameObject player)
    {
        Camera cam = player.GetComponentInChildren<Camera>();
        Vector3 chest = player.transform.position + Vector3.up * 1.2f;

        // 화면 번쩍임
        Canvas canvas = new GameObject("Flash").AddComponent<Canvas>();
        canvas.transform.SetParent(host.transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        Image flash = new GameObject("Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        flash.transform.SetParent(canvas.transform, false);
        RectTransform rect = flash.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        flash.raycastTarget = false;

        // 불티
        Sparks(host.transform, chest);

        // 빛 펄스
        Light light = new GameObject("Pulse").AddComponent<Light>();
        light.transform.SetParent(host.transform, false);
        light.transform.position = chest;
        light.type = LightType.Point;
        light.color = Gold;
        light.range = 9f;

        Quaternion camBase = cam != null ? cam.transform.localRotation : Quaternion.identity;
        for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
        {
            float k = t / 1.2f;
            flash.color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Lerp(0.85f, 0f, Mathf.Sqrt(k)));
            light.intensity = Mathf.Lerp(12f, 0f, k);
            if (cam != null && k < 0.4f)
            {
                float s = (0.4f - k) * 6f;
                cam.transform.localRotation = camBase * Quaternion.Euler(Random.Range(-s, s), Random.Range(-s, s), 0f);
            }

            yield return null;
        }

        Destroy(host, 2f);
    }

    private static void Sparks(Transform parent, Vector3 position)
    {
        GameObject go = new GameObject("Sparks");
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(Gold, new Color(1f, 0.55f, 0.2f));
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 220) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Resources.Load<Material>("Roguelite/M_ReviveSpark");
        ps.Play();
    }
}

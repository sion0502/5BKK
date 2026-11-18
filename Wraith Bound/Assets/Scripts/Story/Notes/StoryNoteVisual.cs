using UnityEngine;

/// <summary>
/// 스토리 쪽지 모양 공용 — 바랜 종이 쿼드(실행 중 만든 '손글씨 줄' 텍스처). 맵에 떨어진 쪽지(StoryNotePickup)와 허브 게시판(HubNoteArchive)이 같이 씀.
/// 쿼드 앞면 = 로컬 −z (Unity Quad).
/// </summary>
public static class StoryNoteVisual
{
    private static Material paper;

    public static Material PaperMaterial
    {
        get
        {
            if (paper == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                paper = new Material(lit) { name = "M_StoryNote_Runtime" };
                paper.SetTexture("_BaseMap", MakeTexture());
                paper.SetColor("_BaseColor", new Color(0.86f, 0.82f, 0.7f));
                paper.SetFloat("_Smoothness", 0.05f);
                paper.SetFloat("_Cull", 0f); // 양면
                paper.EnableKeyword("_EMISSION");
                paper.SetColor("_EmissionColor", new Color(0.05f, 0.045f, 0.035f)); // 어둠 속에서 아주 희미하게 보이게
            }

            return paper;
        }
    }

    /// <summary>parent 아래 종이 한 장. size = 폭×높이(m).</summary>
    public static GameObject CreatePaper(Transform parent, Vector2 size)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(quad.GetComponent<Collider>());
        quad.name = "Paper";
        quad.transform.SetParent(parent, false);
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        MeshRenderer mr = quad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = PaperMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return quad;
    }

    // 누런 종이 + 얼룩 + 흐린 잉크 줄(손글씨처럼 들쭉날쭉)
    private static Texture2D MakeTexture()
    {
        const int w = 96, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "T_StoryNote", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        var rnd = new System.Random(19);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y)) / 10f;
                float v = 0.82f + 0.14f * n - (edge < 1f ? (1f - edge) * 0.25f : 0f);
                px[y * w + x] = new Color(v, v * 0.96f, v * 0.86f, 1f);
            }
        }

        for (int line = 0; line < 11; line++)
        {
            int ly = h - 16 - line * 10;
            int len = (int)(w * (0.55f + 0.3f * (float)rnd.NextDouble()));
            for (int x = 10; x < 10 + len && x < w - 8; x++)
            {
                if (rnd.NextDouble() < 0.18)
                {
                    continue; // 글자 사이 틈
                }

                int yy = ly + (int)(Mathf.Sin(x * 0.7f + line) * 1.2f);
                for (int t = 0; t < 2; t++)
                {
                    int i = (yy + t) * w + x;
                    if (i >= 0 && i < px.Length)
                    {
                        px[i] = new Color(0.22f, 0.2f, 0.22f, 1f);
                    }
                }
            }
        }

        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }
}

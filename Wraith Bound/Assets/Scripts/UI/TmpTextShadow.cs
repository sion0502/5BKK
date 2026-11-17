using TMPro;
using UnityEngine;

/// <summary>
/// TMP 글자에 그림자(Underlay)를 넣어 밝은/어두운 배경 모두에서 읽히게 합니다.
/// Pol 폰트는 획이 얇아 SDF 값이 0.55 정도까지밖에 안 올라가서, outlineWidth를 주면 글자 면이 전부
/// 외곽선 색으로 덮여 검게 보입니다(→ 글자가 안 보임). 외곽선 대신 이 그림자를 쓰세요.
/// </summary>
public static class TmpTextShadow
{
    public static void Apply(TMP_Text label, float offset = 0.6f, float dilate = 0.35f, float softness = 0.35f, float alpha = 0.85f)
    {
        if (label == null)
        {
            return;
        }

        label.outlineWidth = 0f;
        Material material = label.fontMaterial; // 인스턴스 생성 — 공용 폰트 머티리얼은 건드리지 않음
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, alpha));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, offset);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -offset);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, dilate);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
        label.fontMaterial = material;
    }
}

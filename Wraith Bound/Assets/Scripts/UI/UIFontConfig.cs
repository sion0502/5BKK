using TMPro;
using UnityEngine;

/// <summary>
/// 용도별 폰트 (Resources/UIFontConfig.asset). 2026-11-17 사용자 확정 '안 A':
///  · UI       = Pretendard — HUD·인벤·조작 안내·튜토리얼·상호작용 문구·메뉴·기 화면·게임오버 선택지·화면 안내
///  · Story    = 마루부리(명조) — 대사 자막·챕터 타이틀·인쇄된 문서(인트로 기획서)
///  · Handwriting = Pol 손글씨 — 일지·쪽지 등 손으로 쓴 문서
///  · 제목 鬼行·부적 = TitleFontLoader(붓글씨, 런타임 동적 폰트) / 캠코더 OSD = LiberationSans(영문 기계 표시)
/// Pol·마루부리엔 '…' 등이 없을 수 있어 Pretendard UI를 대체 글꼴로 걸어 둠.
/// </summary>
[CreateAssetMenu(fileName = "UIFontConfig", menuName = "UI/Font Config")]
public class UIFontConfig : ScriptableObject
{
    public TMP_FontAsset polHumanRights;
    public TMP_FontAsset ui;
    public TMP_FontAsset uiBold;
    public TMP_FontAsset story;
    public TMP_FontAsset storyBold;

    private const string UiPath = "Assets/TextMesh Pro/Fonts/Pretandard/Pretendard-Regular UI SDF.asset";
    private const string UiBoldPath = "Assets/TextMesh Pro/Fonts/Pretandard/Pretendard-Bold UI SDF.asset";
    private const string StoryPath = "Assets/TextMesh Pro/Fonts/MaruBuri/MaruBuri-Regular SDF.asset";
    private const string StoryBoldPath = "Assets/TextMesh Pro/Fonts/MaruBuri/MaruBuri-Bold SDF.asset";
    private const string PolPath = "Assets/TextMesh Pro/Fonts/Pol_HumanRight/Griun_PolHumanrights-Rg SDF.asset";

    private static UIFontConfig instance;

    private static UIFontConfig Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<UIFontConfig>("UIFontConfig");
            }

            return instance;
        }
    }

    /// <summary>손글씨(Pol). 예전 이름 유지 — 일지·쪽지 같은 손으로 쓴 문서에만.</summary>
    public static TMP_FontAsset PolHumanRights => Pick(Instance != null ? Instance.polHumanRights : null, PolPath);
    public static TMP_FontAsset Handwriting => PolHumanRights;
    public static TMP_FontAsset UI => Pick(Instance != null ? Instance.ui : null, UiPath);
    public static TMP_FontAsset UIBold => Pick(Instance != null ? Instance.uiBold : null, UiBoldPath) ?? UI;
    public static TMP_FontAsset Story => Pick(Instance != null ? Instance.story : null, StoryPath) ?? UI;
    public static TMP_FontAsset StoryBold => Pick(Instance != null ? Instance.storyBold : null, StoryBoldPath) ?? Story;

    private static TMP_FontAsset Pick(TMP_FontAsset assigned, string editorPath)
    {
        TMP_FontAsset font = assigned;
#if UNITY_EDITOR
        if (font == null)
        {
            font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(editorPath);
        }
#endif
        return font != null ? font : TMP_Settings.defaultFontAsset;
    }
}

using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "UIFontConfig", menuName = "UI/Font Config")]
public class UIFontConfig : ScriptableObject
{
    public TMP_FontAsset polHumanRights;

    private static UIFontConfig instance;

    public static TMP_FontAsset PolHumanRights
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<UIFontConfig>("UIFontConfig");
            }

            return instance != null ? instance.polHumanRights : null;
        }
    }
}

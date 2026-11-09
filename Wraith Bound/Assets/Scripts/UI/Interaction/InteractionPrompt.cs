/// <summary>
/// 조준한 상호작용 대상의 안내 (키 + 행동). 크로스헤어 링·문구 표시에 씀.
/// IInteractable의 기존 프롬프트("[E] 손전등 획득" 등)는 FromLegacyPrompt로 통일된 형태("E" + "손전등 줍기")로 바꿈.
/// </summary>
public readonly struct InteractionPrompt
{
    public readonly string Key;     // 비어 있으면 키 배지 없음 (예: 잠긴 문 "잠겨 있다")
    public readonly string Action;

    public static readonly InteractionPrompt None = default;

    public InteractionPrompt(string key, string action)
    {
        Key = key;
        Action = action;
    }

    public bool IsValid => !string.IsNullOrEmpty(Action);

    public static InteractionPrompt E(string action) => new InteractionPrompt("E", action);

    /// <summary>"[E] 손전등 획득" → (E, 손전등 줍기). 대괄호가 없으면 키 없이 문구만.</summary>
    public static InteractionPrompt FromLegacyPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return None;
        }

        string text = prompt.Trim();
        string key = null;
        if (text.StartsWith("["))
        {
            int close = text.IndexOf(']');
            if (close > 0)
            {
                key = text.Substring(1, close - 1).Trim();
                text = text.Substring(close + 1).Trim();
            }
        }
        else if (text.EndsWith("]"))
        {
            // "Open [E]"처럼 키가 뒤에 붙은 문 기본 문구
            int openBracket = text.LastIndexOf('[');
            if (openBracket > 0)
            {
                key = text.Substring(openBracket + 1, text.Length - openBracket - 2).Trim();
                text = text.Substring(0, openBracket).Trim();
            }
        }

        // "[Error] ..." 같은 개발용 문구는 표시하지 않음
        if (key != null && key.Length > 1 && key != "LMB" && key != "RMB")
        {
            return None;
        }

        return new InteractionPrompt(key, NormalizeAction(text));
    }

    /// <summary>예전 문구를 행동 중심 한국어로 (획득 → 줍기, 영어 기본값 → 한국어).</summary>
    private static string NormalizeAction(string text)
    {
        switch (text)
        {
            case "Switch": return "스위치 켜기";
            case "Unlock": return "잠금 해제";
            case "Open": return "열기";
            case "Close": return "닫기";
        }

        text = text.Replace(" 획득 (", " 줍기 (");
        if (text.EndsWith(" 획득") || text.EndsWith(" 회수"))
        {
            text = text.Substring(0, text.Length - 3) + " 줍기";
        }

        return text;
    }
}

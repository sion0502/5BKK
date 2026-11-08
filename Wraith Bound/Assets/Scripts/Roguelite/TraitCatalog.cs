using System.Collections.Generic;

/// <summary>특성 "기" 종류 (팀 기획 13종 — 팀 노션 '기' 페이지).</summary>
public enum TraitId
{
    RecklessSprint, // 무리한 질주
    Strongman,      // 괴력난신
    StarterPack,    // 스타터팩
    BornStrong,     // 타고난 신체
    HeavyPack,      // 무거운 배낭
    RunningMan,     // 런닝맨
    NarrowEscape,   // 구사일생
    Adrenaline,     // 아드레날린
    Ordeal,         // 시련
    ThornWings,     // 가시날개
    K2C1,           // K2C1
    Map,            // 지도
    Lockpick,       // 락픽 툴
}

/// <summary>기 하나의 정의 — 이름·효과(팀 기획 원문)·해금 조건(진행 카운터가 목표치에 닿으면 해금).</summary>
public sealed class TraitDef
{
    public TraitId Id;
    public string Name;
    public string Effect;
    public string UnlockText;
    public string Counter;   // TraitProgress 카운터 키
    public int Goal;
    public bool Implemented; // 시연 범위 밖이면 false — 목록엔 잠긴 채 '추후 공개'
    public bool SingleSlot;  // 착용하면 기를 1개만 (구사일생·시련)
}

/// <summary>
/// 기 13종 목록. 효과 문구는 팀 기획 그대로, 해금 조건은 사용자 승인(2026-11-09).
/// 시연에는 9종만 실제로 동작 — 괴력난신·지도·K2C1·시련은 잠긴 칸으로만 보임.
/// </summary>
public static class TraitCatalog
{
    public const string ChapterDeaths = "chapterDeaths";
    public const string Deaths = "deaths";
    public const string Chase30 = "chase30";
    public const string LowHpReturn = "lowHpReturn";
    public const string FoodEaten = "food";
    public const string InventoryFull = "inventoryFull";
    public const string Hides = "hides";
    public const string LockedDoorsOpened = "lockedOpened";
    public const string KeyNeeded = "keyNeeded";

    public static readonly IReadOnlyList<TraitDef> All = new List<TraitDef>
    {
        Def(TraitId.RecklessSprint, "무리한 질주", "이동 속도가 50% 증가하는 대신 소모 스태미나와 소모 체력이 50% 증가한다.",
            "추격을 30초 이상 버티고 살아남기", Chase30, 1),
        Def(TraitId.Strongman, "괴력난신", "단 한 번, 자신의 체력이 100일 때 추적자와 부딪혔을 때 추적자를 무력화할 수 있다.",
            null, null, 0, implemented: false),
        Def(TraitId.StarterPack, "스타터팩", "랜덤으로 패시브 아이템 1개를 가지고 시작한다.",
            "챕터에서 처음 죽기", ChapterDeaths, 1),
        Def(TraitId.BornStrong, "타고난 신체", "체력의 80%를 소모해서 잠긴 문을 파괴한다.",
            "열쇠 없이 잠긴 문을 열려고 하기", KeyNeeded, 3),
        Def(TraitId.HeavyPack, "무거운 배낭", "인벤토리 최대 소지량이 2칸 증가하지만 기본 이동 속도가 25% 감소한다.",
            "인벤토리를 가득 채우기", InventoryFull, 1),
        Def(TraitId.RunningMan, "런닝맨", "모든 음식이 체력 대신 스태미나를 회복시켜준다. 음식과 에너지 드링크의 획득 확률이 늘어난다.",
            "음식 먹기", FoodEaten, 5),
        Def(TraitId.NarrowEscape, "구사일생", "죽으면 체력 30으로 한 번 부활할 수 있으나 기를 1개만 착용할 수 있다.",
            "죽기", Deaths, 3, singleSlot: true),
        Def(TraitId.Adrenaline, "아드레날린", "체력이 20% 이하일 때 괴물에게 발각되면 3초 동안 달릴 때 스태미나를 소모하지 않는다.",
            "체력 20% 이하에서 살아남아 허브로 돌아오기", LowHpReturn, 1),
        Def(TraitId.Ordeal, "시련", "최대 체력이 99 감소하고 스태미나 회복 속도가 0이 된다. 기를 1개만 착용할 수 있다. 이 기를 착용하고 모든 챕터를 1번씩 클리어하면 K2C1을 획득할 수 있다.",
            null, null, 0, implemented: false, singleSlot: true),
        Def(TraitId.ThornWings, "가시날개", "앉아있을 때 이동 속도가 30% 증가한다. 걸어다닐 때 이동 속도가 50% 감소한다.",
            "숨기", Hides, 5),
        Def(TraitId.K2C1, "K2C1", "충전형 아이템 탄알집이 맵에 생성되고, K2C1이 기본템으로 지급된다. 이 기를 착용하고 챕터 클리어 시 챕터 기록이 반영되지 않는다.",
            null, null, 0, implemented: false),
        Def(TraitId.Map, "지도", "근처 반경 5미터를 표시하는 미니맵이 표시된다. 미니맵 활성화 중에는 소리를 들을 수 없다.",
            null, null, 0, implemented: false),
        Def(TraitId.Lockpick, "락픽 툴", "20% 확률로 열쇠를 소모하지 않고 문을 열 수 있다.",
            "잠긴 문 열기", LockedDoorsOpened, 3),
    };

    public static TraitDef Get(TraitId id)
    {
        foreach (TraitDef def in All)
        {
            if (def.Id == id)
            {
                return def;
            }
        }

        return null;
    }

    private static TraitDef Def(TraitId id, string name, string effect, string unlock, string counter, int goal,
        bool implemented = true, bool singleSlot = false)
    {
        return new TraitDef
        {
            Id = id, Name = name, Effect = effect, UnlockText = unlock, Counter = counter, Goal = goal,
            Implemented = implemented, SingleSlot = singleSlot,
        };
    }
}

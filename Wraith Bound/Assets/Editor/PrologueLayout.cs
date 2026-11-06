using UnityEngine;

/// <summary>
/// 프롤로그 맵 좌표 (바닥 y=0, 1칸 4m). 맵·이벤트 빌더가 같이 씀.
///
///   ② 진료실 ── 복도 A ══잠긴 문══ ③ 병실(트롤리 숨기) ── 뒷문 ── ④ 긴 복도 B ──────── 비상구(잠김)
///   x−4~4        x4~8                x8~16                         x16~48, z28~32       x=48
///   z20~28       z16~32              z20~32
///   (일지+열쇠)    │ 북쪽 끝 z=32 철문 → 크리처 대기실(x4~8, z32~36)
///              ① 로비 x0~12, z0~16 (남쪽 병원 정문 x3~9, 잠김)
/// </summary>
public static class PrologueLayout
{
    // ① 로비
    public static readonly Vector3 FlashlightSpot = new Vector3(3.0f, 0f, 8.4f);

    // 복도 A — E1 캠코더로만 보이는 환자 (철문 앞)
    public static readonly Vector3 ApparitionSpot = new Vector3(6.0f, 0f, 30.6f);
    public static readonly Bounds CorridorAEntryZone = new Bounds(new Vector3(6f, 1f, 18.5f), new Vector3(4f, 2f, 3f));

    // ② 진료실 — 일지 #0, 그 옆에 병실 열쇠
    public static readonly Vector3 JournalDeskSpot = new Vector3(-1.4f, 0f, 25.6f);
    public static readonly Vector3 KeyDeskSpot = new Vector3(-0.85f, 0f, 25.7f); // 책상 x−2.4~−0.4, z24.5~26.1

    // 철문(복도 A 북쪽 끝)과 크리처 대기 위치
    public static readonly Vector3 NurseSpawn = new Vector3(6.0f, 0f, 34.0f);

    // ③ 병실 — 입구(복도 A 동쪽 z24~28)는 잠긴 문, 숨는 곳은 본편 트롤리(앞면 +z)
    public static readonly Vector3 HideSpotPosition = new Vector3(13.2f, 0f, 21.0f);
    public const float HideSpotYaw = 0f;
    public static readonly Vector3 HidePassStart = new Vector3(11.0f, 0f, 22.9f);  // 크리처가 트롤리 앞을 지나가는 경로
    public static readonly Vector3 HidePassFront = new Vector3(14.3f, 0f, 22.7f);
    public static readonly Vector3 WardEntrance = new Vector3(9.4f, 0f, 26.0f);   // 복도 A → 병실 문 안쪽
    public static readonly Vector3 WardBackDoor = new Vector3(15.6f, 0f, 30.0f);  // 병실 → 복도 B

    // ④ 긴 복도 B
    public static readonly Bounds CorridorBMidZone = new Bounds(new Vector3(30f, 1f, 30f), new Vector3(2f, 2f, 4f));
    public static readonly Vector3 CorridorBStart = new Vector3(17.5f, 0f, 30.0f);  // 들켰을 때 크리처가 나타나는 곳(플레이어 뒤)
    public static readonly Bounds ExitDoorZone = new Bounds(new Vector3(46.4f, 1f, 30f), new Vector3(3.2f, 2f, 4f));
    public static readonly Vector3 ExitDoorFace = new Vector3(48f, 1.4f, 30f);
}

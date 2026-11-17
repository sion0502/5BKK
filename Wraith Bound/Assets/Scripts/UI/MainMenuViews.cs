using UnityEngine;

/// <summary>
/// 첫 플레이 메인 메뉴에서 버튼마다 카메라가 날아가 보여 줄 시점들 (Intro 씬 = HH 데모 외부 맵 좌표).
/// 씬에 이름이 "MenuView_&lt;Id&gt;"인 오브젝트가 있으면 그 위치·회전을 우선 사용합니다.
/// Intro 씬을 열고 Tools > Story > Create Menu View Anchors 로 기본값 위치에 앵커를 만든 뒤,
/// Scene 뷰에서 원하는 구도를 잡고 앵커 선택 → GameObject > Align With View (Ctrl+Shift+F) 하면 됩니다.
/// </summary>
public static class MainMenuViews
{
    public const string AnchorPrefix = "MenuView_";

    public enum Id
    {
        Home,
        NewGame,
        Continue,
        Settings,
        Quit,
    }

    public struct View
    {
        public Vector3 Position;
        public Vector3 LookAt;

        public View(Vector3 position, Vector3 lookAt)
        {
            Position = position;
            LookAt = lookAt;
        }

        public Quaternion Rotation => Quaternion.LookRotation(LookAt - Position, Vector3.up);
    }

    // 기본 구도 (IntroSceneBuilder의 경로 A/B 좌표 근처 — 길 위라서 장애물에 덜 걸림)
    public static View Default(Id id)
    {
        switch (id)
        {
            // 검문소 게이트 앞: 안개 속 멀리 병원 건물
            case Id.Home: return new View(new Vector3(38.5f, 2.4f, 94f), new Vector3(8f, 9f, 40f));
            // 병원 입구 바로 앞: 유리 너머 로비의 희미한 불
            case Id.NewGame: return new View(new Vector3(12f, 1.7f, 5.6f), new Vector3(0.5f, 1.8f, 5.5f));
            // 주차장: 버려진 차들 너머 병원 정면
            case Id.Continue: return new View(new Vector3(30f, 1.8f, 15f), new Vector3(2f, 4f, 8f));
            // 홈 시점에서 몇 걸음만 옆으로 — 설정 창을 보는 동안 배경이 크게 바뀌지 않게
            case Id.Settings: return new View(new Vector3(36.8f, 2.0f, 92.2f), new Vector3(14f, 5f, 52f));
            // 병원을 등지고 섬 밖으로 나가는 길
            case Id.Quit: return new View(new Vector3(40.4f, 1.7f, 100f), new Vector3(41.5f, 1.5f, 130f));
            default: return Default(Id.Home);
        }
    }

    public static string AnchorName(Id id)
    {
        return AnchorPrefix + id;
    }
}

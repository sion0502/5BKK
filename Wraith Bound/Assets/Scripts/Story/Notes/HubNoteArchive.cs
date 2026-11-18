using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 허브(귀문의 방) 뒷벽의 쪽지 게시판 (2026-11-19 사용자 요청 '한번 획득한 일지들은 허브에서 하나씩 볼 수 있게').
/// 20칸(5×4) — 읽은 쪽지는 종이로 걸리고 E로 다시 읽음, 안 읽은 칸은 빈 핀 자리만. 위에 '남겨진 기록 n/20'.
/// 왼쪽엔 깨어난 자리의 기록자 쪽지(HubNoteText)가 항상 크게 걸림 — 재입장 땐 바닥 쪽지가 없어 여기서 읽음.
/// 허브 씬에 이 컴포넌트를 둔 오브젝트(`HubNoteArchive`)가 있으면 그 위치·방향에 게시판을 만듦 — 씬 뷰에서 옮기면 됨(편집 중엔 기즈모로 판 테두리·칸 표시,
/// 로컬 +z = 방 쪽). 메뉴 `Tools > Story > Place Hub Note Archive`로 배치. 씬에 없으면 실행 중 기본 자리(뒷벽 (100,0,−5.76))에 자동 생성(예전 방식, 대비용).
/// </summary>
public class HubNoteArchive : MonoBehaviour
{
    private const string HubScene = "Hub";
    private static readonly Vector3 WallCenter = new Vector3(100f, 0f, -5.76f); // 뒷벽 안쪽 면(z −5.80, 실측) 바로 앞
    private const int Columns = 5;
    private const int Rows = 4;
    private static readonly Vector2 Paper = new Vector2(0.3f, 0.4f);
    private static readonly Vector2 Gap = new Vector2(0.14f, 0.12f);
    private const float BottomY = 0.75f;

    private TextMeshPro header;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreate(scene);
    }

    private static void TryCreate(Scene scene)
    {
        if (scene.name != HubScene || FindFirstObjectByType<HubNoteArchive>() != null)
        {
            return;
        }

        var go = new GameObject("HubNoteArchive");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetPositionAndRotation(WallCenter, Quaternion.identity); // +z(방 안쪽)를 봄
        go.AddComponent<HubNoteArchive>();
    }

    private void Start()
    {
        Build();
        StoryNoteProgress.Changed += Rebuild;
    }

    private void OnDestroy()
    {
        StoryNoteProgress.Changed -= Rebuild;
    }

    private void Rebuild()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        Build();
    }

    public static Vector3 DefaultPosition => WallCenter;

    private static float Width => Columns * Paper.x + (Columns - 1) * Gap.x;
    private static float Height => Rows * Paper.y + (Rows - 1) * Gap.y;

    // 깨어난 자리의 기록자 쪽지(HubNoteText) — 20칸 왼쪽에 크게, 항상 걸려 있음 (2026-11-19 '바닥 일지도 게시판에')
    private static readonly Vector2 FirstPaper = new Vector2(0.38f, 0.52f);
    private static float FirstX => Width * 0.5f + Gap.x + 0.06f + FirstPaper.x * 0.5f; // 방 쪽에서 보면 왼쪽
    private static float BoardLeft => -Width * 0.5f - 0.15f;
    private static float BoardRight => FirstX + FirstPaper.x * 0.5f + 0.15f;
    private static Vector3 BoardCenter => new Vector3((BoardLeft + BoardRight) * 0.5f, BottomY + Height * 0.5f, 0.02f);
    private static Vector3 BoardSize => new Vector3(BoardRight - BoardLeft, Height + 0.6f, 0.04f);

#if UNITY_EDITOR
    // 편집 중 위치 잡기용 — 판 테두리·칸·방 쪽 화살표
    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.9f, 0.7f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(BoardCenter, BoardSize);
        Gizmos.DrawCube(new Vector3(FirstX, BottomY + Height - FirstPaper.y * 0.5f, 0.05f), new Vector3(FirstPaper.x, FirstPaper.y, 0.005f));
        Gizmos.color = new Color(0.95f, 0.9f, 0.75f, 0.35f);
        for (int i = 0; i < Columns * Rows; i++)
        {
            float x = Width * 0.5f - Paper.x * 0.5f - (i % Columns) * (Paper.x + Gap.x);
            float y = BottomY + Height - Paper.y * 0.5f - (i / Columns) * (Paper.y + Gap.y);
            Gizmos.DrawCube(new Vector3(x, y, 0.05f), new Vector3(Paper.x, Paper.y, 0.005f));
        }

        Gizmos.color = Color.cyan;
        Vector3 c = new Vector3(0f, BottomY + Height * 0.5f, 0.05f);
        Gizmos.DrawLine(c, c + Vector3.forward * 0.6f); // 방 쪽(앞면)
    }
#endif

    private void Build()
    {
        float width = Width;
        float height = Height;

        // 코르크판 느낌의 어두운 나무판
        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        board.transform.SetParent(transform, false);
        board.transform.localPosition = BoardCenter;
        board.transform.localScale = BoardSize;
        Destroy(board.GetComponent<Collider>());
        var boardMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        boardMat.SetColor("_BaseColor", new Color(0.2f, 0.14f, 0.1f));
        boardMat.SetFloat("_Smoothness", 0.1f);
        board.GetComponent<MeshRenderer>().sharedMaterial = boardMat;

        header = CreateText(transform, "Header", new Vector3(BoardCenter.x, BottomY + height + 0.15f, 0.045f), 0.9f);
        header.text = "남겨진 기록  " + StoryNoteProgress.Count + " / " + StoryNoteCatalog.Count;

        var pinMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        pinMat.SetColor("_BaseColor", new Color(0.55f, 0.08f, 0.06f));

        for (int i = 0; i < StoryNoteCatalog.Count; i++)
        {
            int col = i % Columns;
            int row = i / Columns;
            float x = width * 0.5f - Paper.x * 0.5f - col * (Paper.x + Gap.x); // 방 쪽(+z)에서 보면 로컬 +x가 왼쪽 — 1번이 왼쪽 위
            float y = BottomY + height - Paper.y * 0.5f - row * (Paper.y + Gap.y);
            var slot = new GameObject("Note_" + (i + 1)).transform;
            slot.SetParent(transform, false);
            slot.localPosition = new Vector3(x, y, 0.05f); // 방 쪽 = +z, 판(0~0.04) 앞

            // 핀
            GameObject pin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(pin.GetComponent<Collider>());
            pin.transform.SetParent(slot, false);
            pin.transform.localPosition = new Vector3(0f, Paper.y * 0.42f, 0.01f);
            pin.transform.localScale = Vector3.one * 0.03f;
            pin.GetComponent<MeshRenderer>().sharedMaterial = pinMat;

            if (!StoryNoteProgress.Has(i))
            {
                TextMeshPro empty = CreateText(slot, "Empty", new Vector3(0f, 0f, 0.002f), 0.7f);
                empty.text = "?";
                empty.color = new Color(1f, 1f, 1f, 0.12f);
                continue;
            }

            GameObject paper = StoryNoteVisual.CreatePaper(slot, Paper);
            paper.transform.localRotation = Quaternion.Euler(0f, 180f, (i * 37 % 7) - 3f); // 앞면(−z)이 방 쪽(+z)을 보게, 조금씩 삐뚤게
            TextMeshPro num = CreateText(slot, "Num", new Vector3(0f, -Paper.y * 0.36f, 0.003f), 0.5f);
            num.text = (i + 1).ToString();
            num.color = new Color(0.25f, 0.2f, 0.18f);

            BoxCollider box = slot.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(Paper.x, Paper.y, 0.08f);
            slot.gameObject.AddComponent<HubNoteArchiveSlot>().Init(i);
        }

        // 기록자 쪽지 (항상)
        var first = new GameObject("FirstNote").transform;
        first.SetParent(transform, false);
        first.localPosition = new Vector3(FirstX, BottomY + height - FirstPaper.y * 0.5f, 0.05f);
        GameObject firstPin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(firstPin.GetComponent<Collider>());
        firstPin.transform.SetParent(first, false);
        firstPin.transform.localPosition = new Vector3(0f, FirstPaper.y * 0.44f, 0.01f);
        firstPin.transform.localScale = Vector3.one * 0.035f;
        firstPin.GetComponent<MeshRenderer>().sharedMaterial = pinMat;
        GameObject firstPaper = StoryNoteVisual.CreatePaper(first, FirstPaper);
        firstPaper.transform.localRotation = Quaternion.Euler(0f, 180f, 2f);
        TextMeshPro firstLabel = CreateText(first, "Label", new Vector3(0f, -FirstPaper.y * 0.5f - 0.08f, 0.003f), 0.55f);
        firstLabel.text = "기록자";
        BoxCollider firstBox = first.gameObject.AddComponent<BoxCollider>();
        firstBox.size = new Vector3(FirstPaper.x, FirstPaper.y, 0.08f);
        first.gameObject.AddComponent<HubNoteArchiveSlot>().Init(HubNoteArchiveSlot.FirstNoteId);

        // 게시판을 비추는 희미한 등
        var lamp = new GameObject("BoardLight").AddComponent<Light>();
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = new Vector3(BoardCenter.x, BottomY + height + 0.9f, 1.1f);
        lamp.transform.localRotation = Quaternion.Euler(60f, 180f, 0f);
        lamp.type = LightType.Spot;
        lamp.spotAngle = 75f;
        lamp.range = 4f;
        lamp.intensity = 3f;
        lamp.color = new Color(1f, 0.85f, 0.62f);
        lamp.shadows = LightShadows.None;
    }

    private static TextMeshPro CreateText(Transform parent, string name, Vector3 localPos, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // TMP 앞면(−z)이 방 쪽을 보게
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        TMP_FontAsset font = UIFontConfig.Handwriting;
        if (font != null)
        {
            t.font = font;
        }

        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.9f, 0.85f, 0.75f);
        t.rectTransform.sizeDelta = new Vector2(3f, 0.5f);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }
}

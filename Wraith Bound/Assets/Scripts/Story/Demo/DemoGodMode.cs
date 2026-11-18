using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 시연용 갓모드 — Ctrl+Shift+G로 켜고 끔(빌드에서도 동작, 씬을 넘어가도 유지).
/// 켜져 있으면 적(EnemyBase)에게 잡혀도 점프스케어 연출만 나오고 죽지 않음 → 조작 복구 후 계속 플레이.
/// 한 번 잡은 적은 그 뒤로 다시 잡지 못함(그 적의 RuntimeState.PlayerDeadLogged를 켜 둠 — EnemySense가 잡기 전에 확인하는 값).
/// 다른 적은 계속 잡을 수 있게 되살아날 때 그 값을 풀어 줌(되살리기가 모든 적에 켜 버리기 때문).
/// 프롤로그 간호사(EnemyBase 아님, 고정 사망 연출)는 영향 없음. 구사일생보다 먼저 확인 — 갓모드면 기를 쓰지 않음.
/// 켜져 있는 동안 왼쪽 위에 작게 'GOD' 표시.
/// </summary>
public class DemoGodMode : MonoBehaviour
{
    public static bool Enabled { get; private set; }

    private static readonly HashSet<EnemyBase> Immune = new HashSet<EnemyBase>();
    private static DemoGodMode instance;
    private TextMeshProUGUI badge;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null)
        {
            return;
        }

        GameObject go = new GameObject("DemoGodMode");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DemoGodMode>();
        PlayerDeathDebug.GodModeHandler = TryRescue;
        SceneManager.sceneLoaded += (s, m) =>
        {
            if (m == LoadSceneMode.Single)
            {
                Immune.Clear();
            }
        };
    }

    private void Awake()
    {
        Canvas canvas = MenuUIUtility.CreateOverlayCanvas("GodModeCanvas", 990, transform);
        badge = TraitUIFactory.Label(canvas.transform, "Badge", "GOD", UIFontConfig.UIBold, 20f, new Color(1f, 0.35f, 0.3f, 0.75f),
            Vector2.zero, new Vector2(80f, 28f), TextAlignmentOptions.Left);
        RectTransform rect = badge.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(14f, -8f);
        badge.enabled = Enabled;
    }

    private void Update()
    {
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (ctrl && shift && Input.GetKeyDown(KeyCode.G))
        {
            SetEnabled(!Enabled);
        }
    }

    /// <summary>갓모드에서 한 번 잡아 이제 못 잡는 적인지.</summary>
    public static bool IsImmune(EnemyBase enemy)
    {
        return enemy != null && Immune.Contains(enemy);
    }

    public static void SetEnabled(bool on)
    {
        Enabled = on;
        if (!on)
        {
            Immune.Clear();
        }

        if (instance != null && instance.badge != null)
        {
            instance.badge.enabled = on;
        }

        ScreenMessageUI.Show(on ? "갓모드 켜짐 — 잡혀도 죽지 않음" : "갓모드 꺼짐", 1.8f);
        Debug.Log("[GodMode] " + (on ? "ON" : "OFF"));
    }

    /// <summary>PlayerDeathDebug가 죽기 직전에 부름. 갓모드면 되살리고 true.</summary>
    private static bool TryRescue()
    {
        if (!Enabled)
        {
            return false;
        }

        EnemyBase catcher = PlayerDeathDebug.LastCatcher;
        if (catcher != null)
        {
            Immune.Add(catcher);
        }

        PlayerDeathDebug.ReviveFromJumpscare();

        // 되살리기가 모든 적을 '이미 잡음'으로 만들어 두므로, 잡았던 적만 남기고 풀어 줌
        foreach (EnemyBase enemy in FindObjectsByType<EnemyBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (enemy != null && enemy.RuntimeState != null)
            {
                enemy.RuntimeState.PlayerDeadLogged = Immune.Contains(enemy);
            }
        }

        ScreenMessageUI.Show("갓모드 — " + (catcher != null ? catcher.name : "적") + "에게서 벗어났다", 2f);
        Debug.Log("[GodMode] 사망 무시 — 다시 못 잡는 적 " + Immune.Count + "마리");
        return true;
    }
}

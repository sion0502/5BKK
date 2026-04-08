using UnityEngine;
using UnityEngine.UI;

public class RadarDot : MonoBehaviour
{
    private Image img;
    private float timer;

    void Awake()
    {
        img = GetComponent<Image>();
        // 처음에는 안 보이게 설정 (Alpha = 0)
        img.color = new Color(img.color.r, img.color.g, img.color.b, 0);
    }

    // 스캔 라인이 이 위치를 지날 때 RadarSystem이 호출해줄 함수
    public void ShowDot()
    {
        timer = 1.2f; // 1.2초 동안 불이 들어왔다 사라짐
    }

    void Update()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
            // 타이머에 따라 서서히 투명해지게 설정
            img.color = new Color(img.color.r, img.color.g, img.color.b, timer);
        }
        else if (img.color.a > 0)
        {
            img.color = new Color(img.color.r, img.color.g, img.color.b, 0);
        }
    }
}
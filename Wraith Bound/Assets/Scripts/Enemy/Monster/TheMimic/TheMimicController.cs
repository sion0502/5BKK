using UnityEngine;

public class TheMimicController : MonoBehaviour
{
    Animator anim;

    bool isRushing;
    float timer;

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
    }

    public bool IsRushing()
    {
        return isRushing;
    }

    public void StartRush()
    {
        if (isRushing) return;

        isRushing = true;

        transform.position -= transform.forward * 0.8f;

        anim.SetFloat("Speed", 1f);

        timer = 1.2f;
    }

    void Update()
    {
        if (!isRushing) return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            isRushing = false;

            anim.SetFloat("Speed", 0.7f);
        }
    }
}
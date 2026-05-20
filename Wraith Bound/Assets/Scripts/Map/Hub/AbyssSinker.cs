using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class AbyssSinker : MonoBehaviour
{
    public string nextSceneName = "Chapter1";
    public float waitTime = 1.0f;      // 툭 멈췄을 때의 정적 시간
    public float fallSpeed = 15.0f;    // 쑤욱 빨려 들어가는 속도 (이전보다 빠르게)
    public float totalDuration = 4.0f; // 전체 연출 시간 (1초 대기 + 3초 추락 등)

    private bool isWorking = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isWorking || !other.CompareTag("Player")) return;

        isWorking = true;
        StartCoroutine(StartFallingSequence(other.gameObject));
    }

    IEnumerator StartFallingSequence(GameObject player)
    {
        // 1. [툭!] 모든 조작과 물리 정지
        MonoBehaviour[] scripts = player.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != this) script.enabled = false;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // [추가 연출] 이때 심장박동 소리나 "툭" 하는 짧은 효과음을 넣으면 베스트입니다.
        // 예: AudioSource.PlayClipAtPoint(thudSound, player.transform.position);

        // 2. [정적] 1초간 그 자리에 고정 (무슨 일이 일어날지 모르는 공포)
        yield return new WaitForSeconds(waitTime);

        // 3. [쑤욱!] 빠른 속도로 추락 시작
        float timer = 0f;
        while (timer < totalDuration - waitTime)
        {
            timer += Time.deltaTime;
            
            // 아래로 빠르게 이동
            player.transform.position += Vector3.down * fallSpeed * Time.deltaTime;
            
            // 추락할수록 더 빨라지는 느낌을 주고 싶다면 아래처럼 가속도를 붙일 수도 있습니다.
            // player.transform.position += Vector3.down * (fallSpeed + timer * 5f) * Time.deltaTime;

            yield return null;
        }

        // 4. 씬 전환
        SceneManager.LoadScene(nextSceneName);
    }
}
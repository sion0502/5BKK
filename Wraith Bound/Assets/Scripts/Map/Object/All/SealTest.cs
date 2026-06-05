using UnityEngine;

public class SealTest : MonoBehaviour
{
    public SealOrb[] seals;

    private int currentIndex = 0;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            if (currentIndex >= seals.Length)
                return;

            seals[currentIndex].BreakSeal();

            currentIndex++;
        }
    }
}
using UnityEngine;

[System.Flags]
public enum Dir
{
    None = 0,
    Up = 1,
    Down = 2,
    Left = 4,
    Right = 8,
    Everything = 15
}

public class Tile : MonoBehaviour
{
    public Dir openings;
}

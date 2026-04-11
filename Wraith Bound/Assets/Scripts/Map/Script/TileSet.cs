using UnityEngine;

[CreateAssetMenu(fileName = "NewTileSet", menuName = "Custom/TileSet")]
public class TileSet : ScriptableObject
{
    public GameObject[] tiles;
}

using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "HouseGenData", menuName = "House Generation")]
public class HouseGenerationData : SerializedScriptableObject
{
    [Header("Cell Prefab Lists – Ground Floor")]
    [Tooltip("Plain wall sections for ground floor")]
    public GameObject[] groundWallPrefabs;
    [Tooltip("Window sections for ground floor")]
    public GameObject[] groundWindowPrefabs;
    [Tooltip("Door sections for ground floor")]
    public GameObject[] groundDoorPrefabs;
    [Tooltip("Corner sections for ground floor")]
    public GameObject[] groundCornerPrefabs;

    [Header("Cell Prefab Lists – Upper Floors")]
    [Tooltip("Plain wall sections for upper floors")]
    public GameObject[] upperWallPrefabs;
    [Tooltip("Window sections for upper floors")]
    public GameObject[] upperWindowPrefabs;
    [Tooltip("Door sections for upper floors")]
    public GameObject[] upperDoorPrefabs;
    [Tooltip("Corner sections for upper floors")]
    public GameObject[] upperCornerPrefabs;

    public Vector3 cellSize;

}

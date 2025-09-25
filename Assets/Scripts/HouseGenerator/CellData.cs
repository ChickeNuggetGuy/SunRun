using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CellData
{
    public Vector3Int coords;
    public Vector3 worldPos;
    public Vector3 direction;
    public GameObject gameObject;
    public List<CellData> neightbors;
    public HouseGenerator.HouseGeneratorCellType cellType;

    public CellData(Vector3Int coords, Vector3 worldPos, Vector3 direction, GameObject gameObject, List<CellData> neightbors, HouseGenerator.HouseGeneratorCellType cellType)
    {
        this.coords = coords;
        this.worldPos = worldPos;
        this.direction = direction;
        this.gameObject = gameObject;
        this.neightbors = neightbors;
        this.cellType = cellType;
    }
}
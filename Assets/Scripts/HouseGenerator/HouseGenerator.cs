using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEngine;
using Sirenix.Serialization;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class HouseGenerator : SerializedMonoBehaviour
{
  [Header("Prefabs")]
  public HouseGenerationData prefabData;

  [Header("Layout")]
  [MinValue(1)]
  [OnValueChanged(nameof(OnFloorsChanged))]
  public int floors = 1;

  [MinValue(1)]
  [OnValueChanged(nameof(OnGridSizeModified))]
  public Vector2Int GridSize = new Vector2Int(10, 8);

  [Header("Generation Mode")]
  [ToggleLeft]
  [LabelText("Auto Generate")]
  [OnValueChanged(nameof(OnGenerationModeChanged))]
  public bool useAutoGenerate = true;

  [ShowIf("@useAutoGenerate")]
  [EnumToggleButtons]
  [LabelText("Front Faces")]
  public FrontDirection frontDirection = FrontDirection.South;

  [ShowIf("@useAutoGenerate")]
  [MinValue(1)]
  [LabelText("Window Every N Perimeter Cells")]
  public int windowSpacing = 2;

  [ShowIf("@useAutoGenerate")]
  [LabelText("Place Windows On Ground Floor")]
  public bool windowsOnGroundFloor = true;

  [ShowIf("@useAutoGenerate")]
  [LabelText("Upper Floors Match Ground")]
  public bool upperFloorsCopyGround = true;

  [ShowIf("@useAutoGenerate && !upperFloorsCopyGround")]
  [MinMaxSlider(0, 3, true)]
  [LabelText("Offshoot Variance Per Upper Floor")]
  public Vector2Int upperFloorOffshootVariance = new Vector2Int(0, 1);

  [ShowIf("@useAutoGenerate")]
  [BoxGroup("Auto Layout Settings")]
  [LabelText("Use Random Seed")]
  public bool useRandomSeed = true;

  [ShowIf("@useAutoGenerate && !useRandomSeed")]
  [BoxGroup("Auto Layout Settings")]
  public int seed = 12345;

  [ShowIf("@useAutoGenerate")]
  [BoxGroup("Auto Layout Settings")]
  [MinMaxSlider(2, 32, true)]
  [LabelText("Base Size (W,H)")]
  public Vector2Int baseSizeMin = new Vector2Int(5, 4);

  [ShowIf("@useAutoGenerate")]
  [BoxGroup("Auto Layout Settings")]
  public Vector2Int baseSizeMax = new Vector2Int(7, 6);

  [ShowIf("@useAutoGenerate")]
  [BoxGroup("Auto Layout Settings")]
  [MinMaxSlider(0, 8, true)]
  [LabelText("Offshoot Count (Min..Max)")]
  public Vector2Int offshootCountRange = new Vector2Int(1, 3);

  [ShowIf("@useAutoGenerate")]
  [BoxGroup("Auto Layout Settings")]
  [MinMaxSlider(1, 16, true)]
  [LabelText("Offshoot Size (W,H) Min")]
  public Vector2Int offshootSizeMin = new Vector2Int(2, 2);

  [ShowIf("@useAutoGenerate")]
  [BoxGroup("Auto Layout Settings")]
  public Vector2Int offshootSizeMax = new Vector2Int(5, 4);

  [ShowIf("@useAutoGenerate")]
  [BoxGroup("Auto Layout Settings")]
  [LabelText("Allow Notches/Indents")]
  public bool allowNotches = true;

  [ShowIf("@useAutoGenerate && allowNotches")]
  [BoxGroup("Auto Layout Settings")]
  [MinMaxSlider(0, 4, true)]
  [LabelText("Notch Count (Min..Max)")]
  public Vector2Int notchCountRange = new Vector2Int(0, 1);

  [ShowIf("@useAutoGenerate && allowNotches")]
  [BoxGroup("Auto Layout Settings")]
  [MinMaxSlider(1, 8, true)]
  [LabelText("Notch Size (W,H) Min")]
  public Vector2Int notchSizeMin = new Vector2Int(2, 2);

  [ShowIf("@useAutoGenerate && allowNotches")]
  [BoxGroup("Auto Layout Settings")]
  public Vector2Int notchSizeMax = new Vector2Int(4, 3);

  [ShowIf("@!useAutoGenerate")]
  [InfoBox(
    "Manual Floor Editor is visible when Auto Generate is OFF.\n" +
    "Ctrl: Wall, Alt: Window, Shift: Door, No modifier: Empty."
  )]
  [ListDrawerSettings(
    ShowFoldout = false,
    DraggableItems = false,
    ShowPaging = true,
    ShowIndexLabels = true,
    HideAddButton = true,
    NumberOfItemsPerPage = 1,
    HideRemoveButton = true
  )]
  [SerializeField]
  public FloorConfig[] Floors;

  [Button(ButtonSizes.Medium)]
  [ShowIf("@useAutoGenerate")]
  public void RerollShape()
  {
    seed = UnityEngine.Random.Range(int.MinValue / 2, int.MaxValue / 2);
    GenerateGrid();
  }

  [HideInInspector]
  public List<GameObject> spawnedCells = new List<GameObject>();

  // Runtime per-cell data
  private CellData[][,] cellData;

  public enum HouseGeneratorCellType
  {
    Straight,
    Corner,
    Internal,
    Peninsula,
    Bridge,
    Empty
  }

  public enum FeatureType
  {
    Empty = 0,
    Wall = 1,
    Window = 2,
    Door = 3
  }

  public enum FrontDirection
  {
    North, // +Z
    South, // -Z
    East, // +X
    West // -X
  }

  [System.Serializable]
  public class FloorConfig
  {
    [TableMatrix(
      HorizontalTitle = "Features",
      DrawElementMethod = "DrawFeatureElement",
      ResizableColumns = false,
      RowHeight = 16,
      SquareCells = true
    )]
    [ShowInInspector]
    [NonSerialized]
    public FeatureType[,] Features;

    [Button("Clear Floor", ButtonSizes.Small)]
    public void ClearFloor()
    {
      if (Features == null)
        return;
      int w = Features.GetLength(0), h = Features.GetLength(1);
      for (int x = 0; x < w; x++)
        for (int z = 0; z < h; z++)
          Features[x, z] = FeatureType.Empty;
    }

#if UNITY_EDITOR
    // Odin drawer: paint with shortcuts, no cycling
    public static FeatureType DrawFeatureElement(Rect rect, FeatureType value)
    {
      Event e = Event.current;

      if (
        (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
        && e.button == 0
        && rect.Contains(e.mousePosition)
      )
      {
        FeatureType newValue = FeatureType.Empty;
        if (e.shift)
          newValue = FeatureType.Door;
        else if (e.alt)
          newValue = FeatureType.Window;
        else if (e.control)
          newValue = FeatureType.Wall;
        else
          newValue = FeatureType.Empty;

        if (value != newValue)
        {
          value = newValue;
          GUI.changed = true;
        }

        e.Use();
      }

      if (e.type == EventType.Repaint && rect.Contains(e.mousePosition))
      {
        EditorGUI.DrawRect(rect.Padding(1), new Color(1, 1, 1, 0.3f));
      }

      Color col;
      switch (value)
      {
        case FeatureType.Empty:
          col = new Color(0, 0, 0, 0.3f);
          break;
        case FeatureType.Wall:
          col = new Color(0.5f, 0.5f, 0.5f, 1);
          break;
        case FeatureType.Window:
          col = Color.yellow;
          break;
        case FeatureType.Door:
          col = new Color(0.6f, 0.3f, 0);
          break;
        default:
          col = Color.clear;
          break;
      }
      EditorGUI.DrawRect(rect.Padding(1), col);

      return value;
    }
#endif
  }

#if UNITY_EDITOR
  // Legend under the inspector
  [OnInspectorGUI]
  private void DrawFeatureLegend()
  {
    GUILayout.Space(10);
    EditorGUILayout.LabelField("Feature Legend", EditorStyles.boldLabel);
    DrawLegendEntry("Empty", new Color(0, 0, 0, 0.3f));
    DrawLegendEntry("Wall", new Color(0.5f, 0.5f, 0.5f, 1));
    DrawLegendEntry("Window", Color.yellow);
    DrawLegendEntry("Door", new Color(0.6f, 0.3f, 0));

    GUILayout.Space(10);
    EditorGUILayout.LabelField("Drawing Shortcuts", EditorStyles.boldLabel);
    EditorGUILayout.HelpBox(
      "Hold & Drag: Paint\n" +
      "Ctrl: Paint Wall\n" +
      "Alt: Paint Window\n" +
      "Shift: Paint Door\n" +
      "No Modifier: Paint Empty",
      MessageType.Info
    );
  }

  private void DrawLegendEntry(string name, Color col)
  {
    EditorGUILayout.BeginHorizontal();
    Rect r = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
    EditorGUI.DrawRect(r, col);
    GUILayout.Label(" " + name, GUILayout.ExpandWidth(false));
    EditorGUILayout.EndHorizontal();
  }
#endif

  private void OnGenerationModeChanged()
  {
    // No-op: we keep manual data intact when toggling modes.
  }

  private void OnFloorsChanged()
  {
    int oldCount = (Floors != null) ? Floors.Length : 0;
    var newArr = new FloorConfig[floors];

    for (int i = 0; i < Mathf.Min(oldCount, floors); i++)
      newArr[i] = Floors[i];

    for (int i = oldCount; i < floors; i++)
    {
      var fc = new FloorConfig
      {
        Features = new FeatureType[GridSize.x, GridSize.y]
      };
      if (i > 0 && newArr[i - 1]?.Features != null)
      {
        var prev = newArr[i - 1].Features;
        int w = prev.GetLength(0), h = prev.GetLength(1);
        for (int x = 0; x < w; x++)
          for (int z = 0; z < h; z++)
            if (prev[x, z] != FeatureType.Empty)
              fc.Features[x, z] = FeatureType.Wall;
      }
      newArr[i] = fc;
    }

    Floors = newArr;
  }

  private void OnGridSizeModified()
  {
    if (Floors == null)
      return;
    foreach (var fc in Floors)
    {
      var oldFeatures = fc.Features;
      var newFeatures = new FeatureType[GridSize.x, GridSize.y];

      if (
        oldFeatures != null
        && oldFeatures.GetLength(0) > 0
        && oldFeatures.GetLength(1) > 0
      )
      {
        int copyX = Mathf.Min(oldFeatures.GetLength(0), GridSize.x);
        int copyZ = Mathf.Min(oldFeatures.GetLength(1), GridSize.y);
        for (int x = 0; x < copyX; x++)
          for (int z = 0; z < copyZ; z++)
            newFeatures[x, z] = oldFeatures[x, z];
      }
      fc.Features = newFeatures;
    }
  }

  [Button]
  public void GenerateGrid()
  {
    EnsureFloorData();

    if (useAutoGenerate)
      AutoPopulateFeaturesSuburban();

    cellData = new CellData[floors][,];
    ClearCells();

    for (int f = 0; f < floors; f++)
    {
      var feats = Floors[f].Features;
      cellData[f] = new CellData[GridSize.x, GridSize.y];

      for (int x = 0; x < GridSize.x; x++)
        for (int z = 0; z < GridSize.y; z++)
        {
          var coords = new Vector3Int(x, f, z);
          var localPos =
            new Vector3(
              x * prefabData.cellSize.x,
              f * prefabData.cellSize.y,
              z * prefabData.cellSize.z
            );

          if (feats[x, z] == FeatureType.Empty)
          {
            cellData[f][x, z] =
              new CellData(
                coords,
                localPos,
                Vector3.zero,
                null,
                new List<CellData>(),
                HouseGeneratorCellType.Empty
              );
            continue;
          }

          bool n = IsOccupied(f, x, z + 1);
          bool s = IsOccupied(f, x, z - 1);
          bool e = IsOccupied(f, x + 1, z);
          bool w = IsOccupied(f, x - 1, z);
          int neighborCount =
            (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);

          HouseGeneratorCellType ct;
          if (neighborCount == 4)
            ct = HouseGeneratorCellType.Internal;
          else if (neighborCount == 3)
            ct = HouseGeneratorCellType.Bridge;
          else if (neighborCount == 2)
            ct = ((n && s) || (e && w))
              ? HouseGeneratorCellType.Straight
              : HouseGeneratorCellType.Corner;
          else if (neighborCount == 1)
            ct = HouseGeneratorCellType.Peninsula;
          else
            ct = HouseGeneratorCellType.Empty;

          Vector3 dir = Vector3.zero;
          if (!e)
            dir += Vector3.right;
          if (!w)
            dir += Vector3.left;
          if (!n)
            dir += Vector3.forward;
          if (!s)
            dir += Vector3.back;
          if (ct == HouseGeneratorCellType.Corner)
          {
            if (!e && !n)
              dir = new Vector3(1, 0, 1);
            else if (!e && !s)
              dir = new Vector3(1, 0, -1);
            else if (!w && !n)
              dir = new Vector3(-1, 0, 1);
            else if (!w && !s)
              dir = new Vector3(-1, 0, -1);
          }

          var neigh = GetNeighbors(f, x, z);
          cellData[f][x, z] =
            new CellData(coords, localPos, dir, null, neigh, ct);
        }
    }

    InstantiateCells();
  }

  public void InstantiateCells()
  {
    for (int f = 0; f < floors; f++)
      for (int x = 0; x < GridSize.x; x++)
        for (int z = 0; z < GridSize.y; z++)
        {
          var cell = cellData[f][x, z];
          if (
            cell.cellType == HouseGeneratorCellType.Empty
            || cell.cellType == HouseGeneratorCellType.Internal
          )
            continue;

          GameObject prefab = null;
          bool isGround = (f == 0);

          if (cell.cellType == HouseGeneratorCellType.Corner)
          {
            var list =
              isGround
                ? prefabData.groundCornerPrefabs
                : prefabData.upperCornerPrefabs;
            prefab = RandomFrom(list);
          }
          else
          {
            var feat = Floors[f].Features[x, z];
            switch (feat)
            {
              case FeatureType.Door:
                prefab =
                  RandomFrom(
                    isGround
                      ? prefabData.groundDoorPrefabs
                      : prefabData.upperDoorPrefabs
                  );
                break;
              case FeatureType.Window:
                prefab =
                  RandomFrom(
                    isGround
                      ? prefabData.groundWindowPrefabs
                      : prefabData.upperWindowPrefabs
                  );
                break;
              default:
                prefab =
                  RandomFrom(
                    isGround
                      ? prefabData.groundWallPrefabs
                      : prefabData.upperWallPrefabs
                  );
                break;
            }
          }

          if (prefab != null)
            InstantiateCell(prefab, cell);
        }
  }

  private GameObject RandomFrom(GameObject[] arr)
  {
    if (arr == null || arr.Length == 0)
      return null;
    return arr[Random.Range(0, arr.Length)];
  }

  public void ClearCells()
  {
#if UNITY_EDITOR
    foreach (var go in spawnedCells)
      if (go != null)
        if (Application.isPlaying)
          Destroy(go);
        else
          DestroyImmediate(go);
    spawnedCells.Clear();
#endif
  }

  public void InstantiateCell(GameObject prefab, CellData cell)
  {
    Vector3 localPos = cell.worldPos;
    Quaternion localRot = Quaternion.identity;
    var d = cell.direction;

    if (d == new Vector3(1, 0, 1))
    {
      localPos +=
        new Vector3(prefabData.cellSize.x, 0, prefabData.cellSize.z);
      localRot = Quaternion.Euler(0, -90, 0);
    }
    else if (d == new Vector3(1, 0, -1))
      localPos += new Vector3(prefabData.cellSize.x, 0, 0);
    else if (d == new Vector3(-1, 0, -1))
      localRot = Quaternion.Euler(0, 90, 0);
    else if (d == new Vector3(-1, 0, 1))
    {
      localPos += new Vector3(0, 0, prefabData.cellSize.z);
      localRot = Quaternion.Euler(0, 180, 0);
    }
    else if (d == Vector3.right)
      localPos += new Vector3(prefabData.cellSize.x, 0, 0);
    else if (d == Vector3.left)
      localPos += new Vector3(0, 0, prefabData.cellSize.z);
    else if (d == Vector3.forward)
      localPos +=
        new Vector3(prefabData.cellSize.x, 0, prefabData.cellSize.z);

    var obj =
      Instantiate(
        prefab,
        transform.TransformPoint(localPos),
        transform.rotation * localRot,
        transform
      );
    if (cell.cellType != HouseGeneratorCellType.Corner)
    {
      var worldForward =
        transform.TransformDirection(d == Vector3.zero
          ? Vector3.forward
          : d);
      if (worldForward != Vector3.zero)
        obj.transform.forward = worldForward;
    }

    cell.gameObject = obj;
    spawnedCells.Add(obj);
  }

  private bool IsOccupied(int f, int x, int z)
  {
    if (x < 0 || x >= GridSize.x || z < 0 || z >= GridSize.y)
      return false;
    return Floors[f].Features[x, z] != FeatureType.Empty;
  }

  private List<CellData> GetNeighbors(int f, int x, int z)
  {
    var list = new List<CellData>();
    for (int dx = -1; dx <= 1; dx++)
      for (int dz = -1; dz <= 1; dz++)
      {
        if ((dx == 0 && dz == 0) || (dx != 0 && dz != 0))
          continue;
        if (!IsOccupied(f, x + dx, z + dz))
          continue;
        list.Add(cellData[f][x + dx, z + dz]);
      }
    return list;
  }

  private List<CellData> GetCellOfType(
    HouseGeneratorCellType type,
    out int count
  )
  {
    var outList = new List<CellData>();
    count = 0;
    foreach (var floor in cellData)
      for (int x = 0; x < floor.GetLength(0); x++)
        for (int z = 0; z < floor.GetLength(1); z++)
          if (floor[x, z].cellType == type)
          {
            outList.Add(floor[x, z]);
            count++;
          }
    return outList;
  }

  // ----------------------------
  // Auto-generation internals
  // ----------------------------

  private void EnsureFloorData()
  {
    if (Floors == null || Floors.Length != floors)
      OnFloorsChanged();

    if (Floors == null)
      Floors = new FloorConfig[floors];

    for (int i = 0; i < floors; i++)
    {
      if (Floors[i] == null)
        Floors[i] = new FloorConfig();
      var feats = Floors[i].Features;
      if (
        feats == null
        || feats.GetLength(0) != GridSize.x
        || feats.GetLength(1) != GridSize.y
      )
      {
        Floors[i].Features = new FeatureType[GridSize.x, GridSize.y];
      }
    }
  }

  private void AutoPopulateFeaturesSuburban()
  {
    System.Random rng =
      useRandomSeed ? new System.Random(Guid.NewGuid().GetHashCode())
      : new System.Random(seed);

    // Build one ground footprint and reuse for upper floors (optional).
    bool[,] groundOcc = BuildCompoundFootprint(rng);

    for (int f = 0; f < floors; f++)
    {
      bool[,] occ =
        (f == 0 || upperFloorsCopyGround)
          ? CloneOcc(groundOcc)
          : BuildCompoundFootprintWithVariance(rng, f);

      // Convert occupancy to features: walls/windows/doors
      var feats = Floors[f].Features;
      for (int x = 0; x < GridSize.x; x++)
        for (int z = 0; z < GridSize.y; z++)
          feats[x, z] = occ[x, z] ? FeatureType.Wall : FeatureType.Empty;

      // Doors: front + back on ground, windows on upper floors at those spots
      Vector2Int frontDoor = PickDoorOnSide(occ, true);
      Vector2Int backDoor = PickDoorOnSide(occ, false);

      if (f == 0)
      {
        if (IsInsideGrid(frontDoor))
          feats[frontDoor.x, frontDoor.y] = FeatureType.Door;
        if (IsInsideGrid(backDoor))
          feats[backDoor.x, backDoor.y] = FeatureType.Door;
      }
      else
      {
        if (IsInsideGrid(frontDoor) && occ[frontDoor.x, frontDoor.y])
          feats[frontDoor.x, frontDoor.y] = FeatureType.Window;
        if (IsInsideGrid(backDoor) && occ[backDoor.x, backDoor.y])
          feats[backDoor.x, backDoor.y] = FeatureType.Window;
      }

      // Windows on perimeter (skip door cells)
      for (int x = 0; x < GridSize.x; x++)
      {
        for (int z = 0; z < GridSize.y; z++)
        {
          if (!occ[x, z])
            continue;

          bool isPerim = IsPerimeterCell(occ, x, z);
          if (!isPerim)
            continue;

          if (
            (f == 0 && !windowsOnGroundFloor)
            || (IsDoorCell(frontDoor, x, z) || IsDoorCell(backDoor, x, z))
          )
            continue;

          // Simple, even distribution along perimeter
          if (windowSpacing <= 1 || ((x + z) % windowSpacing) == 0)
            feats[x, z] = FeatureType.Window;
        }
      }
    }
  }

  private bool[,] BuildCompoundFootprintWithVariance(System.Random rng, int f)
  {
    // Create a slight variation from the ground rules per floor f.
    // We tweak offshoot count within the configured variance.
    int minVar = upperFloorOffshootVariance.x;
    int maxVar = upperFloorOffshootVariance.y;
    int delta = (minVar == maxVar) ? minVar : rng.Next(minVar, maxVar + 1);

    // Temporarily nudge the range, then build and revert.
    var origRange = offshootCountRange;
    offshootCountRange = new Vector2Int(
      Mathf.Max(0, offshootCountRange.x - delta),
      Mathf.Max(0, offshootCountRange.y - delta)
    );
    bool[,] occ = BuildCompoundFootprint(rng);
    offshootCountRange = origRange;
    return occ;
  }

  private bool[,] BuildCompoundFootprint(System.Random rng)
  {
    int W = GridSize.x;
    int H = GridSize.y;
    var occ = new bool[W, H];

    // Base rectangle
    int bwMin = Mathf.Clamp(baseSizeMin.x, 2, W);
    int bhMin = Mathf.Clamp(baseSizeMin.y, 2, H);
    int bwMax = Mathf.Clamp(baseSizeMax.x, bwMin, W);
    int bhMax = Mathf.Clamp(baseSizeMax.y, bhMin, H);

    int bw = rng.Next(bwMin, bwMax + 1);
    int bh = rng.Next(bhMin, bhMax + 1);
    int bx = rng.Next(0, Mathf.Max(1, W - bw + 1));
    int bz = rng.Next(0, Mathf.Max(1, H - bh + 1));
    FillRect(occ, bx, bz, bw, bh, true);

    // Offshoots
    int minOff = Mathf.Max(0, offshootCountRange.x);
    int maxOff = Mathf.Max(minOff, offshootCountRange.y);
    int offCount = (minOff == maxOff) ? minOff : rng.Next(minOff, maxOff + 1);

    for (int i = 0; i < offCount; i++)
      TryAddOffshoot(rng, occ);

    // Optional notches/indents
    if (allowNotches)
    {
      int minNotch = Mathf.Max(0, notchCountRange.x);
      int maxNotch = Mathf.Max(minNotch, notchCountRange.y);
      int notchCount =
        (minNotch == maxNotch) ? minNotch : rng.Next(minNotch, maxNotch + 1);
      for (int i = 0; i < notchCount; i++)
        TryCarveNotch(rng, occ);
    }

    // Ensure connected (very unlikely to be disconnected, but safe)
    EnsureConnected(occ);

    return occ;
  }

  private void TryAddOffshoot(System.Random rng, bool[,] occ)
  {
    int W = GridSize.x, H = GridSize.y;

    int owMin = Mathf.Clamp(offshootSizeMin.x, 1, W);
    int ohMin = Mathf.Clamp(offshootSizeMin.y, 1, H);
    int owMax = Mathf.Clamp(offshootSizeMax.x, owMin, W);
    int ohMax = Mathf.Clamp(offshootSizeMax.y, ohMin, H);

    // Try some attempts to find a valid edge to attach
    for (int attempt = 0; attempt < 12; attempt++)
    {
      FrontDirection dir =
        (FrontDirection)rng.Next(0, Enum.GetValues(typeof(FrontDirection)).Length);

      if (dir == FrontDirection.North || dir == FrontDirection.South)
      {
        int zEdge = (dir == FrontDirection.North)
          ? ExtremeZ(occ, true)
          : ExtremeZ(occ, false);
        if (zEdge < 0)
          continue;

        var segs = EdgeSegmentsX(occ, zEdge);
        if (segs.Count == 0)
          continue;

        var seg = segs[rng.Next(0, segs.Count)];
        int maxW = Mathf.Min(seg.length, owMax);
        if (maxW <= 0)
          continue;

        int w = rng.Next(Mathf.Min(owMin, maxW), maxW + 1);
        int x0 = rng.Next(seg.start, seg.start + seg.length - w + 1);

        int maxDepth =
          (dir == FrontDirection.North) ? (H - 1 - zEdge) : zEdge;
        if (maxDepth <= 0)
          continue;

        int h = rng.Next(Mathf.Min(ohMin, maxDepth), Mathf.Min(ohMax, maxDepth) + 1);
        int z0 = (dir == FrontDirection.North) ? zEdge + 1 : (zEdge - h);

        if (h > 0 && w > 0)
        {
          FillRect(occ, x0, z0, w, h, true);
          return;
        }
      }
      else
      {
        int xEdge = (dir == FrontDirection.East)
          ? ExtremeX(occ, true)
          : ExtremeX(occ, false);
        if (xEdge < 0)
          continue;

        var segs = EdgeSegmentsZ(occ, xEdge);
        if (segs.Count == 0)
          continue;

        var seg = segs[rng.Next(0, segs.Count)];
        int maxW = Mathf.Min(seg.length, owMax);
        if (maxW <= 0)
          continue;

        int w = rng.Next(Mathf.Min(owMin, maxW), maxW + 1);
        int z0 = rng.Next(seg.start, seg.start + seg.length - w + 1);

        int maxDepth =
          (dir == FrontDirection.East) ? (W - 1 - xEdge) : xEdge;
        if (maxDepth <= 0)
          continue;

        int h = rng.Next(Mathf.Min(ohMin, maxDepth), Mathf.Min(ohMax, maxDepth) + 1);
        int x0 = (dir == FrontDirection.East) ? xEdge + 1 : (xEdge - h);

        if (h > 0 && w > 0)
        {
          FillRect(occ, x0, z0, h, w, true); // note: width along X, w along Z
          return;
        }
      }
    }
  }

  private void TryCarveNotch(System.Random rng, bool[,] occ)
  {
    int W = GridSize.x, H = GridSize.y;

    int nwMin = Mathf.Clamp(notchSizeMin.x, 1, W);
    int nhMin = Mathf.Clamp(notchSizeMin.y, 1, H);
    int nwMax = Mathf.Clamp(notchSizeMax.x, nwMin, W);
    int nhMax = Mathf.Clamp(notchSizeMax.y, nhMin, H);

    for (int attempt = 0; attempt < 12; attempt++)
    {
      FrontDirection dir =
        (FrontDirection)rng.Next(0, Enum.GetValues(typeof(FrontDirection)).Length);

      if (dir == FrontDirection.North || dir == FrontDirection.South)
      {
        int zEdge = (dir == FrontDirection.North)
          ? ExtremeZ(occ, true)
          : ExtremeZ(occ, false);
        if (zEdge < 0)
          continue;

        var segs = EdgeSegmentsX(occ, zEdge);
        if (segs.Count == 0)
          continue;

        var seg = segs[rng.Next(0, segs.Count)];
        int maxW = Mathf.Min(seg.length, nwMax);
        if (maxW <= 1)
          continue;

        int w = rng.Next(Mathf.Min(nwMin, maxW), maxW + 1);
        int x0 = rng.Next(seg.start, seg.start + seg.length - w + 1);

        int maxDepth =
          (dir == FrontDirection.North) ? Mathf.Min(4, zEdge) : Mathf.Min(4, H - 1 - zEdge);
        if (maxDepth <= 0)
          continue;

        int h = rng.Next(Mathf.Min(nhMin, maxDepth), Mathf.Min(nhMax, maxDepth) + 1);
        int z0 = (dir == FrontDirection.North) ? (zEdge - h + 1) : (zEdge + 1);

        // Record changed cells to allow revert if disconnects
        var changed = new List<Vector2Int>();
        for (int x = x0; x < x0 + w; x++)
          for (int z = z0; z < z0 + h; z++)
          {
            if (IsInsideGrid(x, z) && occ[x, z])
            {
              occ[x, z] = false;
              changed.Add(new Vector2Int(x, z));
            }
          }

        if (!IsConnected(occ))
        {
          // Revert
          foreach (var c in changed)
            occ[c.x, c.y] = true;
          continue;
        }

        return; // success
      }
      else
      {
        int xEdge = (dir == FrontDirection.East)
          ? ExtremeX(occ, true)
          : ExtremeX(occ, false);
        if (xEdge < 0)
          continue;

        var segs = EdgeSegmentsZ(occ, xEdge);
        if (segs.Count == 0)
          continue;

        var seg = segs[rng.Next(0, segs.Count)];
        int maxW = Mathf.Min(seg.length, nwMax);
        if (maxW <= 1)
          continue;

        int w = rng.Next(Mathf.Min(nwMin, maxW), maxW + 1);
        int z0 = rng.Next(seg.start, seg.start + seg.length - w + 1);

        int maxDepth =
          (dir == FrontDirection.East) ? Mathf.Min(4, xEdge) : Mathf.Min(4, W - 1 - xEdge);
        if (maxDepth <= 0)
          continue;

        int h = rng.Next(Mathf.Min(nhMin, maxDepth), Mathf.Min(nhMax, maxDepth) + 1);
        int x0 = (dir == FrontDirection.East) ? (xEdge - h + 1) : (xEdge + 1);

        var changed = new List<Vector2Int>();
        for (int x = x0; x < x0 + h; x++)
          for (int z = z0; z < z0 + w; z++)
          {
            if (IsInsideGrid(x, z) && occ[x, z])
            {
              occ[x, z] = false;
              changed.Add(new Vector2Int(x, z));
            }
          }

        if (!IsConnected(occ))
        {
          foreach (var c in changed)
            occ[c.x, c.y] = true;
          continue;
        }

        return;
      }
    }
  }

  private Vector2Int PickDoorOnSide(bool[,] occ, bool isFront)
  {
    // Choose a door cell centered on the selected side along the outermost
    // row/column that has occupancy.
    if (frontDirection == FrontDirection.South || frontDirection == FrontDirection.North)
    {
      bool chooseNorth = (frontDirection == FrontDirection.North) == isFront;
      int zEdge = chooseNorth ? ExtremeZ(occ, true) : ExtremeZ(occ, false);
      if (zEdge < 0)
        return new Vector2Int(-1, -1);

      var segs = EdgeSegmentsX(occ, zEdge);
      if (segs.Count == 0)
        return new Vector2Int(-1, -1);

      // Pick the widest segment for a nice centered door
      var seg = segs[0];
      for (int i = 1; i < segs.Count; i++)
        if (segs[i].length > seg.length)
          seg = segs[i];

      int doorX = seg.start + seg.length / 2;
      // Nudge off corners if needed
      doorX = Mathf.Clamp(doorX, seg.start + 1, seg.start + seg.length - 2);
      return new Vector2Int(Mathf.Clamp(doorX, 0, GridSize.x - 1), zEdge);
    }
    else
    {
      bool chooseEast = (frontDirection == FrontDirection.East) == isFront;
      int xEdge = chooseEast ? ExtremeX(occ, true) : ExtremeX(occ, false);
      if (xEdge < 0)
        return new Vector2Int(-1, -1);

      var segs = EdgeSegmentsZ(occ, xEdge);
      if (segs.Count == 0)
        return new Vector2Int(-1, -1);

      var seg = segs[0];
      for (int i = 1; i < segs.Count; i++)
        if (segs[i].length > seg.length)
          seg = segs[i];

      int doorZ = seg.start + seg.length / 2;
      doorZ = Mathf.Clamp(doorZ, seg.start + 1, seg.start + seg.length - 2);
      return new Vector2Int(xEdge, Mathf.Clamp(doorZ, 0, GridSize.y - 1));
    }
  }

  private bool IsDoorCell(Vector2Int door, int x, int z)
  {
    return door.x == x && door.y == z;
  }

  private bool IsInsideGrid(Vector2Int p)
  {
    return IsInsideGrid(p.x, p.y);
  }

  private bool IsInsideGrid(int x, int z)
  {
    return x >= 0 && x < GridSize.x && z >= 0 && z < GridSize.y;
  }

  private void FillRect(bool[,] occ, int x0, int z0, int w, int h, bool val)
  {
    int x1 = Mathf.Clamp(x0 + w - 1, 0, GridSize.x - 1);
    int z1 = Mathf.Clamp(z0 + h - 1, 0, GridSize.y - 1);
    x0 = Mathf.Clamp(x0, 0, GridSize.x - 1);
    z0 = Mathf.Clamp(z0, 0, GridSize.y - 1);

    for (int x = x0; x <= x1; x++)
      for (int z = z0; z <= z1; z++)
        occ[x, z] = val;
  }

  private int ExtremeZ(bool[,] occ, bool top)
  {
    int W = GridSize.x, H = GridSize.y;
    if (top)
    {
      for (int z = H - 1; z >= 0; z--)
        for (int x = 0; x < W; x++)
          if (occ[x, z])
            return z;
    }
    else
    {
      for (int z = 0; z < H; z++)
        for (int x = 0; x < W; x++)
          if (occ[x, z])
            return z;
    }
    return -1;
  }

  private int ExtremeX(bool[,] occ, bool right)
  {
    int W = GridSize.x, H = GridSize.y;
    if (right)
    {
      for (int x = W - 1; x >= 0; x--)
        for (int z = 0; z < H; z++)
          if (occ[x, z])
            return x;
    }
    else
    {
      for (int x = 0; x < W; x++)
        for (int z = 0; z < H; z++)
          if (occ[x, z])
            return x;
    }
    return -1;
  }

  private struct Seg
  {
    public int start;
    public int length;
    public Seg(int s, int l)
    {
      start = s;
      length = l;
    }
  }

  private List<Seg> EdgeSegmentsX(bool[,] occ, int z)
  {
    int W = GridSize.x;
    var segs = new List<Seg>();
    int run = 0;
    int start = 0;

    for (int x = 0; x < W; x++)
    {
      if (occ[x, z])
      {
        if (run == 0)
          start = x;
        run++;
      }
      else
      {
        if (run > 0)
        {
          segs.Add(new Seg(start, run));
          run = 0;
        }
      }
    }
    if (run > 0)
      segs.Add(new Seg(start, run));

    return segs;
  }

  private List<Seg> EdgeSegmentsZ(bool[,] occ, int x)
  {
    int H = GridSize.y;
    var segs = new List<Seg>();
    int run = 0;
    int start = 0;

    for (int z = 0; z < H; z++)
    {
      if (occ[x, z])
      {
        if (run == 0)
          start = z;
        run++;
      }
      else
      {
        if (run > 0)
        {
          segs.Add(new Seg(start, run));
          run = 0;
        }
      }
    }
    if (run > 0)
      segs.Add(new Seg(start, run));

    return segs;
  }

  private bool IsPerimeterCell(bool[,] occ, int x, int z)
  {
    if (!occ[x, z])
      return false;

    // 4-neighborhood
    if (x - 1 < 0 || !occ[x - 1, z])
      return true;
    if (x + 1 >= GridSize.x || !occ[x + 1, z])
      return true;
    if (z - 1 < 0 || !occ[x, z - 1])
      return true;
    if (z + 1 >= GridSize.y || !occ[x, z + 1])
      return true;

    return false;
  }

  private void EnsureConnected(bool[,] occ)
  {
    if (!IsConnected(occ))
    {
      // Very rarely needed; if disconnected, flood fill from the largest
      // component and drop others.
      int W = GridSize.x, H = GridSize.y;
      bool[,] visited = new bool[W, H];
      int bestCount = 0;
      Vector2Int bestSeed = new Vector2Int(-1, -1);

      for (int x = 0; x < W; x++)
        for (int z = 0; z < H; z++)
          if (occ[x, z] && !visited[x, z])
          {
            int cnt = FloodCount(occ, x, z, visited, null);
            if (cnt > bestCount)
            {
              bestCount = cnt;
              bestSeed = new Vector2Int(x, z);
            }
          }

      if (bestSeed.x >= 0)
      {
        // Clear and refill only best component
        for (int x = 0; x < W; x++)
          for (int z = 0; z < H; z++)
            visited[x, z] = false;

        var keep = new HashSet<int>();
        FloodCount(
          occ,
          bestSeed.x,
          bestSeed.y,
          visited,
          (x, z) => keep.Add((z << 16) ^ x)
        );

        for (int x = 0; x < W; x++)
          for (int z = 0; z < H; z++)
            if (occ[x, z] && !keep.Contains((z << 16) ^ x))
              occ[x, z] = false;
      }
    }
  }

  private bool IsConnected(bool[,] occ)
  {
    int W = GridSize.x, H = GridSize.y;
    bool[,] visited = new bool[W, H];
    int total = 0;
    Vector2Int seed = new Vector2Int(-1, -1);

    for (int x = 0; x < W; x++)
      for (int z = 0; z < H; z++)
        if (occ[x, z])
        {
          total++;
          if (seed.x < 0)
            seed = new Vector2Int(x, z);
        }

    if (total == 0)
      return true;

    int reach = FloodCount(occ, seed.x, seed.y, visited, null);
    return reach == total;
  }

  private int FloodCount(
    bool[,] occ,
    int sx,
    int sz,
    bool[,] visited,
    Action<int, int> onVisit
  )
  {
    int W = GridSize.x, H = GridSize.y;
    var q = new Queue<Vector2Int>();
    q.Enqueue(new Vector2Int(sx, sz));
    visited[sx, sz] = true;
    int count = 0;

    while (q.Count > 0)
    {
      var p = q.Dequeue();
      count++;
      onVisit?.Invoke(p.x, p.y);

      // 4-neighbors
      TryEnqueue(p.x + 1, p.y);
      TryEnqueue(p.x - 1, p.y);
      TryEnqueue(p.x, p.y + 1);
      TryEnqueue(p.x, p.y - 1);
    }

    return count;

    void TryEnqueue(int x, int z)
    {
      if (x < 0 || x >= W || z < 0 || z >= H)
        return;
      if (visited[x, z] || !occ[x, z])
        return;
      visited[x, z] = true;
      q.Enqueue(new Vector2Int(x, z));
    }
  }

  private bool[,] CloneOcc(bool[,] src)
  {
    int W = GridSize.x, H = GridSize.y;
    var dst = new bool[W, H];
    for (int x = 0; x < W; x++)
      for (int z = 0; z < H; z++)
        dst[x, z] = src[x, z];
    return dst;
  }
}
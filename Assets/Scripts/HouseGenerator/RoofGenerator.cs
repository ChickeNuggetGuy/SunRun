using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public class RoofGenerator : SerializedMonoBehaviour
{
    [Required, SerializeField]
    private HouseGenerator houseGenerator;

    [Title("Roof Parameters")]
    [Range(0.0f, 2.0f)]
    [SerializeField]
    private float pitchRisePerRun = 0.5f;

    [MinValue(0)]
    [SerializeField]
    private float overhang = 0.12f;

    [Title("Joins")]
    [SerializeField]
    private bool orientPerpendicularOnSharedLongEdge = true;

    [MinValue(0)]
    [SerializeField]
    private float minSharedEdgeWorld = 0.25f;

    [SerializeField]
    private bool extendIntoMainForJoin = true;

    [MinValue(0)]
    [SerializeField]
    private float mergeExtension = 0.25f;

    [MinValue(0)]
    [SerializeField]
    private float mergeDrop = 0.005f;

    [Title("Detailing")]
    [SerializeField]
    private bool addRidgeCap = true;

    [MinValue(0)]
    [SerializeField]
    private float ridgeCapWidth = 0.18f;

    [MinValue(0)]
    [SerializeField]
    private float ridgeCapDrop = 0.01f;

    [SerializeField]
    private bool addFascia = true;

    [MinValue(0)]
    [SerializeField]
    private float fasciaDepth = 0.05f;

    [SerializeField]
    private Vector2 uvScale = new Vector2(0.25f, 0.25f);

    [Title("End Cap UVs")]
    [SerializeField]
    private Vector2 endCapUVScale = new Vector2(1.0f, 1.0f);

    [Title("Materials")]
    [SerializeField]
    private Material slopeMaterial;

    [SerializeField]
    private Material endCapMaterial;

    [SerializeField]
    private Material ridgeCapMaterial;

    [SerializeField]
    private Material fasciaMaterial;

    [Title("Performance")]
    [SerializeField]
    private bool useSharedMeshes = true;

    [SerializeField]
    private bool useObjectPooling = false;

    [Title("Detection")]
    [Tooltip("Use maximal-rectangle decomposition instead of greedy scan.")]
    [SerializeField]
    private bool useLargestRectDecomposition = true;

    [Tooltip("Ignore rectangles smaller than this many cells.")]
    [MinValue(1)]
    [SerializeField]
    private int minRectCells = 1;

    [Tooltip("Ignore rectangles whose smallest span is below this world size.")]
    [MinValue(0)]
    [SerializeField]
    private float minRectWorldSpan = 0.0f;

    [Tooltip("Prefer main roof ridge along the building's longest axis.")]
    [SerializeField]
    private bool preferGlobalRidgeOnMain = true;

    [Tooltip("Bias to decide the building's longest axis (X vs Z).")]
    [Range(1.0f, 2.0f)]
    [SerializeField]
    private float globalAxisBias = 1.15f;

    [Tooltip(
        "Minimum fraction of the small roof's edge that must overlap to be "
            + "considered a join for orientation."
    )]
    [Range(0.0f, 1.0f)]
    [SerializeField]
    private float minJoinOverlapRatio = 0.3f;

    [Title("Debug")]
    [SerializeField]
    private bool drawGizmos = true;

    private readonly List<GameObject> spawnedRoofObjects = new();
    private List<RoofRect> lastDetectedRects = new();
    private Transform roofHolder;

    private Material runtimeSlopeMat;
    private Material runtimeEndCapMat;
    private Material runtimeRidgeCapMat;
    private Material runtimeFasciaMat;

    // Caching for performance
    private Dictionary<string, Mesh> meshCache = new();
    private Queue<GameObject> objectPool = new();

    [Button]
    public void ClearRoofs()
    {
        EnsureRoofHolder();
        for (int i = roofHolder.childCount - 1; i >= 0; i--)
        {
            var ch = roofHolder.GetChild(i);
            if (Application.isPlaying)
                Destroy(ch.gameObject);
            else
                DestroyImmediate(ch.gameObject);
        }
        spawnedRoofObjects.Clear();
        meshCache.Clear();
    }

    [Button]
    public void GenerateRoof()
    {
        if (!ValidateHouse())
            return;

        EnsureRoofHolder();
        ClearRoofs();

        lastDetectedRects = useLargestRectDecomposition
            ? FindTopmostRectanglesLargest()
            : FindTopmostRectanglesGreedy();

        // Sort big → small globally (used for debug and deterministic order)
        lastDetectedRects.Sort((a, b) => b.Area.CompareTo(a.Area));

        // Ridge orientation and joins, with main-roof bias
        ApplyRidgeOrientationAndJoin(lastDetectedRects);

        foreach (var rect in lastDetectedRects)
        {
            var piece = BuildRoofPiece(rect);
            if (piece != null)
                spawnedRoofObjects.Add(piece);
        }
    }

    [Button]
    public void DetectHouseDimensions()
    {
        if (!ValidateHouse())
            return;

        lastDetectedRects = useLargestRectDecomposition
            ? FindTopmostRectanglesLargest()
            : FindTopmostRectanglesGreedy();

        lastDetectedRects.Sort((a, b) => b.Area.CompareTo(a.Area));
        ApplyRidgeOrientationAndJoin(lastDetectedRects);

        Debug.Log($"Detected {lastDetectedRects.Count} rectangles (big → small):");
        for (int i = 0; i < lastDetectedRects.Count; i++)
        {
            var r = lastDetectedRects[i];
            string dir = r.RidgeAlongXFinal ? "Ridge X" : "Ridge Z";
            string join = r.HasJoin
                ? $" join:{r.JoinAxis} {(r.JoinSign > 0 ? "+" : "-")}"
                : "";
            string main = r.IsMain ? " MAIN" : "";
            Debug.Log(
                $"[{i}] F{r.Floor} X:{r.X}..{r.X + r.Width - 1} "
                    + $"Z:{r.Z}..{r.Z + r.Height - 1} {r.Width}x{r.Height} "
                    + $"{dir}{join}{main} Area:{r.Area}"
            );
        }
    }

    private bool ValidateHouse()
    {
        if (houseGenerator == null || houseGenerator.Floors == null)
        {
            Debug.LogWarning("RoofGenerator: HouseGenerator/Floors missing.");
            return false;
        }
        return true;
    }

    private Transform EnsureRoofHolder()
    {
        if (roofHolder != null)
            return roofHolder;
        var parent = houseGenerator != null ? houseGenerator.transform : transform;
        var t = parent.Find("RoofHolder");
        if (t == null)
        {
            var go = new GameObject("RoofHolder");
            t = go.transform;
            t.SetParent(parent, false);
        }
        roofHolder = t;
        return roofHolder;
    }

    private Material MakeMat(
        Color color,
        float smoothness = 0.2f,
        float metallic = 0f
    )
    {
        var shader =
            Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader) { color = color };
        if (m.HasProperty("_Smoothness"))
            m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic"))
            m.SetFloat("_Metallic", metallic);
        return m;
    }

    private Material GetSlopeMat()
    {
        if (slopeMaterial != null)
            return slopeMaterial;
        if (runtimeSlopeMat != null)
            return runtimeSlopeMat;
        runtimeSlopeMat = MakeMat(new Color(0.16f, 0.16f, 0.16f), 0.2f, 0f);
        return runtimeSlopeMat;
    }

    private Material GetEndCapMat()
    {
        if (endCapMaterial != null)
            return endCapMaterial;
        if (runtimeEndCapMat != null)
            return runtimeEndCapMat;
        runtimeEndCapMat = MakeMat(new Color(0.22f, 0.22f, 0.22f), 0.2f, 0f);
        return runtimeEndCapMat;
    }

    private Material GetRidgeCapMat()
    {
        if (ridgeCapMaterial != null)
            return ridgeCapMaterial;
        if (runtimeRidgeCapMat != null)
            return runtimeRidgeCapMat;
        runtimeRidgeCapMat = MakeMat(new Color(0.12f, 0.12f, 0.12f), 0.15f, 0f);
        return runtimeRidgeCapMat;
    }

    private Material GetFasciaMat()
    {
        if (fasciaMaterial != null)
            return fasciaMaterial;
        if (runtimeFasciaMat != null)
            return runtimeFasciaMat;
        runtimeFasciaMat = MakeMat(new Color(0.92f, 0.92f, 0.92f), 0.05f, 0f);
        return runtimeFasciaMat;
    }

    private bool IsOccupied(int f, int x, int z)
    {
        if (
            houseGenerator == null
            || houseGenerator.Floors == null
            || f < 0
            || f >= houseGenerator.Floors.Length
        )
            return false;

        var feats = houseGenerator.Floors[f].Features;
        if (
            feats == null
            || x < 0
            || z < 0
            || x >= feats.GetLength(0)
            || z >= feats.GetLength(1)
        )
            return false;

        return feats[x, z] != HouseGenerator.FeatureType.Empty;
    }

    private bool IsTopmost(int f, int x, int z)
    {
        if (!IsOccupied(f, x, z))
            return false;
        if (f == houseGenerator.Floors.Length - 1)
            return true;
        return !IsOccupied(f + 1, x, z);
    }

    // ORIGINAL GREEDY DETECTOR (kept as fallback)
    private List<RoofRect> FindTopmostRectanglesGreedy()
    {
        var result = new List<RoofRect>();
        if (houseGenerator.Floors == null)
            return result;

        for (int f = 0; f < houseGenerator.Floors.Length; f++)
        {
            var feats = houseGenerator.Floors[f].Features;
            if (feats == null)
                continue;

            int w = feats.GetLength(0);
            int h = feats.GetLength(1);
            var top = new bool[w, h];
            var used = new bool[w, h];

            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                    top[x, z] = IsTopmost(f, x, z);

            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!top[x, z] || used[x, z])
                        continue;

                    int maxWidth = 0;
                    int xi = x;
                    while (xi < w && top[xi, z] && !used[xi, z])
                    {
                        maxWidth++;
                        xi++;
                    }

                    int finalWidth = maxWidth;
                    int finalHeight = 1;

                    int zi = z + 1;
                    while (zi < h)
                    {
                        int rowWidth = 0;
                        int xr = x;
                        while (
                            xr < w && rowWidth < finalWidth && top[xr, zi] && !used[xr, zi]
                        )
                        {
                            rowWidth++;
                            xr++;
                        }

                        if (rowWidth == 0)
                            break;
                        finalWidth = Mathf.Min(finalWidth, rowWidth);
                        finalHeight++;
                        zi++;
                    }

                    for (int zz = z; zz < z + finalHeight; zz++)
                        for (int xx = x; xx < x + finalWidth; xx++)
                            used[xx, zz] = true;

                    result.Add(
                        new RoofRect
                        {
                            Floor = f,
                            X = x,
                            Z = z,
                            Width = finalWidth,
                            Height = finalHeight,
                            RidgeAlongXFinal = finalWidth >= finalHeight,
                            HasJoin = false,
                            JoinAxis = Axis.X,
                            JoinSign = 0,
                            IsMain = false
                        }
                    );
                }
            }
        }

        // Mark a main per floor
        MarkFloorMains(result);
        return result;
    }

    // NEW: largest-rectangle decomposition
    private List<RoofRect> FindTopmostRectanglesLargest()
    {
        var result = new List<RoofRect>();
        if (houseGenerator.Floors == null)
            return result;

        var cellSize =
            houseGenerator.prefabData != null
                ? houseGenerator.prefabData.cellSize
                : new Vector3(1, 1, 1);

        for (int f = 0; f < houseGenerator.Floors.Length; f++)
        {
            var feats = houseGenerator.Floors[f].Features;
            if (feats == null)
                continue;

            int w = feats.GetLength(0);
            int h = feats.GetLength(1);

            // Build topmost mask
            var mask = new bool[w, h];
            int any = 0;
            for (int x = 0; x < w; x++)
            {
                for (int z = 0; z < h; z++)
                {
                    bool v = IsTopmost(f, x, z);
                    mask[x, z] = v;
                    if (v)
                        any++;
                }
            }

            if (any == 0)
                continue;

            // Repeatedly take the largest rectangle of 1s and carve
            while (true)
            {
                if (!AnyTrue(mask, w, h))
                    break;

                var best = FindLargestRectInMask(mask, w, h);
                if (best.area <= 0)
                    break;

                // Remove it from mask
                for (int xi = best.x; xi < best.x + best.width; xi++)
                    for (int zi = best.z; zi < best.z + best.height; zi++)
                        mask[xi, zi] = false;

                // Skip too small rectangles (filters)
                int cells = best.width * best.height;
                float spanX = best.width * cellSize.x;
                float spanZ = best.height * cellSize.z;
                float minSpan = Mathf.Min(spanX, spanZ);

                if (cells < minRectCells || minSpan < minRectWorldSpan)
                    continue;

                result.Add(
                    new RoofRect
                    {
                        Floor = f,
                        X = best.x,
                        Z = best.z,
                        Width = best.width,
                        Height = best.height,
                        RidgeAlongXFinal = best.width >= best.height,
                        HasJoin = false,
                        JoinAxis = Axis.X,
                        JoinSign = 0,
                        IsMain = false
                    }
                );
            }
        }

        // Mark a main per floor
        MarkFloorMains(result);
        return result;
    }

    private static bool AnyTrue(bool[,] m, int w, int h)
    {
        for (int x = 0; x < w; x++)
            for (int z = 0; z < h; z++)
                if (m[x, z])
                    return true;
        return false;
    }

    // Largest rectangle of 1s in a binary matrix using histogram method per row
    private static (int x, int z, int width, int height, int area)
        FindLargestRectInMask(bool[,] mask, int w, int h)
    {
        int[] heights = new int[w];
        int bestArea = 0;
        int bestX = 0, bestZ = 0, bestW = 0, bestH = 0;

        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                heights[x] = mask[x, z] ? heights[x] + 1 : 0;
            }

            // Largest rectangle in histogram "heights"
            // Stack of indices with increasing heights
            var stack = new Stack<int>();
            for (int i = 0; i <= w; i++)
            {
                int curH = i < w ? heights[i] : 0;
                int lastIndex = i;
                while (stack.Count > 0 && curH < heights[stack.Peek()])
                {
                    int top = stack.Pop();
                    int height = heights[top];
                    int left = stack.Count == 0 ? 0 : stack.Peek() + 1;
                    int right = i - 1;
                    int width = right - left + 1;
                    int area = width * height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        bestW = width;
                        bestH = height;
                        bestX = left;
                        bestZ = z - height + 1;
                    }
                    lastIndex = left;
                }
                stack.Push(i);
            }
        }

        return (bestX, bestZ, bestW, bestH, bestArea);
    }

    private enum Axis
    {
        X,
        Z
    }

    private static int OverlapLen(int a0, int aLen, int b0, int bLen)
    {
        int a1 = a0 + aLen;
        int b1 = b0 + bLen;
        int lo = Math.Max(a0, b0);
        int hi = Math.Min(a1, b1);
        return Math.Max(0, hi - lo);
    }

    private static bool TrySharedEdgeDetailed(
        RoofRect a,
        RoofRect b,
        Vector3 cellSize,
        out Axis axis,
        out int signFromATowardsB,
        out float overlapWorld
    )
    {
        axis = Axis.X;
        signFromATowardsB = 0;
        overlapWorld = 0f;
        if (a.Floor != b.Floor)
            return false;

        if (a.Z + a.Height == b.Z || b.Z + b.Height == a.Z)
        {
            int overlap = OverlapLen(a.X, a.Width, b.X, b.Width);
            if (overlap > 0)
            {
                axis = Axis.X;
                overlapWorld = overlap * cellSize.x;
                signFromATowardsB = a.Z + a.Height == b.Z ? +1 : -1;
                return true;
            }
        }

        if (a.X + a.Width == b.X || b.X + b.Width == a.X)
        {
            int overlap = OverlapLen(a.Z, a.Height, b.Z, b.Height);
            if (overlap > 0)
            {
                axis = Axis.Z;
                overlapWorld = overlap * cellSize.z;
                signFromATowardsB = a.X + a.Width == b.X ? +1 : -1;
                return true;
            }
        }

        return false;
    }

    private void MarkFloorMains(List<RoofRect> rects)
    {
        var floors = rects.GroupBy(r => r.Floor);
        foreach (var g in floors)
        {
            int bestIdx = -1;
            int bestArea = -1;
            var list = g.ToList();
            foreach (var r in list)
            {
                int idx = rects.FindIndex(
                    rr =>
                        rr.Floor == r.Floor
                        && rr.X == r.X
                        && rr.Z == r.Z
                        && rr.Width == r.Width
                        && rr.Height == r.Height
                );
                if (idx >= 0 && rects[idx].Area > bestArea)
                {
                    bestArea = rects[idx].Area;
                    bestIdx = idx;
                }
            }
            if (bestIdx >= 0)
            {
                var rr = rects[bestIdx];
                rr.IsMain = true;
                rects[bestIdx] = rr;
            }
        }
    }

    private void ApplyRidgeOrientationAndJoin(List<RoofRect> rects)
    {
        if (rects == null || rects.Count == 0)
            return;

        var cellSize =
            houseGenerator.prefabData != null
                ? houseGenerator.prefabData.cellSize
                : new Vector3(1, 1, 1);

        // Index rectangles per floor by list of indices, sorted by area (desc)
        var byFloor = new Dictionary<int, List<int>>();
        for (int i = 0; i < rects.Count; i++)
        {
            int f = rects[i].Floor;
            if (!byFloor.TryGetValue(f, out var list))
            {
                list = new List<int>();
                byFloor[f] = list;
            }
            list.Add(i);
        }
        foreach (var kv in byFloor)
        {
            kv.Value.Sort((ia, ib) => rects[ib].Area.CompareTo(rects[ia].Area));
        }

        // Optionally compute global longest axis per floor mask to bias the main
        var floorAxisIsX = new Dictionary<int, bool>();
        if (preferGlobalRidgeOnMain)
        {
            foreach (var kv in byFloor)
            {
                int floor = kv.Key;
                ComputeFloorAxisBias(floor, out bool longX);
                floorAxisIsX[floor] = longX;
            }
        }

        // For each floor, set ridge orientation and join info
        foreach (var kv in byFloor)
        {
            var indices = kv.Value; // already sorted desc by area
            if (indices.Count == 0)
                continue;

            // First pass: set main ridge orientation
            int mainIdx = indices[0];
            var main = rects[mainIdx];
            bool mainAxisX = main.Width >= main.Height;
            if (preferGlobalRidgeOnMain && floorAxisIsX.TryGetValue(main.Floor, out bool g))
            {
                // Only override if there is a clear bias
                mainAxisX = g;
            }
            main.RidgeAlongXFinal = mainAxisX;
            rects[mainIdx] = main;

            // Precompute adjacency pairs to larger rectangles
            // Map small index -> list of candidates (larger neighbor rectangles)
            var neighbors = new Dictionary<int, List<(int j, Axis axis, int sign, float overlap)>>();

            for (int i = 0; i < indices.Count; i++)
            {
                int idxI = indices[i];
                var ri = rects[idxI];

                for (int j = 0; j < i; j++)
                {
                    int idxJ = indices[j];
                    var rj = rects[idxJ];

                    if (
                        TrySharedEdgeDetailed(
                            ri,
                            rj,
                            cellSize,
                            out Axis sharedAxis,
                            out int sign,
                            out float overlapWorld
                        )
                    )
                    {
                        if (!neighbors.TryGetValue(idxI, out var list))
                        {
                            list = new List<(int, Axis, int, float)>();
                            neighbors[idxI] = list;
                        }
                        list.Add((idxJ, sharedAxis, sign, overlapWorld));
                    }
                }
            }

            // Second pass: decide orientation for non-main based on best neighbor
            for (int i = 1; i < indices.Count; i++)
            {
                int idx = indices[i];
                var rSmall = rects[idx];

                // Default orientation by aspect
                bool ridgeAlongX = rSmall.Width >= rSmall.Height;
                bool hasJoin = false;
                Axis joinAxis = Axis.X;
                int joinSign = 0;

                if (neighbors.TryGetValue(idx, out var cands) && cands.Count > 0)
                {
                    // Prefer main neighbor, then max overlap
                    (int j, Axis axis, int sign, float overlap) best = default;
                    float bestScore = -1f;
                    foreach (var cand in cands)
                    {
                        bool neighborIsMain = rects[cand.j].IsMain;
                        float score = cand.overlap * (neighborIsMain ? 10f : 1f);
                        if (score > bestScore)
                        {
                            best = cand;
                            bestScore = score;
                        }
                    }

                    var rBig = rects[best.j];
                    // Check minimum world overlap and ratio requirement
                    float effectiveEdge =
                        best.axis == Axis.X
                            ? rSmall.Width * cellSize.x
                            : rSmall.Height * cellSize.z;
                    float overlapRatio =
                        effectiveEdge > 1e-5f ? best.overlap / effectiveEdge : 0f;

                    if (
                        best.overlap >= minSharedEdgeWorld
                        && overlapRatio >= minJoinOverlapRatio
                    )
                    {
                        hasJoin = true;
                        joinAxis = best.axis;
                        joinSign = best.sign;

                        Axis bigLongAxis = rBig.Width >= rBig.Height ? Axis.X : Axis.Z;
                        if (
                            orientPerpendicularOnSharedLongEdge
                            && best.axis == bigLongAxis
                        )
                        {
                            // Perpendicular to shared long edge
                            ridgeAlongX = best.axis == Axis.Z;
                        }
                    }
                }

                rSmall.RidgeAlongXFinal = ridgeAlongX;
                rSmall.HasJoin = hasJoin;
                rSmall.JoinAxis = joinAxis;
                rSmall.JoinSign = joinSign;

                rects[idx] = rSmall;
            }
        }
    }

    private void ComputeFloorAxisBias(int floor, out bool longX)
    {
        longX = true;
        var feats = houseGenerator.Floors[floor].Features;
        if (feats == null)
            return;

        int w = feats.GetLength(0);
        int h = feats.GetLength(1);

        int minX = int.MaxValue, maxX = int.MinValue;
        int minZ = int.MaxValue, maxZ = int.MinValue;
        bool any = false;

        for (int x = 0; x < w; x++)
        {
            for (int z = 0; z < h; z++)
            {
                if (IsTopmost(floor, x, z))
                {
                    any = true;
                    if (x < minX)
                        minX = x;
                    if (x > maxX)
                        maxX = x;
                    if (z < minZ)
                        minZ = z;
                    if (z > maxZ)
                        maxZ = z;
                }
            }
        }

        if (!any)
            return;

        var cellSize =
            houseGenerator.prefabData != null
                ? houseGenerator.prefabData.cellSize
                : new Vector3(1, 1, 1);

        float spanX = (maxX - minX + 1) * cellSize.x;
        float spanZ = (maxZ - minZ + 1) * cellSize.z;

        // Longest axis with bias
        if (spanX >= spanZ * globalAxisBias)
            longX = true;
        else if (spanZ >= spanX * globalAxisBias)
            longX = false;
        else
            longX = spanX >= spanZ;
    }

    private GameObject GetRoofPieceObject(string name)
    {
        GameObject piece;
        if (useObjectPooling && objectPool.Count > 0)
        {
            piece = objectPool.Dequeue();
            piece.name = name;
            piece.SetActive(true);
        }
        else
        {
            piece = new GameObject(name);
        }
        return piece;
    }

    private Mesh GetCachedMesh(string name, Func<Mesh> meshCreator)
    {
        if (useSharedMeshes && meshCache.TryGetValue(name, out Mesh cachedMesh))
        {
            return cachedMesh;
        }

        Mesh newMesh = meshCreator();
        newMesh.name = name;

        if (useSharedMeshes)
        {
            meshCache[name] = newMesh;
        }

        return newMesh;
    }

    private GameObject BuildRoofPiece(RoofRect rect)
    {
        var cellSize =
            houseGenerator != null && houseGenerator.prefabData != null
                ? houseGenerator.prefabData.cellSize
                : new Vector3(1, 1, 1);

        float baseY = (rect.Floor + 1) * cellSize.y;

        float ohXNeg = overhang, ohXPos = overhang, ohZNeg = overhang,
            ohZPos = overhang;

        float extXNeg = 0f, extXPos = 0f, extZNeg = 0f, extZPos = 0f;
        float mergeDropLocal = 0f;

        if (extendIntoMainForJoin && rect.HasJoin)
        {
            if (rect.JoinAxis == Axis.X)
            {
                if (rect.JoinSign > 0)
                {
                    ohZPos = 0f;
                    extZPos = mergeExtension;
                }
                else
                {
                    ohZNeg = 0f;
                    extZNeg = mergeExtension;
                }
                mergeDropLocal = mergeDrop;
            }
            else
            {
                if (rect.JoinSign > 0)
                {
                    ohXPos = 0f;
                    extXPos = mergeExtension;
                }
                else
                {
                    ohXNeg = 0f;
                    extXNeg = mergeExtension;
                }
                mergeDropLocal = mergeDrop;
            }
        }

        float xMin =
            rect.X * cellSize.x - ohXNeg - (extXNeg > 0 ? extXNeg : 0f);
        float xMax =
            (rect.X + rect.Width) * cellSize.x
            + ohXPos
            + (extXPos > 0 ? extXPos : 0f);
        float zMin =
            rect.Z * cellSize.z - ohZNeg - (extZNeg > 0 ? extZNeg : 0f);
        float zMax =
            (rect.Z + rect.Height) * cellSize.z
            + ohZPos
            + (extZPos > 0 ? extZPos : 0f);

        float xMid = 0.5f * (xMin + xMax);
        float zMid = 0.5f * (zMin + zMax);

        // Calculate ridge height based on the rectangle's dimensions and pitch
        float shortSideWorld = rect.RidgeAlongXFinal ? (zMax - zMin) : (xMax - xMin);
        float ridgeHeight = pitchRisePerRun * (shortSideWorld * 0.5f);

        var A = new Vector3(xMin, baseY, zMin);
        var B = new Vector3(xMax, baseY, zMin);
        var C = new Vector3(xMin, baseY, zMax);
        var D = new Vector3(xMax, baseY, zMax);

        if (mergeDropLocal > 0f && rect.HasJoin)
        {
            if (rect.JoinAxis == Axis.X)
            {
                if (rect.JoinSign > 0)
                {
                    C.y -= mergeDropLocal;
                    D.y -= mergeDropLocal;
                }
                else
                {
                    A.y -= mergeDropLocal;
                    B.y -= mergeDropLocal;
                }
            }
            else
            {
                if (rect.JoinSign > 0)
                {
                    B.y -= mergeDropLocal;
                    D.y -= mergeDropLocal;
                }
                else
                {
                    A.y -= mergeDropLocal;
                    C.y -= mergeDropLocal;
                }
            }
        }

        Vector3 RA, RB;
        if (rect.RidgeAlongXFinal)
        {
            RA = new Vector3(xMin, baseY + ridgeHeight, zMid);
            RB = new Vector3(xMax, baseY + ridgeHeight, zMid);
        }
        else
        {
            RA = new Vector3(xMid, baseY + ridgeHeight, zMin);
            RB = new Vector3(xMid, baseY + ridgeHeight, zMax);
        }

        string pieceName =
            $"Roof_F{rect.Floor}_{rect.X}_{rect.Z}_{rect.Width}x{rect.Height}";
        var piece = GetRoofPieceObject(pieceName);
        piece.transform.SetParent(EnsureRoofHolder(), false);

        // Slopes
        {
            string meshName = $"{pieceName}_Slopes";
            var mesh = GetCachedMesh(
                meshName,
                () =>
                {
                    var v = new List<Vector3> { A, B, C, D, RA, RB };
                    var t = new List<int>();
                    var uv = new List<Vector2>
                    {
                        new Vector2(A.x * uvScale.x, A.z * uvScale.y),
                        new Vector2(B.x * uvScale.x, B.z * uvScale.y),
                        new Vector2(C.x * uvScale.x, C.z * uvScale.y),
                        new Vector2(D.x * uvScale.x, D.z * uvScale.y),
                        new Vector2(RA.x * uvScale.x, RA.z * uvScale.y),
                        new Vector2(RB.x * uvScale.x, RB.z * uvScale.y)
                    };

                    if (rect.RidgeAlongXFinal)
                    {
                        AddQuadOriented(v, t, 0, 1, 5, 4, Vector3.up);
                        AddQuadOriented(v, t, 2, 3, 5, 4, Vector3.up);
                    }
                    else
                    {
                        AddQuadOriented(v, t, 0, 2, 5, 4, Vector3.up);
                        AddQuadOriented(v, t, 1, 3, 5, 4, Vector3.up);
                    }

                    var newMesh = new Mesh();
                    newMesh.SetVertices(v);
                    newMesh.SetTriangles(t, 0);
                    newMesh.SetUVs(0, uv);
                    newMesh.RecalculateNormals();
                    newMesh.RecalculateBounds();
                    return newMesh;
                }
            );

            var go = new GameObject("Slopes");
            go.transform.SetParent(piece.transform, false);
            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = GetSlopeMat();
        }

        // End caps (gable triangles)
        {
            string meshName = $"{pieceName}_EndCaps";
            var mesh = GetCachedMesh(
                meshName,
                () =>
                {
                    var v = new List<Vector3> { A, B, C, D, RA, RB };
                    var t = new List<int>();
                    var uv = new List<Vector2>();

                    // Calculate UVs for end caps with proper scaling
                    if (rect.RidgeAlongXFinal)
                    {
                        // For X-oriented ridge, use Z dimension for UVs
                        float zSize = zMax - zMin;
                        float uScale = endCapUVScale.x / Mathf.Max(zSize, 1e-6f);
                        float vScale = endCapUVScale.y / Mathf.Max(ridgeHeight, 1e-6f);

                        uv.Add(new Vector2(0, 0));
                        uv.Add(new Vector2(0, 0));
                        uv.Add(new Vector2((C.z - zMin) * uScale, 0));
                        uv.Add(new Vector2((D.z - zMin) * uScale, 0));
                        uv.Add(
                            new Vector2(
                                (RA.z - zMin) * uScale,
                                (RA.y - baseY) * vScale
                            )
                        );
                        uv.Add(
                            new Vector2(
                                (RB.z - zMin) * uScale,
                                (RB.y - baseY) * vScale
                            )
                        );
                    }
                    else
                    {
                        // For Z-oriented ridge, use X dimension for UVs
                        float xSize = xMax - xMin;
                        float uScale = endCapUVScale.x / Mathf.Max(xSize, 1e-6f);
                        float vScale = endCapUVScale.y / Mathf.Max(ridgeHeight, 1e-6f);

                        uv.Add(new Vector2(0, 0));
                        uv.Add(new Vector2((B.x - xMin) * uScale, 0));
                        uv.Add(new Vector2(0, 0));
                        uv.Add(new Vector2((D.x - xMin) * uScale, 0));
                        uv.Add(
                            new Vector2(
                                (RA.x - xMin) * uScale,
                                (RA.y - baseY) * vScale
                            )
                        );
                        uv.Add(
                            new Vector2(
                                (RB.x - xMin) * uScale,
                                (RB.y - baseY) * vScale
                            )
                        );
                    }

                    if (rect.RidgeAlongXFinal)
                    {
                        AddTriOriented(v, t, 0, 2, 4, -Vector3.right);
                        AddTriOriented(v, t, 1, 3, 5, Vector3.right);
                    }
                    else
                    {
                        AddTriOriented(v, t, 0, 1, 4, -Vector3.forward);
                        AddTriOriented(v, t, 2, 3, 5, Vector3.forward);
                    }

                    var newMesh = new Mesh();
                    newMesh.SetVertices(v);
                    newMesh.SetTriangles(t, 0);
                    newMesh.SetUVs(0, uv);
                    newMesh.RecalculateNormals();
                    newMesh.RecalculateBounds();
                    return newMesh;
                }
            );

            var go = new GameObject("EndCaps");
            go.transform.SetParent(piece.transform, false);
            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = GetEndCapMat();
        }

        // Ridge cap
        if (addRidgeCap && ridgeCapWidth > 0f)
        {
            string meshName = $"{pieceName}_RidgeCap";
            var mesh = GetCachedMesh(
                meshName,
                () =>
                {
                    float half = ridgeCapWidth * 0.5f;
                    float yCap = (RA.y + RB.y) * 0.5f - ridgeCapDrop;

                    var v = new List<Vector3>();
                    var t = new List<int>();
                    var uv = new List<Vector2>();

                    if (rect.RidgeAlongXFinal)
                    {
                        var a1 = new Vector3(RA.x, yCap, zMid + half);
                        var a2 = new Vector3(RA.x, yCap, zMid - half);
                        var b1 = new Vector3(RB.x, yCap, zMid + half);
                        var b2 = new Vector3(RB.x, yCap, zMid - half);

                        v.AddRange(new[] { a1, b1, b2, a2 });
                        uv.AddRange(
                            new[]
                            {
                                new Vector2(a1.x * uvScale.x, a1.z * uvScale.y),
                                new Vector2(b1.x * uvScale.x, b1.z * uvScale.y),
                                new Vector2(b2.x * uvScale.x, b2.z * uvScale.y),
                                new Vector2(a2.x * uvScale.x, a2.z * uvScale.y)
                            }
                        );
                        AddQuadOriented(v, t, 0, 1, 2, 3, Vector3.up);
                    }
                    else
                    {
                        var a1 = new Vector3(xMid + half, yCap, RA.z);
                        var a2 = new Vector3(xMid - half, yCap, RA.z);
                        var b1 = new Vector3(xMid + half, yCap, RB.z);
                        var b2 = new Vector3(xMid - half, yCap, RB.z);

                        v.AddRange(new[] { a1, b1, b2, a2 });
                        uv.AddRange(
                            new[]
                            {
                                new Vector2(a1.x * uvScale.x, a1.z * uvScale.y),
                                new Vector2(b1.x * uvScale.x, b1.z * uvScale.y),
                                new Vector2(b2.x * uvScale.x, b2.z * uvScale.y),
                                new Vector2(a2.x * uvScale.x, a2.z * uvScale.y)
                            }
                        );
                        AddQuadOriented(v, t, 0, 1, 2, 3, Vector3.up);
                    }

                    var newMesh = new Mesh();
                    newMesh.SetVertices(v);
                    newMesh.SetTriangles(t, 0);
                    newMesh.SetUVs(0, uv);
                    newMesh.RecalculateNormals();
                    newMesh.RecalculateBounds();
                    return newMesh;
                }
            );

            var go = new GameObject("RidgeCap");
            go.transform.SetParent(piece.transform, false);
            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = GetRidgeCapMat();
        }

        // Fascia on exterior eaves
        if (addFascia && fasciaDepth > 0f)
        {
            string meshName = $"{pieceName}_Fascia";
            var mesh = GetCachedMesh(
                meshName,
                () =>
                {
                    var v = new List<Vector3>();
                    var t = new List<int>();
                    var uv = new List<Vector2>();

                    float y2 = baseY - fasciaDepth;

                    void AddEdge(Vector3 p0, Vector3 p1, Vector3 outward)
                    {
                        int i0 = v.Count;
                        v.Add(p0);
                        v.Add(p1);
                        v.Add(new Vector3(p1.x, y2, p1.z));
                        v.Add(new Vector3(p0.x, y2, p0.z));
                        uv.Add(new Vector2(p0.x * uvScale.x, p0.z * uvScale.y));
                        uv.Add(new Vector2(p1.x * uvScale.x, p1.z * uvScale.y));
                        uv.Add(new Vector2(p1.x * uvScale.x, p1.z * uvScale.y));
                        uv.Add(new Vector2(p0.x * uvScale.x, p0.z * uvScale.y));
                        AddQuadOriented(v, t, i0 + 0, i0 + 1, i0 + 2, i0 + 3, outward);
                    }

                    bool fasciaZMin =
                        !(rect.HasJoin && rect.JoinAxis == Axis.X && rect.JoinSign < 0);
                    bool fasciaZMax =
                        !(rect.HasJoin && rect.JoinAxis == Axis.X && rect.JoinSign > 0);
                    bool fasciaXMin =
                        !(rect.HasJoin && rect.JoinAxis == Axis.Z && rect.JoinSign < 0);
                    bool fasciaXMax =
                        !(rect.HasJoin && rect.JoinAxis == Axis.Z && rect.JoinSign > 0);

                    if (fasciaZMin)
                        AddEdge(A, B, -Vector3.forward);
                    if (fasciaZMax)
                        AddEdge(C, D, Vector3.forward);
                    if (fasciaXMin)
                        AddEdge(A, C, -Vector3.right);
                    if (fasciaXMax)
                        AddEdge(B, D, Vector3.right);

                    var newMesh = new Mesh();
                    newMesh.SetVertices(v);
                    newMesh.SetTriangles(t, 0);
                    newMesh.SetUVs(0, uv);
                    newMesh.RecalculateNormals();
                    newMesh.RecalculateBounds();
                    return newMesh;
                }
            );

            // Only create object if geometry exists
            if (mesh.vertexCount > 0)
            {
                var go = new GameObject("Fascia");
                go.transform.SetParent(piece.transform, false);
                var meshFilter = go.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = GetFasciaMat();
            }
        }

        return piece;
    }

    private static void AddTriOriented(
        List<Vector3> v,
        List<int> t,
        int a,
        int b,
        int c,
        Vector3 outwardHint
    )
    {
        Vector3 n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(n, outwardHint) >= 0f)
        {
            t.Add(a);
            t.Add(b);
            t.Add(c);
        }
        else
        {
            t.Add(a);
            t.Add(c);
            t.Add(b);
        }
    }

    private static void AddQuadOriented(
        List<Vector3> v,
        List<int> t,
        int a,
        int b,
        int c,
        int d,
        Vector3 outwardHint
    )
    {
        AddTriOriented(v, t, a, b, c, outwardHint);
        AddTriOriented(v, t, a, c, d, outwardHint);
    }

    [Serializable]
    private struct RoofRect
    {
        public int Floor;
        public int X;
        public int Z;
        public int Width;
        public int Height;

        public bool RidgeAlongXFinal;

        public bool HasJoin;
        public Axis JoinAxis;
        public int JoinSign;

        public bool IsMain;

        public int Area => Width * Height;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || lastDetectedRects == null || houseGenerator == null)
            return;

        var cellSize =
            houseGenerator.prefabData != null
                ? houseGenerator.prefabData.cellSize
                : new Vector3(1, 1, 1);

        Gizmos.color = Color.cyan;

        foreach (var r in lastDetectedRects)
        {
            float baseY = (r.Floor + 1) * cellSize.y;

            float xMin = r.X * cellSize.x;
            float xMax = (r.X + r.Width) * cellSize.x;
            float zMin = r.Z * cellSize.z;
            float zMax = (r.Z + r.Height) * cellSize.z;

            float xMid = 0.5f * (xMin + xMax);
            float zMid = 0.5f * (zMin + zMax);

            float shortSideWorld = r.RidgeAlongXFinal ? (zMax - zMin) : (xMax - xMin);
            float ridgeHeight = pitchRisePerRun * (shortSideWorld * 0.5f);

            Vector3 p1, p2;
            if (r.RidgeAlongXFinal)
            {
                p1 = houseGenerator.transform.TransformPoint(
                    new Vector3(xMin, baseY + ridgeHeight, zMid)
                );
                p2 = houseGenerator.transform.TransformPoint(
                    new Vector3(xMax, baseY + ridgeHeight, zMid)
                );
            }
            else
            {
                p1 = houseGenerator.transform.TransformPoint(
                    new Vector3(xMid, baseY + ridgeHeight, zMin)
                );
                p2 = houseGenerator.transform.TransformPoint(
                    new Vector3(xMid, baseY + ridgeHeight, zMax)
                );
            }

            // Ridge line
            Gizmos.color = r.IsMain ? Color.yellow : Color.cyan;
            Gizmos.DrawLine(p1, p2);

            // Rect outline (faint)
            Gizmos.color = new Color(0f, 1f, 1f, r.IsMain ? 0.35f : 0.15f);
            Vector3 A = houseGenerator.transform.TransformPoint(
                new Vector3(xMin, baseY, zMin)
            );
            Vector3 B = houseGenerator.transform.TransformPoint(
                new Vector3(xMax, baseY, zMin)
            );
            Vector3 C = houseGenerator.transform.TransformPoint(
                new Vector3(xMin, baseY, zMax)
            );
            Vector3 D = houseGenerator.transform.TransformPoint(
                new Vector3(xMax, baseY, zMax)
            );
            Gizmos.DrawLine(A, B);
            Gizmos.DrawLine(B, D);
            Gizmos.DrawLine(D, C);
            Gizmos.DrawLine(C, A);
        }
    }
#endif
}
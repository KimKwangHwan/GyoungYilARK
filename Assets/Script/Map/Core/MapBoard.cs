using System;
using System.Collections.Generic;
using UnityEngine;

public class MapBoard : MonoBehaviour
{
 

    private readonly Dictionary<Vector2Int, Tile> _cells = new();
    private readonly List<Tile> _spawns = new();
    private readonly List<Tile> _cores = new();
    private readonly Dictionary<GameObject, Tile> _enemyCell = new(); // 적→현재 칸(직전 칸과 비교해 이동 감지)
    private readonly Dictionary<GameObject, List<Tile>> _rangeCoverByUnit = new();

    

    [SerializeField] private float _cellSize = 1f;
    private float _originX, _originZ; // (0,0)칸의 월드 x,z — 월드↔칸 변환 기준
    private Bounds _worldBounds;

    private readonly TileIndexer _indexer = new(); // 좌표(Col/Row) → 1차원 인덱스. _cells와 같은 Tile을 가리키는 배열.
     

    public IReadOnlyDictionary<Vector2Int, Tile> Cells => _cells;
    public int CellCount => _cells.Count;
    public IReadOnlyList<Tile> Spawns => _spawns;
    public IReadOnlyList<Tile> Cores => _cores;
    public bool HasEndpoints => _spawns.Count > 0 && _cores.Count > 0;
    public Bounds WorldBounds => _worldBounds;
    public float CellSize => _cellSize;
    public int Cols => _indexer.Cols;
    public int Rows => _indexer.Rows;

    public GameObject UnitPrefab { get; set; }

    public event Action<Tile> Occupied;
    public event Action<Tile> Vacated;

    /// <summary>적이 어떤 타일에 새로 올라섰을 때(칸 진입). 인자는 진입당한 타일.</summary>
    public event Action<Tile> EnemyEntered;
    /// <summary>적이 타일에서 벗어났을 때(칸 이탈·despawn). 인자는 이탈당한 타일.</summary>
    public event Action<Tile> EnemyExited;

    private void Awake() => Build();

    [ContextMenu("Build")]
    public void Build()
    {
        _cells.Clear();
        _spawns.Clear();
        _cores.Clear();
        _enemyCell.Clear();
        _rangeCoverByUnit.Clear();

        // 씬을 순회해 타일을 모은다(Find 함수 미사용 — GetRootGameObjects + GetComponentsInChildren).
        var tiles = new List<Tile>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            tiles.AddRange(root.GetComponentsInChildren<Tile>(true));
        }

        if (tiles.Count == 0)
        {
            Debug.LogWarning("[MapBoard] Tile 컴포넌트를 가진 타일을 찾지 못했습니다. " +
                "Tools/Map/Bake Tiles From Cubes로 큐브에 Tile을 부착하세요.", this);
            return;
        }

        foreach (Tile tile in tiles)
        {
            tile.ClearRangeCovers();
        }

        // 1) 셀 크기·원점 = 타일 위치 기준(월드↔칸 변환용). 논리 좌표는 각 타일의 State.Col/Row를 신뢰한다.
        float minX = float.MaxValue, minZ = float.MaxValue;
        foreach(Tile tile in tiles)
        {
            minX = Mathf.Min(minX, tile.transform.position.x);
            minZ = Mathf.Min(minZ, tile.transform.position.z);
        }       
        _originX = minX;
        _originZ = minZ;

        // 2) 타일마다 렌더 캐시 + 격자 등록. 상호작용은 좌표 기반으로 처리한다.
        bool hasBounds = false;
        foreach (Tile tile in tiles)
        {
            tile.State.ImportFlags();

            Transform root = tile.transform;
            Renderer[] rends = root.GetComponentsInChildren<Renderer>();
            Bounds bound = CombinedBounds(rends, root.position);
            tile.SetTop(bound.max.y); // 윗면 높이만 Core에 캐시(렌더러/색은 Core가 모른다)

            Vector2Int coord = tile.Coord;
            // 같은 칸에 타일이 겹치면(바닥 위에 고지 큐브를 쌓은 경우) 더 높은 쪽을 대표로 삼는다.
            if (!_cells.TryGetValue(coord, out Tile existing) || tile.WorldTop.y > existing.WorldTop.y)
                _cells[coord] = tile;

            if (tile.isEnemySpawn) _spawns.Add(tile);
            if (tile.Terrain == TerrainType.Core) _cores.Add(tile);

            if (!hasBounds) { _worldBounds = bound; hasBounds = true; }
            else _worldBounds.Encapsulate(bound);
        }

        // 3) 인덱스 격자: 바운딩 박스(Cols×Rows) 기준 1차원 배열. 좌표는 베이크가 min→0으로 정규화해 둔다.
        _indexer.BuildIndexGrid(_cells.Values);

        Debug.Log($"[MapBoard] 타일 {_cells.Count}개 ({Cols}×{Rows}, 셀크기 {_cellSize:0.###}), 스폰 {_spawns.Count}, 본진 {_cores.Count}", this);

        if (!HasEndpoints)
            Debug.LogWarning("[MapBoard] 스폰(isEnemySpawn) 또는 본진(Terrain=Core) 타일을 찾지 못했습니다.", this);
    }

    // ---- 경로 ----

    public List<Tile> GetPath()
    {
        if (!HasEndpoints)
        {
            ClearLanes();
            return null;
        }

        List<Tile> path = Pathfinder.FindPath(
            _spawns, t => t.Terrain == TerrainType.Core, WalkableNeighbors, HeuristicToNearestCore);
        SetLanes(path);

        if (path == null)
            Debug.LogWarning("[MapBoard] 경로 없음.");

        return path;
    }

    private void SetLanes(List<Tile> path)
    {
        ClearLanes();
        if (path == null) return;

        foreach (Tile tile in path)
        {
            if (tile == null || tile.IsEnemySpawn || tile.IsCore) continue;
            tile.State.EnemyLane = true;
        }
    }

    private void ClearLanes()
    {
        foreach (Tile tile in _cells.Values)
        {
            tile.State.EnemyLane = false;
        }
    }

    private IEnumerable<Tile> WalkableNeighbors(Tile tile)
    {
        foreach (Vector2Int dir in GridCalculator.Directions)
            if (_cells.TryGetValue(tile.Coord + dir, out Tile nb) && nb.Walkable)
                yield return nb;
    }

    /// <summary>가장 가까운 본진까지의 맨해튼 거리(A* 휴리스틱). 4방향 균일비용에서 admissible → 최단 보장.</summary>
    private int HeuristicToNearestCore(Tile tile)
    {
        int best = int.MaxValue;
        foreach (Tile core in _cores)
        {
            int d = GridCalculator.GetDistance(tile.Coord, core.Coord);
            if (d < best) best = d;
        }
        return best == int.MaxValue ? 0 : best;
    }

    public List<Vector3> GetWaypoints(float yOffset = 0f)
    {
        var list = new List<Vector3>();
        List<Tile> path = GetPath();
        if (path != null)
            foreach (Tile t in path) list.Add(t.WorldTop + Vector3.up * yOffset);
        return list;
    }

    // ---- 조회 ----

    public bool TryGetCell(Vector2Int coord, out Tile tile) => _cells.TryGetValue(coord, out tile);

    /// <summary>
    /// 화면 광선이 가리키는 타일(좌표·높이 기반). 각 타일의 실제 윗면 높이에서 광선과
    /// 교차해 그 칸 정사각 안에 드는지 보고, 카메라에 가장 가까운(위로 솟아 가려지지 않은) 타일을 고른다.
    /// High처럼 높이가 다른 타일도 윗면을 그대로 클릭할 수 있다.
    /// </summary>
    public Tile CellFromRay(Ray ray)
    {
        if (Mathf.Abs(ray.direction.y) < 1e-6f) return null; // 수평 시선이면 top면과 안 만남
        float half = _cellSize * 0.5f;

        Tile best = null;
        float bestT = float.MaxValue;
        foreach (Tile tile in _cells.Values)
        {
            Vector3 top = tile.WorldTop;
            float t = (top.y - ray.origin.y) / ray.direction.y;
            if (t < 0f || t >= bestT) continue; // 뒤쪽이거나 이미 더 가까운 타일이 있으면 skip

            Vector3 hit = ray.origin + ray.direction * t;
            if (Mathf.Abs(hit.x - top.x) <= half && Mathf.Abs(hit.z - top.z) <= half)
            {
                bestT = t;
                best = tile;
            }
        }
        return best;
    }

    public IEnumerable<Tile> GroundCells
    {
        get { foreach (Tile t in _cells.Values) if (t.Terrain == TerrainType.Ground) yield return t; }
    }

    /// <summary>길찾기가 보는 논리 격자를 ASCII로 콘솔에 출력. 화면 배치와 대조하면 좌표 어긋남/미로 구멍을 바로 판별.</summary>
    [ContextMenu("Debug: Log Grid Map")]
    public void DebugLogGridMap()
    {
        if (_cells.Count == 0) Build();

        var pathSet = new HashSet<Vector2Int>();
        List<Tile> path = GetPath();
        if (path != null) foreach (Tile t in path) pathSet.Add(t.Coord);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[MapBoard] 논리격자 {Cols}×{Rows}  (S=스폰 C=본진 #=고지(벽) .=지상 *=경로 (공백)=없음/Empty)");
        sb.AppendLine($"경로: {(path == null ? "없음" : path.Count + "칸")}");
        for (int row = Rows - 1; row >= 0; row--) // 위(먼 쪽 row 큰 값)부터 아래로
        {
            var line = new System.Text.StringBuilder();
            for (int col = 0; col < Cols; col++)
            {
                Tile t = ByIndex(Index(col, row));
                char ch;
                if (t == null) ch = ' ';
                else if (t.isEnemySpawn) ch = 'S';
                else if (t.Terrain == TerrainType.Core) ch = 'C';
                else if (t.Terrain == TerrainType.High) ch = '#';
                else if (t.Terrain == TerrainType.Empty) ch = ' ';
                else ch = pathSet.Contains(t.Coord) ? '*' : '.';
                line.Append(ch);
            }
            sb.Append("row ").Append(row.ToString("00")).Append(" : ").AppendLine(line.ToString());
        }
        //Debug.Log(sb.ToString(), this);
    }

    // ---- 인덱스 관리 (index = Row * Cols + Col) ----
    // 좌표 딕셔너리(_cells)와 같은 Tile을 가리키는 1차원 배열. 순회·저장·경로/영토 참조·이웃에 유리.

    

    public bool ValidIndex(int index) => _indexer.IsValidIndex(index);
    public bool InBounds(int col, int row) => GridCalculator.IsInGrid(new Vector2Int(col, row), Cols, Rows);
    public bool InBounds(Vector2Int c) => GridCalculator.IsInGrid(c, Cols, Rows);

    /// <summary>(col,row) → 1차원 인덱스.</summary>
    public int Index(int col, int row) =>  _indexer.ConvertCellToIndex(new Vector2Int(col, row));
    public int Index(Vector2Int c) => _indexer.ConvertCellToIndex(c);

    /// <summary>인덱스 → (col,row).</summary>
    public Vector2Int Coord(int index) => _indexer.ConvertIndexToCell(index);

    /// <summary>인덱스로 타일 얻기. 범위 밖·빈칸이면 null.</summary>
    public Tile ByIndex(int index) => _indexer.GetTileOnIndex(index);

    /// <summary>타일의 인덱스. null이면 -1.</summary>
    public int IndexOf(Tile tile)
    {
        if(tile == null) return -1;
        return _indexer.ConvertCellToIndex(tile.Coord);
    }

    /// <summary>
    /// 상하좌우 4방향 이웃의 인덱스. 격자 밖으로 나가는 방향은 빼고 준다(가장자리 wrap 방지).
    /// 예) 3×3 격자에서 가운데(0-based 4)의 이웃 = 1,3,5,7.
    /// </summary>
    public List<int> NeighborIndices(int index)
    {
        var result = new List<int>(4);
        if (!ValidIndex(index)) return result;
        Vector2Int c = Coord(index);
        foreach (Vector2Int d in GridCalculator.Directions)
        {
            Vector2Int n = c + d;
            if (InBounds(n)) result.Add(Index(n)); // 좌표로 경계 검사 → col 끝에서 옆줄로 새지 않음
        }

        return result;
    }

    /// <summary>상하좌우 이웃 중 실제 타일이 있는 것만(빈칸 제외).</summary>
    public List<Tile> Neighbors(int index)
    {
        var result = new List<Tile>(4);
        foreach (int ni in NeighborIndices(index))
            if (ByIndex(ni) is Tile tile) result.Add(tile);
        return result;
    }

    public List<Tile> Neighbors(Tile tile) => Neighbors(IndexOf(tile));

    // ---- 배치 ----

    public bool CanPlace(Vector2Int coord, OccupantKind kind, out string reason)
    {
        if (!_cells.TryGetValue(coord, out Tile tile))
        {
            reason = "타일 없음";
            return false;
        }

        TilePlacementRule.Result r = TilePlacementRule.CanPlace(tile.State, kind);
        reason = r.Reason;
        return r.Allowed;
    }

    public bool TryPlace(Vector2Int coord, GameObject go, OccupantKind kind, float yOffset, out string reason)
    {
        if (!CanPlace(coord, kind, out reason)) return false;

        Tile tile = _cells[coord];
        if (go != null)
        {
            go.transform.position = tile.WorldTop + Vector3.up * yOffset;
        }
        tile.SetOccupant(go, kind);
        Occupied?.Invoke(tile);
        return true;
    }

    public GameObject Vacate(Vector2Int coord)
    {
        if (!_cells.TryGetValue(coord, out Tile tile) || tile.IsEmpty) return null;
        GameObject go = tile.ClearOccupant();
        ClearRangeCover(go);
        Vacated?.Invoke(tile);
        return go;
    }

    // ---- 적 격자 점유 (움직이는 적의 현재 칸 추적 — 좌표 기반) ----
    // 적은 타일 점유(OccupantObject)와 별개다: 한 칸에 여러 마리가 드나들 수 있다.
    // 적 이동 컴포넌트가 월드 위치를 알려주면, 칸이 바뀐 경우에만 이전/새 타일을 갱신한다.

    /// <summary>
    /// [경로 추종 적 권장] 적이 자기 "현재 타일"을 인덱스로 직접 지정한다. 월드 위치 역산 없이
    /// 격자가 단일 소스 → 부동소수 오차와 무관하게 4→5 전환이 정확하다. index = Row*Cols + Col.
    /// 범위 밖 인덱스면 칸 없음으로 처리(빠짐).
    /// </summary>
    public void SetEnemyCell(GameObject enemy, int index)
    {
        if (enemy != null) ApplyEnemyCell(enemy, ByIndex(index));
    }

    /// <summary>[경로 추종 적 권장] 적의 현재 타일을 직접 지정. null이면 격자 밖으로 처리(빠짐).</summary>
    public void SetEnemyCell(GameObject enemy, Tile tile)
    {
        if (enemy != null) ApplyEnemyCell(enemy, tile);
    }

    /// <summary>[자유 이동 적] 월드 위치를 보고받아 WorldToCell로 현재 칸을 역산해 갱신한다.</summary>
    public void MoveEnemy(GameObject enemy, Vector3 world)
    {
        if (enemy == null) return;
        _cells.TryGetValue(WorldToCell(world), out Tile now); // 격자 밖이면 now = null
        ApplyEnemyCell(enemy, now);
    }

    // enter/exit 단일 처리 — 현재 칸(now, null=격자 밖)과 직전 칸을 비교해 바뀐 경우에만 갱신한다.
    // 4→5 이동 시: 4(prev)에서 RemoveEnemy, 5(now)에 AddEnemy. 안 바뀌면 값싸게 return.
    private void ApplyEnemyCell(GameObject enemy, Tile now)
    {
        _enemyCell.TryGetValue(enemy, out Tile prev);
        if (prev == now) return;

        if (prev != null) { prev.RemoveEnemy(enemy); EnemyExited?.Invoke(prev); }
        if (now != null)
        {
            now.AddEnemy(enemy);
            _enemyCell[enemy] = now;
            EnemyEntered?.Invoke(now);
        }
        else
        {
            _enemyCell.Remove(enemy);
        }
    }

    /// <summary>적이 사라질 때 호출하여 현재 칸에서 제거한다.</summary>
    public void RemoveEnemy(GameObject enemy)
    {
        if (enemy == null || !_enemyCell.TryGetValue(enemy, out Tile tile)) return;
        _enemyCell.Remove(enemy);
        if (tile != null) { tile.RemoveEnemy(enemy); EnemyExited?.Invoke(tile); }
    }

    /// <summary>해당 칸 위에 지금 있는 적들(읽기 전용). 타일이 없으면 빈 목록.</summary>
    public IReadOnlyList<GameObject> GetEnemies(Vector2Int coord)
        => _cells.TryGetValue(coord, out Tile tile) ? tile.Enemies : System.Array.Empty<GameObject>();

    public int GetEnemyCount(Vector2Int coord)
        => _cells.TryGetValue(coord, out Tile tile) ? tile.EnemyCount : 0;

    public int GetBlockCap(Vector2Int coord)
        => _cells.TryGetValue(coord, out Tile tile) ? tile.BlockCapacity : 0;

    public int GetBlockedCount(Vector2Int coord)
        => _cells.TryGetValue(coord, out Tile tile) ? tile.BlockedCount : 0;

    public int GetFreeBlock(Vector2Int coord)
        => _cells.TryGetValue(coord, out Tile tile) ? tile.FreeBlock : 0;

    public bool IsBlocked(GameObject enemy)
        => enemy != null && _enemyCell.TryGetValue(enemy, out Tile tile) && tile.IsBlocked(enemy);

    // ---- 아군 공격범위 커버(RangeCover) ----
    // RangeCover = 아군 유닛의 사거리(손전등 빛)가 이 칸을 비추는 것. 유닛이 칸에 올라선 것(Occupant)과 다르다.
    // 범위 표시는 뷰가 할 수 있지만, 실제 판정용 "이 타일이 사거리에 덮였는가"는 Tile 상태에 기록한다.

    public void SetRangeCover(GameObject unit, Vector2Int origin, int range, bool square = false, bool includeCenter = true)
    {
        if (unit == null) return;

        ClearRangeCover(unit);

        var covered = new List<Tile>();
        int safeRange = Mathf.Max(0, range);
        foreach (Tile tile in GetTiles(origin, safeRange, square))
        {
            if (!includeCenter && tile.Coord == origin) continue;
            tile.AddRangeCover(unit);
            covered.Add(tile);
        }

        if (covered.Count > 0)
            _rangeCoverByUnit[unit] = covered;
    }

    public void ClearRangeCover(GameObject unit)
    {
        if (unit == null || !_rangeCoverByUnit.TryGetValue(unit, out List<Tile> covered)) return;

        foreach (Tile tile in covered)
            if (tile != null)
                tile.RemoveRangeCover(unit);

        _rangeCoverByUnit.Remove(unit);
    }

    public bool IsRangeCovered(Vector2Int coord)
        => _cells.TryGetValue(coord, out Tile tile) && tile.IsRangeCovered;

    // ---- 공간 질의 (상호작용 틀) ----
    // 맵은 "몇 칸 이내에 무엇이 있나"만 계산해 후보를 돌려준다. 타겟 선정·공격·데미지는 담당 몫.

    /// <summary>월드 위치를 가장 가까운 칸 좌표로 변환(연속 이동하는 적의 현재 칸 파악용).</summary>
    public Vector2Int WorldToCell(Vector3 world)
        => GridCalculator.GetCellFromWorldPos(world, _originX, _originZ, _cellSize);

    public static int TileDistance(Vector2Int fromCell, Vector2Int toCell)
        => GridCalculator.GetDistance(fromCell, toCell);

    public List<Tile> GetTiles(Vector2Int origin, int range, bool square = false)
    {
        var result = new List<Tile>();
        for (int dx = -range; dx <= range; dx++)
            for (int dy = -range; dy <= range; dy++)
            {
                if (!square && Mathf.Abs(dx) + Mathf.Abs(dy) > range) continue;
                if (_cells.TryGetValue(new Vector2Int(origin.x + dx, origin.y + dy), out Tile tile))
                    result.Add(tile);
            }
        return result;
    }


    private static Bounds CombinedBounds(Renderer[] rends, Vector3 fallback)
    {
        bool has = false;
        Bounds b = new(fallback, Vector3.one * 0.5f);
        foreach (Renderer r in rends)
        {
            if (r == null) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private void GetplacedUnit(GameObject go, OccupantKind kind)
    {
        if (go == null) return;

        if(OccupantKind.MeleeHero == kind)
        {
            UnitPrefab = go;
        }
       
    }
}

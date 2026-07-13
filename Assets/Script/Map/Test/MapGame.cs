using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class MapGame : MonoBehaviour
{
    [System.Serializable]
    public class Placeable
    {
        public string label = "유닛";
        public GameObject prefab;
        public OccupantKind kind = OccupantKind.MeleeHero;
        [Min(0)] public int attackRange;
    }

    private enum PlaceMode { Off, Place, Remove }

    [Header("References")]
    public MapBoard board;
    public IsoCamera isoCamera;
    [Tooltip("타일 색칠(검증용 시각화). 비우면 색 피드백만 꺼지고 게임 로직은 정상. 나중에 통째 제거 대상.")]
    public TilePainter painter;

    [Header("Enemy")]
    public GameObject enemyPrefab;
    public float enemySpeed = 2f;
    public float enemyYOffset = 0.5f;
    public int waveCount = 5;
    public float waveInterval = 0.5f;

    [Header("Placement")]
    [Tooltip("배치할 프리팹 목록.")]
    public List<Placeable> palette = new();
    public float placeYOffset = 0f;
    public bool showPath = true;

    [Header("타일 상태 피드백 (검증용, Game뷰)")]
    [Tooltip("적이 올라온 타일 색칠.")]
    public bool showEnemyTiles = true;
    [Tooltip("아군+적이 겹친(저지 중) 타일 색칠.")]
    public bool showBlocking = true;
    [Tooltip("지상유닛 배치 + 적 저지중 + 원거리 사거리 포함 = 세 조건 동시 성립 타일을 콘솔에 로그(검증용).")]
    public bool logCombatTiles = true;

    /// <summary>배치 직후 호출(종류, 생성된 오브젝트, 타일). 팀원 로직 연결용 훅.</summary>
    public event System.Action<OccupantKind, GameObject, Tile> Placed;

    private Camera _cam;
    private PlaceMode _mode = PlaceMode.Off;
    private int _paletteIndex;

    private readonly List<GameObject> _enemies = new();
    private readonly List<GameObject> _placed = new();
    private readonly HashSet<GameObject> _ranged = new();      // 배치된 원거리 유닛(커버리지 구분용, 검증)
    private readonly Dictionary<GameObject, int> _ranges = new();
    private readonly HashSet<Vector2Int> _combatLogged = new(); // 이미 로그한 교전 타일(엣지 감지, 중복 로그 방지)

    private List<Tile> _pathCells;
    private readonly List<Vector3> _worldPath = new();
    private readonly HashSet<Vector2Int> _pathSet = new();

    private readonly List<Vector2Int> _tinted = new(); // 이번 프레임 임시 하이라이트(사거리/중심) — 다음 프레임에 원복
    private Tile _selectedTile;
    private bool _inputBlocked;
    private string _status = "";

    public string Status => _status;
    public Tile Selected => _selectedTile;
    public string Mode => _mode.ToString();
    public int UnitIndex => _mode == PlaceMode.Place ? _paletteIndex : -1;
    public IReadOnlyList<Placeable> Items => Palette();

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void Start()
    {
        if (board == null)
        {
            _status = "MapBoard 없음";
            Debug.LogError("[MapGame] 씬에 MapBoard가 없습니다. 빈 오브젝트에 MapBoard 컴포넌트를 추가하세요.", this);
            return;
        }
       
        Repath();
    }
 

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;
        UpdateHover();
        HandlePlacementClick();
    }

 

 

    // ---- 경로 ----

    public void Repath()
    {
        ClearPathHighlight();
        _worldPath.Clear();
        _pathSet.Clear();

        _pathCells = null;
        if (board == null || !board.HasEndpoints)
        {
            _status = "스폰/본진을 찾지 못함";
            return;
        }

        _pathCells = board.GetPath();
        if (_pathCells == null)
        {
            _status = "경로 없음";
            return;
        }

        foreach (Tile tile in _pathCells)
        {
            _pathSet.Add(tile.Coord);
            _worldPath.Add(tile.WorldTop);
        }

        _status = $"경로 {_pathCells.Count}칸";
        if (showPath) ApplyPathHighlight();
    }

    private void ApplyPathHighlight()
    {
        if (_pathCells == null || painter == null) return;
        foreach (Tile tile in _pathCells)
            painter.SetColor(tile.Coord, painter.pathColor);
    }

    private void ClearPathHighlight()
    {
        if (_pathCells == null || painter == null) return;
        foreach (Tile tile in _pathCells)
            painter.ClearColor(tile.Coord);
    }

    // ---- 적 ----

    public void SpawnEnemy()
    {
        if (_worldPath.Count < 2)
        {
            _status = "적 경로 없음";
            return;
        }

        if (enemyPrefab == null)
        {
            throw new MissingReferenceException("적 프리팹이 없습니다.");
        }

        Vector3 spawn = _worldPath[0] + Vector3.up * enemyYOffset;
        GameObject go = Instantiate(enemyPrefab, spawn, Quaternion.identity);
        EnemyMover mover = go.GetComponent<EnemyMover>();
        if (mover == null)
        {
            mover = go.AddComponent<EnemyMover>();
        }

        mover.board = board;
        mover.SetPath(_worldPath, enemySpeed, enemyYOffset, _pathCells);
        _enemies.Add(go);
        _status = "적 생성";
    }

    public void SpawnWave() => WaveRoutine().Forget();

    // 오브젝트 파괴 시 자동 취소(코루틴이 destroy에서 멈추던 동작과 동일). 씬 전환·정리 중 스폰 방지.
    private async UniTaskVoid WaveRoutine()
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();
        for (int i = 0; i < waveCount; i++)
        {
            SpawnEnemy();
            await UniTask.Delay(TimeSpan.FromSeconds(waveInterval), cancellationToken: token);
        }
    }

    public void ClearEnemies()
    {
        foreach (GameObject e in _enemies) if (e != null) Destroy(e);
        _enemies.Clear();
        _status = "적 전멸";
    }

    // ---- 배치 ----

    private List<Placeable> Palette()
    {
        return palette;
    }

    private OccupantKind CurrentKind()
    {
        List<Placeable> pal = Palette();
        return (_paletteIndex >= 0 && _paletteIndex < pal.Count) ? pal[_paletteIndex].kind : OccupantKind.MeleeHero;
    }

    private void UpdateHover()
    {
        // 지난 프레임에 칠한 임시 하이라이트(사거리/중심)를 모두 원복하고 이번 프레임 것을 다시 그린다.
        if (painter == null) return; // 시각화 없음 → 색칠 전부 생략(게임 로직은 클릭 처리에서 계속)

        ClearTinted();
        PaintState(); // 적/저지 상태를 매 프레임 색으로 표시(검증용). 사거리는 호버 시에만.

        if (_inputBlocked)
        {
            return;
        }

        Tile tile = PickCellUnderPointer();
        if (tile == null) return;

        // 1) 배치 모드에서 유닛을 갖다 대면 배치될 칸 주변 사거리를 미리 표시.
        if (_mode == PlaceMode.Place)
        {
            OccupantKind kind = CurrentKind();
            if (kind == OccupantKind.MeleeHero || kind == OccupantKind.RangedHero)
                ShowRange(tile.Coord, PreviewRange(kind), painter.rangeColor, skipCenter: true);

            // 중심 칸(배치 가능=초록 / 불가=빨강)을 사거리 위에 덮어 가장 잘 보이게.
            bool ok = board.CanPlace(tile.Coord, kind, out _);
            painter.SetColor(tile.Coord, ok ? painter.okColor : painter.denyColor);
            _tinted.Add(tile.Coord);
            return;
        }

        // 2) 배치 모드가 아닐 때: 배치된 유닛에 마우스를 올린 동안에만 그 유닛 사거리 표시. 벗어나면 자동 원복.
        if (tile.OccupantObject != null)
        {
            int range = UnitRange(tile.OccupantObject, tile.State.Occupant);
            if (range >= 0)
                ShowRange(tile.Coord, range, painter.rangeColor, skipCenter: true);
        }
    }

    /// <summary>center 기준 range칸 타일을 color로 칠하고 이번 프레임 원복 목록에 등록한다.</summary>
    private void ShowRange(Vector2Int center, int range, Color color, bool skipCenter)
    {
        foreach (Tile t in board.GetTiles(center, range, false))
        {
            if (skipCenter && t.Coord == center) continue;
            painter.SetColor(t.Coord, color);
            _tinted.Add(t.Coord);
        }
    }

    private void ClearTinted()
    {
        for (int i = 0; i < _tinted.Count; i++) RestoreCellColor(_tinted[i]);
        _tinted.Clear();
    }

    // 타일 상태를 매 프레임 색으로 표시(검증용). 우선순위: 겹침(저지) > 적. 사거리는 호버/선택 시에만.
    private void PaintState()
    {
        foreach (Tile tile in board.Cells.Values)
        {
            Color color;
            if (showBlocking && !tile.IsEmpty && tile.HasEnemy)
            {
                color = painter.blockColor; // 아군+적 = 오브젝트 2개 이상
            }
            else if (showEnemyTiles && tile.HasEnemy)
            {
                color = painter.enemyColor;
            }
            else
            {
                continue;
            }

            painter.SetColor(tile.Coord, color);
            _tinted.Add(tile.Coord);
        }
    }

    private void HandlePlacementClick()
    {
        if (_inputBlocked)
        {
            return;
        }
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        Tile tile = PickCellUnderPointer();
        if (tile == null) return;

        switch (_mode)
        {
            case PlaceMode.Remove: RemoveAt(tile); break;
            case PlaceMode.Place: PlaceAt(tile); break;
            default: SelectAt(tile); break;
        }
    }

    private void SelectAt(Tile tile)
    {
        _selectedTile = tile;
        _status = $"{tile.Coord} 선택";
    }

    private void PlaceAt(Tile tile)
    {
        List<Placeable> pal = Palette();
        if (_paletteIndex < 0 || _paletteIndex >= pal.Count) return;
        Placeable entry = pal[_paletteIndex];

        if (!board.CanPlace(tile.Coord, entry.kind, out string reason))
        {
            _status = $"{tile.Coord} {reason}";
            return;
        }

        CheckPrefab(entry);
        GameObject go = Instantiate(entry.prefab);

        if (board.TryPlace(tile.Coord, go, entry.kind, placeYOffset, out reason))
        {
            _placed.Add(go);
            if (entry.kind == OccupantKind.RangedHero) _ranged.Add(go); // 커버리지 원거리 구분(검증)
            _ranges[go] = entry.attackRange;
            RegisterCover(go, tile, entry.kind, entry.attackRange);
            _selectedTile = tile;
            _status = $"{tile.Coord}에 {entry.label} 배치";
            Placed?.Invoke(entry.kind, go, tile);
        }
        else
        {
            if (go != null) Destroy(go);
            _status = $"{tile.Coord} {reason}";
        }
    }

    private static void CheckPrefab(Placeable entry)
    {
        if (entry.prefab == null)
        {
            throw new MissingReferenceException($"{entry.label} 프리팹이 없습니다.");
        }

        if (entry.kind == OccupantKind.MeleeHero && entry.prefab.GetComponent<TileBlocker>() == null)
        {
            throw new MissingComponentException($"{entry.prefab.name}에 TileBlocker가 없습니다.");
        }
    }

    private void RegisterCover(GameObject go, Tile tile, OccupantKind kind, int range)
    {
        if (board == null || go == null || tile == null) return;
        if (kind == OccupantKind.Building) return;

        board.SetRangeCover(go, tile.Coord, Mathf.Max(0, range));
    }

    private int UnitRange(GameObject go, OccupantKind kind)
    {
        if (kind == OccupantKind.Building)
        {
            return -1;
        }

        return _ranges.TryGetValue(go, out int range) ? range : -1;
    }

    private int PreviewRange(OccupantKind kind)
    {
        List<Placeable> pal = Palette();
        return kind == OccupantKind.Building ? -1 : Mathf.Max(0, pal[_paletteIndex].attackRange);
    }

    private void RemoveAt(Tile tile)
    {
        GameObject go = board.Vacate(tile.Coord);
        if (go == null) return;
        _placed.Remove(go);
        _ranged.Remove(go);
        _ranges.Remove(go);
        Destroy(go);
        if (_selectedTile == tile)
        {
            _selectedTile = null;
        }
        _status = $"{tile.Coord} 제거";
    }

    public void ClearPlaced()
    {
        foreach (Tile tile in board.Cells.Values)
        {
            if (tile.OccupantObject == null) continue;
            GameObject go = board.Vacate(tile.Coord);
            if (go != null) Destroy(go);
        }
        _placed.Clear();
        _ranged.Clear();
        _ranges.Clear();
        _combatLogged.Clear();
        _selectedTile = null;
        _status = "배치 전부 제거";
    }

    private Tile PickCellUnderPointer()
    {
        if (_cam == null || Mouse.current == null) return null;
        Ray ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        // 타일별 실제 윗면 높이로 정확 피킹(High 등 높이가 달라도 윗면 클릭이 맞는다).
        return board.CellFromRay(ray);
    }

    private void RestoreCellColor(Vector2Int coord)
    {
        if (showPath && _pathSet.Contains(coord)) painter.SetColor(coord, painter.pathColor);
        else painter.ClearColor(coord);
    }

    public void Rescan()
    {
        ClearEnemies();
        ClearPlaced();
        board.Build();
        if (isoCamera != null) isoCamera.Frame();
        Repath();
    }

     

    public void PathToggle()
    {
        showPath = !showPath;
        if (showPath)
        {
            ApplyPathHighlight();
        }
        else
        {
            ClearPathHighlight();
        }
    }

    public void SetUnit(int index)
    {
        List<Placeable> items = Palette();
        if (index < 0 || index >= items.Count)
        {
            return;
        }

        _paletteIndex = index;
        _mode = PlaceMode.Place;
    }

    public void SetRemove()
    {
        _mode = PlaceMode.Remove;
    }

    public void ClearMode()
    {
        _mode = PlaceMode.Off;
    }

    public void SetBlock(bool value)
    {
        _inputBlocked = value;
    }

 
}

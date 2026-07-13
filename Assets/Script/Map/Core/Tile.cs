using System.Collections.Generic;
using UnityEngine;


//타일은 고유 좌표와 인덱스를 가진다
//타일은 지형·점령·용도 3축으로 상태를 가진다
//타일이 알아야 하는 정보는 해당 타일 위 배치된 오브젝트와 저지되는 적이다.
//타일은 인덱스로 인해 인접한 타일로 접근할수있어야 한다.
[DisallowMultipleComponent]
public partial class Tile : MonoBehaviour
{
    [Tooltip("이 타일의 논리 상태(지형·점령·용도 3축). 인스펙터 또는 베이크(Tools/Map)로 저작.")]
    public TileState State = new();

    [Tooltip("적 스폰 지점 여부(경로 시작). MVP 임시 표식 — 이후 EnemyPath/MapData로 이관 예정.")]
    public bool isEnemySpawn;

    // ---- 런타임 캐시(직렬화하지 않음) ----
    private float _topY;

    //타일을 점유한 오브젝트
    public GameObject OccupantObject { get; private set; }
    public int BlockCapacity { get; private set; }

    // 내 위에 올라온 적
    private readonly List<GameObject> _enemies = new();
    private readonly HashSet<GameObject> _covers = new();

    //지금 이 타일 위에 있는 적들(읽기 전용). 없으면 빈 목록.
    public IReadOnlyList<GameObject> Enemies => _enemies;

    /// <summary>이 타일 위에 적이 하나라도 있는가.</summary>
    public bool HasEnemy => _enemies.Count > 0;
    public int EnemyCount => _enemies.Count;
    public int BlockedCount => Mathf.Min(_enemies.Count, BlockCapacity);
    public int FreeBlock => Mathf.Max(0, BlockCapacity - BlockedCount);
    public bool IsBlocking => BlockedCount > 0;
    public bool IsBlockFull => BlockCapacity > 0 && BlockedCount >= BlockCapacity;
    public bool IsCovered => _covers.Count > 0;
    public int CoverCount => _covers.Count;
    public IReadOnlyCollection<GameObject> Covers => _covers;

    public Vector2Int Coord => new(State.Col, State.Row);
    public TerrainType Terrain => State.Terrain;
    public bool IsGround => Terrain == TerrainType.Ground;
    public bool IsHigh => Terrain == TerrainType.High;
    public bool IsCore => Terrain == TerrainType.Core;
    public bool IsEnemyLane => State.EnemyLane;
    public bool IsEnemySpawn => isEnemySpawn;
    public bool IsEmpty => OccupantObject == null;

 
    public GameObject UnitPrefab;
    public OccupantKind UnitKind = OccupantKind.MeleeHero;

    /// <summary>적 통행 가능 지형인가. 고지·빈 타일은 막힘, 지상·본진은 통행(설계: 고지=이동 차단).</summary>
    public bool Walkable => State.Terrain is TerrainType.Ground or TerrainType.Core;

    /// <summary>타일 윗면 중앙의 월드 좌표(배치·경로 웨이포인트 기준).</summary>
    public Vector3 WorldTop => new(transform.position.x, _topY, transform.position.z);

    /// <summary>MapBoard가 스캔 시 윗면 높이를 캐시해 준다(WorldTop·배치·경로 기준).</summary>
    public void SetTop(float topY)
    {
        _topY = topY;
    }

    //비어 있는 타일에 유닛을 배치(런타임 인스턴스를 기록). UnitPrefab은 인스펙터 저작값이라 건드리지 않는다.
    public void SetOccupant(GameObject go, OccupantKind kind)
    {
        OccupantObject = go;
        State.Occupant = kind;
        BlockCapacity = GetCapacity(go, kind);
    }

    //배치되어 있는 유닛을 해제 후 반환.
    public GameObject ClearOccupant()
    {
        GameObject go = OccupantObject;
        OccupantObject = null;
        State.Occupant = OccupantKind.None;
        BlockCapacity = 0; //언덕 타일은 점유 개념이 없으므로 근접만 우선 해당.
        return go;
    }

    public bool IsBlocked(GameObject enemy)
    {
        if (BlockCapacity <= 0 || enemy == null) return false;
        int index = _enemies.IndexOf(enemy);
        return index >= 0 && index < BlockCapacity;
    }

    //타일에 적 진입 등록
    public void AddEnemy(GameObject enemy)
    {
        if (enemy != null && !_enemies.Contains(enemy)) _enemies.Add(enemy);
    }

    //타일에 적 이탈 등록
    public void RemoveEnemy(GameObject enemy) => _enemies.Remove(enemy);

    public void AddCover(GameObject unit)
    {
        if (unit != null) _covers.Add(unit);
    }

    public void RemoveCover(GameObject unit)
    {
        if (unit != null) _covers.Remove(unit);
    }

    public void ClearCovers()
    {
        _covers.Clear();
    }

    private static int GetCapacity(GameObject go, OccupantKind kind)
    {
        if (go == null || kind != OccupantKind.MeleeHero) return 0;
        return go.TryGetComponent(out TileBlocker blocker) ? blocker.Capacity : 0;
    }
}

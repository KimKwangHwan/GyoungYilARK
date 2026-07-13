using UnityEngine;
using System;

public class AreaExpand : MonoBehaviour
{
    /// <summary>OriginCore의 본진 기준으로 L자 영역을 점령한다.</summary>
    /// 초기 영역을 미점령으로 전체 초기화
    /// 이후 Start에서 areaSize만큼 L자 영역을 점령(인스펙터 AreaSize값)

    [SerializeField] private MapBoard _board;   // 인스펙터 주입. 같은 오브젝트가 아니어도 됨(자동탐색 금지).
    [SerializeField] private int areaSize = 4;  // 본진 코너 기준 초기 정사각형 한 변(칸).

    public int AreaSize => areaSize;            // 외부 확인용
    public bool IsMaxArea => areaSize >= _board.Cols || areaSize >= _board.Rows;

    public event Action<int> OnAreaSizeChanged; // 영역 확장 시 외부에 알림.

    private void Awake()
    {
        if (_board == null)
        {
            _board = GetComponent<MapBoard>(); // 주입이 비었을 때만 같은 오브젝트에서 폴백.
        }
    }
    private void Start()
    {
        if (_board == null) return;

        ResetTerritory(); // 초기 1회: 전체 미점령으로 초기화.
        for (int edge = 0; edge < areaSize; edge++) // 초기 정사각형 = L자 링 0..areaSize-1 누적.
        {
            ClaimArea(edge);
        }
    }
    // 초기 baseline 정리용 1회 리셋. 확장에는 쓰지 않는다(점령은 누적, 되돌리지 않음).
    private void ResetTerritory()
    {
        foreach (Tile tile in _board.Cells.Values)
        {
            tile.State.Territory = TerritoryState.Unclaimed;
        }
    }
    // 점령 원점 좌표 보드의 core부분이 원점이 된다. 
    private Vector2Int OriginCore()
    {
        return _board.Cores[0].Coord;
    }
    // 본진 코너 기준 offset(edge) 위치에 L자(우측 열 + 하단 행) 한 줄을 점령한다.
    private void ClaimArea(int edge)
    {
        //행/열 
        Vector2Int origin = OriginCore();
        for (int row = 0; row <= edge; row++) // 우측 열 한줄
        {
            ClaimTile(origin.x + edge, origin.y + row);
        }
        for (int col = 0; col < edge; col++)  // 좌측 행 
        {
            ClaimTile(origin.x + col, origin.y + edge);
        }
    }
    public bool TryExpandArea()
    {
        // 확장 버튼 등에서 호출. 본진 코너에서 맵 안쪽으로 L자 한 줄씩 누적 확장한다.
        // 정식 페이즈 시스템 완성 시 그쪽을 참조하도록 변경.
        if (_board == null) return false;      // 보드가 주입되지 않았으면 확장 불가.
        if (IsMaxArea) return false;           // 최대 영역이면 확장 불가.

        ClaimArea(areaSize); // 현재 정사각형 한 변(areaSize) 위치에 L자 한 줄 추가.
        areaSize++;
        OnAreaSizeChanged?.Invoke(areaSize);
        return true;
    }

    private void ClaimTile(int col, int row)
    {
        if (!_board.TryGetCell(new Vector2Int(col, row), out Tile tile)) return; // 없는 칸이면 건너뜀.
        if (tile.Terrain is not (TerrainType.Ground or TerrainType.High)) return; // 테두리·본진은 점령 제외.
        if (tile.IsEnemySpawn) return; 
        
        //해당 영역 점령                                             
        tile.State.Territory = TerritoryState.Claimed;
    }

}

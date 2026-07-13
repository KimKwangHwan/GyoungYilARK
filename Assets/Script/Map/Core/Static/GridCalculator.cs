using UnityEngine;

/// <summary>
/// [GridCalculator] 그리드 좌표 관련 "계산 전담" 정적 클래스.
///   월드 위치 → 칸 번호, 칸↔배열 인덱스, 두 칸 사이 거리 같은
///                  순수한 산수(나눗셈·뺄셈·곱셈)만 담당한다.
 
/// </summary>
public static class GridCalculator
{

    public static readonly Vector2Int Right = new(1, 0);   // col + 1
    public static readonly Vector2Int Left = new(-1,  0);   // col - 1
    public static readonly Vector2Int Up = new(0, 1);   // row + 1
    public static readonly Vector2Int Down = new( 0, -1);   // row - 1


    public static readonly Vector2Int[] Directions = { Right, Left, Up, Down };

    //월드 좌표 -> 칸 좌표 

    public static Vector2Int GetCellFromWorldPos(Vector3 worldPos, float originX, float originZ, float cellSize)
    {
        // 월드 좌표를 칸 좌표로 변환하는 공식
        return new Vector2Int(
            Mathf.RoundToInt((worldPos.x - originX) / cellSize),
            Mathf.RoundToInt((worldPos.z - originZ) / cellSize)
        );
    }

    //WorldToCell로 변환한 칸 좌표를 1차원 배열 인덱스로 변환
    public static int GetIndexFromCell(int col, int row, int width)
    {
        return row * width + col;
    }
    public static int GetIndexFromCell(Vector2Int cell, int width)
    {
        return GetIndexFromCell(cell.x, cell.y, width);
    }
    //1차원 배열 번호 -> 2차원 칸 좌표: Index의 역방향
    public static Vector2Int GetCellFromIndex(int index, int width)
    {
        return new Vector2Int(index % width, index / width);
    }

    //좌표가 격자 범위 안에 있는지 여부 정적 bool 함수
    public static bool IsInGrid(Vector2Int cell, int width, int height)
    {
        return cell.x >= 0 
        && cell.x < width 
        && cell.y >= 0 
        && cell.y < height;
    }

    public static int GetDistance(Vector2Int fromCell, Vector2Int toCell)
    {
        int gapX = Mathf.Abs(fromCell.x - toCell.x);
        int gapY = Mathf.Abs(fromCell.y - toCell.y);
        return gapX + gapY;
    }
   
    


}

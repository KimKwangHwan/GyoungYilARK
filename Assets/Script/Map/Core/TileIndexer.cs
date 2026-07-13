
using System;
using System.Collections.Generic;
using UnityEngine;



/// <summary>
/// 좌표(Col/Row)가 이미 부여된 Tile들을 받아 1차원 인덱스를 산출.
/// index → Tile 대응표를 구성·제공.
///
///  넘겨받은 타일만 다룬다. 좌표 부여는 TilePosBaker담당.
/// 전제: 좌표는 0 이상(TilePosBaker가 min→0 정규화). 같은 좌표 중복은 호출 측이 정리해 넘긴다.
/// </summary>
public class TileIndexer 
{
    private Tile[] _byIndex = Array.Empty<Tile>();

    public int Cols { get; private set; }
    public int Rows { get; private set; }

    public void BuildIndexGrid(IEnumerable<Tile> tiles)
    {
        int maxCol = 0;
        int maxRow = 0;
        foreach (Tile tile in tiles)
        {
            if (tile.State.Col > maxCol) maxCol = tile.State.Col;
            if (tile.State.Row > maxRow) maxRow = tile.State.Row;
        }

        Cols = maxCol + 1;
        Rows = maxRow + 1;
        _byIndex = new Tile[Cols * Rows];
        
        foreach (Tile tile in tiles)
        {
            _byIndex[GridCalculator.GetIndexFromCell(tile.Coord, Cols)] = tile;
        }
    }


    // 좌표 ↔ 인덱스 변환 
    public int ConvertCellToIndex(Vector2Int cell)
    {
        return GridCalculator.GetIndexFromCell(cell, Cols);
    }
    // 좌표 ↔ 인덱스 변환
    public Vector2Int ConvertIndexToCell(int index)
    {
        return GridCalculator.GetCellFromIndex(index, Cols);
    }

    public bool IsValidIndex(int index) 
    {
        // index가 0 이상이고, _byIndex 배열 길이보다 작은지 확인
        return index >= 0 && index < _byIndex.Length;
    }

    // 인덱스 → Tile. 없는 칸은 null 반환.
    public Tile GetTileOnIndex(int index)
    {
        if (!IsValidIndex(index)) return null;
        return _byIndex[index];
    }
}

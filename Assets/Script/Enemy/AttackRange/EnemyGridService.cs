using UnityEngine;

// 좌표 변환 담당 (타일맵 의존 지점 = seam).
// ★ 실제 타일맵 오면 이 클래스 내부만 교체하면 됨. 나머지 게임 코드는 안 건드림.
public static class EnemyGridService
{
    // 임시 구현: 1 유닛 = 1 칸
    public static Vector2Int WorldToCell(Vector3 world)
        => new(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.y));

    public static Vector3 CellToWorld(Vector2Int cell)
        => new(cell.x, 0f ,cell.y);
}

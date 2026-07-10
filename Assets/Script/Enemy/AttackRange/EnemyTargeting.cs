using System;
using System.Collections.Generic;
using UnityEngine;

// 범위/판정 로직 (순수 — 타일맵 와도 안 바뀜). 모든 사거리 판정은 여기로 통일한다.
public static class EnemyTargeting
{
    public static int Distance(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    public static bool InRange(Vector2Int a, Vector2Int b, int range)
        => Distance(a, b) <= range;

    public static bool InRange(Vector3 from, Vector3 to, int range)
        => InRange(EnemyGridService.WorldToCell(from), EnemyGridService.WorldToCell(to), range);

    public static T FindNearest<T>(Vector3 from, int range, IEnumerable<T> candidates, Func<T, Vector3> posOf)
        where T : class
    {
        var origin = EnemyGridService.WorldToCell(from);
        T best = null;
        int bestDist = int.MaxValue;
        foreach (var c in candidates)
        {
            if (c == null) continue;
            int d = Distance(origin, EnemyGridService.WorldToCell(posOf(c)));
            if (d <= range && d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    // origin 기준 range 칸 마름모 안의 모든 셀을 열거. (타일 점유맵 조회용)
    // includeSelf=false 면 자기 칸(0,0)은 제외.
    public static IEnumerable<Vector2Int> CellsInRange(Vector2Int origin, int range, bool includeSelf = true)
    {
        for (int dx = -range; dx <= range; dx++)
        {
            int remain = range - Mathf.Abs(dx);          // ★ 마름모 핵심: x가 멀수록 y 범위 축소
            for (int dy = -remain; dy <= remain; dy++)
            {
                if (!includeSelf && dx == 0 && dy == 0) continue;

                yield return origin + new Vector2Int(dx, dy);
            }
        }
    }

    // 할당 없는 버전: 매 프레임/매 공격 호출 시 buffer 를 재사용. (yield 버전은 열거자 할당 발생)
    public static void CellsInRange(Vector2Int origin, int range, List<Vector2Int> buffer, bool includeSelf = true)
    {
        buffer.Clear();
        for (int dx = -range; dx <= range; dx++)
        {
            int remain = range - Mathf.Abs(dx);
            for (int dy = -remain; dy <= remain; dy++)
            {
                if (!includeSelf && dx == 0 && dy == 0) continue;
                buffer.Add(origin + new Vector2Int(dx, dy));
            }
        }
    }
}

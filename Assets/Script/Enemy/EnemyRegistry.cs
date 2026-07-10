using System.Collections.Generic;

// 살아있는 적 전역 목록. 사거리/오라/힐 등 "근처 적 찾기"에 쓴다.
// (다음 스프린트에 그리드 기반으로 확장 예정 — 지금은 리스트로 충분)
public static class EnemyRegistry
{
    public static readonly List<EnemyBase> Alive = new();

    public static void Register(EnemyBase e)
    {
        if (e != null && !Alive.Contains(e)) Alive.Add(e);
    }

    public static void Unregister(EnemyBase e)
    {
        Alive.Remove(e);
    }
}

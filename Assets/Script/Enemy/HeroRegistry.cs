using System.Collections.Generic;

// 살아있는 영웅(타워) 전역 목록. 적이 공격 대상을 찾을 때 쓴다.
// ★ 실제로 채우려면 Hero 가 OnEnable/OnDisable 에서 Register/Unregister 를 호출해야 함.
// 나중에 타일로 변경
public static class HeroRegistry
{
    public static readonly List<Hero> Alive = new();

    public static void Register(Hero h)
    {
        if (h != null && !Alive.Contains(h)) Alive.Add(h);
    }

    public static void Unregister(Hero h)
    {
        Alive.Remove(h);
    }
}

// 포트폴리오용 파티클 개수 제한(VFX budget) 데모 기능. Hero.SpawnEffect(연출용 1회성 타격
// 이펙트 경로) 한 곳에서만 획득/해제가 짝지어 호출된다 — 장판/빔/상시 이펙트 등 다른 스폰
// 경로와는 카운트가 섞이지 않는다.
public static class ParticleBudget
{
    public static bool LimitEnabled = true;
    public static int MaxConcurrent = 10;

    private static int activeCount;
    public static int ActiveCount => activeCount;

    public static bool TryAcquire()
    {
        if (!LimitEnabled) return true;
        if (activeCount >= MaxConcurrent) return false;
        activeCount++;
        return true;
    }

    public static void Release()
    {
        if (activeCount > 0) activeCount--;
    }
}

// ParticleBudget(Hero용)과 동일한 구조의 적(Enemy) 전용 VFX 예산. Hero 쪽 ActiveCount 지표와
// 카운트가 섞이지 않도록 별도 클래스로 분리했다. PoolManager.SpawnBudgeted/Despawn 한 쌍에서만
// 획득/해제가 이뤄진다.
public static class EnemyParticleBudget
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

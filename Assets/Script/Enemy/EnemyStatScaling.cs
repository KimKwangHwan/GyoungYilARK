using UnityEngine;

public static class EnemyStatScaling
{
    public readonly struct Stats
    {
        public readonly float Hp;
        public readonly float Attack;
        public readonly float Defense;

        public Stats(float hp, float attack, float defense)
        {
            Hp = hp;
            Attack = attack;
            Defense = defense;
        }
    }

    public static Stats Compute(EnemyTable.Data data, EnemyClass cls, int dayCount)
    {
        int fiveDayStep = dayCount / 5;
        return new Stats(
            (data.Health + (dayCount * data.UpHealthScale)) * RegionHpScale(cls,dayCount),
            (data.Attack + (data.UpAttackScale * fiveDayStep)) * RegionAttackScale(cls,dayCount),
            (data.Defense + (data.UpDefenseScale * fiveDayStep)) * RegionDefenseScale(cls,dayCount));
    }

    public static Stats Compute(EnemyTable.Data data, string cls, int dayCount)
        => Compute(data, ParseClass(cls), dayCount);

    public static EnemyClass ParseClass(string raw)
        => !string.IsNullOrEmpty(raw) && System.Enum.TryParse(raw.Trim(), true, out EnemyClass c)
            ? c
            : EnemyClass.Normal;

    private const string KeyStepRounds = "BossStatStepRounds";
    private const string KeyDeepenDay = "BossStatDeepenDay";
    private const string KeyHpStepEarly = "BossHpStepEarly";
    private const string KeyHpStepLate = "BossHpStepLate";
    private const string KeyAttackStepEarly = "BossAttackStepEarly";
    private const string KeyAttackStepLate = "BossAttackStepLate";
    private const string KeyDefenseStepEarly = "BossDefenseStepEarly";
    private const string KeyDefenseStepLate = "BossDefenseStepLate";


    private static EnemyScaleTable.Data RegionRow()
        => DataTableManager.EnemyScaleTable?.Get(SpawnerManager.UnlockedRegionCount);

    public static float RegionHpScale(EnemyClass cls, int dayCount)
    {
        var row = RegionRow();
        if (row == null) return 1f;
        float baseScale = cls == EnemyClass.Boss ? row.BossHp : row.NormalHp;
        return baseScale + BossFullUnlockBonus(cls, dayCount, KeyHpStepEarly, KeyHpStepLate);
    }

    public static float RegionDefenseScale(EnemyClass cls, int dayCount)
    {
        var row = RegionRow();
        if (row == null) return 1f;
        float baseScale = cls == EnemyClass.Boss ? row.BossDefense : row.NormalDefense;
        return baseScale + BossFullUnlockBonus(cls, dayCount, KeyDefenseStepEarly, KeyDefenseStepLate);
    }

    public static float RegionAttackScale(EnemyClass cls, int dayCount)
    {
        var row = RegionRow();
        if (row == null) return 1f;
        float baseScale = cls == EnemyClass.Boss ? row.BossAttack : row.NormalAttack;
        return baseScale + BossFullUnlockBonus(cls, dayCount, KeyAttackStepEarly, KeyAttackStepLate);
    }

    private static float BossFullUnlockBonus(EnemyClass cls, int dayCount, string earlyKey, string lateKey)
    {
        if (cls != EnemyClass.Boss) return 0f;

        var config = DataTableManager.EnemyScaleConfigTable;
        if (config == null) return 0f;

        // 0이면 나눌 수 없다 — 설정이 비었다는 뜻이므로 추가 성장 없이 넘어간다.
        int stepRounds = config.GetInt(KeyStepRounds);
        if (stepRounds <= 0) return 0f;

        int rounds = SpawnerManager.RoundsSinceFullUnlock;
        int steps = rounds / stepRounds;
        if (steps <= 0) return 0f;

        int fullUnlockDay = dayCount - rounds;
        int earlyRounds = Mathf.Clamp(config.GetInt(KeyDeepenDay) - fullUnlockDay, 0, rounds);
        int earlySteps = earlyRounds / stepRounds;
        int lateSteps = steps - earlySteps;

        return earlySteps * config.Get(earlyKey) + lateSteps * config.Get(lateKey);
    }
}

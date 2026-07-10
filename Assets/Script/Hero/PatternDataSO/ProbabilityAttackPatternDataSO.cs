using UnityEngine;

[CreateAssetMenu(menuName = "HeroPattern/AttackPatternData/Probaility")]
public class ProbabilityAttackPatternDataSO : AttackPatternDataSO
{
    public AttackDataSO strongAttack;
    [Range(0f, 1f)] public float strongChance = 0.2f;

    public override AttackDataSO GetStep(int comboIndex)
    {
        return Random.value < strongChance ? strongAttack : patterns[comboIndex];
    }
}
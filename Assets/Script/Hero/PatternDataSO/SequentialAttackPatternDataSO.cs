using UnityEngine;

[CreateAssetMenu(menuName = "HeroPattern/AttackPatternData/Sequential")]
public class SequentialAttackPatternData : AttackPatternDataSO
{
    public override AttackDataSO GetStep(int comboIndex)
    {
        return patterns[comboIndex % patterns.Length];
    }
}

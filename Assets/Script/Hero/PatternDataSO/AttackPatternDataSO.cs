using UnityEngine;

[CreateAssetMenu(fileName = "AttackPatternDataSO", menuName = "HeroPattern/AttackPatternData")]
public abstract class AttackPatternDataSO : ScriptableObject
{
    public AttackDataSO[] patterns;
    public abstract AttackDataSO GetStep(int comboIndex);
}

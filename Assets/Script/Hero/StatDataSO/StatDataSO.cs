using UnityEngine;

[CreateAssetMenu(fileName = "StatDataSO", menuName = "StatData/HeroStat")]
public class StatDataSO : ScriptableObject
{
    public float maxHp;
    public float attackPower;
    public float defence;
    public float maxSp;
    public float attackSpeed;
    public float spRecover;
    public float blockCount;
    public float coolDownPer;
    public float criticalPer;
    public float criticalDmg;
}

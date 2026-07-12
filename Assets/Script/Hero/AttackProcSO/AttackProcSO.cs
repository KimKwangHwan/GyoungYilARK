using UnityEngine;

public abstract class AttackProcSO : ScriptableObject
{
    public AttackDataSO triggerOnAttack;  // null = 아무 공격에나 반응
    public AttackDataSO procAttack;       // null = 트리거 공격 반복
    public bool allowRecursiveProc = false;

    public abstract bool ShouldProc(AttackDataSO source, AttackContext ctx);

    public AttackDataSO GetProcAttack(AttackDataSO source) => procAttack != null ? procAttack : source;
}

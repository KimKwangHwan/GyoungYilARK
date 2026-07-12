using UnityEngine;

public abstract class AttackSelectorSO : ScriptableObject
{
    public AttackDataSO specialAttack;

    // 조건 충족 시 specialAttack 반환, 아니면 null (→ basePattern 폴백)
    public abstract AttackDataSO Select(int hitCount, AttackContext ctx);
}

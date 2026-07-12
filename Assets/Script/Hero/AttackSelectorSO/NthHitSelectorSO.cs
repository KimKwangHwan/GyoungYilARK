using UnityEngine;

[CreateAssetMenu(fileName = "NthHitSelector", menuName = "HeroAttack/Selector/NthHit")]
public class NthHitSelectorSO : AttackSelectorSO
{
    [Min(1)] public int everyN = 4;

    public override AttackDataSO Select(int hitCount, AttackContext ctx)
        => (hitCount + 1) % everyN == 0 ? specialAttack : null;
}

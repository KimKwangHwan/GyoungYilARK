using UnityEngine;

[CreateAssetMenu(fileName = "ProbabilitySelector", menuName = "HeroAttack/Selector/Probability")]
public class ProbabilitySelectorSO : AttackSelectorSO
{
    [Range(0f, 1f)] public float chance = 0.2f;

    public override AttackDataSO Select(int hitCount, AttackContext ctx)
        => Random.value < chance ? specialAttack : null;
}

using UnityEngine;

[CreateAssetMenu(fileName = "ProbabilityProc", menuName = "HeroAttack/Proc/Probability")]
public class ProbabilityProcSO : AttackProcSO
{
    [Range(0f, 1f)] public float chance = 0.3f;

    public override bool ShouldProc(AttackDataSO source, AttackContext ctx)
    {
        if (triggerOnAttack != null && triggerOnAttack != source) return false;
        return Random.value < chance;
    }
}

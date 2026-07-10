using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

[CreateAssetMenu(menuName = "HeroAttack/AttackDataSO/RangedAttack")]
public class RangedAttackDataSO : AttackDataSO
{
    public override async UniTask Execute(AttackContext ctx, CancellationToken ct)
    {
        ctx.anim.SetTrigger(AnimHash);
        ctx.bowAnim.SetTrigger(AnimHash);
        ctx.arrowAnim.SetTrigger(AnimHash);
        await ctx.WaitForAnimEvent("Attack", ct);
        Debug.Log("Ranged Attack");
    }
}

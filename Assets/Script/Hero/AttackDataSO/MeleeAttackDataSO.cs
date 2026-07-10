using Cysharp.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "HeroAttack/AttackDataSO/MeleeAttack")]
public class MeleeAttackDataSO : AttackDataSO
{
    public override async UniTask Execute(AttackContext ctx, CancellationToken ct)
    {
        ctx.anim.SetTrigger(AnimHash);
        await ctx.WaitForAnimEvent("Attack", ct);
        Debug.Log("Melee Attack");
    }
}

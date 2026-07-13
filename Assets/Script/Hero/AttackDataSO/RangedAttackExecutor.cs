using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

public class RangedAttackExecutor : IAttackExecutor
{
    private readonly Dictionary<AttackDataSO, int> sequentialIndices = new();

    public async UniTask Execute(AttackDataSO data, AttackContext ctx, CancellationToken ct)
    {
        string trigger = PickTrigger(data);
        ctx.anim.SetTrigger(trigger);
        ctx.bowAnim.SetTrigger("Attack");
        ctx.arrowAnim.SetTrigger("Attack");
        await ctx.WaitForAnimEvent("Attack", ct);

        IObjectPool<Projectile> pool = ctx.getProjectilePool(data.projectilePrefab);
        Projectile arrow = pool.Get();
        arrow.transform.SetPositionAndRotation(ctx.muzzle.position, ctx.muzzle.rotation);
        arrow.Launch(ctx.target, data.attackDamage, pool);
    }

    private string PickTrigger(AttackDataSO data)
    {
        var triggers = data.animTriggers;
        if (triggers == null || triggers.Length == 0)
        {
            Debug.LogError($"[RangedAttackExecutor] '{data.name}' 의 animTriggers가 비어 있습니다.");
            return string.Empty;
        }
        if (triggers.Length == 1) return triggers[0];

        if (data.selectMode == AnimSelectMode.Random)
            return triggers[Random.Range(0, triggers.Length)];

        sequentialIndices.TryGetValue(data, out int idx);
        sequentialIndices[data] = (idx + 1) % triggers.Length;
        return triggers[idx];
    }
}

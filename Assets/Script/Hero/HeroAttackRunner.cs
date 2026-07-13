using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

public class HeroAttackRunner
{
    private const int MaxProcDepth = 5;

    private readonly AttackDataSO[] basePattern;
    private readonly List<AttackSelectorSO> selectors;
    private readonly List<AttackProcSO> procs;
    private readonly IAttackExecutor executor;

    public int HitCount { get; private set; }
    public bool IsExecuting { get; private set; }

    public HeroAttackRunner(
        AttackDataSO[] basePattern,
        List<AttackSelectorSO> selectors,
        List<AttackProcSO> procs,
        IAttackExecutor executor)
    {
        this.basePattern = basePattern;
        this.selectors = selectors;
        this.procs = procs;
        this.executor = executor;
    }

    public AttackDataSO ResolveNext(AttackContext ctx)
    {
        foreach (var sel in selectors)
        {
            var result = sel.Select(HitCount, ctx);
            if (result != null) return result;
        }
        return basePattern[HitCount % basePattern.Length];
    }

    public async UniTask ExecuteNext(AttackContext ctx, CancellationToken ct)
    {
        if (IsExecuting) return;
        IsExecuting = true;
        try
        {
            var attack = ResolveNext(ctx);
            HitCount++;
            await ExecuteWithProcs(attack, ctx, ct, depth: 0);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async UniTask ExecuteWithProcs(
        AttackDataSO attack, AttackContext ctx, CancellationToken ct, int depth)
    {
        if (depth >= MaxProcDepth) return;

        await executor.Execute(attack, ctx, ct);
        
        foreach (var proc in procs)
        {
            if (depth > 0 && !proc.allowRecursiveProc) continue;
            if (!proc.ShouldProc(attack, ctx)) continue;
            await ExecuteWithProcs(proc.GetProcAttack(attack), ctx, ct, depth + 1);
        }
    }
}

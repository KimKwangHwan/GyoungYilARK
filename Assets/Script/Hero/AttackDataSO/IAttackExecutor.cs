using System.Threading;
using Cysharp.Threading.Tasks;

public interface IAttackExecutor
{
    UniTask Execute(AttackDataSO data, AttackContext ctx, CancellationToken ct);
}

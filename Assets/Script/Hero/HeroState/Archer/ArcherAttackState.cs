using Cysharp.Threading.Tasks;
using UnityEngine;

public class ArcherAttackState : HeroAttackState
{
    private Archer archer;
    private HeroAttackRunner runner;

    public ArcherAttackState(Archer archer, HeroStateMachine stateMachine)
        : base(archer, stateMachine)
    {
        this.archer = archer;
    }

    public override void Enter()
    {
        base.Enter();
        runner = new HeroAttackRunner(archer.BasePattern, archer.Selectors, archer.Procs, new RangedAttackExecutor());
    }

    public override void Update()
    {
        base.Update();
        timer += Time.deltaTime;
        if (timer >= archer.AttackSpeed)
        {
            timer = 0f;
            TryExecuteCurrentStep();
        }
    }

    protected override void TryExecuteCurrentStep()
    {
        if (runner.IsExecuting) return;
        runner.ExecuteNext(archer.Context, attackCts.Token).Forget();
    }
}

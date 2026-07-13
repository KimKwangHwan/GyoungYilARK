using Cysharp.Threading.Tasks;
using UnityEngine;

public class SwordManAttackState : HeroAttackState
{
    private SwordMan swordMan;
    private HeroAttackRunner runner;

    public SwordManAttackState(SwordMan swordMan, HeroStateMachine stateMachine)
        : base(swordMan, stateMachine)
    {
        this.swordMan = swordMan;
    }

    public override void Enter()
    {
        base.Enter();
        runner ??= new HeroAttackRunner(swordMan.BasePattern, swordMan.Selectors, swordMan.Procs, new MeleeAttackExecutor());
        TryExecuteCurrentStep();
    }

    public override void Update()
    {
        base.Update();
        if (stateMachine.CurrentState != this) return;
        timer += Time.deltaTime;
        if (timer >= swordMan.AttackSpeed)
        {
            timer = 0f;
            TryExecuteCurrentStep();
        }
    }

    protected override void TryExecuteCurrentStep()
    {
        if (runner.IsExecuting) return;
        runner.ExecuteNext(swordMan.Context, attackCts.Token).Forget();
    }
}

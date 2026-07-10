using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public class HeroAttackState : HeroState
{
    protected float timer;
    protected CancellationTokenSource attackCts;

    public HeroAttackState(Hero hero, HeroStateMachine stateMachine) : base(hero, stateMachine)
    {
        
    }

    public override void Enter()
    {
        timer = 0f;
        attackCts = new CancellationTokenSource();
        hero.AttackData.Execute(hero.Context, attackCts.Token).Forget();
    }

    public override void Exit()
    {
        attackCts?.Cancel();
        attackCts?.Dispose();
        attackCts = null;
    }

    public override void Update()
    {
        if (hero.Context.target == null)
        {
            stateMachine.ChangeState(hero.IdleState);
            return;
        }
        Vector3 aimVector = hero.Context.target.transform.position - hero.transform.position;
        aimVector.y = 0f;

        if (aimVector.sqrMagnitude > 0.00001f)
            hero.transform.rotation = Quaternion.LookRotation(aimVector);
    }

    protected virtual void TryExecuteCurrentStep()
    {
        
    }
}

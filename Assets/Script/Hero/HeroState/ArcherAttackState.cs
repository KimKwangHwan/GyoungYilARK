using Cysharp.Threading.Tasks;
using UnityEngine;

public class ArcherAttackState : HeroAttackState
{
    private Archer archer;
    public ArcherAttackState(Archer archer, HeroStateMachine stateMachine) : base(archer, stateMachine)
    {
        this.archer = archer;
    }

    protected override void PlayAttack()
    {
        base.PlayAttack();
        archer.ArrowAnim.SetTrigger(HeroAnimHash.attack);
        archer.BowAnim.SetTrigger(HeroAnimHash.attack);
    }

    protected override async UniTask Attack()
    {
        await WaitForAnimEvent("Attack", attackCts.Token);
        Debug.Log("Archer Attack");
    }
}

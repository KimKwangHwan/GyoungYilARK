using Cysharp.Threading.Tasks;
using UnityEngine;

public class SwordManAttackState : HeroAttackState
{
    private int comboIndex = 0;
    private SwordMan swordMan;
    public SwordManAttackState(SwordMan swordMan, HeroStateMachine stateMachine) : base(swordMan, stateMachine)
    {
        this.swordMan = swordMan;
    }

    public override void Update()
    {
        base.Update();
        timer += Time.deltaTime;
        if (timer >= hero.AttackData.attackSpeed)
        {
            timer = 0f;
            //hero.AttackData.Execute(hero.Context, attackCts.Token).Forget();
            TryExecuteCurrentStep();
        }
    }

    protected override void TryExecuteCurrentStep()
    {
        var step = swordMan.AttackPattern.GetStep(comboIndex);
        if (step.CanExecute(swordMan.Context))
        {
            swordMan.Anim.SetInteger("ComboIndex", comboIndex % swordMan.AttackPattern.patterns.Length);
            step.Execute(swordMan.Context, attackCts.Token).Forget();
        }
        comboIndex++;
    }
}

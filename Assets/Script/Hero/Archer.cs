using UnityEngine;

public class Archer : Hero
{
    [SerializeField] private Animator bowAnim;
    [SerializeField] private Animator arrowAnim;
    public Animator BowAnim => bowAnim;
    public Animator ArrowAnim => arrowAnim;

    protected override void Awake()
    {
        base.Awake();
        attackState = new ArcherAttackState(this, stateMachine);
        context = new AttackContext
        {
            self = transform,
            target = null,
            anim = Anim,
            bowAnim = BowAnim,
            arrowAnim = ArrowAnim,
            animEvents = AnimEvents
        };
        range = 5;
    }
}

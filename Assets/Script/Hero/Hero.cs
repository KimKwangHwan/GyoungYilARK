using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Hero : MonoBehaviour, IDamageAble
{
    [SerializeField] private AttackDataSO attackData;
    public AttackDataSO AttackData => attackData;
    protected AttackContext context;
    public AttackContext Context => context;
    [SerializeField] private AttackPatternDataSO attackPattern;
    public AttackPatternDataSO AttackPattern => attackPattern;

    public float Hp => throw new System.NotImplementedException();

    public int Defense => throw new System.NotImplementedException();

    public void Die()
    {
        throw new System.NotImplementedException();
    }

    public void TakeDamage(int damage)
    {
        throw new System.NotImplementedException();
    }

    protected HeroStateMachine stateMachine;
    protected HeroIdleState idleState;
    public HeroIdleState IdleState => idleState;
    protected HeroAttackState attackState;
    public HeroAttackState AttackState => attackState;
    [SerializeField] private Animator anim;
    [SerializeField] private HeroAnimEvents animEvents;
    public Animator Anim => anim;
    public HeroAnimEvents AnimEvents => animEvents;

    protected GameObject target;
    public GameObject Target => target;
    [SerializeField] private float attackSpeed;
    public float AttackSpeed => attackSpeed;
    protected virtual void Awake()
    {
        stateMachine = new HeroStateMachine();
        idleState = new HeroIdleState(this, stateMachine);
        stateMachine.Initialize(idleState);
    }

    protected virtual void Update()
    {
        stateMachine.CurrentState.Update();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Enemy")
        {
            target = other.gameObject;
            context.target = target.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (target == other.gameObject)
        {
            target = null;
            context.target = null;
        }
    }
}

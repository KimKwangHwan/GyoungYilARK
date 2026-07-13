using System.Collections.Generic;
using UnityEngine;

public class Hero : MonoBehaviour, IDamageAble
{
    [SerializeField] private AttackDataSO[] basePattern;
    public AttackDataSO[] BasePattern => basePattern;

    [SerializeField] private List<AttackSelectorSO> selectors = new();
    public List<AttackSelectorSO> Selectors => selectors;

    [SerializeField] private List<AttackProcSO> procs = new();
    public List<AttackProcSO> Procs => procs;

    public void AddSelector(AttackSelectorSO sel) => selectors.Add(sel);
    public void AddProc(AttackProcSO proc) => procs.Add(proc);

    public float Hp => throw new System.NotImplementedException();
    public int Defense => throw new System.NotImplementedException();

    public void Die() => throw new System.NotImplementedException();
    public void TakeDamage(int damage) => throw new System.NotImplementedException();

    protected AttackContext context;
    public AttackContext Context => context;

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

    [SerializeField] private MapBoard board;
    public MapBoard Board => board;
    private Vector2Int origin;
    private Tile currentTile;
    public Tile CurrentTile => currentTile;
    //private List<Tile> attackRangedTiles;
    protected int range = 1;
    protected virtual void Awake()
    {
        stateMachine = new HeroStateMachine();
        idleState = new HeroIdleState(this, stateMachine);
        stateMachine.Initialize(idleState);
        //attackRangedTiles = board.GetTiles(origin, range);
    }

    protected virtual void Start()
    {
        origin = board.WorldToCell(transform.position);
        if (board.TryGetCell(origin, out Tile current))
            currentTile = current;
        currentTile.SetOccupant(this.gameObject, OccupantKind.MeleeHero);
    }

    protected virtual void Update()
    {
        stateMachine.CurrentState.Update();
        if (target != null)
            CheckTargetStillInRange();
        if (target == null)
            AcquireTargetFromTiles();
    }

    private void AcquireTargetFromTiles()
    {
        foreach (Tile tile in board.GetTiles(origin, range, false))
        {
            foreach (GameObject enemy in tile.Enemies)
            {
                if (enemy == null) continue;
                if (enemy.tag == "Enemy")
                {
                    //if (enemy.GetComponentInParent<IDamageAble>() is not IDamageAble damageable) continue;
                    target = enemy;
                    context.target = target.transform;
                    return;
                }
            }
        }
    }

    private void CheckTargetStillInRange()
    {
        foreach (Tile tile in board.GetTiles(origin, range, false))
        {
            foreach (GameObject enemy in tile.Enemies)
            {
                if (enemy == target)
                    return;
            }
        }
        target = null;
        context.target = null;
    }
}

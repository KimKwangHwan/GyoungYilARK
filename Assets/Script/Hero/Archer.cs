using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class Archer : Hero
{
    [SerializeField] private Animator bowAnim;
    [SerializeField] private Animator arrowAnim;
    public Animator BowAnim => bowAnim;
    public Animator ArrowAnim => arrowAnim;

    [SerializeField] private Transform muzzle;
    public Transform Muzzle => muzzle;

    private readonly Dictionary<Projectile, IObjectPool<Projectile>> projectilePools = new();

    private IObjectPool<Projectile> GetProjectilePool(Projectile prefab)
    {
        if (!projectilePools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<Projectile>(
                createFunc: () => Instantiate(prefab),
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => Destroy(p.gameObject),
                collectionCheck: true,
                defaultCapacity: 8,
                maxSize: 32);
            projectilePools[prefab] = pool;
        }
        return pool;
    }

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
            animEvents = AnimEvents,
            muzzle = muzzle,
            getProjectilePool = GetProjectilePool
        };
        range = 10;
    }
}

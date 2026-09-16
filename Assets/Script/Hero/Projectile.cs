using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

public struct ProjectileAoEConfig
{
    public AttackType attackType;
    public AreaShape areaShape;
    public int areaRange;
    public int chainRange;
    public int chainCount;
    public float chainFalloff;
    public GameObject chainEffectPrefab;
    public float chainEffectLifetime;
    public List<TargetDebuffRef> targetDebuffs;
    public BuffManager buffManager;
    public object source; // 보통 발사한 AttackDataSO 인스턴스
    public GameObject groundZonePrefab;
    public EnemyAttribute areaUnattackableTarget;
    public StatContainer attackerStats;
    public Vector3 casterPos;
    public Hero hero;

    // RangedAttackExecutor.FireArrow와 ContinuousBeamStrategy.FireOneProjectile 두 곳이 이 구성을
    // 동일하게 만들고 있었다. 필드가 늘어날 때 한쪽만 고쳐 조용히 어긋나는 걸 막기 위해 유일한
    // 구성 지점으로 모은다. AttackContext를 그대로 받지 않는 이유: ContinuousBeamStrategy는 ctx가
    // 낡은 스냅샷일 수 있어 hero를 별도 인자로 받는다(ctx.hero를 쓰면 어느 쪽이 권위인지 흐려진다).
    public static ProjectileAoEConfig From(AttackDataSO data, Hero hero,
        StatContainer attackerStats, BuffManager buffManager, Vector3 casterPos) => new()
    {
        attackType = data.attackType,
        areaShape = data.areaShape,
        areaRange = data.areaRange,
        chainRange = data.chainRange,
        chainCount = data.chainCount,
        chainFalloff = data.chainFalloff,
        chainEffectPrefab = data.chainEffectPrefab,
        chainEffectLifetime = data.chainEffectLifetime,
        casterPos = casterPos,
        targetDebuffs = data.targetDebuffs,
        buffManager = buffManager,
        source = data,
        groundZonePrefab = data.groundZonePrefab,
        areaUnattackableTarget = data.AreaUnattackableTarget,
        attackerStats = attackerStats,
        hero = hero,
    };
}

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 15f;
    [SerializeField] private float hitDistance = 0.3f;
    [SerializeField] private float maxLifetime = 5f;
    [SerializeField] private float turnSpeed = 720f;

    [Header("이펙트 (프리팹 자체 소유 — AttackDataSO.attackEffect/hitEffect는 참조하지 않음)")]
    [SerializeField] private GameObject flashEffectPrefab;
    [SerializeField] private float flashEffectLifetime = 1f;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectLifetime = 1f;
    [Tooltip("hitEffectPrefab이 기본 크기(localScale=1)로 나타내는 반경(타일 수). areaRange/이 값 비율로 스케일한다. 0이면 스케일 안 함 — attackType이 Area인 발사체 전용.")]
    [SerializeField] private float hitEffectVisualRadius = 0f;
    [Tooltip("비행 내내 따라다니는 자식 파티클 — 부모 Transform을 자동으로 따라가므로 재배치 코드 불필요")]
    [SerializeField] private ParticleSystem projectileEffect;

    [Header("사운드")]
    [Tooltip("발사 시 재생할 EnemySoundManager 키. 비워두면 재생하지 않음")]
    [SerializeField] private string projectileSoundKey;
    [Tooltip("명중/착탄 시 재생할 EnemySoundManager 키. 비워두면 재생하지 않음")]
    [SerializeField] private string hitSoundKey;

    private Transform target;
    private Vector3 destination;
    private bool visualOnly;
    private bool visible = true;
    private float damage;
    private float elapsed;
    private IObjectPool<Projectile> pool;
    private ProjectileAoEConfig cfg;
    private Hero hero;
    // 라인 관통형 visualOnly 화살 전용 — destination(라인 끝)에 닿기 전에 지나치는 중간 타격 지점들.
    // 가까운 순으로 미리 정렬해서 넘겨받고, 화살이 실제로 그 지점을 지나칠 때마다 순서대로 소비한다.
    private List<Vector3> pendingHitPoints;
    private List<GameObject> pendingHitTargets;
    private int nextHitIndex;

    public Projectile CreatePooledInstance()
    {
        Projectile instance = Instantiate(this);
        // PierceArrow처럼 피격 이펙트가 프리팹의 자식이어도 원본 참조를 공유한다.
        // 복제된 자식을 키로 쓰면 같은 이펙트가 발사체마다 다른 종류로 집계된다.
        instance.hitEffectPrefab = hitEffectPrefab;
        return instance;
    }

    public void Launch(Transform target, float damage, IObjectPool<Projectile> pool, ProjectileAoEConfig cfg)
    {
        this.target = target;
        this.damage = damage;
        this.pool = pool;
        this.cfg = cfg;
        this.hero = cfg.hero;
        this.visualOnly = false;
        elapsed = 0f;
        ResetVisibility();

        if (target != null && TryLook(target.position - transform.position, out Quaternion look))
            transform.rotation = look;

        SpawnFlashEffect(cfg.hero);
    }

    public void LaunchVisualOnly(Vector3 destination, IObjectPool<Projectile> pool, Hero hero,
        List<Vector3> hitPoints = null, List<GameObject> hitTargets = null)
    {
        this.destination = destination;
        this.pool = pool;
        this.hero = hero;
        this.visualOnly = true;
        this.target = null;
        elapsed = 0f;
        pendingHitPoints = hitPoints;
        pendingHitTargets = hitTargets;
        nextHitIndex = 0;
        ResetVisibility();

        if (TryLook(destination - transform.position, out Quaternion look))
            transform.rotation = look;

        SpawnFlashEffect(hero);
    }

    private void SpawnFlashEffect(Hero hero)
    {
        if (!string.IsNullOrEmpty(projectileSoundKey)) EnemySoundManager.Play(projectileSoundKey, at: transform.position);
        if (flashEffectPrefab != null && hero != null)
            AttackDamageUtil.SpawnCasterEffect(hero, flashEffectPrefab, transform.position, transform.rotation, flashEffectLifetime);
        if (projectileEffect != null)
        {
            projectileEffect.Clear(true);
            projectileEffect.Play(true);
        }
    }

    // 풀에서 재사용될 때 이전 비행에서 꺼진 상태(화면 밖에서 반납된 경우)로 남아있지 않도록 초기화한다.
    private void ResetVisibility()
    {
        visible = true;
        VfxVisibility.SetVisualActive(gameObject, true);
    }

    // 이동/충돌 판정과 완전히 분리된 순수 표시 토글 — 화면 밖이면 렌더러/파티클만 끄고, 이 메서드를
    // 호출하는 Update()의 나머지 로직(이동, 히트 판정, Hit())은 화면 표시 여부와 무관하게 계속 실행된다.
    private void UpdateVisibility()
    {
        bool offscreen = VfxVisibility.IsOffscreen(transform.position);
        if (offscreen == visible)
        {
            visible = !offscreen;
            VfxVisibility.SetVisualActive(gameObject, visible);
        }
    }

    private void Update()
    {
        UpdateVisibility();

        elapsed += Time.deltaTime;
        if (elapsed >= maxLifetime || (!visualOnly && target == null))
        {
            Return();
            return;
        }

        Vector3 dest = visualOnly ? destination : AttackDamageUtil.EffectPosition(target.gameObject);
        Vector3 oldPos = transform.position;
        Vector3 toTarget = dest - oldPos;

        if (TryLook(toTarget, out Quaternion desired))
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);

        Vector3 newPos = oldPos + transform.forward * speed * Time.deltaTime;

        // 라인 관통형 화살은 destination(끝점)에 닿기 전에 중간 적들을 먼저 지나친다 — 이번 프레임
        // 이동 구간이 다음 대기 지점을 지나쳤으면 반납하지 않고 히트 이펙트/사운드만 재생한 뒤 계속
        // 날아간다. 한 프레임에 여러 지점을 동시에 지나칠 수 있어 while로 전부 소비한다.
        if (visualOnly && pendingHitPoints != null)
        {
            while (nextHitIndex < pendingHitPoints.Count)
            {
                Vector3 hitPoint = pendingHitPoints[nextHitIndex];
                Vector3 closestToHit = ClosestPointOnSegment(hitPoint, oldPos, newPos);
                if ((hitPoint - closestToHit).sqrMagnitude > hitDistance * hitDistance) break;
                // 적들이 서로 가까우면 화살이 지나는 간격이 0.05초 스로틀보다 짧아 대부분 씹힐 수 있다 —
                // 한 발이 라인 위 여러 적을 연속으로 맞히는 의도된 다중 히트이므로 스로틀을 우회한다.
                if (!string.IsNullOrEmpty(hitSoundKey)) EnemySoundManager.Play(hitSoundKey, ignoreThrottle: true, at: hitPoint);
                if (pendingHitTargets != null && nextHitIndex < pendingHitTargets.Count)
                    AttackDamageUtil.SpawnHitEffect(hero, hitEffectPrefab, pendingHitTargets[nextHitIndex],
                        hitEffectLifetime, hitPoint);
                else
                    SpawnHitEffect(hitPoint);
                nextHitIndex++;
            }
        }

        // 고배속/프레임드랍으로 한 프레임 이동거리가 hitDistance를 넘으면 목표를 관통하거나
        // 옆으로 스쳐 지나갈 수 있다. 이번 프레임 이동 경로(선분) 기준으로 판정해 그런 경우도 잡는다.
        Vector3 closest = ClosestPointOnSegment(dest, oldPos, newPos);
        if ((dest - closest).sqrMagnitude <= hitDistance * hitDistance)
        {
            // Hit()의 범위 공격/장판 스폰이 transform.position을 기준으로 하므로,
            // 관통/스쳐 지나간 경우에도 실제 명중 지점(선분상 최근접점)으로 스냅해 둔다.
            transform.position = closest;
            if (visualOnly) { if (!string.IsNullOrEmpty(hitSoundKey)) EnemySoundManager.Play(hitSoundKey, at: dest); SpawnHitEffect(dest); Return(); }
            else Hit();
            return;
        }

        transform.position = newPos;
    }

    private static bool TryLook(Vector3 dir, out Quaternion rot)
    {
        rot = Quaternion.identity;
        if (dir.sqrMagnitude < 1e-6f) return false;
        Vector3 up = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.999f
            ? Vector3.forward : Vector3.up;
        rot = Quaternion.LookRotation(dir, up);
        return true;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float abLenSqr = ab.sqrMagnitude;
        float t = abLenSqr > 1e-9f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / abLenSqr) : 0f;
        return a + ab * t;
    }

    private void Hit()
    {
        if (!string.IsNullOrEmpty(hitSoundKey)) EnemySoundManager.Play(hitSoundKey, at: transform.position);
        RangeShape aoeShape = cfg.areaShape == AreaShape.Square ? RangeShape.Square : RangeShape.Diamond;

        if (cfg.areaShape == AreaShape.Chain)
        {
            List<GameObject> hits = ChainResolver.Resolve(target.gameObject, damage, cfg.chainRange, cfg.chainCount, cfg.chainFalloff,
                (p, r, s) => cfg.hero.GetObjectsInRange(p, r, s, RangeQueryAffinity.TargetableEnemy), cfg.hero.NotifyHit);
            foreach (GameObject go in hits)
            {
                AttackDamageUtil.ApplyTargetDebuffs(go.transform, cfg.targetDebuffs, cfg.buffManager, cfg.source, hero.SC[StatType.ATK]);
                ApplyHealOptions(damage);
                SpawnHitEffect(go);
            }
            // hits[0]은 착탄 지점(캐스터→hits[0] 구간은 발사체 자체가 표현) — 튕긴 대상들 사이만 아크로 잇는다.
            for (int i = 0; i < hits.Count - 1; i++)
                cfg.hero.SpawnChainArc(cfg.chainEffectPrefab, hits[i], hits[i + 1], cfg.chainEffectLifetime);
        }
        else if (cfg.attackType == AttackType.Area)
        {
            foreach (GameObject go in cfg.hero.GetObjectsInRange(transform.position, cfg.areaRange, aoeShape, RangeQueryAffinity.Enemy, cfg.areaUnattackableTarget))
            {
                if (go.GetComponentInParent<IDamageAble>() is not IDamageAble enemy) continue;
                enemy.TakeDamage((int)damage);
                cfg.hero.NotifyHit(go, (int)damage, false);
                AttackDamageUtil.ApplyTargetDebuffs(go.transform, cfg.targetDebuffs, cfg.buffManager, cfg.source, hero.SC[StatType.ATK]);
                ApplyHealOptions(damage);
            }
            SpawnAreaHitEffect(transform.position, cfg.areaRange);
        }
        else if (target != null && target.GetComponentInParent<IDamageAble>() is IDamageAble damageable)
        {
            damageable.TakeDamage((int)damage);
            cfg.hero.NotifyHit(target.gameObject, (int)damage, false);
            AttackDamageUtil.ApplyTargetDebuffs(target, cfg.targetDebuffs, cfg.buffManager, cfg.source, hero.SC[StatType.ATK]);
            ApplyHealOptions(damage);
            SpawnHitEffect(target.gameObject);
        }

        if (cfg.groundZonePrefab != null)
            cfg.hero.SpawnGroundZone(cfg.groundZonePrefab, transform.position);

        Return();
    }

    // 대상이 없는 라인 끝점 등의 착탄 연출 전용. 적을 지나치는 중간 타격은 대상별 제한을 거친다.
    private void SpawnHitEffect(Vector3 pos)
    {
        if (hitEffectPrefab != null)
            hero.SpawnEffect(hitEffectPrefab, pos, hitEffectLifetime);
    }

    private void SpawnHitEffect(GameObject target)
        => AttackDamageUtil.SpawnHitEffect(hero, hitEffectPrefab, target, hitEffectLifetime);

    // Area 타입 착탄 전용 — 맞은 적마다 따로 띄우지 않고, 착탄 중심에 areaRange 크기로 스케일한
    // 이펙트 하나만 띄운다. 여러 발이 같은 적에게 겹쳐 착탄할 수 있으므로 주 타겟의 제한을 공유한다.
    private void SpawnAreaHitEffect(Vector3 pos, int range)
    {
        if (hitEffectPrefab == null) return;
        GameObject go = AttackDamageUtil.SpawnHitEffect(hero, hitEffectPrefab,
            target != null ? target.gameObject : null, hitEffectLifetime, pos);
        if (go != null && hitEffectVisualRadius > 0f)
            go.transform.localScale = Vector3.one * (range / hitEffectVisualRadius);
    }

    private void ApplyHealOptions(float damageDealt)
    {
        if (cfg.source is AttackDataSO data)
            AttackDamageUtil.ApplyHealOptions(data, cfg.casterPos, cfg.hero.Heal,
                (p, r, s) => cfg.hero.GetObjectsInRange(p, r, s, RangeQueryAffinity.Ally), damageDealt, cfg.attackerStats[StatType.ATK]);
    }

    private void Return()
    {
        if (pool != null) pool.Release(this);
        else Destroy(gameObject);
    }
}

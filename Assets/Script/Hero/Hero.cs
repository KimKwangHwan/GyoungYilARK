using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using VContainer;

public enum RangeQueryAffinity { Enemy, TargetableEnemy, Ally }

public class Hero : MonoBehaviour, IDamageAble, IUnit, IStunAble, IDebuffCarrier
{
    [SerializeField] private HeroData heroData;
    public int Tier => heroData.Tier;
    public int UnitId => heroData.UnitId;
    public string HeroName => heroData.HeroName;
    public MergeKey MergeKey => heroData.MergeKey;
    public int HeroType => heroData.HeroType;

    [Header("유닛 정보")]
    [SerializeField] private List<AttackDataSO> basePattern;
    public List<AttackDataSO> BasePattern => basePattern;
    // 히어로의 "현재 공격 데이터" — 항상 basePattern[0] 고정. 로테이션 중인 실제 공격과는 무관하게
    // 배치 프리뷰(RangeInfo)나 사거리 판정처럼 전투 시작 전에도 값이 있어야 하고 전투 중에도 흔들리면
    // 안 되는 곳에서 쓴다.
    public AttackDataSO CurrentAttackData => basePattern != null && basePattern.Count > 0 ? basePattern[0] : null;

    // 트레잇/액티브 스킬은 이제 SO가 아니라 같은 프리팹에 붙은 Component다 — Awake에서 자동 수집한다.
    private HeroTrait[] traits;
    public IReadOnlyList<HeroTrait> Traits => traits;

    private HeroActiveSkill activeSkill;
    public HeroActiveSkill ActiveSkill => activeSkill;
    private float skillCooldownRemaining;
    public bool IsSkillReady => activeSkill == null || skillCooldownRemaining <= 0f;
    private CancellationTokenSource skillCts;

    protected AttackContext context;
    public AttackContext Context => context;

    protected HeroStateMachine stateMachine;
    protected HeroIdleState idleState;
    public HeroIdleState IdleState => idleState;
    protected HeroAttackState attackState;
    public HeroAttackState AttackState => attackState;
    protected HeroDeathState deathState;
    public HeroDeathState DeathState => deathState;
    protected HeroStunState stunState;
    public HeroStunState StunState => stunState;

    [SerializeField] private Animator anim;
    [SerializeField] private HeroAnimEvents animEvents;
    public Animator Anim => anim;
    public HeroAnimEvents AnimEvents => animEvents;

    private HeroOutlineEffect outlineEffect;
    public void SetSelected(bool selected)
    {
        outlineEffect ??= new HeroOutlineEffect(transform);
        outlineEffect.SetActive(selected);
    }

    protected GameObject target;
    public GameObject Target => target;

    // 다중 타겟 볼리 도중 "지금 실제로 쏘는 대상"을 몸이 바라보게 하기 위한 오버라이드.
    // null이면 평소처럼 Context.target(락온 타겟)을 본다 — FireVolley류가 발사 직전 갱신하고,
    // 볼리가 끝나면 다시 null로 되돌린다.
    public Transform AimOverrideTarget { get; set; }

    [SerializeField] private StatDataSO statData;
    public StatDataSO StatData => statData;
    public float PreviewAttackPower => HeroStatManager.GetStat(heroData, StatType.ATK);
    public float PreviewDefence => HeroStatManager.GetStat(heroData, StatType.DEF);

    [SerializeField] private MapBoard board;
    public MapBoard Board => board;
    protected Vector2Int origin;
    protected Tile currentTile;
    public Tile CurrentTile => currentTile;

    // 사거리/사거리 형태는 AttackDataSO(CurrentAttackData = basePattern[0])로 이전됨 — 프로퍼티
    // 이름/시그니처는 그대로 유지해 RangeInfo 등 기존 소비 코드는 변경 없이 그대로 쓴다.
    public int Range => CurrentAttackData?.range ?? 0;
    public RangeShape RangeShape => CurrentAttackData?.rangeShape ?? RangeShape.Diamond;

    [Tooltip("소유자가 죽을 때까지 유지되는 오라 장판(GroundZoneEffect, duration<=0) 프리팹들")]
    [SerializeField] private List<GameObject> auraZonePrefabs = new();

    // 이펙트 풀은 Hero 인스턴스 소유(Archer/Mage의 projectilePools와 동일 패턴) — 씬이 언로드돼 이 Hero가
    // 파괴되면 풀도 함께 사라지므로, 파괴된 인스턴스를 다시 꺼내 쓰는 일이 없다.
    private readonly Dictionary<GameObject, IObjectPool<GameObject>> effectPools = new();

    private IObjectPool<GameObject> GetEffectPool(GameObject prefab)
    {
        if (!effectPools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(prefab),
                actionOnGet: go => { if (go != null) go.SetActive(true); },
                actionOnRelease: go => { if (go != null) go.SetActive(false); },
                actionOnDestroy: go => { if (go != null) Destroy(go); },
                collectionCheck: true,
                defaultCapacity: 8,
                maxSize: 256);
            effectPools[prefab] = pool;
        }
        return pool;
    }

    // 투사체 풀도 이펙트 풀과 동일한 패턴(Hero 인스턴스 소유) — 원거리 Hero(Archer/Mage)만 사용한다.
    private readonly Dictionary<Projectile, IObjectPool<Projectile>> projectilePools = new();

    public IObjectPool<Projectile> GetProjectilePool(Projectile prefab)
    {
        if (!projectilePools.TryGetValue(prefab, out var pool))
        {
            pool = new ObjectPool<Projectile>(
                createFunc: () => prefab.CreatePooledInstance(),
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

    // HeroTrait/GroundZoneEffect(같은 GameObject의 다른 컴포넌트)도 써야 해서 public.
    // 모든 1회성 타격/스킬 이펙트가 결국 여기로 모이므로, ParticleBudget 획득/해제를 이 메서드
    // 안에서만 짝지어 처리한다 — 장판/빔/상시 이펙트(SpawnEffectAlways, SpawnPersistentEffect
    // 직접 호출, SpawnGroundZone)는 이 경로를 타지 않으므로 버짓과 무관하게 그대로 동작한다.
    public GameObject SpawnEffect(GameObject prefab, Vector3 pos, Quaternion rot, float lifetime, Action onReturned = null)
    {
        bool budgeted = lifetime > 0f && ParticleBudget.TryAcquire();
        if (lifetime > 0f && !budgeted) return null; // 버짓 초과 — 연출 전용이라 생략해도 안전

        GameObject go = SpawnPersistentEffect(prefab, pos, rot);
        if (go == null)
        {
            if (budgeted) ParticleBudget.Release();
            return null;
        }

        if (lifetime > 0f)
            ReturnEffectAfter(prefab, go, lifetime, budgeted, onReturned).Forget();
        return go;
    }

    // SpawnEffect와 동일하지만 SpawnPersistentEffectAlways를 통해 화면 밖이어도 스폰을 생략하지 않는다.
    // 스폰 후 계속 살아남는 이펙트(GroundZoneEffect의 self/오라 등) 전용 — 호출부가 매 프레임
    // VfxVisibility.SetVisualActive로 직접 가시성을 관리해야 한다.
    public GameObject SpawnEffectAlways(GameObject prefab, Vector3 pos, Quaternion rot, float lifetime)
    {
        GameObject go = SpawnPersistentEffectAlways(prefab, pos, rot);
        if (go != null && lifetime > 0f)
            ReturnEffectAfter(prefab, go, lifetime).Forget();
        return go;
    }

    // 회전을 생략하면 identity가 아니라 프리팹 자신에게 구워둔 회전을 쓴다 — Quaternion.identity를
    // 넘기면 눕혀두거나 특정 방향을 보게 만든 프리팹의 로컬 회전이 스폰 때마다 지워지기 때문.
    // localRotation을 쓰는 이유: hitEffect 필드가 (일부 장판 프리팹처럼) 스폰된 다른 오브젝트의
    // 자식 Transform을 직접 가리키는 경우가 있는데, 그때 rotation(월드)을 읽으면 부모(장판 루트 등)의
    // 런타임 회전과 합성되어 버린다. 진짜 프리팹 애셋 루트는 parent가 없어 local==world라 안전하다.
    public GameObject SpawnEffect(GameObject prefab, Vector3 pos, float lifetime, Action onReturned = null)
        => SpawnEffect(prefab, pos, prefab != null ? prefab.transform.localRotation : Quaternion.identity, lifetime, onReturned);

    public GameObject SpawnPersistentEffect(GameObject prefab, Vector3 pos)
        => SpawnPersistentEffect(prefab, pos, prefab != null ? prefab.transform.localRotation : Quaternion.identity);

    public GameObject SpawnPersistentEffect(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null;
        // 순수 연출용 이펙트 — 화면 밖(마진 포함)이면 인스턴스화 자체를 생략한다. 이 시점에 도달하는
        // 모든 호출부는 피해/디버프가 이미 적용된 뒤의, 1회성·단명 연출 스폰뿐이다(AttackDamageUtil.
        // SpawnHitEffect, HeroActiveSkill 캐스터 이펙트, BeamLinkEffect 등) — SpawnGroundZone(별도
        // 메서드, 장판 자체가 데미지/힐 틱 소유자)은 이 메서드를 타지 않으므로 영향 없다. 인스턴스를
        // 계속 추적하며 매 프레임 가시성을 재검사할 대상(GroundZoneEffect의 self/버프/힐 이펙트 등)은
        // 대신 SpawnPersistentEffectAlways를 써야 한다 — 그래야 나중에 화면에 들어와도 켤 인스턴스가
        // 남아있다.
        if (VfxVisibility.IsOffscreen(pos)) return null;
        return SpawnPersistentEffectAlways(prefab, pos, rot);
    }

    // SpawnPersistentEffect와 동일하지만 화면 밖이어도 스폰을 생략하지 않는다. 스폰 후에도 계속
    // 살아남아 위치가 바뀌는 이펙트(GroundZoneEffect의 self/오라, 아군에 붙는 버프/힐 수신 이펙트 등)
    // 전용 — 호출부가 매 프레임 VfxVisibility.SetVisualActive로 직접 가시성을 관리해야 한다.
    public GameObject SpawnPersistentEffectAlways(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null;
        IObjectPool<GameObject> pool = GetEffectPool(prefab);
        GameObject go = pool.Get();
        while (go == null)
            go = pool.Get();
        go.transform.SetPositionAndRotation(pos, rot);
        // 이전 대여 때 화면 밖이라 VfxVisibility.SetVisualActive(false)로 꺼진 채 반납됐을 수 있다 —
        // 새로 스폰되는 인스턴스는 항상 보이는 상태로 시작해야 하므로 렌더러를 명시적으로 켠다.
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear(true);
            ps.Play(true);
        }
        return go;
    }

    public void DespawnEffect(GameObject prefab, GameObject instance)
    {
        if (prefab == null || instance == null) return;
        if (instance.TryGetComponent(out BeamLinkEffect link)) link.StopTracking();
        GetEffectPool(prefab).Release(instance);
    }

    // 체인 튕김 두 지점을 잇는 이펙트. 좌표가 아니라 대상 GameObject를 받아서, 스폰 후에도
    // TrackLinkEndpoints로 계속 그 대상들을 따라가게 한다(대상이 움직이는 동안 얼어붙지 않도록).
    public GameObject SpawnChainArc(GameObject prefab, GameObject fromTarget, GameObject toTarget, float lifetime)
    {
        if (fromTarget == null || toTarget == null) return null;
        GameObject go = AttackDamageUtil.SpawnHitEffect(this, prefab, toTarget, lifetime,
            AttackDamageUtil.EffectPosition(fromTarget));
        TrackLinkEndpoints(go, AttackDamageUtil.TrackingPosition(fromTarget), AttackDamageUtil.TrackingPosition(toTarget));
        return go;
    }

    // 빔/체인 두 점 이펙트의 끝점을 한 번만 적용하는 공용 헬퍼. BeamLinkEffect가 붙어 있으면 텍스처
    // 스크롤/히트 이펙트 배치까지 맡기고, 없으면(단순 LineRenderer만 있는 프리팹) 두 점만 직접
    // 세팅하는 폴백을 유지한다.
    public static void UpdateLinkEndpoints(GameObject go, Vector3 from, Vector3 to)
    {
        if (go == null) return;
        if (go.TryGetComponent(out BeamLinkEffect link))
            link.SetEndpoints(from, to);
        else if (go.TryGetComponent(out LineRenderer lr))
        {
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
        }
    }

    // 매 프레임 스스로 갱신하도록 두 지점 제공자를 등록한다. BeamLinkEffect가 있으면 Track으로
    // 넘겨 매 프레임 다시 계산하게 하고, 없으면(단순 LineRenderer 프리팹) 기존처럼 1회만 적용한다.
    public static void TrackLinkEndpoints(GameObject go, Func<Vector3> from, Func<Vector3> to)
    {
        if (go == null) return;
        if (go.TryGetComponent(out BeamLinkEffect link))
            link.Track(from, to);
        else
            UpdateLinkEndpoints(go, from(), to());
    }

    private async UniTask ReturnEffectAfter(GameObject prefab, GameObject go, float delay, bool releaseBudget = false,
        Action onReturned = null)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay));
            if (go != null) DespawnEffect(prefab, go);
        }
        finally
        {
            if (releaseBudget) ParticleBudget.Release();
            onReturned?.Invoke();
        }
    }

    // GroundZoneEffect 프리팹을 풀에서 꺼내 위치를 잡고 Init만 넘긴다 — 이후 틱/소멸(풀 반납)은
    // GroundZoneEffect 컴포넌트가 스스로 처리한다.
    // TilePainter.lift(0.02f)와 동일한 값 — 바닥 메시 윗면과 같은 높이에 놓이면 알파블렌드 장판
    // 링 VFX가 오파크 바닥과 z-fighting을 일으켜 깜빡여 보인다.
    private const float GroundZoneLift = 0.02f;

    public void SpawnGroundZone(GameObject prefab, Vector3 pos, bool followOwner = false)
    {
        if (prefab == null) return;
        IObjectPool<GameObject> pool = GetEffectPool(prefab);
        GameObject go = pool.Get();
        while (go == null)
            go = pool.Get();
        go.transform.SetParent(followOwner ? transform : null, worldPositionStays: false);
        // 공중 적에게 명중해 pos가 공중 높이일 수 있다(발사체가 비행 유닛을 맞힌 경우 등) — 장판은
        // 항상 바닥 타일 높이에 스폰해야 하므로 X/Z(착탄 위치)는 유지하고 Y만 타일 바닥 높이로 스냅한다.
        Vector3 groundPos = pos;
        if (Board.TryGetCell(Board.WorldToCell(pos), out Tile tile))
            groundPos.y = tile.WorldTop.y;
        go.transform.position = groundPos + Vector3.up * GroundZoneLift;
        if (go.TryGetComponent(out GroundZoneEffect zone))
            zone.Init(Board, this, followOwner, released => pool.Release(released));
    }

    private StatContainer sc = new();
    public StatContainer SC => sc;
    public StatContainer Stats => sc;

    public int BlockCount => IsDead ? 0 : (int)SC[StatType.BLK];
    private float currentHp;
    public float Hp => currentHp;
    public float MaxHp => sc[StatType.HP];
    public int Defense => Mathf.RoundToInt(sc[StatType.DEF]); // 기존 NotImplementedException 버그 수정
    private bool isDead;
    public bool IsDead => isDead;

    [SerializeField] private Slider healthSlider;
    private readonly HeroHealthBar _bar = new();

    private readonly DebuffTracker debuffTracker = new();
    public DebuffTracker Debuffs => debuffTracker;
    public DebuffType ImmuneDebuffs => DebuffType.None;

    public bool IsStunned => debuffTracker.Has(DebuffType.Stun);

    public void Stun(float duration)
    {
        if (isDead || duration <= 0f) return;
        bool wasStunned = IsStunned;
        debuffTracker.Apply(DebuffType.Stun, duration);
        if (!wasStunned) stateMachine.ChangeState(stunState);
    }

    private readonly EnemyDebuffEffects debuffEffects = new();


    private GameManager gameManager;
    private ResourcesManager resourcesManager;
    protected BuffManager buffManager;
    public BuffManager Buffs => buffManager;
    [Inject]
    private void Construct(GameManager gameManager, BuffManager buffManager, ResourcesManager resourcesManager)
    {
        this.gameManager = gameManager;
        this.buffManager = buffManager;
        this.resourcesManager = resourcesManager;
    }

    public void Die()
    {
        isDead = true;
        anim.SetBool(HeroAnimHash.idle, false);
        stateMachine.ChangeState(deathState);
        debuffEffects.Reset();
        skillCts?.Cancel();
        skillCts?.Dispose();
        skillCts = null;
    }

    public void TakeDamage(int damage,bool ignore = false)
    {
        if (isDead) return;
        
        int hitDamage = Mathf.Max(1, damage - (ignore ? 0 : Defense));
        currentHp -= hitDamage;
        if (currentHp <= 0) Die();
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f) return;
        currentHp = Mathf.Min(currentHp + amount, sc[StatType.HP]);
    }

    [SerializeField] protected OccupantKind occupantKind;
    public OccupantKind OccupantKind => occupantKind;

    protected virtual void Awake()
    {
        stateMachine = new HeroStateMachine();
        idleState = new HeroIdleState(this, stateMachine);
        deathState = new HeroDeathState(this, stateMachine);
        stunState = new HeroStunState(this, stateMachine);
        stateMachine.Initialize(idleState);

        Dictionary<StatType, float> resolvedStats = HeroStatManager.GetAll(heroData);
        sc.AddStat(StatType.HP, resolvedStats[StatType.HP]);
        sc.AddStat(StatType.ATK, resolvedStats[StatType.ATK]);
        sc.AddStat(StatType.DEF, resolvedStats[StatType.DEF]);
        sc.AddStat(StatType.BLK, resolvedStats[StatType.BLK]);
        sc.AddStat(StatType.AS, resolvedStats[StatType.AS]);
        currentHp = sc[StatType.HP];
        _bar.Setup(healthSlider, 10f);
        _bar.ResetTo(currentHp, sc[StatType.HP]);

        traits = GetComponents<HeroTrait>();
        activeSkill = GetComponent<HeroActiveSkill>();

        GameObject stunEffectPrefab = Resources.Load<GameObject>("EnemyEffectPrefab/Stun");
        var debuffEffectSet = Resources.Load<DebuffEffectSetSO>("EnemyEffectPrefab/DebuffEffectSet");
        debuffEffects.Setup(debuffEffectSet, transform, transform, transform, stunEffectPrefab);

        EnsureClickCollider();
    }

    // 프리팹에 콜라이더가 없어(순수 렌더러만 있음) 맵의 타일 판정만으로는 캐릭터 모델을 직접 클릭해
    // 선택할 수 없다. 렌더러 전체를 감싸는 콜라이더를 하나 붙여 PointerPick이 물리 레이캐스트로
    // "영웅 몸통을 직접 클릭"한 경우를 잡아낼 수 있게 한다.
    private void EnsureClickCollider()
    {
        if (TryGetComponent<Collider>(out _)) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);

        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.center = transform.InverseTransformPoint(worldBounds.center);
        collider.size = worldBounds.size;
    }

    protected virtual void Start()
    {
        SetCurrentTile();
        HeroStatManager.StatsChanged += RefreshBaseStats;
        if (gameManager != null)
        {
            gameManager.ChangeToDay += Resurrection;
            gameManager.ChangeToDay += HealFull;
            gameManager.ChangeToDay += ResetSkillCooldown;
            gameManager.ChangeToDay += NotifyDayStart;
        }
        SpawnAuraZones();
    }

    // GroundZoneEffect(duration<=0)는 소유자가 죽으면 스스로 감지하고 풀에 반납되므로, 부활 시엔
    // 그냥 다시 스폰하면 된다 — 예전의 _auraCts 취소/재시작 관리가 필요 없어졌다.
    private void SpawnAuraZones()
    {
        foreach (GameObject prefab in auraZonePrefabs)
            SpawnGroundZone(prefab, transform.position, followOwner: true);
    }

    // 업그레이드(talent+티어+클래스)가 반영된 기본 스탯은 HeroStatManager가 HeroData 단위로 미리
    // 계산해 캐싱해둔다 — 여기서는 그 값을 SetBaseValue로 얹기만 한다. AddStat이 아니라 SetBaseValue를
    // 쓰는 이유: AddStat은 Stat을 새로 만들어 진행 중인 버프 Modifier까지 날려버리지만, SetBaseValue는
    // baseValue만 바꾸고 버프 Modifier는 그대로 둔다.
    private void RefreshBaseStats()
    {
        Dictionary<StatType, float> resolved = HeroStatManager.GetAll(heroData);
        sc.SetBaseValue(StatType.HP, resolved[StatType.HP]);
        sc.SetBaseValue(StatType.ATK, resolved[StatType.ATK]);
        sc.SetBaseValue(StatType.DEF, resolved[StatType.DEF]);
        sc.SetBaseValue(StatType.BLK, resolved[StatType.BLK]);
        sc.SetBaseValue(StatType.AS, resolved[StatType.AS]);
        if (!isDead) currentHp = sc[StatType.HP]; // 업그레이드로 최대체력이 늘어난 만큼 낮이니 그냥 전부 채운다
    }

    protected virtual void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.ChangeToDay -= Resurrection;
            gameManager.ChangeToDay -= HealFull;
            gameManager.ChangeToDay -= ResetSkillCooldown;
            gameManager.ChangeToDay -= NotifyDayStart;
        }
        HeroStatManager.StatsChanged -= RefreshBaseStats;
        debuffEffects.Reset();
        skillCts?.Cancel();
        skillCts?.Dispose();
        skillCts = null;
    }

    // ---- 트레잇 훅 팬아웃 — HeroAttackRunner/GroundZoneEffect/AttackDamageUtil이 호출한다 ----
    public void NotifyAttackPerformed(AttackDataSO data)
    {
        for (int i = 0; i < traits.Length; i++) traits[i]?.OnAttackPerformed(data);
    }

    public void NotifyAttackResolved(AttackDataSO data)
    {
        for (int i = 0; i < traits.Length; i++) traits[i]?.OnAttackResolved(data);
    }

    public void NotifyHit(GameObject hitTarget, int amount, bool isCrit)
    {
        for (int i = 0; i < traits.Length; i++) traits[i]?.OnHit(hitTarget, amount, isCrit);
        if (hitTarget != null && hitTarget.GetComponentInParent<IDamageAble>() is IDamageAble d && d.Hp <= 0f)
            NotifyKill(hitTarget);
    }

    public void NotifyDayStart()
    {
        for (int i = 0; i < traits.Length; i++) traits[i]?.OnDayStart();
    }

    public void NotifyKill(GameObject killedTarget)
    {
        for (int i = 0; i < traits.Length; i++) traits[i]?.OnKill(killedTarget);
    }

    // ---- 다음 공격 오버라이드 (트레잇의 N타 강공 / 확률 재발동) ----
    private AttackDataSO queuedOverride;
    private bool queuedOverrideIsProc;
    public AttackDataSO LastUsedAttackData { get; private set; }
    public bool LastAttackWasProc { get; private set; }

    public void QueueNextAttackOverride(AttackDataSO data, bool isProc = false)
    {
        queuedOverride = data;
        queuedOverrideIsProc = isProc;
    }

    public AttackDataSO ConsumeAttackOverride(out bool isProc)
    {
        isProc = queuedOverrideIsProc;
        AttackDataSO data = queuedOverride;
        queuedOverride = null;
        queuedOverrideIsProc = false;
        return data;
    }

    public void SetLastUsedAttack(AttackDataSO data, bool isProc)
    {
        LastUsedAttackData = data;
        LastAttackWasProc = isProc;
    }

    // AttackStanceSkill처럼 지속시간 동안 basePattern[0]을 바꿔치기하는 스킬용.
    public AttackDataSO SwapPrimaryAttackData(AttackDataSO next)
    {
        if (basePattern.Count == 0) return null;
        AttackDataSO previous = basePattern[0];
        basePattern[0] = next != null ? next : previous;
        return previous;
    }

    // ---- 액티브 스킬 (HeroSkillCastController가 플레이어 클릭을 받아 호출) ----
    public bool TryUseActiveSkill(Tile targetTile)
    {
        if (activeSkill == null || isDead || targetTile == null || targetTile.Board != Board)
            return false;
        if (!IsSkillReady)
            return false;

        if (activeSkill.targetScope == SkillTargetScope.Self)
        {
            Vector2Int casterCell = Board.WorldToCell(transform.position);
            if (targetTile.Coord != casterCell) return false;
        }
        // AnywhereOnBoard: 위에서 이미 targetTile.Board == Board를 확인했으므로 거리 제한 없이 통과.

        skillCooldownRemaining = activeSkill.cooldown;

        if (activeSkill.blocksBasicAttack)
            stateMachine.ChangeState(new HeroSkillState(this, stateMachine, targetTile));
        else
            RunActiveSkillUnblocked(targetTile).Forget();

        return true;
    }

    private async UniTask RunActiveSkillUnblocked(Tile targetTile)
    {
        skillCts?.Cancel();
        skillCts?.Dispose();
        skillCts = new CancellationTokenSource();
        try { await activeSkill.Execute(targetTile, skillCts.Token); }
        catch (OperationCanceledException) { }
    }

    private void ResetSkillCooldown() => skillCooldownRemaining = 0f;

    protected virtual void Update()
    {
        if (target != null)
            CheckTargetStillInRange();
        if (target == null)
            AcquireTargetFromTiles();

        stateMachine.CurrentState.Update();
        if (skillCooldownRemaining > 0f) skillCooldownRemaining -= Time.deltaTime;

        for (int i = 0; i < traits.Length; i++)
            traits[i]?.OnPassiveTick(Time.deltaTime);

        debuffEffects.Tick(debuffTracker, isDead);
    }

    // 체력바는 LateUpdate에서 굴린다 — 이동(Update)과 카메라 회전이 모두 끝난 뒤라야 빌보드가 한 프레임 밀리지 않는다.
    protected virtual void LateUpdate()
    {
        _bar.Tick(Hp, MaxHp, isDead);
    }

    protected virtual void AcquireTargetFromTiles()
    {
        GameObject nearest = null;
        float nearestSqrDist = float.MaxValue;

        foreach (Tile tile in TileShapeQuery.GetTiles(Board, origin, Range, RangeShape))
        {
            foreach (GameObject enemy in tile.Enemies)
            {
                if (!IsTargetable(enemy)) continue;
                float sqrDist = (enemy.transform.position - transform.position).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = enemy;
                }
            }
        }

        if (nearest != null)
        {
            target = nearest;
            context.target = nearest.transform;
        }
    }

    private bool IsTargetable(GameObject enemy)
    {
        if (enemy == null) return false;
        var eb = enemy.GetComponent<EnemyBase>();
        return eb != null && !eb.IsDead && (eb.Attribute & (CurrentAttackData?.unattackableTarget ?? EnemyAttribute.None)) == 0;
    }

    // AoE/장판 경로 전용 필터. mask가 None(기본값, 인자를 안 넘긴 호출부)이면 항상 통과 — 기존 동작 유지.
    // 그 외엔 AttackDataSO.AreaUnattackableTarget(Cloaking은 이미 빠진 마스크)과 적 속성을 대조한다.
    private static bool PassesAreaMask(GameObject enemy, EnemyAttribute mask)
    {
        if (mask == EnemyAttribute.None) return true;
        var eb = enemy.GetComponent<EnemyBase>();
        return eb == null || (eb.Attribute & mask) == 0;
    }

    // Enemy = AOE 스플래시용. areaUnattackableMask로 넘어온 속성(예: Fly)만 걸러내고, 그 외(은신 등)는
    // 그대로 맞는다 — 의도적으로 unattackableTarget 전체가 아니라 AreaUnattackableTarget만 적용한다.
    // TargetableEnemy = unattackableTarget 필터 적용(체인/멀티샷처럼 "특정 적을 타겟으로 선정"할 때).
    // Ally = 타일 점유자(OccupantObject) 기준 아군 조회. 영웅은 EnemyRegistry 같은 전역 리스트가
    // 없고 이미 타일당 1개 점유자 모델을 쓰고 있으므로 그 점유자를 훑는다(힐/피흡/힐 장판/오라용).
    // List + "이미 본 것" HashSet을 함께 써서 중복은 제거하되 타일 순회 순서는 유지한다 —
    // AttackTargetSelector.SelectTargets가 결과 리스트의 순서(pool[i % poolSize])에 의존하므로
    // HashSet 하나로만 중복 제거하면(순서 미보장) 멀티샷 대상 선정이 매 프레임 흔들릴 수 있다.
    public List<GameObject> GetObjectsInRange(Vector3 originWorld, int range, RangeShape shape,
        RangeQueryAffinity affinity = RangeQueryAffinity.Enemy, EnemyAttribute areaUnattackableMask = EnemyAttribute.None)
    {
        Vector2Int originCell = Board.WorldToCell(originWorld);
        var found = new List<GameObject>();
        var seen = new HashSet<GameObject>();

        if (affinity == RangeQueryAffinity.Ally)
        {
            foreach (Tile tile in TileShapeQuery.GetTiles(Board, originCell, range, shape))
            {
                GameObject occupant = tile.OccupantObject;
                if (occupant != null && occupant.GetComponent<Hero>() is Hero ally && !ally.IsDead && seen.Add(occupant))
                    found.Add(occupant);
            }
            return found;
        }

        foreach (Tile tile in TileShapeQuery.GetTiles(Board, originCell, range, shape))
            foreach (GameObject enemy in tile.Enemies)
            {
                if (enemy == null || !seen.Add(enemy)) continue;
                bool passes = affinity == RangeQueryAffinity.TargetableEnemy
                    ? IsTargetable(enemy)
                    : PassesAreaMask(enemy, areaUnattackableMask);
                if (passes) found.Add(enemy);
            }

        return found;
    }

    public List<IDamageAble> GetEnemiesInLine(Vector3 originWorld, Vector3 towardWorld, int length, int width = 0, EnemyAttribute areaUnattackableMask = EnemyAttribute.None)
    {
        Vector2Int originCell = Board.WorldToCell(originWorld);
        Vector2Int dir = GridCalculator.CardinalToward(originCell, Board.WorldToCell(towardWorld));

        var found = new HashSet<IDamageAble>();

        if (Board.TryGetCell(originCell, out Tile originTile))
            foreach (GameObject enemy in originTile.Enemies)
                if (enemy != null && PassesAreaMask(enemy, areaUnattackableMask) && enemy.GetComponentInParent<IDamageAble>() is IDamageAble d)
                    found.Add(d);

        foreach (Tile tile in TileShapeQuery.GetLineTiles(Board, originCell, dir, length, width))
            foreach (GameObject enemy in tile.Enemies)
                if (enemy != null && PassesAreaMask(enemy, areaUnattackableMask) && enemy.GetComponentInParent<IDamageAble>() is IDamageAble d)
                    found.Add(d);

        return new List<IDamageAble>(found);
    }

    public Vector2Int GetCardinalDirection(Vector3 originWorld, Vector3 towardWorld)
        => GridCalculator.CardinalToward(Board.WorldToCell(originWorld), Board.WorldToCell(towardWorld));

    public Vector3 GetLineEndPoint(Vector3 originWorld, Vector2Int direction, int length)
    {
        Vector2Int originCell = Board.WorldToCell(originWorld);
        List<Tile> line = TileShapeQuery.GetLineTiles(Board, originCell, direction, length);
        if (line.Count > 0) return line[line.Count - 1].WorldTop;
        return originWorld + new Vector3(direction.x, 0, direction.y) * length;
    }

    protected virtual void CheckTargetStillInRange()
    {
        if (!IsTargetable(target))
        {
            target = null;
            context.target = null;
            return;
        }

        foreach (Tile tile in TileShapeQuery.GetTiles(Board, origin, Range, RangeShape))
            foreach (GameObject enemy in tile.Enemies)
                if (enemy == target)
                    return;

        target = null;
        context.target = null;
    }

    public void SetBoard(MapBoard board) => this.board = board;

    public void Resurrection()
    {
        bool wasDead = isDead;
        currentHp = sc[StatType.HP];
        isDead = false;
        _bar.ResetTo(currentHp, sc[StatType.HP]);
        if (wasDead) SpawnAuraZones(); // 살아있던 영웅은 오라가 이미 돌고 있으므로 다시 스폰하면 중복된다
    }

    public void HealFull()
    {
        Heal(sc[StatType.HP]);
    }

    // 풀에서 다시 꺼내 배치될 때 호출 — Awake/Start는 인스턴스 생애 최초 1회만 돌므로, 두 번째 이후
    // "삶"에 필요한 런타임 상태 초기화는 여기서 명시적으로 다시 해준다.
    public void PrepareForSpawn()
    {
        isDead = false;
        stateMachine.ChangeState(idleState);
        anim.SetBool(HeroAnimHash.idle, true);
        RefreshBaseStats();
        _bar.ResetTo(currentHp, sc[StatType.HP]);
        target = null;
        context.target = null;
        SpawnAuraZones();
    }

    // 풀로 돌려보내기 직전 호출 — 이번 삶에서 쌓인 상태를 걷어내 다음 삶으로 새어 들어가지 않게 한다.
    public void PrepareForDespawn()
    {
        HeroSelectionService.ClearIfSelected(this);
        SetSelected(false);
        isDead = true; // 오라 장판(GroundZoneEffect)이 다음 tick에서 owner.IsDead를 보고 스스로 풀에 반납한다
        buffManager.RemoveAllBuffs(this); // 다음 삶에 좀비 버프/디버프 수정자가 겹치지 않도록 정리
        debuffTracker.Clear();
        debuffEffects.Reset();
        skillCts?.Cancel();
        skillCts?.Dispose();
        skillCts = null;
        // GameObject가 SetActive(false)되면 Update()가 멈춰 CurrentState가 그대로 얼어붙는다 — 공격/스킬
        // 중이었다면 그 상태의 attackCts/cts는 Destroy 토큰에만 묶여 있어(비활성화로는 안 풀림) 진행 중이던
        // UniTask 루프(특히 Continuous 채널링)가 비활성 인스턴스 뒤에서 계속 돌 수 있다. 다음 스폰까지
        // 기다리지 않고 여기서 즉시 idle로 되돌려 현재 상태의 Exit()(CTS 취소)을 지금 실행시킨다.
        stateMachine.ChangeState(idleState);
    }

    public void SetCurrentTile()
    {
        origin = Board.WorldToCell(transform.position);
        if (Board.TryGetCell(origin, out Tile current))
            currentTile = current;
    }

    public void ExchangeAttackDatas(List<AttackDataSO> datas) => basePattern = datas;

}

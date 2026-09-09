using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum GroundZoneMode { Damage, Heal, Buff }

// 장판 프리팹에 직접 붙는 컴포넌트. 예전엔 GroundZoneDataSO(데이터)+GroundZoneRunner(정적 tick 루프)+
// landEffect(별도 참조 프리팹)로 3분할돼 있었지만, 장판은 원래도 "스폰되는 자기 완결 오브젝트"라 프리팹
// 자신이 비주얼이자 틱 로직 주체가 되는 게 더 자연스럽다. Init 직후 스스로 tick을 돌다가 duration이
// 지나면(오라는 소유자가 죽을 때까지) 스스로 풀에 반납한다 — Hero._auraCts 같은 수동 재시작 로직이
// 필요 없어진다(소유자가 죽으면 이 컴포넌트가 다음 루프에서 스스로 감지하고 반납).
[DisallowMultipleComponent]
public class GroundZoneEffect : MonoBehaviour
{
    public GroundZoneMode mode = GroundZoneMode.Damage;
    public RangeShape shape = RangeShape.Diamond;
    public int radius = 1;
    public float tickInterval = 1f;
    [Tooltip("mode==Damage: 틱당 데미지 = 소유자 ATK * damagePer")]
    public float damagePer = 0.5f;
    [Tooltip("mode==Heal: 틱당 힐량 = 소유자 ATK * healPer (범위 내 최저 체력 아군 1명)")]
    public float healPer = 0f;
    public float hpHealPer = 0f;
    [Tooltip("0 이하 = 오라(소유자가 죽을 때까지 유지). 공격/스킬 트리거형 장판은 반드시 양수로 설정.")]
    public float duration = 3f;
    public List<TargetDebuffRef> targetDebuffs = new();
    public GameObject hitEffect;
    public float hitEffectLifetime = 0.5f;
    [Tooltip("mode==Heal: 범위 안에 머무는 동안 아군 발밑에 붙어 유지되는 이펙트. null이면 안 스폰.")]
    public GameObject healReceiveEffect;
    [Tooltip("mode==Buff: 버프 받는 아군 발밑에 붙어 범위 안에 머무는 동안 유지되는 이펙트. null이면 안 스폰.")]
    public GameObject buffReceiveEffect;
    [Tooltip("이 프리팹의 파티클/데칼이 기본 크기(localScale=1)로 나타내는 반경(타일 수). radius/이값만큼 자기 자신을 스케일한다. 0이면 스케일하지 않음.")]
    public float visualRadius = 0f;
    [Tooltip("장판이 살아있는 동안 자기 위치에 한 번 스폰해 유지하는 이펙트(범위 표시용). null이면 안 스폰.")]
    public GameObject selfEffect;
    [Tooltip("selfEffect가 기본 크기(localScale=1)로 나타내는 반경(타일 수). radius/이값만큼 스케일한다. 0이면 스케일하지 않음.")]
    public float selfEffectVisualRadius = 0f;
    [Tooltip("mode==Buff: 범위 안 아군에게 적용할 버프 목록. duration/maxStacks는 무시된다 — 범위에 머무는 동안 유지되고 벗어나면 즉시 제거된다.")]
    public List<BuffEffect> allyBuffs = new();

    [Header("사운드")]
    [Tooltip("장판 생성 시 재생할 EnemySoundManager 키. 비워두면 재생하지 않음")]
    public string spawnSoundKey;
    [Tooltip("장판이 살아있는 동안 계속 루프 재생할 EnemySoundManager 키. 소멸 시 정지. 비워두면 재생하지 않음")]
    public string sustainSoundKey;
    [Tooltip("장판이 대상에게 데미지/힐을 적용할 때 재생할 EnemySoundManager 키. 비워두면 재생하지 않음")]
    public string hitSoundKey;

    private MapBoard board;
    private Hero owner;
    private bool followOwner;
    private Action<GameObject> release;
    private CancellationTokenSource cts;
    private GameObject selfEffectInstance;
    private bool selfEffectVisible = true;
    private readonly HashSet<Hero> buffedAllies = new();
    private readonly Dictionary<Hero, GameObject> buffEffectInstances = new();
    private readonly HashSet<Hero> healPresentAllies = new();
    private readonly Dictionary<Hero, GameObject> healEffectInstances = new();
    private AudioSource sustainVoice;

    // Hero.SpawnGroundZone이 풀에서 꺼낸 직후 호출한다. followOwner는 오라(소유자를 따라다녀야 하는
    // 장판)인지, 스킬/공격이 심어놓고 떠나는 장판인지를 호출부가 명시한다(duration 값으로는 구분 불가 —
    // 실제 오라 프리팹도 duration을 999처럼 유한값으로 쓴다).
    public void Init(MapBoard board, Hero owner, bool followOwner, Action<GameObject> release)
    {
        this.board = board;
        this.owner = owner;
        this.followOwner = followOwner;
        this.release = release;
        selfEffectVisible = true;
        ApplyVisualScale();
        SpawnSelfEffect();
        if (!string.IsNullOrEmpty(spawnSoundKey)) EnemySoundManager.Play(spawnSoundKey, at: transform.position);
        if (!string.IsNullOrEmpty(sustainSoundKey)) sustainVoice = EnemySoundManager.PlayLoop(sustainSoundKey);
        if (duration > 0f) FitParticlesToDuration(duration);

        // 생명주기 루프는 여기서 시작한다 — OnEnable(풀에서 SetActive되는 순간, Init보다 먼저 도는
        // 시점)에서 시작하면 owner/board/selfEffectInstance가 아직 이전 대여의 잔여값이거나 null인
        // 채로 첫 프레임이 돌아 UpdatePersistentEffectVisibility가 엉뚱한 인스턴스를 만지거나,
        // 오라(owner==null)일 땐 루프가 즉시 종료되며 자기 자신을 풀에 반납해버린다.
        cts ??= new CancellationTokenSource();
        RunLifetime(cts.Token).Forget();
    }

    private void OnEnable()
    {
        cts = new CancellationTokenSource();
    }

    private void OnDisable()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private async UniTask RunLifetime(CancellationToken token)
    {
        try
        {
            bool persistent = duration <= 0f;
            float elapsed = 0f, tickTimer = 0f;
            while (!token.IsCancellationRequested && (persistent ? owner != null && !owner.IsDead : elapsed < duration))
            {
                float dt = Time.deltaTime;
                if (!persistent) elapsed += dt;
                tickTimer += dt;
                if (tickTimer >= tickInterval) { tickTimer = 0f; Tick(); }
                UpdatePersistentEffectVisibility();
                await UniTask.Yield(token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // sustainVoice는 EnemySoundManager 쪽 오브젝트에 속한 별개의 AudioSource라, this(장판
            // 자신)가 이미 파괴된 상태여도 안전하게 정지할 수 있다.
            if (sustainVoice != null) { sustainVoice.Stop(); sustainVoice = null; }

            if (mode == GroundZoneMode.Buff)
                ClearAllyBuffs();
            else if (mode == GroundZoneMode.Heal)
                ClearHealEffects();

            // this가 이미 파괴된 상태(예: followOwner 오라가 소유자 파괴와 함께 자식으로 같이 파괴된 경우)면
            // gameObject 등 네이티브 접근은 전부 건너뛴다 — 반납할 풀도 소유자와 함께 사라지는 것이므로 안전하다.
            if (this != null)
            {
                if (duration <= 0f && selfEffectInstance != null && owner != null)
                    owner.DespawnEffect(selfEffect, selfEffectInstance);

                // 풀로 돌아가기 전에 이번 생애의 잔여 상태를 전부 걷어낸다. 다음 대여 때 이 필드들이
                // 남아있으면(Init 전 첫 프레임/재사용 시) 이전 생애의 selfEffect/owner를 만지게 된다.
                // PlayerGroundZoneEffect.DespawnSelfEffect가 selfEffectInstance를 null로 되돌리는 것과 같은 취지.
                Action<GameObject> pendingRelease = release;
                selfEffectInstance = null;
                selfEffectVisible = true;
                owner = null;
                board = null;
                release = null;
                followOwner = false;
                buffEffectInstances.Clear();
                healEffectInstances.Clear();

                pendingRelease?.Invoke(gameObject);
            }
        }
    }

    // Buff 모드 장판이 사라질 때(정상 만료/소유자 사망/파괴) 아직 범위 안에 있던 아군의 버프도
    // "지금 나간 것"과 동일하게 정리한다 — 방치되는 버프가 없도록.
    private void ClearAllyBuffs()
    {
        if (buffedAllies.Count == 0) return;
        foreach (Hero ally in buffedAllies)
        {
            if (ally == null) continue;
            foreach (BuffEffect effect in allyBuffs)
                owner.Buffs.RemoveBuff(ally, effect.statType, this);
            DespawnAllyBuffEffect(ally);
        }
        buffedAllies.Clear();
    }

    private void SpawnAllyBuffEffect(Hero ally)
    {
        if (buffReceiveEffect == null) return;
        // SpawnPersistentEffect(스폰 시점 컷)가 아니라 Always를 쓴다 — 이 인스턴스는 아군이 범위를
        // 벗어날 때까지 계속 추적하며 매 프레임(RunLifetime) 가시성을 따로 토글하므로, 스폰 시점에
        // 화면 밖이라고 아예 건너뛰면 나중에 화면에 들어와도 켤 인스턴스가 없어 영구히 안 보이게 된다.
        GameObject fx = owner.SpawnPersistentEffectAlways(buffReceiveEffect, ally.transform.position,
            buffReceiveEffect.transform.localRotation);
        if (fx == null) return;
        fx.transform.SetParent(ally.transform, worldPositionStays: true);
        buffEffectInstances[ally] = fx;
    }

    private void DespawnAllyBuffEffect(Hero ally)
    {
        if (!buffEffectInstances.Remove(ally, out GameObject fx)) return;
        if (fx != null) owner.DespawnEffect(buffReceiveEffect, fx);
    }

    private void SpawnHealPresenceEffect(Hero ally)
    {
        if (healReceiveEffect == null) return;
        // 이유는 SpawnAllyBuffEffect와 동일 — Always로 스폰하고 가시성은 RunLifetime에서 매 프레임 관리.
        GameObject fx = owner.SpawnPersistentEffectAlways(healReceiveEffect, ally.transform.position,
            healReceiveEffect.transform.localRotation);
        if (fx == null) return;
        fx.transform.SetParent(ally.transform, worldPositionStays: true);
        healEffectInstances[ally] = fx;
    }

    private void DespawnHealPresenceEffect(Hero ally)
    {
        if (!healEffectInstances.Remove(ally, out GameObject fx)) return;
        if (fx != null) owner.DespawnEffect(healReceiveEffect, fx);
    }

    private void ClearHealEffects()
    {
        if (healPresentAllies.Count == 0) return;
        foreach (Hero ally in healPresentAllies)
            DespawnHealPresenceEffect(ally);
        healPresentAllies.Clear();
    }

    private void UpdateHealPresence(List<GameObject> alliesInRange)
    {
        var inRange = new HashSet<Hero>();
        foreach (GameObject go in alliesInRange)
            if (go.GetComponentInParent<Hero>() is Hero ally)
                inRange.Add(ally);

        foreach (Hero ally in inRange)
            if (healPresentAllies.Add(ally))
                SpawnHealPresenceEffect(ally);

        healPresentAllies.RemoveWhere(ally =>
        {
            if (ally != null && inRange.Contains(ally)) return false;
            if (ally != null) DespawnHealPresenceEffect(ally);
            return true;
        });
    }

    // selfEffect(오라/장판 표시)와 아군에 붙는 버프/힐 수신 이펙트는 스폰 시점 컷 없이 항상 살아있으므로,
    // 매 프레임 현재 위치 기준으로 화면 안/밖 여부를 다시 확인해 렌더러/파티클만 켜고 끈다. Tick()의
    // 데미지/힐/버프 적용 로직과는 무관하게 실행되므로 화면 표시 여부가 판정에 영향을 주지 않는다.
    private void UpdatePersistentEffectVisibility()
    {
        if (owner == null) return; // Init 전(잔여 상태)엔 손대지 않는다

        if (selfEffectInstance != null)
        {
            // Projectile.UpdateVisibility와 동일한 상태변화 가드 — 화면 안/밖이 실제로 바뀌는
            // 순간에만 토글한다(매 프레임 SetVisualActive를 때리지 않도록).
            bool visible = !VfxVisibility.IsOffscreen(selfEffectInstance.transform.position);
            if (visible != selfEffectVisible)
            {
                selfEffectVisible = visible;
                VfxVisibility.SetVisualActive(selfEffectInstance, visible);
            }
        }
        foreach (GameObject fx in buffEffectInstances.Values)
            if (fx != null)
                VfxVisibility.SetVisualActive(fx, !VfxVisibility.IsOffscreen(fx.transform.position));
        foreach (GameObject fx in healEffectInstances.Values)
            if (fx != null)
                VfxVisibility.SetVisualActive(fx, !VfxVisibility.IsOffscreen(fx.transform.position));
    }

    private void Tick()
    {
        if (board == null || owner == null) return;

        if (mode == GroundZoneMode.Heal)
        {
            List<GameObject> alliesInRange = owner.GetObjectsInRange(transform.position, radius, shape, RangeQueryAffinity.Ally);
            UpdateHealPresence(alliesInRange);

            float heal = owner.SC[StatType.ATK] * healPer;
            if (heal <= 0f) return;
            Hero target = AttackDamageUtil.FindLowestHpAlly(alliesInRange);
            if (target == null) return;
            heal = heal + target.SC[StatType.HP] * hpHealPer;
            target.Heal(heal);
            if (!string.IsNullOrEmpty(hitSoundKey)) EnemySoundManager.Play(hitSoundKey, at: transform.position);
            SpawnHitEffect(transform.position);
            return;
        }

        if (mode == GroundZoneMode.Buff)
        {
            var inRange = new HashSet<Hero>();
            foreach (GameObject go in owner.GetObjectsInRange(transform.position, radius, shape, RangeQueryAffinity.Ally))
                if (go.GetComponentInParent<Hero>() is Hero ally)
                    inRange.Add(ally);

            foreach (Hero ally in inRange)
            {
                if (!buffedAllies.Add(ally)) continue;
                foreach (BuffEffect effect in allyBuffs)
                    owner.Buffs.ApplyStackingModifier(ally, effect.statType, effect.modifierType, effect.value, 0f, effect.maxStacks, this);
                SpawnAllyBuffEffect(ally);
            }

            buffedAllies.RemoveWhere(ally =>
            {
                if (ally != null && inRange.Contains(ally)) return false;
                if (ally != null)
                {
                    foreach (BuffEffect effect in allyBuffs)
                        owner.Buffs.RemoveBuff(ally, effect.statType, this);
                    DespawnAllyBuffEffect(ally);
                }
                return true;
            });
            return;
        }

        int dmg = Mathf.RoundToInt(owner.SC[StatType.ATK] * damagePer);
        // 장판(지상)은 공중 적을 절대 때릴 수 없다 — 호출부 설정과 무관한 GroundZoneEffect 자체의 불변식.
        foreach (GameObject go in owner.GetObjectsInRange(transform.position, radius, shape, RangeQueryAffinity.Enemy, EnemyAttribute.Fly))
        {
            if (dmg > 0 && go.GetComponentInParent<IDamageAble>() is IDamageAble d)
            {
                d.TakeDamage(dmg);
                owner.NotifyHit(go, dmg, false);
                if (!string.IsNullOrEmpty(hitSoundKey)) EnemySoundManager.Play(hitSoundKey, at: go.transform.position);
                SpawnDamageHitEffect(go);
            }

            AttackDamageUtil.ApplyTargetDebuffs(go.transform, targetDebuffs, owner.Buffs, this, owner.SC[StatType.ATK]);
        }
    }

    private void SpawnHitEffect(Vector3 pos) => owner.SpawnEffect(hitEffect, pos, hitEffectLifetime);

    // 데미지 틱 전용 — 장판 안 적이 여러 마리면 틱마다 각자에게 이펙트가 뜨는데, 같은 적이 여러 틱에
    // 걸쳐 겹치면 파티클이 쌓이므로 대상별 중복 방지 헬퍼를 거친다. 힐 틱(SpawnHitEffect(Vector3))은
    // 아군 대상이라 억제 대상이 아니므로 그대로 둔다.
    private void SpawnDamageHitEffect(GameObject target)
        => AttackDamageUtil.SpawnHitEffect(owner, hitEffect, target, hitEffectLifetime);

    private void SpawnSelfEffect()
    {
        if (selfEffect == null) return;
        // SpawnEffect(스폰 시점 컷)가 아니라 Always를 쓴다 — 오라(duration<=0)/장판 자체가 살아있는
        // 동안 계속 유지되며 RunLifetime이 매 프레임 가시성을 따로 토글하므로, 스폰 시점에 화면 밖이면
        // 아예 건너뛰는 기존 방식으로는 나중에 화면에 들어와도 켤 인스턴스가 없다.
        selfEffectInstance = owner.SpawnEffectAlways(selfEffect, transform.position, selfEffect.transform.rotation, duration > 0f ? duration : 0f);
        if (selfEffectInstance == null) return;
        // 장판 자신의 transform(ApplyVisualScale로 이미 스케일됨)이 아니라 owner에 직접 매달아야
        // 스케일이 중첩되지 않는다.
        if (followOwner)
            selfEffectInstance.transform.SetParent(owner.transform, worldPositionStays: true);
        if (selfEffectVisualRadius > 0f)
            selfEffectInstance.transform.localScale = Vector3.one * (radius / selfEffectVisualRadius);
    }

    private void ApplyVisualScale()
    {
        if (visualRadius <= 0f) return;
        transform.localScale = Vector3.one * (radius / visualRadius);
    }

    // duration<=0(오라)일 땐 호출되지 않는다 — 원래도 반복되는 자식(Trail 등)은 그대로 두고,
    // 1회성(loop=false)으로 authored된 자식만 targetDuration에 맞춰 재생 속도를 조정한다.
    private void FitParticlesToDuration(float targetDuration)
    {
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            // hitEffect가 장판의 자식으로 박혀있는 프리팹(FireMageZone 등)이 있어, 이 스캔이
            // 장판 자신의 연출용 자식뿐 아니라 hitEffect까지 잘못 건드려 재생 속도를 깎을 수 있다.
            // hitEffect(및 그 하위)는 부모가 손댈 수 없도록 애초에 순회 대상에서 제외한다.
            if (hitEffect != null && ps.transform.IsChildOf(hitEffect.transform)) continue;
            ParticleSystem.MainModule main = ps.main;
            if (main.loop || main.duration <= 0f) continue;
            main.simulationSpeed = main.duration / targetDuration;
        }
    }
}

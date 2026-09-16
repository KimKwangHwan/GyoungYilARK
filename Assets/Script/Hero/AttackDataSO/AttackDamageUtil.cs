using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class AttackDamageUtil
{
    // 적의 bodyEffectAnchor(EnemyBase)가 있으면 그 위치, 없으면(적이 아니거나 앵커 미설정) 기존처럼
    // 루트 transform.position — 사거리/AoE 판정에는 쓰지 않고 이펙트·투사체 유도 좌표 전용.
    public static Vector3 EffectPosition(GameObject go) =>
        go.GetComponentInParent<EnemyBase>() is EnemyBase enemy ? enemy.BodyEffectAnchor.position : go.transform.position;

    public static Vector3 EffectPosition(Component c) => EffectPosition(c.gameObject);

    // 영웅 전체가 공유하는 대상별 피격 이펙트 목록. 같은 프리팹은 한 번만, 서로 다른 프리팹도
    // 최대 3개까지만 허용한다. 자식 콜라이더도 같은 EnemyBase로 묶고, 실제 풀 반납 때 해제한다.
    // 재생 구간은 기존 Hero.SpawnEffect의 lifetime(게임 시간)과 동일하다.
    private static readonly Dictionary<EnemyBase, HashSet<GameObject>> activeHitEffects = new();
    private const int MaxConcurrentHitEffectsPerTarget = 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetHitEffects() => activeHitEffects.Clear();

    public static GameObject SpawnHitEffect(Hero hero, GameObject prefab, GameObject target, float lifetime,
        Vector3? position = null)
    {
        if (hero == null || prefab == null || target == null) return null;
        Vector3 pos = position ?? EffectPosition(target);
        // 자동 반납하지 않는 상시 이펙트와 아군 이펙트는 피격 제한 대상이 아니다.
        if (lifetime <= 0f || target.GetComponentInParent<EnemyBase>() is not EnemyBase enemy)
            return hero.SpawnEffect(prefab, pos, lifetime);

        activeHitEffects.TryGetValue(enemy, out var effects);
        if (effects != null && (effects.Contains(prefab) || effects.Count >= MaxConcurrentHitEffectsPerTarget))
            return null;

        effects ??= new HashSet<GameObject>();
        var registeredEffects = effects;
        GameObject instance = hero.SpawnEffect(prefab, pos, lifetime, () =>
        {
            // 이전 플레이 세션의 콜백이 새 세션의 목록을 지우지 않도록 목록 참조도 확인한다.
            if (!activeHitEffects.TryGetValue(enemy, out var current) || current != registeredEffects) return;
            current.Remove(prefab);
            if (current.Count == 0) activeHitEffects.Remove(enemy);
        });
        // 화면 밖 컷/전체 예산 초과로 스폰되지 않았다면 대상 슬롯도 소비하지 않는다.
        if (instance == null) return null;
        effects.Add(prefab);
        activeHitEffects[enemy] = effects;
        return instance;
    }

    public static void SpawnHitEffect(Hero hero, GameObject prefab, Component target, float lifetime)
        => SpawnHitEffect(hero, prefab, target != null ? target.gameObject : null, lifetime);

    private static readonly Dictionary<(Hero caster, GameObject prefab), float> casterEffectBusyUntil = new();
    // unscaled 실시간 예산이므로 shotInterval(게임시간, 배속에 비례해 짧아짐)과는 더 이상 같은 축이
    // 아니다 — 배속이 오를수록 shotInterval(실시간)이 이 값보다 먼저 짧아지고, 그 순간부터는 "볼리
    // 한 발마다"가 아니라 "볼리 전체에 근원 이펙트 1개"로 자연스럽게 접힌다. 회귀가 아니라 의도:
    // 배속에서 정확히 줄이고 싶은 지점이 여기다.
    private const float CasterEffectDedupInterval = 0.15f;

    // attackEffect(스윙/발사 이펙트)·flashEffect(투사체 착탄 플래시)처럼 캐스터 자신이 짧은 간격으로
    // 반복 스폰하는 "근원" 이펙트 전용 억제. SpawnHitEffect(타겟 기준)와 축이 달라 서로 간섭하지
    // 않는다 — 절대 Hero.SpawnEffect 안에 넣지 말 것: SpawnHitEffect도 내부적으로 그걸 호출하므로,
    // 거기서 캐스터 키로 억제하면 같은 캐스터가 AoE로 서로 다른 적 여러 명을 때릴 때 정당한 개별
    // 히트 이펙트까지 지워진다.
    public static void SpawnCasterEffect(Hero hero, GameObject prefab, Vector3 pos, Quaternion rot, float lifetime)
    {
        if (prefab == null) return;
        var key = (hero, prefab);
        float now = Time.unscaledTime;
        if (casterEffectBusyUntil.TryGetValue(key, out float busyUntil) && now < busyUntil) return;
        casterEffectBusyUntil[key] = now + CasterEffectDedupInterval;
        hero.SpawnEffect(prefab, pos, rot, lifetime);
    }

    // 대상이 살아있는 동안은 EffectPosition을 다시 읽고, 파괴되거나 풀에 반납되면 마지막 위치에
    // 고정한다 — 빔/체인 이펙트(BeamLinkEffect.Track)가 매 프레임 스스로 위치를 갱신할 때 쓴다.
    public static Func<Vector3> TrackingPosition(GameObject target)
    {
        Vector3 last = EffectPosition(target);
        return () =>
        {
            if (target != null) last = EffectPosition(target);
            return last;
        };
    }

    // 호출부가 이미 자체적으로(예: 채널링 틱마다) 사운드를 관리하는 경우 playSound=false(기본값)로
    // 이중 재생을 피한다. true면 아래 attackCount 기반 반복마다 1회씩 재생해 히트 횟수와 맞춘다.
    private static void PlaySound(AttackDataSO data, Vector3 at)
    {
        if (!string.IsNullOrEmpty(data.attackSoundKey)) EnemySoundManager.Play(data.attackSoundKey, at: at);
    }

    public static async UniTask ApplyInstantDamage(AttackDataSO data, AttackContext ctx, CancellationToken ct, bool playSound = false)
    {
        float baseDamage = ctx.sc[StatType.ATK] * data.attackPer;

        // ctx.hero.GetObjectsInRange를 부분적용한 어댑터 — ApplyHealOptions/ChainResolver는 여전히
        // Func<Vector3,int,RangeShape,List<GameObject>> 3-인자 시그니처를 기대한다.
        List<GameObject> AllyQuery(Vector3 p, int r, RangeShape s) => ctx.hero.GetObjectsInRange(p, r, s, RangeQueryAffinity.Ally);
        List<GameObject> TargetableEnemyQuery(Vector3 p, int r, RangeShape s) => ctx.hero.GetObjectsInRange(p, r, s, RangeQueryAffinity.TargetableEnemy);

        ApplySelfBuffs(ctx.hero, data.buffList, ctx.buffManager, data);

        if (data.attackType == AttackType.Area && data.areaShape == AreaShape.Line)
        {
            for (int i = 0; i < data.attackCount; i++)
            {
                if (playSound) PlaySound(data, ctx.self.position);
                foreach (IDamageAble e in ctx.hero.GetEnemiesInLine(ctx.self.position, ctx.target.position, data.lineLength, data.areaRange, data.AreaUnattackableTarget))
                {
                    e.TakeDamage((int)baseDamage);
                    ctx.hero.NotifyHit((e as Component)?.gameObject, (int)baseDamage, false);
                    ApplyTargetDebuffs(e as Component, data.targetDebuffs, ctx.buffManager, data, ctx.sc[StatType.ATK]);
                    ApplyHealOptions(data, ctx.self.position, ctx.hero.Heal, AllyQuery, baseDamage, ctx.sc[StatType.ATK]);
                    SpawnHitEffect(ctx.hero, data.hitEffect, e as Component, data.hitEffectLifetime);
                }
                if (i < data.attackCount - 1)
                    await UniTask.Delay(TimeSpan.FromSeconds(data.shotInterval), cancellationToken: ct);
            }
            return;
        }

        if (data.areaShape == AreaShape.Chain)
        {
            if (playSound) PlaySound(data, ctx.self.position);
            List<GameObject> hits = ChainResolver.Resolve(ctx.target.gameObject, baseDamage, data.chainRange, data.chainCount,
                data.chainFalloff, TargetableEnemyQuery, ctx.hero.NotifyHit);
            foreach (GameObject go in hits)
            {
                ApplyTargetDebuffs(go.transform, data.targetDebuffs, ctx.buffManager, data, ctx.sc[StatType.ATK]);
                ApplyHealOptions(data, ctx.self.position, ctx.hero.Heal, AllyQuery, baseDamage, ctx.sc[StatType.ATK]);
                SpawnHitEffect(ctx.hero, data.hitEffect, go, data.hitEffectLifetime);
            }
            // hits[0]은 체인 시작 타겟(캐스터→시작 타겟 구간은 빔 비주얼 등 별도 이펙트가 표현) —
            // 튕긴 대상들 사이(hits[i]→hits[i+1])만 아크로 잇는다.
            for (int i = 0; i < hits.Count - 1; i++)
                ctx.hero.SpawnChainArc(data.chainEffectPrefab, hits[i], hits[i + 1], data.chainEffectLifetime);
            return;
        }

        if (data.attackType == AttackType.Single && data.targetMode == TargetMode.SameTarget)
        {
            for (int i = 0; i < data.attackCount; i++)
            {
                if (playSound) PlaySound(data, ctx.self.position);
                if (ctx.target.GetComponent<IDamageAble>() is IDamageAble d)
                {
                    d.TakeDamage((int)baseDamage);
                    ctx.hero.NotifyHit(ctx.target.gameObject, (int)baseDamage, false);
                    ApplyTargetDebuffs(ctx.target, data.targetDebuffs, ctx.buffManager, data, ctx.sc[StatType.ATK]);
                    ApplyHealOptions(data, ctx.self.position, ctx.hero.Heal, AllyQuery, baseDamage, ctx.sc[StatType.ATK]);
                    SpawnHitEffect(ctx.hero, data.hitEffect, ctx.target, data.hitEffectLifetime);
                }
                if (i < data.attackCount - 1)
                    await UniTask.Delay(TimeSpan.FromSeconds(data.shotInterval), cancellationToken: ct);
            }
            return;
        }

        if (data.attackType == AttackType.Single && data.targetMode == TargetMode.DifferentEnemies)
        {
            List<GameObject> enemies = ctx.hero.GetObjectsInRange(ctx.self.position, data.range, data.rangeShape, RangeQueryAffinity.TargetableEnemy);
            List<GameObject> targets = AttackTargetSelector.SelectTargets(enemies, data.attackCount, data.targetCount);
            await FireEach(targets, t =>
            {
                if (playSound) PlaySound(data, t.transform.position);
                if (t.GetComponentInParent<IDamageAble>() is not IDamageAble d) return;
                d.TakeDamage((int)baseDamage);
                ctx.hero.NotifyHit(t, (int)baseDamage, false);
                ApplyTargetDebuffs(t.transform, data.targetDebuffs, ctx.buffManager, data, ctx.sc[StatType.ATK]);
                ApplyHealOptions(data, ctx.self.position, ctx.hero.Heal, AllyQuery, baseDamage, ctx.sc[StatType.ATK]);
                SpawnHitEffect(ctx.hero, data.hitEffect, t, data.hitEffectLifetime);
            }, data.shotInterval, ct);
            return;
        }

        RangeShape aoeShape = ResolveAoeShape(data);

        if (data.attackType == AttackType.Area && data.targetMode == TargetMode.SameTarget)
        {
            Vector3 aoeCenter = data.areaCenterOnTarget && ctx.target != null ? ctx.target.position : ctx.self.position;
            for (int i = 0; i < data.attackCount; i++)
            {
                if (playSound) PlaySound(data, ctx.self.position);
                foreach (GameObject go in ctx.hero.GetObjectsInRange(aoeCenter, data.areaRange, aoeShape, RangeQueryAffinity.Enemy, data.AreaUnattackableTarget))
                {
                    if (go.GetComponentInParent<IDamageAble>() is not IDamageAble e) continue;
                    e.TakeDamage((int)baseDamage);
                    ctx.hero.NotifyHit(go, (int)baseDamage, false);
                    ApplyTargetDebuffs(go.transform, data.targetDebuffs, ctx.buffManager, data, ctx.sc[StatType.ATK]);
                    ApplyHealOptions(data, ctx.self.position, ctx.hero.Heal, AllyQuery, baseDamage, ctx.sc[StatType.ATK]);
                    SpawnHitEffect(ctx.hero, data.hitEffect, go, data.hitEffectLifetime);
                }
                if (i < data.attackCount - 1)
                    await UniTask.Delay(TimeSpan.FromSeconds(data.shotInterval), cancellationToken: ct);
            }
            return;
        }

        List<GameObject> enemyObjects = TargetableEnemyQuery(ctx.self.position, data.range, data.rangeShape);
        List<GameObject> centers = AttackTargetSelector.SelectTargets(enemyObjects, data.attackCount, data.targetCount);
        await FireEach(centers, go =>
        {
            if (playSound) PlaySound(data, go.transform.position);
            foreach (GameObject hit in ctx.hero.GetObjectsInRange(go.transform.position, data.areaRange, aoeShape, RangeQueryAffinity.Enemy, data.AreaUnattackableTarget))
            {
                if (hit.GetComponentInParent<IDamageAble>() is not IDamageAble e) continue;
                e.TakeDamage((int)baseDamage);
                ctx.hero.NotifyHit(hit, (int)baseDamage, false);
                ApplyTargetDebuffs(hit.transform, data.targetDebuffs, ctx.buffManager, data, ctx.sc[StatType.ATK]);
                ApplyHealOptions(data, ctx.self.position, ctx.hero.Heal, AllyQuery, baseDamage, ctx.sc[StatType.ATK]);
                SpawnHitEffect(ctx.hero, data.hitEffect, hit, data.hitEffectLifetime);
            }
        }, data.shotInterval, ct);
    }

    // Area 공격의 AOE 판정 모양 — Square만 실제 사각형, 그 외(Diamond/Line/Chain 오분류 방지용 기본값)는
    // 전부 Diamond로 취급한다. ApplyInstantDamage의 자기중심/다중센터 AOE 분기와 ContinuousBeamStrategy의
    // SelfArea 종료 판정(주변에 적이 남아있는지 체크)이 서로 다른 모양을 쓰면 안 되므로 한 곳에 모은다.
    public static RangeShape ResolveAoeShape(AttackDataSO data) =>
        data.areaShape == AreaShape.Square ? RangeShape.Square : RangeShape.Diamond;

    // Healer 전용 — TakeDamage 대신 Heal을 적용한다. 힐은 "적중"이 아니므로 onHit 훅을 부르지 않는다.
    public static UniTask ApplyInstantHeal(AttackDataSO data, AttackContext ctx, CancellationToken ct)
    {
        float healAmount = ctx.sc[StatType.ATK] * data.attackPer;
        ApplySelfBuffs(ctx.hero, data.buffList, ctx.buffManager, data);

        if (data.attackType == AttackType.Single)
        {
            if (ctx.target != null && ctx.target.GetComponent<Hero>() is Hero singleAlly)
            {
                singleAlly.Heal(healAmount);
                ctx.hero.SpawnEffect(data.hitEffect, ctx.target.position, data.hitEffectLifetime);
            }
            return UniTask.CompletedTask;
        }

        RangeShape aoeShape = ResolveAoeShape(data);
        foreach (GameObject go in ctx.hero.GetObjectsInRange(ctx.self.position, data.areaRange, aoeShape, RangeQueryAffinity.Ally))
            if (go.GetComponent<Hero>() is Hero areaAlly)
            {
                areaAlly.Heal(healAmount);
                ctx.hero.SpawnEffect(data.hitEffect, areaAlly.transform.position, data.hitEffectLifetime);
            }

        return UniTask.CompletedTask;
    }

    public static void ApplyHealOptions(AttackDataSO data, Vector3 selfPos,
        Action<float> healSelf,
        Func<Vector3, int, RangeShape, List<GameObject>> getAllyObjectsInRange,
        float damageDealt,
        float casterAtk)
    {
        if (data.lifestealPercent > 0f && damageDealt > 0f)
            healSelf?.Invoke(damageDealt * data.lifestealPercent);

        if (data.allyHealAmount > 0f && getAllyObjectsInRange != null)
        {
            Hero target = FindLowestHpAlly(getAllyObjectsInRange(selfPos, data.allyHealRange, data.allyHealRangeShape));
            target?.Heal(casterAtk * data.allyHealAmount);
        }
    }

    // 다친(풀피가 아닌) 후보 중 Hp가 가장 낮은 대상. 풀피 아군을 먼저 제외해야 "최대 체력이 작은 풀피
    // 영웅"이 "다쳤지만 최대 체력이 큰 영웅"보다 절대 Hp가 낮게 나와 잘못 선택되는 문제를 피한다.
    // Healer.AcquireTargetFromTiles / GroundZoneEffect 힐 틱에서도 재사용.
    public static Hero FindLowestHpAlly(List<GameObject> candidates)
    {
        Hero lowest = null;
        float lowestHp = float.MaxValue;
        foreach (GameObject go in candidates)
        {
            if (go.GetComponentInParent<Hero>() is Hero d
                && d.Hp < d.SC[StatType.HP]
                && d.Hp < lowestHp)
            {
                lowestHp = d.Hp;
                lowest = d;
            }
        }
        return lowest;
    }

    public static void ApplySelfBuffs(IUnit selfUnit, List<BuffEffect> buffList, BuffManager buffManager, object source)
    {
        if (buffList == null || selfUnit == null) return;
        foreach (BuffEffect effect in buffList)
        {
            buffManager.ApplyStackingModifier(selfUnit, effect.statType, effect.modifierType,
                effect.value, effect.duration, effect.maxStacks, source);
        }
    }

    // 적 디버프 시스템(DebuffSO/DebuffContext/DebuffTracker)을 그대로 재사용한다 — EnemyBase.ApplyDebuff와
    // 하는 일이 같고, DebuffApply.To가 유일한 공용 입구다. 이걸 거쳐야 DebuffTracker 장부·면역·UI 아이콘이
    // 반영된다(예전처럼 BuffManager를 직접 호출하면 스탯만 바뀌고 장부에는 안 남는다).
    public static void ApplyTargetDebuffs(Component target, List<TargetDebuffRef> targetDebuffs, BuffManager buffManager, object source, float attackerAtk = 0f)
    {
        if (target == null || targetDebuffs == null) return;
        foreach (TargetDebuffRef entry in targetDebuffs)
        {
            if (entry.debuff == null) continue;
            float scale = entry.scale > 0f ? entry.scale : 1f;
            DebuffApply.To(target, entry.debuff, buffManager, null, entry.durationOverride, scale, source, attackerAtk);
        }
    }

    private static async UniTask FireEach<T>(List<T> items, Action<T> apply, float interval, CancellationToken ct)
    {
        for (int i = 0; i < items.Count; i++)
        {
            apply(items[i]);
            if (i < items.Count - 1)
                await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: ct);
        }
    }
}

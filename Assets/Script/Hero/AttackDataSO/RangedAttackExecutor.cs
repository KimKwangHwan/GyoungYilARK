using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

public class RangedAttackExecutor : IAttackExecutor
{
    private readonly AnimTriggerPicker triggers = new(nameof(RangedAttackExecutor));

    public async UniTask Execute(AttackDataSO data, AttackContext ctx, CancellationToken ct)
    {
        float interval = ctx.sc[StatType.AS] > 0f ? 1f / ctx.sc[StatType.AS] : 1f;
        float scale = AttackAnimSpeedUtil.ComputeScale(data, interval);
        AttackAnimSpeedUtil.SetSpeed(ctx.anim, scale);
        if (ctx.bowAnim != null && ctx.arrowAnim != null)
        {
            AttackAnimSpeedUtil.SetSpeed(ctx.bowAnim, scale);
            AttackAnimSpeedUtil.SetSpeed(ctx.arrowAnim, scale);
        }

        ctx.anim.SetTrigger(triggers.Pick(data));

        if (ctx.bowAnim != null && ctx.arrowAnim != null)
        {
            ctx.bowAnim.SetTrigger("Attack");
            ctx.arrowAnim.SetTrigger("Attack");
        }

        IObjectPool<Projectile> pool = ctx.hero.GetProjectilePool(data.projectilePrefab);
        int damage = (int)(ctx.sc[StatType.ATK] * data.attackPer);

        try
        {
            float windowDuration = AttackAnimSpeedUtil.ComputeWindowDuration(data, interval);
            await AttackEventWindow.RunHits(ctx.animEvents, windowDuration, async token =>
            {
                AttackDamageUtil.ApplySelfBuffs(ctx.hero, data.buffList, ctx.buffManager, data);
                await FireVolley(data, ctx, pool, damage, token);
            }, ct);
        }
        finally
        {
            AttackAnimSpeedUtil.SetSpeed(ctx.anim, 1f);
            if (ctx.bowAnim != null && ctx.arrowAnim != null)
            {
                AttackAnimSpeedUtil.SetSpeed(ctx.bowAnim, 1f);
                AttackAnimSpeedUtil.SetSpeed(ctx.arrowAnim, 1f);
            }
            ctx.hero.AimOverrideTarget = null;
        }
    }

    private async UniTask FireVolley(AttackDataSO data, AttackContext ctx, IObjectPool<Projectile> pool, int damage, CancellationToken ct)
    {
        List<GameObject> targets;
        if (data.targetMode == TargetMode.DifferentEnemies)
        {
            List<GameObject> enemies = ctx.hero.GetObjectsInRange(ctx.self.position, data.range, data.rangeShape, RangeQueryAffinity.TargetableEnemy);
            targets = AttackTargetSelector.SelectTargets(enemies, data.attackCount, data.targetCount);
        }
        else
        {
            if (ctx.target == null) return; // 채널링/취소 경합 등으로 타겟이 비는 순간 방어
            targets = new List<GameObject>(data.attackCount);
            for (int i = 0; i < data.attackCount; i++)
                targets.Add(ctx.target.gameObject);
        }

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == null) continue; // 볼리 도중 타겟이 먼저 죽었으면(다른 피해 등) 건너뛴다
            ctx.hero.AimOverrideTarget = targets[i].transform;
            FireArrow(pool, ctx, targets[i].transform, damage, data);
            if (i < targets.Count - 1)
                await UniTask.Delay(System.TimeSpan.FromSeconds(data.shotInterval), cancellationToken: ct);
        }
    }

    private void FireArrow(IObjectPool<Projectile> pool, AttackContext ctx, Transform target, int damage, AttackDataSO data)
    {
        Projectile arrow = pool.Get();
        arrow.transform.SetPositionAndRotation(ctx.MuzzleOrSelf.position, ctx.MuzzleOrSelf.rotation);
        AttackDamageUtil.SpawnCasterEffect(ctx.hero, data.attackEffect, ctx.MuzzleOrSelf.position, ctx.MuzzleOrSelf.rotation, data.attackEffectLifetime);
        if (!string.IsNullOrEmpty(data.attackSoundKey)) EnemySoundManager.Play(data.attackSoundKey, at: ctx.MuzzleOrSelf.position);

        if (data.attackType == AttackType.Area && data.areaShape == AreaShape.Line)
        {
            Vector2Int dir = ctx.hero.GetCardinalDirection(ctx.self.position, target.position);
            List<IDamageAble> enemies = ctx.hero.GetEnemiesInLine(ctx.self.position, target.position, data.lineLength, data.areaRange, data.AreaUnattackableTarget);
            // 화살이 가까운 적부터 지나가므로, 캐스터 기준 거리순으로 정렬해 지나치는 순서와 맞춘다.
            enemies.Sort((a, b) =>
                Vector3.SqrMagnitude(AttackDamageUtil.EffectPosition(a as Component) - ctx.self.position)
                    .CompareTo(Vector3.SqrMagnitude(AttackDamageUtil.EffectPosition(b as Component) - ctx.self.position)));
            List<Vector3> hitPoints = new List<Vector3>(enemies.Count);
            List<GameObject> hitTargets = new List<GameObject>(enemies.Count);
            foreach (IDamageAble e in enemies)
            {
                e.TakeDamage(damage);
                ctx.hero.NotifyHit((e as Component)?.gameObject, damage, false);
                AttackDamageUtil.ApplyTargetDebuffs(e as Component, data.targetDebuffs, ctx.buffManager, data, ctx.sc[StatType.ATK]);
                AttackDamageUtil.ApplyHealOptions(data, ctx.self.position, ctx.hero.Heal,
                    (p, r, s) => ctx.hero.GetObjectsInRange(p, r, s, RangeQueryAffinity.Ally), damage, ctx.sc[StatType.ATK]);
                AttackDamageUtil.SpawnHitEffect(ctx.hero, data.hitEffect, e as Component, data.hitEffectLifetime);
                hitPoints.Add(AttackDamageUtil.EffectPosition(e as Component));
                hitTargets.Add((e as Component)?.gameObject);
            }
            if (data.groundZonePrefab != null)
                ctx.hero.SpawnGroundZone(data.groundZonePrefab, target.position);
            Vector3 endPoint = ctx.hero.GetLineEndPoint(ctx.self.position, dir, data.lineLength);
            arrow.LaunchVisualOnly(endPoint, pool, ctx.hero, hitPoints, hitTargets);
            return;
        }

        arrow.Launch(target, damage, pool,
            ProjectileAoEConfig.From(data, ctx.hero, ctx.sc, ctx.buffManager, ctx.self.position));
    }
}

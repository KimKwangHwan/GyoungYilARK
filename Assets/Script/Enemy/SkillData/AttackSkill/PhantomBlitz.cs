using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 팬텀 블리츠 — 사거리 안의 영웅들을 분신이 하나씩 스치고 지나가는 연속 타격.
///
/// 시전자는 제자리에 서 있고, 타수(value)만큼 분신을 뿌린다. 분신 하나가 영웅 하나를 한 대만 때리고
/// 자기 공격 애니가 끝나면 바로 사라진다. 분신마다 아래 세 스테이트 중 하나를 무작위로 재생한다.
///
/// 대상 순번은 "사거리 안 영웅을 섞어 한 명씩" 돌린다 — 매 타격마다 무작위로 다시 뽑으면
/// 한 영웅만 다섯 대 맞고 옆 영웅은 안 맞는 판이 나온다. 한 바퀴를 다 돌면 사거리를 다시 훑어
/// 새로 섞으므로, 타수가 영웅 수보다 많으면 두 바퀴째부터 다시 맞는다.
///
/// 타격은 tickInterval 간격으로 어긋나게 나간다(동시 타격 아님). 분신은 저마다 자기 수명을 따로 살아
/// 서로 겹쳐 보이고, Execute는 마지막 분신이 사라질 때까지 기다린 뒤에야 끝난다 —
/// EnemyBase.RunSkill이 Execute가 끝나는 순간 토큰을 dispose하므로, 여기서 안 기다리면
/// 아직 날아가는 분신이 이미 버려진 토큰을 붙들게 된다.
///
/// ※ 데미지는 이 스킬이 코드에서 직접 준다. 분신의 공격 클립에는 "AttackHit" 애니메이션 이벤트를
///   넣지 말 것 — RushAttackSkillSO와 같은 이유로 이중 타격이 된다.
/// </summary>
[CreateAssetMenu(menuName = "Data/Skill/PhantomBlitz")]
public class PhantomBlitz : AttackSkillDataSO
{
    public int value; //분신 마릿수
    public int valueScale; // 마릿수 증가량

    [Tooltip("분신 프리팹. Animator가 붙어 있어야 하고, 그 Animator에 SkillAttack / SkillAttack1 / SkillAttack2 " +
             "스테이트가 있어야 한다.\n" +
             "★ EnemyBase(및 파생) 컴포넌트가 붙어 있으면 안 된다. 적 프리팹을 그대로 복제해 쓰면 " +
             "활성화되는 순간 EnemyBase.OnEnable이 돌아 분신이 진짜 적으로 살아난다 — " +
             "EnemyRegistry에 등록되고, 도감이 해금되고, 자기 스킬 루프와 공격 루프까지 돌린다. " +
             "모델 + Animator + 재질만 남긴 껍데기 프리팹으로 만들 것.\n" +
             "비우면 분신 없이 피해만 들어간다 — 수치만 먼저 확인할 때 쓴다.")]
    public GameObject phantomPrefab;

    [Tooltip("분신이 나타나는 순간의 이펙트. 비우면 안 나온다.")]
    public GameObject onSkillEffectPrefab;

    [Tooltip("피해가 들어가는 순간 대상 위치에 뜨는 타격 이펙트. 비우면 안 나온다.")]
    public GameObject onAttackEffectPrefab;

    [Tooltip("이펙트 자동 반환까지의 수명(초). 이펙트 재생 길이에 맞춘다.")]
    [Min(0f)] public float effectLifetime = 1f;

    [Tooltip("분신이 나타난 뒤 실제로 피해가 들어가기까지의 시간(초). 공격 클립에서 칼이 닿는 순간에 맞춘다.")]
    [Min(0f)] public float phantomHitDelay = 0.2f;

    [Tooltip("분신이 대상에게서 얼마나 떨어져 나타날지(월드 단위). 방향은 매번 무작위라 같은 자리에 겹치지 않는다.")]
    [Min(0f)] public float phantomDistance = 1.2f;

    [Tooltip("분신 소환 높이 보정. 발이 뜨거나 파묻히면 여기서 맞춘다.")]
    public float phantomHeightOffset;
    private string hitSoundKey = "DeathNormalAttack2";

    [Tooltip("분신을 뿌리는 동안 시전자를 제자리에 세운다. " +
             "멈춰 있는 시간은 대략 (마릿수-1) x tickInterval + 분신 한 마리 수명이다 — 마릿수가 크면 그만큼 오래 굳는다.")]
    public bool suspendMovementWhileCasting = true;

    // 분신이 재생할 공격 애니 스테이트 세 개. 분신마다 이 중 하나를 무작위로 고른다.
    // 분신 Animator의 스테이트 이름과 정확히 같아야 한다(대소문자 구분).
    // RushAttackSkillSO의 leftState/rightState와 같은 방식으로 코드에 고정해 둔다.
    private string skillAttackState = "SkillAttack";
    private string skillAttackState1 = "SkillAttack1";
    private string skillAttackState2 = "SkillAttack2";
    // 분신 클립 길이를 못 읽었을 때 강제로 반납하는 시간(초). 안 두면 분신이 영영 안 사라진다.
    private float animTimeout = 3f;

    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        if (owner == null || owner.IsDead || owner.animator == null) return;
        if (owner.Board == null) return;

        int attackCount = value + (valueScale * (owner.GameManager != null ? owner.GameManager.DayCount / 5 : 0));
        if (attackCount <= 0) return;

        int cellRange = range > 0f ? Mathf.RoundToInt(range) : 1;

        // 목록은 반드시 지역 변수. 이 SO는 Resources.Load로 같은 스킬을 쓰는 모든 적이 공유하는 애셋이라
        // 필드에 담으면 두 적이 동시에 시전할 때 서로의 대상 순번을 덮어쓴다.
        var targets = new List<Hero>();
        CollectHeroes(owner, cellRange, targets);
        // 사거리에 영웅이 없으면 분신을 안 뿌린다. 단 EnemyBase.RunSkill의 finally가 skillTimers를 0으로
        // 되돌리므로 쿨다운은 그대로 소모된다 — 헛방을 치면 다음 시전까지 쿨다운을 또 기다린다.
        if (targets.Count == 0) return;
        Shuffle(targets);

        int damageValue = Mathf.RoundToInt(owner.AttackPower*0.15f);
        // tickInterval이 0이면 한 프레임에 전부 나가 "동시 타격"이 된다 — 표에서 빠뜨렸을 때의 하한.
        float strikeInterval = Mathf.Max(0.02f, tickInterval);

        var strikes = new List<UniTask>(attackCount);
        if (suspendMovementWhileCasting) owner.MovementSuspended = true;

        try
        {
            owner.animator.SetTrigger("Skill");
            await WaitForAnimationEnd(owner, "Skill", animTimeout, token);
            int cursor = 0;
            for (int i = 0; i < attackCount; i++)
            {
                if (owner.IsDead) break;

                Hero target = NextTarget(owner, cellRange, targets, ref cursor);
                if (target == null) break;   // 사거리 안에 살아있는 영웅이 없다 — 남은 타수는 버린다

                // 분신 하나하나를 떼어 굴린다. 여기서 await하면 분신 수명만큼 다음 타격이 밀려
                // tickInterval이 의미를 잃고, 분신이 겹쳐 보이지도 않는다.
                strikes.Add(StrikeOnce(owner, target, damageValue, token));

                // 마지막 타격 뒤엔 기다리지 않는다 — 뒤에 나갈 분신이 없다.
                if (i < attackCount - 1)
                    await UniTask.Delay(TimeSpan.FromSeconds(strikeInterval), cancellationToken: token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // 취소로 빠져나와도 날아가 있는 분신이 스스로 정리될 때까지 기다린다.
            // StrikeOnce가 취소를 자기 안에서 삼키므로 취소 시에도 곧바로 끝난다.
            if (strikes.Count > 0) await UniTask.WhenAll(strikes);
            if (owner != null && suspendMovementWhileCasting) owner.MovementSuspended = false;
        }
    }
    private static async UniTask WaitForAnimationEnd(EnemyBase owner, string stateName, float timeout, CancellationToken token)
    {
        var anim = owner.animator;
        const int layer = 0;

        float elapsed = 0f;
        while (owner != null && !anim.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
        {
            if (owner.IsDead) return;
            elapsed += Time.deltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"PhantomBlitz: Animator에서 '{stateName}' 스테이트를 찾지 못함 — 이름/전이 확인.", owner);
                return;
            }
            await UniTask.Yield(token);
        }
        if (owner == null) return;

        var info = anim.GetCurrentAnimatorStateInfo(layer);
        float wait = info.length / Mathf.Max(0.01f, anim.speed);
        await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token);
    }

    /// <summary>분신 하나의 한살이 — 나타나고, 한 대 때리고, 애니가 끝나면 사라진다.</summary>
    private async UniTask StrikeOnce(EnemyBase owner, Hero target, int damageValue, CancellationToken token)
    {
        // 핸들은 반드시 지역 변수. 필드에 두면 뒤에 나온 분신이 앞 분신의 핸들을 덮어써서
        // 남의 살아있는 분신을 Despawn하고 자기 것은 풀에 못 돌려주는 누수가 난다
        // (DamageZoneSO·SummonSkillDataSO에 같은 주의가 적혀 있다).
        GameObject phantom = null;
        try
        {
            Vector3 targetPos = target.transform.position;
            // 대상 주위 무작위 방향에서 나타나 대상을 바라본다 — 여러 분신이 같은 자리에 겹치지 않게.
            Vector3 offset = Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 0f) * Vector3.forward * phantomDistance;
            Vector3 pos = targetPos + offset + Vector3.up * phantomHeightOffset;
            // 대상 쪽을 보되 높이 차는 무시한다(분신이 고개를 들거나 숙이지 않게).
            Vector3 look = targetPos - pos;
            look.y = 0f;
            Quaternion rot = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look) : Quaternion.identity;

            float lifetime = animTimeout;
            if (phantomPrefab != null)
            {
                phantom = PoolManager.Instance.Spawn(phantomPrefab, pos, rot);
                lifetime = PlayRandomState(phantom);
            }
            SpawnEffect(onSkillEffectPrefab, pos, rot);

            if (phantomHitDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(phantomHitDelay), cancellationToken: token);

            // 나타나는 사이에 시전자나 대상이 죽었으면 이 타격은 없던 일로 한다(분신은 아래 finally가 정리한다).
            if (owner != null && !owner.IsDead && target != null && !target.IsDead)
            {
                target.TakeDamage(damageValue);
                SpawnEffect(onAttackEffectPrefab, target.transform.position, rot);
                if (!string.IsNullOrEmpty(hitSoundKey)) EnemySoundManager.Play(hitSoundKey, at: target.transform.position);
            }

            // 공격 애니가 끝날 때까지만 남는다 — "한 대만 때리고 바로 사라진다".
            float rest = lifetime - phantomHitDelay;
            if (rest > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(rest), cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            // 시전 중 사망·디스폰·스턴 — 아래 finally가 분신을 반납한다.
        }
        finally
        {
            if (phantom != null) PoolManager.Instance.Despawn(phantom);
        }
    }

    /// <summary>
    /// 분신 Animator에 세 스테이트 중 하나를 골라 0프레임부터 재생하고, 그 클립 길이를 돌려준다.
    /// 길이를 못 읽으면 animTimeout — 분신이 영영 안 사라지는 것만은 막는다.
    /// </summary>
    private float PlayRandomState(GameObject phantom)
    {
        if (phantom == null) return animTimeout;

        var anim = phantom.GetComponentInChildren<Animator>();
        if (anim == null)
        {
            Debug.LogWarning($"PhantomBlitz({name}): 분신 프리팹 '{phantom.name}'에 Animator가 없다 — 공격 애니가 재생되지 않는다.", phantom);
            return animTimeout;
        }

        int pick = UnityEngine.Random.Range(0, 3);
        string state = pick == 0 ? skillAttackState : pick == 1 ? skillAttackState1 : skillAttackState2;

        // 풀에서 꺼낸 분신은 지난번 클립 중간에 멈춰 있다 — PoolManager는 SetActive만 하고 Rebind는 안 한다
        // (EnemyBase.OnEnable이 굳이 Rebind/Update(0)을 부르는 것과 같은 이유). 되감고 나서 재생한다.
        anim.Rebind();
        anim.Update(0f);

        // 트리거 대신 Play로 직접 짚는다 — 분신은 매번 새로 꺼내는 일회용이라 전이를 깔 이유가 없고,
        // 트리거가 큐에 남아 다음 분신에서 엉뚱하게 소비되는 사고도 없다.
        const int layer = 0;
        int hash = Animator.StringToHash(state);
        if (!anim.HasState(layer, hash))
        {
            Debug.LogWarning($"PhantomBlitz({name}): 분신 Animator에 '{state}' 스테이트가 없다 — " +
                             $"스테이트 이름을 SkillAttack / SkillAttack1 / SkillAttack2로 맞출 것(대소문자 구분).", phantom);
            return animTimeout;
        }
        anim.Play(hash, layer, 0f);
        // Play 직후엔 아직 이전 스테이트가 '현재'다. 0초 갱신으로 새 스테이트를 반영시켜야 길이를 읽을 수 있다.
        anim.Update(0f);

        float length = anim.GetCurrentAnimatorStateInfo(layer).length / Mathf.Max(0.01f, anim.speed);
        return length > 0f ? Mathf.Min(length, animTimeout) : animTimeout;
    }

    private void SpawnEffect(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return;
        // Spawn은 넘긴 회전으로 월드 회전을 덮어쓴다 → 프리팹에 구워둔 회전을 곱해 넘긴다.
        GameObject fx = PoolManager.Instance.SpawnBudgeted(prefab, pos, rot * prefab.transform.rotation);
        if (effectLifetime > 0f) PoolManager.Instance.Despawn(fx, effectLifetime);
    }

    /// <summary>
    /// 다음에 때릴 영웅. 섞어 둔 순번을 한 명씩 소비하고, 다 쓰면 사거리를 다시 훑어 새로 섞는다
    /// (그 사이 죽은 영웅은 빠지고 새로 배치된 영웅이 들어온다).
    /// 다시 훑어도 살아있는 대상이 없으면 null — 남은 타수는 버린다.
    /// </summary>
    private static Hero NextTarget(EnemyBase owner, int cellRange, List<Hero> targets, ref int cursor)
    {
        // 두 번만 돈다. 새로 훑은 목록에서도 못 찾으면 정말 없는 것이라 더 돌 이유가 없다(무한 루프 방지).
        for (int pass = 0; pass < 2; pass++)
        {
            if (cursor >= targets.Count)
            {
                CollectHeroes(owner, cellRange, targets);
                if (targets.Count == 0) return null;
                Shuffle(targets);
                cursor = 0;
            }
            while (cursor < targets.Count)
            {
                Hero h = targets[cursor++];
                if (h != null && !h.IsDead) return h;
            }
        }
        return null;
    }

    /// <summary>사거리 안의 살아있는 영웅들. 목록을 비우고 다시 채운다.</summary>
    private static void CollectHeroes(EnemyBase owner, int cellRange, List<Hero> into)
    {
        into.Clear();
        if (owner == null || owner.Board == null) return;

        Vector2Int origin = owner.Board.WorldToCell(owner.transform.position);
        foreach (Tile tile in owner.Board.GetTiles(origin, cellRange))
        {
            if (tile.OccupantObject == null) continue;
            if (tile.OccupantObject.GetComponentInParent<Hero>() is not Hero hero || hero.IsDead) continue;
            // 큰 영웅은 여러 칸을 함께 점유해 같은 오브젝트가 여러 번 잡힌다(FireZoneSO와 같은 이유) — 한 번만 담는다.
            if (!into.Contains(hero)) into.Add(hero);
        }
    }

    // 제자리 셔플(Fisher-Yates). 순번을 섞어 두면 "무작위로 한 대씩"이면서도 타격이 한 영웅에게 몰리지 않는다.
    private static void Shuffle(List<Hero> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

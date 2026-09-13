using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 연타(러시) 스킬. 현재 "저지 중인 대상"(적이 서 있는 칸을 점유한 근접 영웅)에게
// 좌/우 공격 애니를 번갈아 value번 타격한다. 대상이 도중에 죽으면 즉시 중단한다.
//
// ※ 데미지는 이 스킬(ApplyHit)이 코드에서 직접 준다. 따라서 LeftAttack/RightAttack 클립에는
//   "AttackHit" 애니메이션 이벤트를 넣지 말 것 — 넣으면 EnemyBase.AnimEvent_AttackHit이 기본
//   공격력으로 한 번 더 때려 스윙마다 이중 타격이 된다(코드로는 안 잡힘).
[CreateAssetMenu(menuName = "Data/Skill/RushAttackSkill")]
public class RushAttackSkillSO : AttackSkillDataSO
{
    public int value; //때리는횟수
    public int valueScale; // 5라운드마다 때리는 횟수 증가
    [Tooltip("연타 횟수가 늘수록 애니 재생속도를 올린다. 기준 횟수(value) 대비 비율에 이 값을 곱해 배속. 1이면 비례(2배 횟수→2배속), 0이면 배속 없음.")]
    public float animSpeedScale = 1f;
    public GameObject onSkillEffectPrefab;
    public GameObject onAttackEffectPrefab;
    [Tooltip("타격 이펙트 자동 반환까지의 수명(초). 이펙트 재생 길이에 맞춰 설정.")]
    public float attackEffectLifetime = 1f;
    [Tooltip("타격 이펙트 회전 보정(도). 이펙트가 반대로/옆으로 나오면 Y에 180 등으로 맞춘다.")]
    public Vector3 attackEffectEulerOffset;

    [Tooltip("애니 배속 상한(너무 빨라지는 것 방지).")]
    public float maxAnimSpeed = 8f;
    private float hitDelay = 0.1f;
    private string leftState = "LeftAttack";
    private string rightState = "RightAttack";
    private float animTimeout = 3f;
    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        if (owner == null || owner.IsDead || owner.animator == null) return;
        int attackCount = value + (valueScale*(owner.GameManager.DayCount/10));
        Hero target = FindBlockingHero(owner);
        if (target == null || target.IsDead) return;

        // 연타 횟수가 기준(value)보다 많을수록 애니를 배속 — 더 많이 때리는데 콤보가 길어지지 않고 더 격렬해 보인다.
        // 예) value=6, attackCount=12, animSpeedScale=1 → 2배속. 상한(maxAnimSpeed)으로 과속 방지.
        float animSpeed = 1f;
        if (value > 0 && animSpeedScale > 0f)
            animSpeed = Mathf.Clamp(1f + ((float)attackCount / value - 1f) * animSpeedScale, 1f, maxAnimSpeed);

        owner.transform.LookAt(target.transform);
        owner.MovementSuspended = true;
        
        try
        {
            owner.animator.SetTrigger("Skill");
            EnemySoundManager.Play("RushReady", at: owner.transform.position);
            GameObject go = PoolManager.Instance.SpawnBudgeted(onSkillEffectPrefab,owner.transform.position,Quaternion.identity);
            await WaitForAnimationEnd(owner,"Skill",animTimeout,token); // 상태 이름과 정확히 일치해야 함(대소문자 구분)
            PoolManager.Instance.Despawn(go);
            // owner.animator.speed = animSpeed; // WaitForAnimationEnd가 info.length/speed로 대기sdadqqdqddsdfsfdsaafdsadsfasdfdsf하므로 스윙도 그만큼 짧아짐
            owner.animator.speed = 6f;
            var anchors = owner.GetComponent<AttackEffectAnchors>(); // 없으면 owner 위치로 폴백
            for (int i = 0; i < attackCount; i++)
            {
                // 스윙 직전 확인 — 적 사망 or 대상(영웅) 사망 시 연타 중단.
                if (owner.IsDead || target == null || target.IsDead) break;
                string state = (i % 2 == 0) ? leftState : rightState;   // 좌/우 번갈아
                owner.animator.SetTrigger(state);
                await UniTask.Delay(TimeSpan.FromSeconds(hitDelay / animSpeed), cancellationToken: token); // 타격 딜레이도 배속에 맞춰 단축
                await WaitForAnimationEnd(owner, state, animTimeout, token); // 스윙 끝까지 대기 → 트리거 레이스 방지
                if (target == null || target.IsDead) break; // 스윙 도중 대상이 죽었으면 이 타격은 취소
                SpawnAttackEffect(owner, anchors, i); // 이번 스윙 손 위치에 타격 이펙트
                ApplyHit(owner, target);
            }
        }
        catch (OperationCanceledException)
        {

        }
        finally
        {
            if (owner != null)
            {
                if (owner.animator != null) owner.animator.speed = 1f; // 배속 원복(전역 speed라 이동/기본공격에 안 새게)
                owner.MovementSuspended = false;
            }
        }

    }

    // 이번 스윙 위치에 타격 이펙트. 앵커가 있으면 그 위치/회전, 없으면 owner 위치로 폴백.
    // 수명(attackEffectLifetime) 뒤 자동으로 풀에 반환된다.
    private void SpawnAttackEffect(EnemyBase owner, AttackEffectAnchors anchors, int swingIndex)
    {
        if (onAttackEffectPrefab == null) return;
        Transform p = anchors != null ? anchors.Get(swingIndex) : null;
        Vector3 pos = p != null ? p.position : owner.transform.position;
        // 위치는 손 앵커, 회전은 owner 정면(LookAt으로 대상을 향함) + 인스펙터 보정.
        Quaternion rot = owner.transform.rotation * Quaternion.Euler(attackEffectEulerOffset);
        GameObject fx = PoolManager.Instance.SpawnBudgeted(onAttackEffectPrefab, pos, rot);
        if (attackEffectLifetime > 0f) PoolManager.Instance.Despawn(fx, attackEffectLifetime);
    }

    // 저지 대상에게 데미지. damage(SO 필드)가 0 이하면 적의 기본 공격력을 사용.
    private void ApplyHit(EnemyBase owner, Hero target)
    {
        if (target == null || target.IsDead) return;
        // 5일마다 한 단계(+10). 괄호가 없으면 (10*DayCount)/5 = 매일 +2가 되어 계단이 생기지 않는다.
        int subDamage = 10 * (owner.GameManager.DayCount / 5);
        int dmg = damage > 0f ? Mathf.RoundToInt(damage+subDamage) : owner.AttackPower+subDamage;
        EnemySoundManager.Play("RushAttack", at: owner.transform.position);
        EnemySoundManager.Play("RushAttackHit", at: target.transform.position);
        target.TakeDamage(dmg);
    }

    // 저지 중인 대상 = 적이 서 있는 칸의 점유 영웅. 근접 저지 구조상 적은 막는 영웅의 칸으로 들어와 정지하므로
    // 그 칸의 OccupantObject가 곧 저지자다. 저지 중엔 적이 멈춰 있어 WorldToCell이 등록 칸과 일치한다.
    private static Hero FindBlockingHero(EnemyBase owner)
    {
        if (owner.Board == null || !owner.Board.IsBlocked(owner.gameObject)) return null;
        Vector2Int origin = owner.Board.WorldToCell(owner.transform.position);
        var tiles = owner.Board.GetTiles(origin, 0);
        GameObject go = tiles.Count > 0 ? tiles[0].OccupantObject : null;
        return go != null ? go.GetComponent<Hero>() : null;
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
                // 여기 왔다는 건 stateName이 애니메이터 스테이트 이름과 안 맞는다는 뜻(트리거명 오타/대소문자/전이 누락).
                // 매 스윙 timeout(초)만큼 Idle에서 멈추게 되니 이 경고가 뜨면 이름부터 확인.
                Debug.LogWarning($"RushAttackSkill: Animator에서 '{stateName}' 스테이트를 찾지 못함 — 이름/전이 확인.", owner);
                return;
            }
            await UniTask.Yield(token);
        }
        if (owner == null) return;
        var info = anim.GetCurrentAnimatorStateInfo(layer);
        float wait = info.length / Mathf.Max(0.01f, anim.speed);
        await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token);
    }
}

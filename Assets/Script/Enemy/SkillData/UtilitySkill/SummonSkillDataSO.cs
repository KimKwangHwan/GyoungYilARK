using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

[CreateAssetMenu(menuName = "Data/Skill/SummonSkill")]
public class SummonSkillDataSO : UtilitySkillDataSO
{
    public GameObject summonPrefab;   // 소환할 프리팹
    public string summonStateName = "Summon";  //애니메이션 스테이트 이름이 Summon으로 일치해야함
    private string spawnStateName = "Spawn";
    public GameObject summonEffectPrefab;
    public float animTimeout = 5f;

    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        if (summonPrefab == null)
        {
            Debug.LogWarning($"SummonSkill '{skillName}': summonPrefab 미지정");
            return;
        }

        if (owner.Board == null)
        {
            Debug.LogWarning($"SummonSkill '{skillName}': 소환자에 MapBoard가 없어 소환수가 이동할 수 없습니다.");
            return;
        }

        owner.MovementSuspended = true; // 소환 모션 동안 제자리 정지 (경로 이동이 위치를 안 덮어씀)
        try
        {
            // 소환 애니메이션 재생 → 모션이 끝날 때까지 대기.
            if (owner.animator != null)
            {
                owner.animator.SetTrigger("Summon");
                
                await WaitForAnimationEnd(owner, summonStateName, animTimeout, token);
            }

            if (owner == null || owner.IsDead) return; // 대기 중 죽었으면 소환 안 함

            int count = Mathf.Max(1, Mathf.RoundToInt(value));   // value = 소환 수
            Vector3 center = owner.transform.position;
            owner.Owner?.AddSpawnCount(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = center + new Vector3((i - (count - 1) * 0.5f) * 0.6f, 0f, 0f);
                var go = PoolManager.Instance.Spawn(summonPrefab, pos, Quaternion.identity);
                if (go.TryGetComponent(out EnemyBase enemy))
                {
                    enemy.EnterMap(owner.Board, owner.Path, snapToStart: false);
                    enemy.SetOwner(owner.Owner);
                }
                await UniTask.Delay(TimeSpan.FromSeconds(0.05f));
                // TryGetComponent가 실패하면 enemy는 null이다 → 안에서 바로 NRE가 난다.
                if (enemy != null) WaitForSpawnAnimationEnd(enemy,spawnStateName,5,token).Forget();
            }
        }
        finally
        {
            if (owner != null) owner.MovementSuspended = false; // 모션 끝 → 다시 이동
        }
    }

    // 트리거로 진입한 소환 스테이트가 끝날 때까지 대기.
    // 스테이트를 못 찾으면 timeout 초 후 탈출해 적이 영영 멈추는 것을 방지.
    private static async UniTask WaitForAnimationEnd(EnemyBase owner, string stateName, float timeout, CancellationToken token)
    {
        var anim = owner.animator;
        const int layer = 0;

        // 1) 트리거 → 소환 스테이트로 실제 전이될 때까지(전이 프레임) 대기.
        float elapsed = 0f;
        while (owner != null && !anim.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
        {
            if (owner.IsDead) return;
            elapsed += Time.deltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"SummonSkill: Animator에서 '{stateName}' 스테이트를 찾지 못함 — 이름/전이 확인.", owner);
                return;
            }
            await UniTask.Yield(token); // 파괴/비활성 시 취소돼 파괴된 오브젝트 접근 방지
        }
        if (owner == null) return;

        // 2) 진입 시점의 클립 길이만큼 대기(애니메이터 speed 반영).
        var info = anim.GetCurrentAnimatorStateInfo(layer);
        float wait = info.length / Mathf.Max(0.01f, anim.speed);
        await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token);
    }
    // static이 아니다 — summonEffectPrefab(인스턴스 필드)을 쓰기 때문. static으로 두면 컴파일되지 않는다.
    // 이펙트 핸들은 지역 변수로 둔다. 필드로 두면 소환 수가 2 이상일 때 두 번째가 첫 번째를 덮어써
    // 첫 이펙트가 회수되지 않고, ScriptableObject라 같은 스킬을 쓰는 모든 적이 한 칸을 공유해버린다.
    private async UniTask WaitForSpawnAnimationEnd(EnemyBase owner, string stateName, float timeout, CancellationToken token)
    {
        GameObject fx = null;
        owner.MovementSuspended = true;
        owner.IsSpawnInvincible = true;
        owner.animator.SetTrigger("IsSpawn");
        if (summonEffectPrefab != null)
        {
            // Spawn은 넘긴 회전으로 월드 회전을 덮어쓴다 → 프리팹에 구운 회전을 그대로 넘긴다.
            fx = PoolManager.Instance.SpawnBudgeted(summonEffectPrefab, owner.transform.position,
                                           summonEffectPrefab.transform.rotation);
            FitEffectToRange(fx,owner,1);
        }
        owner.Board.RemoveEnemy(owner.gameObject);
        var anim = owner.animator;
        const int layer = 0;
        try
        {
            float elapsed = 0f;
            while (owner != null && !anim.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
            {
                if (owner.IsDead) return;
                elapsed += Time.deltaTime;
                if (elapsed >= timeout)
                {
                    Debug.LogWarning($"SummonSkill: Animator에서 '{stateName}' 스테이트를 찾지 못함 — 이름/전이 확인.", owner);
                    return;
                }
                await UniTask.Yield(token);
            }
            if (owner == null) return;

            var info = anim.GetCurrentAnimatorStateInfo(layer);
            float wait = info.length / Mathf.Max(0.01f, anim.speed);
            await UniTask.Delay(TimeSpan.FromSeconds(wait), cancellationToken: token);
        }
        finally
        {
            // 중간에 빠져나가도(타임아웃·취소) 이펙트는 반납하고 정지·무적을 풀어야 한다.
            // 안 그러면 소환수가 영영 멈춘 채 무적으로 남고 이펙트도 새어나간다.
            if (fx != null) PoolManager.Instance.Despawn(fx);
            if (owner != null && !owner.IsDead)
            {
                owner.MovementSuspended = false;
                owner.IsSpawnInvincible = false;
                // 애니 끝 → 현재 칸에 다시 등록 → 타겟 대상 복귀
                owner.Board.MoveEnemy(owner.gameObject, owner.transform.position);
            }
        }
    }
    private void FitEffectToRange(GameObject effect, EnemyBase owner, int cellRange)
    {
        if (effect == null || 0.3 <= 0f) return;   // 0이면 프리팹 원래 크기 유지

        float factor = 0.3f * cellRange;
        // 프리팹이 비균일 스케일로 만들어졌을 수 있으니 비율을 유지한 채 곱한다.
        effect.transform.localScale = summonEffectPrefab.transform.localScale * factor;
    }
}

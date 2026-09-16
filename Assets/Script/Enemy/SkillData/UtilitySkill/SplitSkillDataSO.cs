using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

// 분열 스킬 — 유닛이 죽을 때(TriggerOnDeath) 자신과 같은 적을 value마리 스폰한다.
// 분열체는 최대 체력의 hpPercent 비율로 시작하며, maxGeneration으로 무한 분열을 막는다.
public class SplitSkillDataSO : UtilitySkillDataSO
{
    [Range(0f, 1f)]
    [Tooltip("분열체가 가질 체력 비율(최대 체력 대비). 0.5 = 50%.")]
    public float hpPercent = 0.5f;
    public GameObject splitEffect;
    [Tooltip("분열 최대 세대. 1이면 원본만 분열하고 분열체는 다시 분열하지 않음(무한 방지).")]
    public int maxGeneration = 1;
    public string stateName = "Spawn";

    public override bool TriggerOnDeath => true; // 쿨다운이 아니라 죽을 때 발동

    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        if (owner == null) return;
        if (owner.SplitGeneration >= maxGeneration) return; // 분열체는 더 이상 분열하지 않음

        int count = Mathf.Max(1, Mathf.RoundToInt(value)); // value = 분열 수
        int nextGen = owner.SplitGeneration + 1;
        float childHp = owner.MaxHp * hpPercent;
        if(splitEffect!=null)
        {
            GameObject go = PoolManager.Instance.SpawnBudgeted(splitEffect,owner.transform.position,Quaternion.identity);
            PoolManager.Instance.Despawn(go,1f);
        }
        // 분열체를 소유 레인 카운트에 미리 더한다(각자 죽을 때 EnemyDieEvent 감소와 상쇄 → 전멸 시 정확히 0).
        owner.Owner?.AddSpawnCount(count);

        Vector3 center = owner.transform.position;
        var board = owner.Board;
        var path = owner.Path;
        var prefab = owner.TryGetComponent(out PooledObject po) ? po.SourcePrefab : owner.gameObject;
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = center + new Vector3((i - (count - 1) * 0.5f) * 0.6f, 0f, 0f);
            var go = PoolManager.Instance.Spawn(prefab, pos, Quaternion.identity);
            if (go.TryGetComponent(out EnemyBase enemy))
            {
                enemy.SetSplitGeneration(nextGen);            // 세대 부여(무한 분열 차단)
                enemy.SetOwner(owner.Owner);                  // 부모와 같은 레인 소속 → 이 분열체 죽을 때 같은 카운트 감소
                enemy.EnterMap(board, path, snapToStart: false);
                enemy.SetCurrentHp(childHp);                  // OnEnable 풀피 리셋을 현재 체력으로 덮어씀
                enemy.SetScaleMul(0.6f);                      // 분열체는 0.6 크기(인스턴스에만 적용, 풀 재사용 시 원복)

                // if 블록 안에서 부른다 — 밖에 두면 EnemyBase가 없는 프리팹을 스폰했을 때
                // enemy가 null인 채로 넘어가 WaitForAnimationEnd 첫 줄에서 NullReference가 난다.
                WaitForAnimationEnd(enemy, stateName, 5, token).Forget();
            }
        }
        
        await UniTask.CompletedTask;
    }
    private static async UniTask WaitForAnimationEnd(EnemyBase owner, string stateName, float timeout, CancellationToken token)
    {
        owner.MovementSuspended = true;
        owner.IsSpawnInvincible = true;
        owner.animator.SetTrigger("IsSpawn");
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
                    Debug.LogWarning($"SplitSkill: Animator에서 '{stateName}' 스테이트를 찾지 못함 — 이름/전이 확인.", owner);
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
            // 중간에 빠져나가도(타임아웃·취소·스턴으로 애니가 멈춤) 정지·무적을 반드시 풀어야 한다.
            // 안 그러면 분열체가 영영 멈춘 채 무적으로 남고, 보드에도 등록되지 않아 때릴 수조차 없다.
            // (SummonSkillDataSO.WaitForSpawnAnimationEnd와 같은 구조 — 죽은 개체는 보드에 되돌리지 않는다.)
            if (owner != null && !owner.IsDead)
            {
                owner.MovementSuspended = false;
                owner.IsSpawnInvincible = false;
                owner.Board.MoveEnemy(owner.gameObject, owner.transform.position); // 애니 끝 → 현재 칸에 다시 등록 → 타겟 대상 복귀
            }
        }
    }
}

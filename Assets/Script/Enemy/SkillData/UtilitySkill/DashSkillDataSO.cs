using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

[CreateAssetMenu(menuName = "Data/Skill/DashSkill")]
public class DashSkillDataSO : UtilitySkillDataSO
{
    public GameObject dashEffect;
    public float distance = 20f;   // 경로를 따라 앞으로 이동할 거리(월드). 칸 단위로 쓰려면 board.CellSize를 곱해 넘길 것.

    public override bool MovesSelf => true;   // 속박 중 시전 불가, 대시 중 속박되면 그 자리에서 중단
    public override async UniTask Execute(EnemyBase owner, CancellationToken token)
    {
        if (owner == null || !owner.HasPath || owner.Board == null) return;

        Vector3 start = owner.transform.position;
        Vector3 end   = owner.PointAhead(owner.Board.CellSize*distance, out int landIndex); // 경로 기준 앞선 지점

        // 시작점 → 경로 코너들(PathIndex..landIndex-1) → 착지점(end) 순으로 지나갈 지점들.
        // 직선 Lerp로 질러가면 꺾이는 구간에서 경로를 벗어나 코너의 저지 영웅을 통과해버리므로,
        // 일반 이동(EnemyMovement.Tick)과 동일하게 웨이포인트를 하나씩 밟으며 이동한다.
        var points = new List<Vector3>();
        for (int i = owner.PathIndex; i < landIndex && i < owner.Path.Count; i++) points.Add(owner.Path[i]);
        points.Add(end);

        // 대시 총 이동거리(경로 길이) — duration 동안 이 거리를 주파하도록 속도를 맞춘다.
        float total = 0f;
        Vector3 prev = start;
        foreach (Vector3 pt in points) { total += Vector3.Distance(prev, pt); prev = pt; }
        float moveSpeed = duration > 0f ? total / duration : total; // 월드거리/초

        owner.MovementSuspended = true; // 대시 동안 일반 경로 이동이 위치를 덮어쓰지 않게 정지
        float prevSpeed = owner.animator != null ? owner.animator.speed : 1f; // 대시 후 원래 속도로 복원(슬로우/헤이스트 등 보존)
        // 대시 이펙트: owner에 붙여 대시 내내 따라오게. 위치·회전은 스폰 시점 owner 기준(로컬 변수 — SO 필드에 담으면 여러 적이 공유돼 오염됨).
        Vector3 fxPos = owner.transform.position; // 앞쪽에 두려면 + owner.transform.forward * offset
        GameObject go = PoolManager.Instance.SpawnBudgeted(dashEffect, fxPos, owner.transform.rotation, owner.transform);
        try
        {
            if (owner.animator != null) owner.animator.speed = 4f;
            bool stopped = false;   // 저지 또는 속박/기절로 중도 정지 — 착지 스냅을 건너뛰는 표시
            Vector3 cur = start;
            int seg = 0;
            // total<=0(이미 경로 끝 등)이면 이동할 것이 없어 루프를 건너뛴다(무한루프 방지).
            if(!owner.Board.IsBlocked(owner.gameObject))
            EnemySoundManager.Play("DashSkill", at: owner.transform.position);
            while (moveSpeed > 0f && seg < points.Count && owner != null && !owner.IsDead)
            {
                if (owner.Board.IsBlocked(owner.gameObject)) { stopped = true; break; }
                // 저지당하면(대시 시작 시 이미 저지 or 대시 중 적을 만남) 그 자리에서 대시 중단.

                // 대시 도중 속박/기절이 걸리면 같은 처리로 그 자리에 멈춘다.
                // 착지 지점으로 스냅하지 않고 ResumeFromNearest로 현재 위치에서 경로를 이어받는다.
                if (owner.CannotMove) { stopped = true; break; }

                // 이번 프레임 이동량을 코너를 넘어가며 소진 — 코너에서 속도가 꺾이지 않게.
                float budget = moveSpeed * Time.deltaTime;
                while (budget > 0f && seg < points.Count)
                {
                    Vector3 tgt = points[seg];
                    float d = Vector3.Distance(cur, tgt);
                    if (d <= budget) { cur = tgt; budget -= d; seg++; }
                    else { cur = Vector3.MoveTowards(cur, tgt, budget); budget = 0f; }
                }

                owner.transform.position = cur;
                owner.Board.MoveEnemy(owner.gameObject, cur); // 칸 보고(여기서 저지 상태가 갱신됨)
                await UniTask.Yield(token); // 파괴/비활성 시 취소돼 파괴된 오브젝트 접근 방지
            }
            
            if (owner != null && !owner.IsDead)
            {
                if (!stopped)
                {
                    owner.transform.position = end;                 // 완주했을 때만 착지 지점으로 스냅
                    if (landIndex >= 0) owner.ResumeFrom(landIndex); // 정상 이동을 착지 지점부터 이어받기
                }
                else
                {
                    owner.ResumeFromNearest(); // 저지·속박으로 멈춤 — 현재 위치에서 경로 이어가기(멈춘 자리 유지)
                }
            }
        }
        finally
        {
            if (owner != null)
            {
                owner.MovementSuspended = false;
                if (owner.animator != null) owner.animator.speed = prevSpeed;
            }
            PoolManager.Instance.Despawn(go,0.3f);
        }
    }
}

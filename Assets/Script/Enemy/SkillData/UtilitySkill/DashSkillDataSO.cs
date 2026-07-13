using UnityEngine;
using Cysharp.Threading.Tasks;

[CreateAssetMenu(menuName = "Data/Skill/DashSkill")]
public class DashSkillDataSO : UtilitySkillDataSO
{
    public float distance = 10f;   // 경로를 따라 앞으로 이동할 거리(월드). 칸 단위로 쓰려면 board.CellSize를 곱해 넘길 것.

    public override async UniTask Execute(EnemyBase owner)
    {
        if (owner == null || !owner.HasPath || owner.Board == null) return;

        Vector3 start = owner.transform.position;
        Vector3 end   = owner.PointAhead(distance, out int landIndex); // 경로 기준 앞선 지점

        owner.MovementSuspended = true; // 대시 동안 일반 경로 이동이 위치를 덮어쓰지 않게 정지
        try
        {
            float speed = duration > 0f ? 1f / duration : 1f;
            for (float t = 0f; t < 1f && !owner.IsDie; )
            {
                t += Time.deltaTime * speed;
                owner.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t));
                owner.Board.MoveEnemy(owner.gameObject, owner.transform.position); // 칸 보고
                await UniTask.Yield();
            }

            if (!owner.IsDie)
            {
                owner.transform.position = end;
                if (landIndex >= 0) owner.ResumeFrom(landIndex); // 정상 이동을 착지 지점부터 이어받기
            }
        }
        finally
        {
            if (owner != null) owner.MovementSuspended = false;
        }
    }
}

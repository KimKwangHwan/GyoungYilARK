using UnityEngine;

// 공격범위(마름모) 테스트용 컴포넌트.
//  1) 씬뷰에서 이 오브젝트를 선택하면 range 만큼 마름모가 그려짐 → 범위 판정 눈으로 확인
//  2) 인스펙터 우클릭 → "Test FindNearest" → 범위 내 최근접 적을 콘솔에 로그 (플레이 중)
// 실제 로직과 동일한 EnemyTargeting/EnemyGridService 를 쓰므로, 이게 맞으면 공격 판정도 맞음.
public class AttackRangeTester : MonoBehaviour
{
    [SerializeField] private int range = 2;

    private void OnDrawGizmosSelected()
    {
        var origin = EnemyGridService.WorldToCell(transform.position);
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);

        for (int dx = -range; dx <= range; dx++)
        for (int dy = -range; dy <= range; dy++)
        {
            var cell = origin + new Vector2Int(dx, dy);
            if (!EnemyTargeting.InRange(origin, cell, range)) continue;   // 마름모만 통과
            Gizmos.DrawCube(EnemyGridService.CellToWorld(cell), Vector3.one * 0.9f);
        }
    }

    [ContextMenu("Test FindNearest")]
    private void TestFindNearest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("AttackRangeTester: 플레이 중에 실행하세요 (EnemyRegistry는 런타임에 채워짐).");
            return;
        }

        var target = EnemyTargeting.FindNearest(
            transform.position, range, EnemyRegistry.Alive, e => e.transform.position);

        Debug.Log(target != null
            ? $"AttackRangeTester: 범위({range}칸) 내 최근접 = {target.name}"
            : $"AttackRangeTester: 범위({range}칸) 내 대상 없음");
    }
}

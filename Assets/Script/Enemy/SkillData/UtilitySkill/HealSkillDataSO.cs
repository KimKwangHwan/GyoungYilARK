using Cysharp.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Skill/HealSkill")]
public class HealSkillDataSO : UtilitySkillDataSO
{
    public float valueScale;   // 스테이지당 힐량 증가

    // Cooldown 을 틱 간격으로 사용한다(예: 0.5초). EnemyBase 스킬 루프가 쿨다운마다 호출 = 매 틱 1회 힐.
    public override async UniTask Execute(EnemyBase owner)
    {
        float heal = value + valueScale * (CurrentStage() - 1);   // value = 기본 힐량
        if (heal <= 0f) return;

        float sqrRange = range * range;
        Vector3 center = owner.transform.position;

        foreach (var ally in EnemyRegistry.Alive)
        {
            if (ally == null || ally.IsDie) continue;
            //지금은 월드 거리 임시.
            if ((ally.transform.position - center).sqrMagnitude > sqrRange) continue;
            ally.Heal(heal);
        }
        await UniTask.CompletedTask;
    }

    // TODO: 스테이지 시스템 생기면 현재 스테이지 반환하도록 연결 (EnemyTable.UpHealthScale 도 여기 연동 예정)
    private static int CurrentStage() => 1;
}

using UnityEngine;
using Cysharp.Threading.Tasks;

[CreateAssetMenu(menuName = "Data/Skill/SummonSkill")]
public class SummonSkillDataSO : UtilitySkillDataSO
{
    public GameObject summonPrefab;   // 소환할 프리팹

    public override async UniTask Execute(EnemyBase owner)
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

        int count = Mathf.Max(1, Mathf.RoundToInt(value));   // value = 소환 수
        Vector3 center = owner.transform.position;
        for (int i = 0; i < count; i++)
        {
            // 겹치지 않게 여왕 주변에 살짝 흩뿌린다.
            Vector3 pos = center + new Vector3((i - (count - 1) * 0.5f) * 0.6f, 0f, 0f);
            var go = Object.Instantiate(summonPrefab, pos, Quaternion.identity);

            // 소환자(owner)의 board·경로를 물려주고, 스폰으로 스냅하지 않고 지금 자리에서 본진으로 향한다.
            if (go.TryGetComponent(out EnemyBase enemy))
                enemy.EnterMap(owner.Board, owner.Path, snapToStart: false);
        }
        await UniTask.CompletedTask;
    }
}

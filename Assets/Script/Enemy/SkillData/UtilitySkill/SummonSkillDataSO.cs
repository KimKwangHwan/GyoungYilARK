using UnityEngine;
using Cysharp.Threading.Tasks;

[CreateAssetMenu(menuName = "Data/Skill/SummonSkill")]
public class SummonSkillDataSO : UtilitySkillDataSO
{
    public GameObject summonPrefab;   // 소환할 프리팹
    public float spawnRadius = 2f;    // owner 주변 스폰 반경

    public override async UniTask Execute(EnemyBase owner)
    {
        if (summonPrefab == null)
        {
            Debug.LogWarning($"SummonSkill '{skillName}': summonPrefab 미지정");
            return;
        }

        int count = Mathf.Max(1, Mathf.RoundToInt(value));   // value = 소환 수
        Vector3 center = owner.transform.position;
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 pos = center + new Vector3(offset.x, offset.y, 0f);
            Object.Instantiate(summonPrefab, pos, Quaternion.identity);
        }
        await UniTask.CompletedTask;
    }
}

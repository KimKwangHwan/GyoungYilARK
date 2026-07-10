using UnityEngine;
using Cysharp.Threading.Tasks;

[CreateAssetMenu(menuName = "Data/Skill/DashSkill")]
public class DashSkillDataSO : UtilitySkillDataSO
{
    public float distance = 10f;   // 이동 거리

    public override async UniTask Execute(EnemyBase owner)
    {
        var tf = owner.transform;
        Vector3 start = tf.position;
        Vector3 end = start + new Vector3(distance, 0f, 0f);

        // duration 초 동안 등속으로 이동
        float move = duration > 0f ? 1f / duration : 1f;
        float t = 0f;
        while (t < 1f && !owner.IsDie)
        {
            t += Time.deltaTime * move;
            tf.position = Vector3.Lerp(start, end, Mathf.Clamp01(t));
            await UniTask.Yield();
        }
        if (!owner.IsDie) tf.position = end;
    }
}

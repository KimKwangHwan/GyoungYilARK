using System.Collections.Generic;
using UnityEngine;

public class EnemyScaleTable : DataTable
{
    public class Data
    {
        public int Unlocked { get; set; }
        public float NormalHp { get; set; }
        public float NormalAttack { get; set; }
        public float NormalDefense { get; set; }
        public float BossHp { get; set; }
        public float BossAttack { get; set; }
        public float BossDefense { get; set; }
    }

    private readonly Dictionary<int, Data> table = new();
    private int maxUnlocked = -1;

    public override void Load(string filename)
    {
        table.Clear();
        maxUnlocked = -1;

        var path = $"DataTable/{filename}";
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            //Debug.LogError($"EnemyScaleTable: '{path}' 로드 실패 — 지역 배율이 전부 1배로 동작한다.");
            return;
        }

        List<Data> list;
        try
        {
            list = LoadCsv<Data>(textAsset.text);
        }
        catch (System.Exception e)
        {
            //Debug.LogError($"EnemyScaleTable: '{path}' 파싱 실패 — {e.Message}");
            return;
        }

        foreach (var data in list)
        {
            if (table.ContainsKey(data.Unlocked))
            {
                //Debug.LogWarning($"EnemyScaleTable 키 중복 '{data.Unlocked}'");
                continue;
            }
            table.Add(data.Unlocked, data);
            if (data.Unlocked > maxUnlocked) maxUnlocked = data.Unlocked;
        }

        // 행이 중간에 비면 그 해금 수에서만 배율이 조용히 1배로 떨어진다 — 밸런스가 눈치 못 채게 무너지는
        // 유일한 경로라 로드할 때 잡아 둔다(0부터 최대까지 빠짐없이 있어야 한다).
        for (int i = 0; i <= maxUnlocked; i++)
            if (!table.ContainsKey(i)){
                //Debug.LogError($"EnemyScaleTable: Unlocked={i} 행이 없다 — 지역 {i}개 해금 상태에서 배율이 1배가 된다.");
            }
    }

    public IReadOnlyDictionary<int, Data> GetAll() => table;
    public Data Get(int unlocked)
    {
        if (maxUnlocked < 0) return null;
        int key = Mathf.Clamp(unlocked, 0, maxUnlocked);
        return table.TryGetValue(key, out var data) ? data : null;
    }
}

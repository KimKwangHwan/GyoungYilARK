using UnityEngine;
using System.Collections.Generic;

public class SkillTable : DataTable
{
    public class Data
    {
        public string SkillId { get; set; }
        public string Category { get; set; } 
        public string Type { get; set; }
        public string NameKey { get; set; }
        public float Cooldown { get; set; }
        public float Duration { get; set; }
        public float Range { get; set; }
        public float? Damage { get; set; }
        public float? TickInterval { get; set; }
        public float? Value { get; set; }
        public float? ValueScale { get; set; }   // 스테이지당 Value 증가량 (힐 등)
        public float? Distance { get; set; }
        public string Desc{get ; set ;}
    }

    private readonly Dictionary<string, Data> table = new();

    public override void Load(string filename)
    {
        table.Clear();

        var path = $"DataTable/{filename}";
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogWarning($"SkillTable: '{path}' 로드 실패");
            return;
        }
        var list = LoadCsv<Data>(textAsset.text);
        foreach (var data in list)
        {
            if (string.IsNullOrEmpty(data.SkillId)) continue;
            if (!table.ContainsKey(data.SkillId)) table.Add(data.SkillId, data);
            else Debug.LogWarning($"SkillTable 키 중복 '{data.SkillId}'");
        }
    }

    public Data Get(string key)
    {
        if (string.IsNullOrEmpty(key) || !table.ContainsKey(key)) return null;
        return table[key];
    }

    public IReadOnlyDictionary<string, Data> GetAll()
    {
        return table;
    }
}

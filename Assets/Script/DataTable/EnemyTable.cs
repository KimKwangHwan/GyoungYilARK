using UnityEngine;
using CsvHelper;
using System.Collections.Generic;
public class EnemyTable : DataTable
{
    public class Data
    {
        public string Name {get ; set ;}
        public int Attack{get ; set ;}
        public float AttackSpeed{get ; set ;}
        public int Range {get ; set ; }
        public int Defense {get ; set ; }
        public int Health {get ; set ; }
        public float MoveSpeed {get ; set ;}
        public int UpHealthScale {get ; set ;}
        public int UpDefenseScale {get ; set ;}
        public string Skills {get ; set ;}   // 세미콜론(;)으로 구분된 SkillId 목록
        public string Type {get ; set ;}     // EnemyType 이름 (Normal, Elite, Boss)
    }
    
    private readonly Dictionary<string, Data> table = new();

    public override void Load(string filename)
    {
        table.Clear();

        var path = $"DataTable/{filename}";
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if(textAsset == null)
        {
            Debug.LogWarning($"EnemyTable: '{path}' 로드 실패");
            return;
        }
        var list = LoadCsv<Data>(textAsset.text);
        foreach (var data in list)
        {
            if (string.IsNullOrEmpty(data.Name)) continue;
            if (!table.ContainsKey(data.Name)) table.Add(data.Name, data);
            else Debug.LogWarning($"EnemyTalbe 키 중복 '{data.Name}'");
        }
    }
    public Data Get(string key)
    {
        if (string.IsNullOrEmpty(key) || !table.ContainsKey(key)) return null;
        return table[key];
    }

}

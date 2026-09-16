using System.Collections.Generic;
using UnityEngine;
public class EnemyScaleConfigTable : DataTable
{
    public class Data
    {
        public string Key { get; set; }
        public float Value { get; set; }
    }

    private readonly Dictionary<string, float> table = new();

    public override void Load(string filename)
    {
        table.Clear();

        var path = $"DataTable/{filename}";
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            //Debug.LogError($"EnemyScaleConfigTable: '{path}' 로드 실패 — 보스 추가 성장이 멈춘다.");
            return;
        }

        List<Data> list;
        try
        {
            list = LoadCsv<Data>(textAsset.text);
        }
        catch (System.Exception e)
        {
            //Debug.LogError($"EnemyScaleConfigTable: '{path}' 파싱 실패 — {e.Message}");
            return;
        }

        foreach (var data in list)
        {
            if (string.IsNullOrWhiteSpace(data.Key)) continue;
            var key = data.Key.Trim();
            if (table.ContainsKey(key))
            {
                //Debug.LogWarning($"EnemyScaleConfigTable 키 중복 '{key}'");
                continue;
            }
            table.Add(key, data.Value);
        }
    }

    public IReadOnlyDictionary<string, float> GetAll() => table;

    public float Get(string key, float fallback = 0f)
        => key != null && table.TryGetValue(key, out var value) ? value : fallback;

    public int GetInt(string key, int fallback = 0)
        => key != null && table.TryGetValue(key, out var value) ? Mathf.RoundToInt(value) : fallback;
}

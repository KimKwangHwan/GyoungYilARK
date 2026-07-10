using System.Collections.Generic;
using UnityEngine;

public static class DataTableManager
{
    private static readonly Dictionary<string, DataTable> tables = new Dictionary<string, DataTable>();

    static DataTableManager()
    {
        Init();
    }
    private static void Init()
    {
        var stringTable = new StringTable();
        stringTable.Load(DataTableIds.String);
        tables.Add(DataTableIds.String, stringTable);
        var enemyTable = new EnemyTable();
        enemyTable.Load(DataTableIds.Enemy);
        tables.Add(DataTableIds.Enemy,enemyTable);
        var waveTable = new WaveTable();
        waveTable.Load(DataTableIds.Wave);
        tables.Add(DataTableIds.Wave, waveTable);
        var skillTable = new SkillTable();
        skillTable.Load(DataTableIds.Skill);
        tables.Add(DataTableIds.Skill, skillTable);
    }
    public static T Get<T>(string id) where T : DataTable
    {
        if (!tables.ContainsKey(id))
        {
            Debug.Log($"테이블 없음: {id}");
            return null;
        }
        return tables[id] as T;
    }
}

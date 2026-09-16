using System.Collections.Generic;
using UnityEngine;

public static class DataTableManager
{
    private static readonly Dictionary<string, DataTable> tables = new Dictionary<string, DataTable>();
    public static StringTable StringTable => Get<StringTable>(DataTableIds.String);
    public static EnemyTable EnemyTable => Get<EnemyTable>(DataTableIds.Enemy);
    public static WaveTable WaveTable => Get<WaveTable>(DataTableIds.Wave);
    public static SkillTable SkillTable => Get<SkillTable>(DataTableIds.Skill);
    public static PortalTable PortalTable => Get<PortalTable>(DataTableIds.Portal);
    public static DebuffTable DebuffTable => Get<DebuffTable>(DataTableIds.Debuff);
    public static EnemyScaleTable EnemyScaleTable => Get<EnemyScaleTable>(DataTableIds.EnemyScale);
    public static EnemyScaleConfigTable EnemyScaleConfigTable => Get<EnemyScaleConfigTable>(DataTableIds.EnemyScaleConfig);
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
        var portalTable = new PortalTable();
        portalTable.Load(DataTableIds.Portal);
        tables.Add(DataTableIds.Portal, portalTable);
        // 런타임은 보통 임포터가 만든 SO만 보므로 이 표를 직접 읽을 일은 없다.
        // 그래도 등록해 두는 이유 — DataTableIds에 항목이 있는데 여기 없으면 Get이 "테이블 없음"만 남기고 null을 준다.
        var debuffTable = new DebuffTable();
        debuffTable.Load(DataTableIds.Debuff);
        tables.Add(DataTableIds.Debuff, debuffTable);
        // 적 스탯 배율 — EnemyStatScaling이 매번 읽는다(예전엔 그 파일에 하드코딩돼 있던 표).
        var enemyScaleTable = new EnemyScaleTable();
        enemyScaleTable.Load(DataTableIds.EnemyScale);
        tables.Add(DataTableIds.EnemyScale, enemyScaleTable);
        var enemyScaleConfigTable = new EnemyScaleConfigTable();
        enemyScaleConfigTable.Load(DataTableIds.EnemyScaleConfig);
        tables.Add(DataTableIds.EnemyScaleConfig, enemyScaleConfigTable);
    }
    public static T Get<T>(string id) where T : DataTable
    {
        if (!tables.ContainsKey(id))
        {
            // Debug.Log($"테이블 없음: {id}");
            return null;
        }
        return tables[id] as T;
    }
}

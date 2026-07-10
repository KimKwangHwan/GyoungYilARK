#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;


public static class SkillTableImporter
{
    private const string OutputFolder = "Assets/Resources/Skills";

    [MenuItem("Tools/Skill/Import SkillTable")]
    public static void Import()
    {
        var table = new SkillTable();
        table.Load(DataTableIds.Skill);
        var all = table.GetAll();
        if (all == null || all.Count == 0)
        {
            Debug.LogWarning("SkillTableImporter: SkillTable 데이터가 없습니다.");
            return;
        }

        EnsureFolder();

        int created = 0, updated = 0, skipped = 0;
        foreach (var kv in all)
        {
            var data = kv.Value;
            var soType = ResolveType(data);
            if (soType == null)
            {
                Debug.LogWarning($"SkillTableImporter: '{data.SkillId}' 처리 불가 (Category='{data.Category}', Type='{data.Type}')");
                skipped++;
                continue;
            }

            string path = $"{OutputFolder}/{data.SkillId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<SkillDataSO>(path);

            if (existing != null && existing.GetType() != soType)
            {
                AssetDatabase.DeleteAsset(path);
                existing = null;
            }

            bool isNew = existing == null;
            var so = isNew ? (SkillDataSO)ScriptableObject.CreateInstance(soType) : existing;

            ApplyFields(so, data);

            if (isNew)
            {
                AssetDatabase.CreateAsset(so, path);
                created++;
            }
            else
            {
                EditorUtility.SetDirty(so);
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"SkillTableImporter 완료 → 생성 {created}, 갱신 {updated}, 건너뜀 {skipped}");
    }

    private static Type ResolveType(SkillTable.Data d)
    {
        var category = (d.Category ?? "").Trim();
        var type = (d.Type ?? "").Trim();

        if (category == "Attack") return typeof(AttackSkillDataSO);
        if (category == "Utility")
        {
            switch (type)
            {
                case "Dash": return typeof(DashSkillDataSO);
                case "Summon": return typeof(SummonSkillDataSO);
                case "Heal": return typeof(HealSkillDataSO); // 나중에 스킬 추가
            }
        }
        return null;
    }
    
    private static void ApplyFields(SkillDataSO so, SkillTable.Data d)
    {
        so.skillName = d.NameKey;
        so.cooldown = d.Cooldown;
        so.duration = d.Duration;
        so.range = d.Range;

        switch (so)
        {
            case AttackSkillDataSO attack:
                attack.damage = d.Damage ?? 0f;
                attack.tickInterval = d.TickInterval ?? 0f;
                break;

            case DashSkillDataSO dash:
                dash.value = d.Value ?? 0f;
                dash.tickInterval = d.TickInterval ?? 0f;
                dash.distance = d.Distance ?? 0f;
                break;

            case SummonSkillDataSO summon:
                summon.value = d.Value ?? 0f;          // value = 소환 수
                summon.tickInterval = d.TickInterval ?? 0f;
                break;

            case HealSkillDataSO heal:
                heal.value = d.Value ?? 0f;            // value = 틱당 기본 힐량
                heal.valueScale = d.ValueScale ?? 0f;  // 스테이지당 증가
                heal.tickInterval = d.TickInterval ?? 0f;
                break;
        }
    }

    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(OutputFolder)) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        AssetDatabase.CreateFolder("Assets/Resources", "Skills");
    }
}
#endif

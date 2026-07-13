using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildingPrefabRegistry", menuName = "Config/Building Prefab Registry")]
public class BuildingPrefabRegistry : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public ProductionType type;
        public ProductionFacility prefab;
    }

    [SerializeField] private List<Entry> entries;

    private Dictionary<ProductionType, ProductionFacility> _map;

    public Dictionary<ProductionType, ProductionFacility> Prefabs
    {
        get
        {
            if (_map == null)
            {
                _map = new Dictionary<ProductionType, ProductionFacility>();
                foreach (var e in entries)
                    _map[e.type] = e.prefab;
            }
            return _map;
        }
    }
}
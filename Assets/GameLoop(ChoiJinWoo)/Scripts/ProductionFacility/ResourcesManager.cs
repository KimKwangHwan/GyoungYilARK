using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourcesManager : MonoBehaviour
{
    [SerializeField] private int wood = 500;
    [SerializeField] private int food = 500;
    [SerializeField] private int gold = 500;
    [SerializeField] private int iron = 500;
    [SerializeField] private int stone = 500;

    public event Action<int, int, int, int, int> ProductUpdate;

    private void Start()
    {
        ProductUpdate?.Invoke(wood, stone, iron, gold, food);
    }

    public void ProductChanged(Dictionary<ProductionType, int> products)
    {
        Debug.Log("자원 변경");
        if(products.Count == 0)
        {
            Debug.LogWarning("자원 소모 없음");
        }
        foreach(var prod in products)
        {
            Debug.Log($"{prod.Key} : {prod.Value}");
        }

        if(products.ContainsKey(ProductionType.Wood))
            wood += products[ProductionType.Wood];
        if (products.ContainsKey(ProductionType.Food))
            food += products[ProductionType.Food];
        if (products.ContainsKey(ProductionType.Gold))
            gold += products[ProductionType.Gold];
        if (products.ContainsKey(ProductionType.Iron))
            iron += products[ProductionType.Iron];
        if (products.ContainsKey(ProductionType.Stone))
            stone += products[ProductionType.Stone];

        ProductUpdate?.Invoke(wood, stone, iron, gold, food);
    }

    public bool CheckResources(Dictionary<ProductionType, int> resources)
    {
        if (resources.ContainsKey(ProductionType.Wood))
        {
            if (-resources[ProductionType.Wood] > wood)
                return false;
        }
        if (resources.ContainsKey(ProductionType.Stone))
        {
            if (-resources[ProductionType.Stone] > stone)
                return false;
        }
        if (resources.ContainsKey(ProductionType.Gold))
        {
            if (-resources[ProductionType.Gold] > gold)
                return false;
        }
        if (resources.ContainsKey(ProductionType.Iron))
        {
            if (-resources[ProductionType.Iron] > iron)
                return false;
        }
        if (resources.ContainsKey(ProductionType.Food))
        {
            if (-resources[ProductionType.Food] > food)
                return false;
        }

        return true;
    }
}

using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class House : MonoBehaviour
{
    [SerializeField] private int maxCitizenAmount;
    [Header("건설에 필요한 자원 종류")]
    [SerializeField] private List<ProductionType> products;
    [Header("건설에 필요한 자원량")]
    [SerializeField] private List<int> amount;
    private CitizenManager manager;
    private ResourcesManager resourcesManager;
    public Dictionary<ProductionType, int> Resources
    {
        get
        {
            if(products.Count != amount.Count)
            {
                Debug.LogError("자원 종류와 자원량이 매치되지 않습니다.");
                return null;
            }
            else
            {
                var temp = new Dictionary<ProductionType, int>();

                for (int i = 0; i < products.Count; i++)
                {
                    temp[products[i]] = amount[i];
                }

                return temp;
            }
        }
    }

    [Inject]
    private void Construct(CitizenManager manager)
    {
        this.manager = manager;
    }

    [Inject]
    private void Construct(ResourcesManager resourcesManager)
    {
        this.resourcesManager = resourcesManager;
    }

    private void Start()
    {
        manager.IncreaseMaxCitizen(maxCitizenAmount);
    }
}

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ProductionValue", menuName = "Scriptable Objects/ProductionValue")]
public class ProductionValue : ScriptableObject
{
    [Header("생산하는 자원 종류")]
    [SerializeField] private ProductionType type;
    [Header("초기 자원 생산량")]
    [SerializeField] private int defaultAmount;
    [Header("생산 건물 초기 내구도")]
    [SerializeField] private int defaulthp;
    [Header("생산 건물 초기 최대 주민 배치 수")]
    [SerializeField] private int defaultMaxWorker;
    [Header("생산 건물 건설에 필요한 자원 종류")]
    [SerializeField] private List<ProductionType> constructProduct;
    [Header("생산 건물 건설에 필요한 자원량(자원 종류 순서에 맞게 설정해주세요)")]
    [SerializeField] private List<int> constructAmount;


    public ProductionType Type => type;
    public int DefaultAmount => defaultAmount;
    public int DefaultHp => defaulthp;
    public int DefaultMaxWorker => defaultMaxWorker;
    public Dictionary<ProductionType, int> ConstructProduct
    { 
        get
        {
            if(constructProduct.Count != constructAmount.Count)
            {
                Debug.LogError("생산 건물에 필요한 자원과 자원량이 매칭되지 않습니다. 다시 설정해주세요");
                return null;
            }
            else
            {
                Dictionary<ProductionType, int> temp = new();

                for (int i = 0; i < constructProduct.Count; i++)
                {
                    temp[constructProduct[i]] = -constructAmount[i];
                }

                return temp;
            }
        }
    }
}

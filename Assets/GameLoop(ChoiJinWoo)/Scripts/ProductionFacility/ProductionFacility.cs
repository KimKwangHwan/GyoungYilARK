using System;
using UnityEngine;
using VContainer;

public class ProductionFacility : MonoBehaviour, IDamageAble
{
    [SerializeField] private ProductionValue basicValue;
    private int productAmount;
    private bool isCrashed = false;
    private int workerAmount = 0;
    private int maxWorker;
    private float maxHp;
    private float currentHp;
    private ResourcesManager resourcesManager;
    private BuildingPool buildingPool;
    private CitizenManager citizenManager;
    private FacilityManager facilityManager;
    public ProductionType ProductionType => basicValue.Type;

    public event Action<int, int> OnWorkerChanged;
    public ProductionValue BasicValue => basicValue;

    public float Hp
    {
        get => currentHp;
        set => currentHp = Mathf.Clamp(value, 0, maxHp);
    }

    public int Defense => 0;

    [Inject]
    private void Construct(ResourcesManager resourcesManager, CitizenManager citizenManager, BuildingPool buildingPool, FacilityManager facilityManager)
    {
        this.resourcesManager = resourcesManager;
        this.citizenManager = citizenManager;
        this.buildingPool = buildingPool;
        this.facilityManager = facilityManager;
    }

    public void Init()
    {
        productAmount = basicValue.DefaultAmount;
        maxWorker = basicValue.DefaultMaxWorker;
        maxHp = basicValue.DefaultHp;
        currentHp = maxHp;
        workerAmount = 0;
        isCrashed = false;

        resourcesManager.ProductChanged(basicValue.ConstructProduct);
        facilityManager.AddFacility(this);
    }

    private void OnDisable()
    {
        ReleaseAllWorkers();
        facilityManager.RemoveFacility(this);
    }

    private void ReleaseAllWorkers()
    {
        while (workerAmount > 0)
        {
            workerAmount--;
            citizenManager.RecycleCitizen();
        }
        UpdateWorker();
    }

    public void Release()
    {
        var refund = new System.Collections.Generic.Dictionary<ProductionType, int>();
        foreach (var kv in basicValue.ConstructProduct)
        {
            refund[kv.Key] = -kv.Value;
        }

        resourcesManager.ProductChanged(refund);
        buildingPool.Return(this);
    }

    public void IncreaseWorker()
    {
        if (citizenManager == null) return;

        if (workerAmount < maxWorker && citizenManager.CheckCanUseCitizen())
        {
            workerAmount++;
            UpdateWorker();
            citizenManager.UseCitizen();
        }
    }

    public void DecreaseWorker()
    {
        if (citizenManager == null) return;

        if (workerAmount > 0)
        {
            workerAmount--;
            UpdateWorker();
            citizenManager.RecycleCitizen();
        }
    }

    public void UpdateWorker()
    {
        OnWorkerChanged?.Invoke(workerAmount, maxWorker);
    }

    public (ProductionType, int) ProduceProduction()
    {
        if (!isCrashed && resourcesManager != null)
        {
            Recover();
            return (basicValue.Type, productAmount * workerAmount);
        }
        else
        {
            Recover();
            return default;
        }
    }

    private void Recover()
    {
        Hp = maxHp;
        isCrashed = false;
    }

    public void TakeDamage(int damage)
    {
        Hp -= damage;
        if (Hp <= 0f)
        {
            isCrashed = true;
            Debug.Log("파괴됨");
        }
    }

    public void Die()
    {
        throw new NotImplementedException();
    }
}
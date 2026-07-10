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
    private EnviromentManager enviromentManager;
    private CitizenManager citizenManager;
    private FacilityManager facilityManager;
    public ProductionType ProductionType => basicValue.Type;

    public event Action<int, int> OnWorkerChanged;
    public ProductionValue BasicValue => basicValue;


    public float Hp
    {
        get
        {
            return currentHp;
        }
        set
        {
            currentHp = Mathf.Clamp(value, 0, maxHp);
        }
    }

    public int Defense => 0;

    [Inject]
    private void Construct(ResourcesManager resourcesManager, CitizenManager citizenManager, EnviromentManager enviromentManager, FacilityManager facilityManager)
    {
        this.resourcesManager = resourcesManager;
        this.citizenManager = citizenManager;
        this. enviromentManager = enviromentManager;
        this.facilityManager = facilityManager;
    }

    private void Awake()
    {
        productAmount = basicValue.DefaultAmount;
        maxWorker = basicValue.DefaultMaxWorker;
        maxHp = basicValue.DefaultHp;
        currentHp = maxHp;
    }

    private void Start()
    {
        resourcesManager.ProductChanged(basicValue.ConstructProduct);
        facilityManager.AddFacility(this);
    }

    private void OnDestroy()
    {
        facilityManager.RemoveFacility(this);
    }

    public void IncreaseWorker()
    {
        if (citizenManager == null)
            return;

        if(workerAmount < maxWorker && citizenManager.CheckCanUseCitizen())
        {
            workerAmount++;
            UpdateWorker();
            citizenManager.UseCitizen();
        }
    }

    public void DecreaseWorker()
    {
        if (citizenManager == null)
            return;

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
        if(Hp <= 0f)
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

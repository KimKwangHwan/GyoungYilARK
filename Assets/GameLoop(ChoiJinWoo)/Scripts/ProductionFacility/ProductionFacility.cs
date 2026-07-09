using System;
using UnityEngine;
using VContainer;

public class ProductionFacility : MonoBehaviour, IDamageAble
{
    [SerializeField] ProductionValue basicValue;
    private int productAmount;
    private bool isCrashed = false;
    private int workerAmount = 0;
    private int maxWorker;
    private float maxHp;
    private float currentHp;
    private ResourcesManager resourcesManager;
    private EnviromentManager enviromentManager;
    private CitizenManager citizenManager;
    public ProductionType ProductionType => basicValue.Type;

    public event Action<int, int> OnWorkerChanged;


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
    public void Construct(ResourcesManager resourcesManager)
    {
        this.resourcesManager = resourcesManager;
    }

    [Inject]
    public void Construct(CitizenManager citizenManager)
    {
        this.citizenManager = citizenManager;
    }

    [Inject]
    public void Construct(EnviromentManager enviromentManager)
    {
        this.enviromentManager = enviromentManager;
    }

    private void Awake()
    {
        productAmount = basicValue.DefaultAmount;
        maxWorker = basicValue.DefaultMaxWorker;
        maxHp = basicValue.DefaultHp;
        currentHp = maxHp;

        foreach(var type in basicValue.ConstructProduct)
        {
            resourcesManager.ProductChanged(type, -basicValue.ConstructAmount[basicValue.ConstructProduct.IndexOf(type)]);
        }
    }

    private void Start()
    {
        enviromentManager.OnDay += ProduceProduction;
    }

    private void OnDestroy()
    {
        enviromentManager.OnDay -= ProduceProduction;
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

    public void ProduceProduction()
    {
        if (!isCrashed && resourcesManager != null)
        {
            resourcesManager.ProductChanged(basicValue.Type, productAmount * workerAmount);
        }

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

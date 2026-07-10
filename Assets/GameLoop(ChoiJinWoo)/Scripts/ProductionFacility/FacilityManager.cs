using System.Collections.Generic;
using UnityEngine;
using VContainer;

public class FacilityManager : MonoBehaviour
{
    private List<ProductionFacility> facilities = new();
    private Dictionary<ProductionType, int> products = new();

    private ResourcesManager resourcesManager;

    [Inject]
    private void Construct(ResourcesManager resourcesManager)
    {
        this.resourcesManager = resourcesManager;
    }

    public void AddFacility(ProductionFacility facility)
    {
        facilities.Add(facility);
    }

    public void RemoveFacility(ProductionFacility facility)
    {
        facilities.Remove(facility);
    }

    public void SumProduct()
    {
        foreach(var facility in facilities)
        {
            var product = facility.ProduceProduction();
            if (product == default)
                continue;
            if(!products.ContainsKey(product.Item1))
            {
                products[product.Item1] = product.Item2;
            }
            else
            {
                products[product.Item1] += product.Item2;
            }
        }

        resourcesManager.ProductChanged(products);
        products.Clear();
    }
}

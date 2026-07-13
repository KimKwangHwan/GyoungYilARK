public interface IBuildingPool
{
    ProductionFacility Rent(ProductionType type);
    void Return(ProductionFacility instance);
}
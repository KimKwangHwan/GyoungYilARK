using UnityEngine;

public class DayState : IState
{
    private GameManager gameManager;
    private FacilityManager facilityManager;

    public DayState(GameManager manager, FacilityManager facilityManager)
    {
        gameManager = manager;
        this.facilityManager = facilityManager;
    }

    public void Enter()
    {
        if(gameManager.DayCount == 0)
        {
            gameManager.IncreaseDayCount();
            gameManager.ChangeCanBuild(true);
            Debug.Log($"{gameManager.DayCount}일차");
            return;
        }

        gameManager.ChangeCanBuild(true);
        gameManager.IncreaseDayCount();
        Debug.Log($"{gameManager.DayCount}일차");
        facilityManager.SumProduct();
    }

    public void Exit()
    {
        gameManager.ChangeCanBuild(false);
    }

    public void Update() { }
}

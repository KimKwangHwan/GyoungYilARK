using UnityEngine;

public class NightState : IState
{
    private GameManager gameManager;
    public NightState(GameManager manager)
    {
        this.gameManager = manager;
    }

    public void Enter()
    {
        gameManager.SpawnEnemy();
    }

    public void Exit()
    {
        
    }

    public void Update()
    {
        
    }
}

using UnityEngine;
using VContainer;

public class ResultState : IState
{
    private GameManager manager;
    private UiManager uiManager;

    public ResultState(GameManager manager, UiManager uiManager)
    {
        this.manager = manager;
        this.uiManager = uiManager;
    }

    public void Enter()
    {
        if(Time.timeScale > 0f)
            Time.timeScale = 0f;
        uiManager.OpenExpeditoinUi();
    }

    public void Exit()
    {
        Time.timeScale = 1f;
    }

    public void Update()
    {
        throw new System.NotImplementedException();
    }
}

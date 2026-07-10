using UnityEngine;

public class FSM
{
    public IState CurrentState { get; private set; }

    public void ChangeState(IState newState)
    {
        var c = CurrentState;
        if (CurrentState == newState)
            return;

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState?.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }
}

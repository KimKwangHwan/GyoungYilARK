using System;
using System.Collections.Generic;

// 낮·밤 전환 상태와 모듈 준비 상태를 받아 Trail 재생 명령을 한 곳에서 결정한다.
public class TrailStateController
{
    private enum TrailState
    {
        LoadPending,
        DayReady,
        DayTransition,
        NightTransition,
        NightReady
    }

    private readonly List<PathTrail> trails = new();
    private readonly List<PathTrail> preparingTrails = new();
    private readonly List<ModuleLogic> modules = new();
    private readonly List<Action<ModuleState>> handlers = new();
    private TrailState currentState = TrailState.LoadPending;

    // 모듈과 Trail을 등록하고 이후 준비 상태 변경을 받는다.
    public void Collect(ModuleLogic module, PathTrail trail)
    {
        Action<ModuleState> handler = state => HandleModuleState(trail, state);
        modules.Add(module);
        trails.Add(trail);
        handlers.Add(handler);
        module.OnStateChanged += handler;
        HandleModuleState(trail, module.CurrentState);
    }

    // 낮 전환이 시작되면 전환 상태를 기록하고 모든 Trail을 정지한다.
    public void StartDay()
    {
        currentState = TrailState.DayTransition;
        StopAll();
    }

    // 밤 전환이 시작되면 전환 상태를 기록하고 모든 Trail을 정지한다.
    public void StartNight()
    {
        currentState = TrailState.NightTransition;
        StopAll();
    }

    // 낮 환경 전환이 끝나면 준비된 모든 Trail을 반복 재생한다.
    public void FinishDay()
    {
        currentState = TrailState.DayReady;
        PlayLoops();
    }

    // 밤 환경 전환이 끝나면 준비된 모든 Trail을 한 번 재생한다.
    public void FinishNight()
    {
        currentState = TrailState.NightReady;
        PlayOnce();
    }

    // DayStart 저장본 복원이 끝난 완성된 낮에서 반복 재생을 시작한다.
    public void FinishDayLoad()
    {
        currentState = TrailState.DayReady;
        PlayLoops();
    }

    // 모듈이 준비 상태가 된 순간 현재 완료된 낮·밤에 맞는 재생 명령을 보낸다.
    private void HandleModuleState(PathTrail trail, ModuleState moduleState)
    {
        preparingTrails.Remove(trail);
        if (moduleState != ModuleState.Preparing) return;

        preparingTrails.Add(trail);

        switch (currentState)
        {
            case TrailState.DayReady:
                trail.PlayLoop();
                return;
            case TrailState.NightReady:
                trail.PlayOnce();
                return;
        }
    }

    // 준비된 모든 Trail을 반복 재생한다.
    private void PlayLoops()
    {
        for (int index = 0; index < preparingTrails.Count; index++)
        {
            preparingTrails[index].PlayLoop();
        }
    }

    // 준비된 모든 Trail을 한 번 재생한다.
    private void PlayOnce()
    {
        for (int index = 0; index < preparingTrails.Count; index++)
        {
            preparingTrails[index].PlayOnce();
        }
    }

    // 모든 Trail의 현재 재생을 정지한다.
    private void StopAll()
    {
        for (int index = 0; index < trails.Count; index++)
        {
            trails[index].StopTrail();
        }
    }

    // 등록한 모든 모듈 상태 구독을 해제한다.
    public void Release()
    {
        for (int index = 0; index < modules.Count; index++)
        {
            modules[index].OnStateChanged -= handlers[index];
        }
    }
}

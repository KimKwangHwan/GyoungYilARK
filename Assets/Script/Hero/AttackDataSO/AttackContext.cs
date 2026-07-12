using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading;

public struct AttackContext
{
    public Transform self;
    public Transform target;
    public Animator anim;
    public Animator bowAnim;
    public Animator arrowAnim;
    public HeroAnimEvents animEvents;
    // 

    public async UniTask WaitForAnimEvent(string eventName, CancellationToken ct)
    {
        bool fired = false;
        System.Action handler = () => fired = true;
        animEvents.Subscribe(eventName, handler);
        try
        {
            await UniTask.WaitUntil(() => fired, cancellationToken: ct);
        }
        finally
        {
            animEvents.Unsubscribe(eventName, handler);
        }
    }
}

using Cysharp.Threading.Tasks;
using UnityEngine;

public class ArcherAttackState : HeroAttackState
{
    private Archer archer;
    public ArcherAttackState(Archer archer, HeroStateMachine stateMachine) : base(archer, stateMachine)
    {
        this.archer = archer;
    }
}

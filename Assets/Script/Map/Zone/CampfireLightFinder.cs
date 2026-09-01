using UnityEngine;

// 얼음 보드에 놓인 모닥불 불빛을 찾아낸다.
public class CampfireLightFinder
{
    private const int SelfChainLength = 1;

    // 이 얼음 보드가 매달린 자리 아래에 있는 모닥불 불빛을 모두 찾아 돌려준다.
    public CampfireLight[] FindLights(MapBoard iceBoard)
    {
        Transform holderRoot = SelectHolder(iceBoard);
        return holderRoot.GetComponentsInChildren<CampfireLight>(true);
    }

    // 보드 위에 자리가 있으면 그 한 칸 위를, 없으면 보드 자신을 고른다.
    private static Transform SelectHolder(MapBoard iceBoard)
    {
        Transform boardRoot = iceBoard.transform;
        Transform[] holderChoices = { boardRoot, boardRoot.parent };
        return holderChoices[ReadParentStep(iceBoard)];
    }

    // 보드 위에 자리가 있으면 1을, 없으면 0을 돌려준다.
    private static int ReadParentStep(MapBoard iceBoard)
    {
        int chainLength = iceBoard.GetComponentsInParent<Transform>(true).Length;
        return Mathf.Min(chainLength - SelfChainLength, SelfChainLength);
    }
}

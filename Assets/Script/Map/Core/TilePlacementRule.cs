 
public static class TilePlacementRule
{
    public readonly struct Result
    {
        public readonly bool Allowed;
        public readonly string Reason; // 불가 사유(성공 시 빈 문자열). UI 토스트/로그용

        public Result(bool allowed, string reason)
        {
            Allowed = allowed;
            Reason = reason;
        }

        public static readonly Result Ok = new(true, "");
        public static Result No(string reason) => new(false, reason);
    }

    /// <summary>placing 종류를 tile에 놓을 수 있는지. isDayPhase=false면 밤이라 전부 불가(설계 §4·§6).</summary>
    public static Result CanPlace(TileState tile, OccupantKind placing)
    {

        if (tile == null)
        {
            return Result.No("타일 없음");
        }

        if (tile.Terrain == TerrainType.Core)
        {
            return Result.No("본진 영역에는 배치할 수 없다");
        }

        if (tile.Territory != TerritoryState.Claimed)
        {
            return Result.No("미점령 타일이다");
        }

        // 한 타일에는 하나의 주요 점유만(설계 §4). 적 경로(EnemyLane)는 점유가 아니라 통과 허용.
        if (!tile.IsEmpty)
        {
            return Result.No("이미 다른 오브젝트가 있다");
        }

        return placing switch
        {
            OccupantKind.MeleeHero  => CheckMelee(tile),
            OccupantKind.RangedHero => CheckRanged(tile),
            OccupantKind.Building   => CheckBuild(tile),
            _ => Result.No("배치 대상이 없다")
        };
    }

    private static Result CheckMelee(TileState tile)
    {
        if (tile.Terrain != TerrainType.Ground)
        {
            return Result.No("근접 영웅은 지상 타일에만 배치할 수 있다");
        }

        return tile.CanMelee ? Result.Ok : Result.No("근접 영웅 배치 불가 타일");
    }

    private static Result CheckRanged(TileState tile)
    {
        if (tile.Terrain != TerrainType.High)
        {
            return Result.No("원거리 영웅은 고지 타일에만 배치할 수 있다");
        }

        return tile.CanRanged ? Result.Ok : Result.No("원거리 영웅 배치 불가 타일");
    }

    private static Result CheckBuild(TileState tile)
    {
        if (tile.Terrain != TerrainType.Ground)
        {
            return Result.No("생산 건물은 지상 타일에만 건설할 수 있다");
        }

        return tile.CanBuild ? Result.Ok : Result.No("생산 건물 건설 불가 타일");
    }

}

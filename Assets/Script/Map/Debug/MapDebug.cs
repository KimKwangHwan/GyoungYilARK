using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class MapDebug : MonoBehaviour
{
    public enum View
    {
        Terrain,
        Territory,
        Place,
        Lane,
        Unit,
        RangeCover
    }

    [SerializeField] private MapBoard board;
    [SerializeField] private View view = View.Terrain;
    [SerializeField] private bool showColor = true;
    [SerializeField] private bool showLabel = true;
    [SerializeField] private float yOffset = 0.08f;
    [SerializeField] private float size = 0.85f;

    private void OnValidate()
    {
        if (board == null)
        {
            board = GetComponent<MapBoard>();
        }
    }

    private void Reset()
    {
        board = GetComponent<MapBoard>();
    }

    private void OnDrawGizmos()
    {
        if (board == null)
        {
            board = GetComponent<MapBoard>();
        }

        if (board == null)
        {
            return;
        }

        if (board.CellCount == 0)
        {
            return;
        }

        foreach (Tile tile in board.Cells.Values)
        {
            DrawTile(tile);
        }
    }

    private void DrawTile(Tile tile)
    {
        if (tile == null)
        {
            return;
        }

        Vector3 pos = tile.WorldTop + Vector3.up * yOffset;

        if (showColor)
        {
            Gizmos.color = GetColor(tile);
            Gizmos.DrawCube(pos, new Vector3(size, 0.03f, size));
        }

#if UNITY_EDITOR
        if (showLabel)
        {
            Handles.Label(pos + Vector3.up * 0.08f, GetText(tile));
        }
#endif
    }

    private Color GetColor(Tile tile)
    {
        return view switch
        {
            View.Terrain => TerrainColor(tile),
            View.Territory => tile.State.Territory == TerritoryState.Claimed ? new Color(0.2f, 0.75f, 1f, 0.45f) : new Color(0.25f, 0.25f, 0.25f, 0.35f),
            View.Place => PlaceColor(tile),
            View.Lane => tile.IsEnemyLane ? new Color(1f, 0.55f, 0.1f, 0.55f) : new Color(0.25f, 0.25f, 0.25f, 0.2f),
            View.Unit => UnitColor(tile),
            View.RangeCover => tile.IsRangeCovered ? new Color(0.45f, 0.65f, 1f, 0.55f) : new Color(0.25f, 0.25f, 0.25f, 0.2f),
            _ => Color.white
        };
    }

    private static Color TerrainColor(Tile tile)
    {
        if (tile.IsEnemySpawn)
        {
            return new Color(1f, 0.9f, 0.1f, 0.55f);
        }

        return tile.Terrain switch
        {
            TerrainType.Core => new Color(0.2f, 0.45f, 1f, 0.55f),
            TerrainType.Ground => new Color(0.25f, 0.8f, 0.35f, 0.45f),
            TerrainType.High => new Color(0.95f, 0.25f, 0.2f, 0.5f),
            _ => new Color(0.3f, 0.3f, 0.3f, 0.35f)
        };
    }

    private static Color PlaceColor(Tile tile)
    {
        if (tile.State.CanMelee)
        {
            return new Color(0.2f, 0.8f, 0.35f, 0.5f);
        }

        if (tile.State.CanRanged)
        {
            return new Color(0.55f, 0.45f, 1f, 0.5f);
        }

        if (tile.State.CanBuild)
        {
            return new Color(0.35f, 0.75f, 1f, 0.5f);
        }

        return new Color(0.25f, 0.25f, 0.25f, 0.25f);
    }

    private static Color UnitColor(Tile tile)
    {
        if (tile.HasEnemy)
        {
            return new Color(1f, 0.2f, 0.15f, 0.55f);
        }

        if (!tile.IsEmpty)
        {
            return new Color(1f, 0.9f, 0.25f, 0.55f);
        }

        return new Color(0.25f, 0.25f, 0.25f, 0.2f);
    }

    private string GetText(Tile tile)
    {
        return view switch
        {
            View.Terrain => $"{board.IndexOf(tile)}\n{tile.Coord}\n{tile.Terrain}",
            View.Territory => $"{tile.State.Label}\n{tile.State.Territory}",
            View.Place => PlaceText(tile),
            View.Lane => tile.IsEnemyLane ? $"{tile.State.Label}\nLane" : tile.State.Label,
            View.Unit => $"U:{tile.State.Occupant}\nE:{tile.EnemyCount}\nB:{tile.BlockedCount}/{tile.BlockCapacity}",
            View.RangeCover => $"RangeCover:{tile.RangeCoverCount}",
            _ => tile.State.Label
        };
    }

    private static string PlaceText(Tile tile)
    {
        string text = tile.State.Label;

        if (tile.State.CanMelee)
        {
            text += "\nMelee";
        }

        if (tile.State.CanRanged)
        {
            text += "\nRanged";
        }

        if (tile.State.CanBuild)
        {
            text += "\nBuild";
        }

        return text;
    }
}

namespace DAIgame.World;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Sets up navigation from a TileMapLayer at runtime.
/// Attach to a NavigationRegion2D node and configure the FloorTileMap export.
/// </summary>
public partial class NavigationSetup : NavigationRegion2D
{
	/// <summary>
	/// The TileMapLayer containing floor tiles to generate navigation from.
	/// </summary>
	[Export]
	public TileMapLayer? FloorTileMap { get; set; }

	/// <summary>
	/// The TileMapLayer containing wall tiles to exclude from navigation.
	/// </summary>
	[Export]
	public TileMapLayer? WallsTileMap { get; set; }

	/// <summary>
	/// Agent radius for navigation mesh baking.
	/// </summary>
	[Export]
	public float AgentRadius { get; set; } = 8f;

	public override void _Ready() => CallDeferred(MethodName.BakeNavigationFromTiles);

        private void BakeNavigationFromTiles()
        {
                if (FloorTileMap is null)
                {
                        GD.PrintErr("NavigationSetup: FloorTileMap not assigned");
                        return;
                }

                var navPoly = new NavigationPolygon
                {
                        AgentRadius = AgentRadius
                };

                // Get all used floor tile positions
                var usedCells = FloorTileMap.GetUsedCells();
                if (usedCells.Count == 0)
                {
                        GD.PrintErr("NavigationSetup: No floor tiles found");
                        return;
                }

                var tileSize = FloorTileMap.TileSet?.TileSize ?? new Vector2I(16, 16);
                var halfSize = (Vector2)tileSize / 2f;

                // Collect valid floor cells (not blocked by walls)
                var validCells = new HashSet<Vector2I>();
                var blockedCells = 0;
                foreach (var cell in usedCells)
                {
                        // Skip if this cell has a wall on top
                        if (WallsTileMap is not null && WallsTileMap.GetCellSourceId(cell) != -1)
                        {
                                blockedCells++;
                                continue;
                        }
                        validCells.Add(cell);
                }

                if (validCells.Count == 0)
                {
                        GD.PrintErr("NavigationSetup: No valid floor tiles after wall exclusion");
                        return;
                }

                foreach (var cell in validCells)
                {
                        // MapToLocal gives position in TileMapLayer's local space
                        // We need to convert to NavigationRegion2D's local space
                        var localPos = FloorTileMap.MapToLocal(cell);
                        var globalPos = FloorTileMap.ToGlobal(localPos);
                        var navLocalPos = ToLocal(globalPos);

                        var tileOutline = new Vector2[]
                        {
                                navLocalPos + new Vector2(-halfSize.X, -halfSize.Y),
                                navLocalPos + new Vector2(halfSize.X, -halfSize.Y),
                                navLocalPos + new Vector2(halfSize.X, halfSize.Y),
                                navLocalPos + new Vector2(-halfSize.X, halfSize.Y),
                        };

                        navPoly.AddOutline(tileOutline);
                }

                navPoly.MakePolygonsFromOutlines();
                var polygonCount = navPoly.GetPolygonCount();
                GD.Print($"NavigationSetup: Built {polygonCount} nav polygons from {validCells.Count} floor tiles, {blockedCells} walls removed.");

                if (polygonCount == 0)
                {
                        GD.PrintErr("NavigationSetup: Generated navigation mesh has 0 polygons");
                        return;
                }

                NavigationPolygon = navPoly;
        }
}

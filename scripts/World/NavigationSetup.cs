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

		// Collect valid floor cells (not blocked by walls)
		var validCells = new HashSet<Vector2I>();
		foreach (var cell in usedCells)
		{
			// Skip if this cell has a wall on top
			if (WallsTileMap is not null && WallsTileMap.GetCellSourceId(cell) != -1)
			{
				continue;
			}
			validCells.Add(cell);
		}

		if (validCells.Count == 0)
		{
			GD.PrintErr("NavigationSetup: No valid floor tiles after wall exclusion");
			return;
		}

		// Find bounding box
		var minX = int.MaxValue;
		var maxX = int.MinValue;
		var minY = int.MaxValue;
		var maxY = int.MinValue;

		foreach (var cell in validCells)
		{
			if (cell.X < minX)
			{
				minX = cell.X;
			}

			if (cell.X > maxX)
			{
				maxX = cell.X;
			}

			if (cell.Y < minY)
			{
				minY = cell.Y;
			}

			if (cell.Y > maxY)
			{
				maxY = cell.Y;
			}
		}

		// Create outer boundary polygon from bounding box
		// Then let NavigationServer handle the complex merging
		var halfSize = (Vector2)tileSize / 2f;

		// Add each floor tile as a separate outline - NavigationServer will merge them
		foreach (var cell in validCells)
		{
			// MapToLocal gives position in TileMapLayer's local space
			// We need to convert to NavigationRegion2D's local space
			var localPos = FloorTileMap.MapToLocal(cell);
			var globalPos = FloorTileMap.ToGlobal(localPos);
			var navLocalPos = ToLocal(globalPos);

			var tilePolygon = new Vector2[]
			{
				navLocalPos + new Vector2(-halfSize.X, -halfSize.Y),
				navLocalPos + new Vector2(halfSize.X, -halfSize.Y),
				navLocalPos + new Vector2(halfSize.X, halfSize.Y),
				navLocalPos + new Vector2(-halfSize.X, halfSize.Y),
			};

			navPoly.AddOutline(tilePolygon);
		}

// Use NavigationServer2D to bake from source geometry
		var sourceGeometry = new NavigationMeshSourceGeometryData2D();
		NavigationServer2D.ParseSourceGeometryData(navPoly, sourceGeometry, this);
		NavigationServer2D.BakeFromSourceGeometryData(navPoly, sourceGeometry);

		NavigationPolygon = navPoly;
	}
}

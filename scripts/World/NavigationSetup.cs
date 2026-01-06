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

		// Find bounding box for the navigable area.
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

		var halfSize = (Vector2)tileSize / 2f;

		// Create outer boundary polygon from bounding box to define bake bounds.
		var minLocal = FloorTileMap.MapToLocal(new Vector2I(minX, minY)) - halfSize;
		var maxLocal = FloorTileMap.MapToLocal(new Vector2I(maxX, maxY)) + halfSize;
		var minGlobal = FloorTileMap.ToGlobal(minLocal);
		var maxGlobal = FloorTileMap.ToGlobal(maxLocal);
		var minNav = ToLocal(minGlobal);
		var maxNav = ToLocal(maxGlobal);

		var boundsOutline = new Vector2[]
		{
			new Vector2(minNav.X, minNav.Y),
			new Vector2(maxNav.X, minNav.Y),
			new Vector2(maxNav.X, maxNav.Y),
			new Vector2(minNav.X, maxNav.Y),
		};

		navPoly.AddOutline(boundsOutline);

		var sourceGeometry = new NavigationMeshSourceGeometryData2D();

		var wallCellCount = WallsTileMap?.GetUsedCells().Count ?? 0;
		GD.Print($"NavigationSetup: Baking nav mesh from {validCells.Count} floor tiles, {wallCellCount} walls.");

		// Add each floor tile as traversable source geometry.
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

			sourceGeometry.AddTraversableOutline(tilePolygon);
		}

		if (WallsTileMap is not null && wallCellCount > 0)
		{
			var wallTileSize = WallsTileMap.TileSet?.TileSize ?? tileSize;
			var wallHalfSize = (Vector2)wallTileSize / 2f;

			foreach (var cell in WallsTileMap.GetUsedCells())
			{
				var localPos = WallsTileMap.MapToLocal(cell);
				var globalPos = WallsTileMap.ToGlobal(localPos);
				var navLocalPos = ToLocal(globalPos);

				var wallPolygon = new Vector2[]
				{
					navLocalPos + new Vector2(-wallHalfSize.X, -wallHalfSize.Y),
					navLocalPos + new Vector2(wallHalfSize.X, -wallHalfSize.Y),
					navLocalPos + new Vector2(wallHalfSize.X, wallHalfSize.Y),
					navLocalPos + new Vector2(-wallHalfSize.X, wallHalfSize.Y),
				};

				sourceGeometry.AddObstructionOutline(wallPolygon);
			}
		}

		if (!sourceGeometry.HasData())
		{
			GD.PrintErr("NavigationSetup: No source geometry to bake");
			return;
		}

		NavigationServer2D.BakeFromSourceGeometryData(navPoly, sourceGeometry);
		if (navPoly.GetPolygonCount() == 0)
		{
			GD.PrintErr("NavigationSetup: Baked navigation mesh has 0 polygons");
		}

		NavigationPolygon = navPoly;
	}
}

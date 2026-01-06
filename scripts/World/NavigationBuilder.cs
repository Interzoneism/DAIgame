namespace DAIgame.World;

using Godot;

/// <summary>
/// Builds a navigation polygon for a NavigationRegion2D based on a wall TileMapLayer.
/// Creates a traversable navigation mesh where 8x8 tiles are the unit size,
/// carving out wall tiles as obstacles.
/// </summary>
public partial class NavigationBuilder : NavigationRegion2D
{
	/// <summary>
	/// Path to the wall TileMapLayer node. If empty, will try to find "Walls" sibling.
	/// </summary>
	[Export]
	public NodePath WallTileMapPath { get; set; } = "";

	/// <summary>
	/// Size of each tile in pixels. Default 8x8 matching the wall tiles.
	/// </summary>
	[Export]
	public int TileSize { get; set; } = 8;

	/// <summary>
	/// Padding around the navigation area in tiles.
	/// </summary>
	[Export]
	public int Padding { get; set; } = 2;

	/// <summary>
	/// How many tiles beyond the used area to extend navigation (in each direction).
	/// </summary>
	[Export]
	public int NavigationExtent { get; set; } = 10;

	private TileMapLayer? _wallTileMap;

	public override void _Ready()
	{
		// Find the wall tilemap
		if (!string.IsNullOrEmpty(WallTileMapPath))
		{
			_wallTileMap = GetNodeOrNull<TileMapLayer>(WallTileMapPath);
		}
		else
		{
			// Try to find a sibling named "Walls"
			_wallTileMap = GetParent()?.GetNodeOrNull<TileMapLayer>("Walls");
		}

		if (_wallTileMap is null)
		{
			GD.PrintErr("NavigationBuilder: Could not find wall TileMapLayer");
			return;
		}

		// Defer navigation build to ensure the navigation server is ready
		CallDeferred(MethodName.BuildNavigationMesh);
	}

	/// <summary>
	/// Builds the navigation polygon by creating a large traversable area
	/// and carving out wall tiles as obstacles.
	/// </summary>
	public void BuildNavigationMesh()
	{
		if (_wallTileMap is null)
		{
			GD.PrintErr("NavigationBuilder: No wall TileMapLayer assigned");
			return;
		}

		// Get all used cells from the wall tilemap
		var usedCells = _wallTileMap.GetUsedCells();
		if (usedCells.Count == 0)
		{
			GD.Print("NavigationBuilder: No wall tiles found, creating open navigation area");
		}

		// Calculate bounds of the navigable area based on wall tiles
		var bounds = CalculateBounds(usedCells);

		// Create the navigation polygon
		var navPoly = new NavigationPolygon
		{
			AgentRadius = 4f // Half tile size for tight navigation
		};

		// Create outer boundary (traversable area)
		var outerBoundary = CreateOuterBoundary(bounds);
		navPoly.AddOutline(outerBoundary);

		// Add each wall tile as an obstacle (hole in the navigation mesh)
		foreach (var cellCoord in usedCells)
		{
			var obstacle = CreateTileObstacle(cellCoord);
			navPoly.AddOutline(obstacle);
		}

		// Use the newer baking API
		var sourceGeometry = new NavigationMeshSourceGeometryData2D();
		NavigationServer2D.ParseSourceGeometryData(navPoly, sourceGeometry, this);
		NavigationServer2D.BakeFromSourceGeometryData(navPoly, sourceGeometry);

		NavigationPolygon = navPoly;

		GD.Print($"NavigationBuilder: Built navigation mesh with {usedCells.Count} wall obstacles, bounds: {bounds}");
	}

	/// <summary>
	/// Rebuild the navigation mesh. Call this if walls change at runtime.
	/// </summary>
	public void RebuildNavigation() => BuildNavigationMesh();

	private Rect2I CalculateBounds(Godot.Collections.Array<Vector2I> usedCells)
	{
		if (usedCells.Count == 0)
		{
			// Default area if no walls - centered on origin
			return new Rect2I(
				-NavigationExtent,
				-NavigationExtent,
				NavigationExtent * 2,
				NavigationExtent * 2
			);
		}

		int minX = int.MaxValue, minY = int.MaxValue;
		int maxX = int.MinValue, maxY = int.MinValue;

		foreach (var cell in usedCells)
		{
			if (cell.X < minX)
			{
				minX = cell.X;
			}

			if (cell.Y < minY)
			{
				minY = cell.Y;
			}

			if (cell.X > maxX)
			{
				maxX = cell.X;
			}

			if (cell.Y > maxY)
			{
				maxY = cell.Y;
			}
		}

		// Extend bounds by NavigationExtent tiles in each direction
		minX -= NavigationExtent;
		minY -= NavigationExtent;
		maxX += NavigationExtent;
		maxY += NavigationExtent;

		return new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
	}

	private Vector2[] CreateOuterBoundary(Rect2I bounds)
	{
		// Convert tile coordinates to world coordinates
		float left = bounds.Position.X * TileSize;
		float top = bounds.Position.Y * TileSize;
		float right = (bounds.Position.X + bounds.Size.X) * TileSize;
		float bottom = (bounds.Position.Y + bounds.Size.Y) * TileSize;

		// Clockwise winding for outer boundary
		return
		[
			new(left, top),
			new(right, top),
			new(right, bottom),
			new(left, bottom)
		];
	}

	private Vector2[] CreateTileObstacle(Vector2I cellCoord)
	{
		// Convert tile coordinate to world position (top-left corner)
		float x = cellCoord.X * TileSize;
		float y = cellCoord.Y * TileSize;

		// Counter-clockwise winding for obstacles (holes)
		return
		[
			new(x, y),
			new(x, y + TileSize),
			new(x + TileSize, y + TileSize),
			new(x + TileSize, y)
		];
	}
}

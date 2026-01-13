namespace DAIgame.Loot;

using System.Collections.Generic;
using Godot;

/// <summary>
/// Handles the ViewLoot toggle mode and highlighting of nearby lootables.
/// Should be added as a child of the player.
/// </summary>
public partial class LootHighlighter : Node
{
    /// <summary>
    /// Radius around the player in which lootables are highlighted.
    /// </summary>
    [Export]
    public float HighlightRadius { get; set; } = 100f;

    /// <summary>
    /// Whether ViewLoot mode is currently active.
    /// </summary>
    public bool IsViewLootActive { get; private set; }

    /// <summary>
    /// Emitted when ViewLoot mode is toggled on or off.
    /// </summary>
    [Signal]
    public delegate void ViewLootToggledEventHandler(bool isActive);

    private Node2D? _player;
    private readonly List<ILootable> _highlightedLootables = [];

    public static LootHighlighter? Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        _player = GetParent<Node2D>();

        if (_player is null)
        {
            GD.PrintErr("LootHighlighter: Must be a child of a Node2D (player).");
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ViewLoot"))
        {
            IsViewLootActive = !IsViewLootActive;
            EmitSignal(SignalName.ViewLootToggled, IsViewLootActive);
            if (IsViewLootActive)
            {
                UpdateHighlights();
                GD.Print("LootHighlighter: ViewLoot activated");
            }
            else
            {
                ClearAllHighlights();
                GD.Print("LootHighlighter: ViewLoot deactivated");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (!IsViewLootActive)
        {
            return;
        }

        UpdateHighlights();
    }

    private void UpdateHighlights()
    {
        if (_player is null)
        {
            return;
        }

        var playerPos = _player.GlobalPosition;
        var radiusSq = HighlightRadius * HighlightRadius;

        // Gather all lootables
        var containers = GetTree().GetNodesInGroup("lootable_container");
        var groundItems = GetTree().GetNodesInGroup("ground_item");

        var newHighlighted = new List<ILootable>();

        foreach (var node in containers)
        {
            if (node is not ILootable lootable)
            {
                continue;
            }

            var distSq = playerPos.DistanceSquaredTo(lootable.GetGlobalPosition());
            if (distSq <= radiusSq)
            {
                newHighlighted.Add(lootable);
                lootable.IsHighlighted = true;
            }
            else
            {
                lootable.IsHighlighted = false;
            }
        }

        foreach (var node in groundItems)
        {
            if (node is not ILootable lootable)
            {
                continue;
            }

            var distSq = playerPos.DistanceSquaredTo(lootable.GetGlobalPosition());
            if (distSq <= radiusSq)
            {
                newHighlighted.Add(lootable);
                lootable.IsHighlighted = true;
            }
            else
            {
                lootable.IsHighlighted = false;
            }
        }

        // Remove highlights from lootables that are no longer in range
        foreach (var lootable in _highlightedLootables)
        {
            if (!newHighlighted.Contains(lootable))
            {
                lootable.IsHighlighted = false;
            }
        }

        _highlightedLootables.Clear();
        _highlightedLootables.AddRange(newHighlighted);
    }

    private void ClearAllHighlights()
    {
        foreach (var lootable in _highlightedLootables)
        {
            lootable.IsHighlighted = false;
        }
        _highlightedLootables.Clear();
    }

    /// <summary>
    /// Gets all lootables within range of the player.
    /// </summary>
    public List<ILootable> GetLootablesInRange()
    {
        if (_player is null)
        {
            return [];
        }

        var result = new List<ILootable>();
        var playerPos = _player.GlobalPosition;
        var radiusSq = HighlightRadius * HighlightRadius;

        var containers = GetTree().GetNodesInGroup("lootable_container");
        var groundItems = GetTree().GetNodesInGroup("ground_item");

        foreach (var node in containers)
        {
            if (node is not ILootable lootable)
            {
                continue;
            }

            var distSq = playerPos.DistanceSquaredTo(lootable.GetGlobalPosition());
            if (distSq <= radiusSq)
            {
                result.Add(lootable);
            }
        }

        foreach (var node in groundItems)
        {
            if (node is not ILootable lootable)
            {
                continue;
            }

            var distSq = playerPos.DistanceSquaredTo(lootable.GetGlobalPosition());
            if (distSq <= radiusSq)
            {
                result.Add(lootable);
            }
        }

        return result;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

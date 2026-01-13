namespace DAIgame.Loot;

using System.Collections.Generic;
using DAIgame.Core;
using DAIgame.Player;
using DAIgame.UI;
using Godot;

/// <summary>
/// Base class for lootable containers (boxes, shelves, corpses, etc.).
/// Attach to a Node2D to make it a lootable container.
/// </summary>
public partial class LootableContainer : Node2D, ILootable, IInteractable
{
    private const float HighlightPulseSpeed = 4f;
    private const float HighlightMinAlpha = 0.3f;
    private const float HighlightMaxAlpha = 0.8f;
    private static readonly StringName HighlightColorParam = new("highlight_color");

    /// <summary>
    /// Display name shown in the loot UI.
    /// </summary>
    [Export]
    public string DisplayName { get; set; } = "Container";

    /// <summary>
    /// Number of inventory slots in this container.
    /// </summary>
    [Export]
    public int Slots { get; set; } = 6;

    /// <summary>
    /// Number of columns for the container grid UI.
    /// </summary>
    [Export]
    public int Columns { get; set; } = 3;

    private InventoryItem?[] _items = [];
    private bool _isHighlighted;
    private bool _isInteractionHighlighted;
    private ShaderMaterial? _highlightMaterial;
    private CanvasItem? _spriteNode;
    private Material? _originalMaterial;
    private float _highlightTime;
    private Area2D? _interactionArea;

    public string LootDisplayName => DisplayName;
    public int SlotCount => _items.Length;
    public bool RemoveWhenEmpty => false;
    public string InteractionTooltip => "Loot";

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set
        {
            if (_isHighlighted == value)
            {
                return;
            }

            _isHighlighted = value;
            UpdateHighlightVisual();
        }
    }

    public bool IsInteractionHighlighted
    {
        get => _isInteractionHighlighted;
        set
        {
            if (_isInteractionHighlighted == value)
            {
                return;
            }

            _isInteractionHighlighted = value;
            // Use interaction highlight for the visual when hovering
            if (_isInteractionHighlighted && !_isHighlighted)
            {
                UpdateHighlightVisual();
            }
            else if (!_isInteractionHighlighted && !_isHighlighted)
            {
                UpdateHighlightVisual();
            }
        }
    }

    public override void _Ready()
    {
        _items = new InventoryItem?[Mathf.Max(1, Slots)];
        AddToGroup("lootable_container");

        SetupHighlightShader();

        // Look for interaction area if present
        _interactionArea = GetNodeOrNull<Area2D>("InteractionArea");
    }

    public override void _Process(double delta)
    {
        // Show highlight if either ViewLoot is highlighting or mouse is hovering
        var shouldHighlight = _isHighlighted || _isInteractionHighlighted;
        if (!shouldHighlight || _highlightMaterial is null)
        {
            return;
        }

        _highlightTime += (float)delta * HighlightPulseSpeed;
        var alpha = Mathf.Lerp(HighlightMinAlpha, HighlightMaxAlpha,
            (Mathf.Sin(_highlightTime) + 1f) * 0.5f);
        // Use green for interaction hover, yellow for ViewLoot
        var color = _isInteractionHighlighted
            ? new Color(0.5f, 1f, 0.5f, alpha)
            : new Color(1f, 1f, 0f, alpha);
        _highlightMaterial.SetShaderParameter(HighlightColorParam, color);
    }

    private void SetupHighlightShader()
    {
        // Find the sprite node to apply the shader to
        _spriteNode = GetNodeOrNull<Sprite2D>("Sprite2D") as CanvasItem
            ?? GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") as CanvasItem;

        if (_spriteNode is null)
        {
            GD.PrintErr($"LootableContainer '{Name}': No Sprite2D or AnimatedSprite2D found for highlighting");
            return;
        }

        _originalMaterial = _spriteNode.Material;

        var shader = GD.Load<Shader>("res://shaders/highlight_outline.gdshader");
        if (shader is null)
        {
            GD.PrintErr("LootableContainer: Failed to load highlight shader");
            return;
        }

        _highlightMaterial = new ShaderMaterial { Shader = shader };
        _highlightMaterial.SetShaderParameter(HighlightColorParam, new Color(1f, 1f, 0f, 0.5f));
    }

    private void UpdateHighlightVisual()
    {
        if (_spriteNode is null || _highlightMaterial is null)
        {
            return;
        }

        var shouldHighlight = _isHighlighted || _isInteractionHighlighted;
        _spriteNode.Material = shouldHighlight ? _highlightMaterial : _originalMaterial;
        _highlightTime = 0f;
    }

    public IReadOnlyList<InventoryItem?> GetItems() => _items;

    public InventoryItem? GetItemAt(int index)
    {
        return index >= 0 && index < _items.Length ? _items[index] : null;
    }

    public bool SetItemAt(int index, InventoryItem? item)
    {
        if (index < 0 || index >= _items.Length)
        {
            return false;
        }

        _items[index] = item;
        return true;
    }

    public InventoryItem? TakeItemAt(int index)
    {
        if (index < 0 || index >= _items.Length)
        {
            return null;
        }

        var item = _items[index];
        _items[index] = null;
        return item;
    }

    public void OnBecameEmpty()
    {
        // Default containers don't do anything when empty
    }

    public new Vector2 GetGlobalPosition() => GlobalPosition;

    /// <summary>
    /// Adds an item to the first available slot.
    /// </summary>
    public bool AddItem(InventoryItem item)
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (_items[i] is null)
            {
                _items[i] = item;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if the container is empty.
    /// </summary>
    public bool IsEmpty()
    {
        foreach (var item in _items)
        {
            if (item is not null)
            {
                return false;
            }
        }
        return true;
    }

    // IInteractable implementation
    public void OnInteract()
    {
        GD.Print($"LootableContainer: Interacting with {DisplayName}");
        InventoryScreen.Instance?.OpenWithLootable(this);
    }

    public Vector2 GetInteractionPosition() => GlobalPosition;

    public Area2D? GetInteractionArea() => _interactionArea;
}

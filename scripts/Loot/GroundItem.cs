namespace DAIgame.Loot;

using System.Collections.Generic;
using DAIgame.Core;
using DAIgame.Core.Items;
using DAIgame.UI;
using Godot;

/// <summary>
/// A single item on the ground that can be looted.
/// </summary>
public partial class GroundItem : Node2D, ILootable, IInteractable
{
    private const float HighlightPulseSpeed = 4f;
    private const float HighlightMinAlpha = 0.3f;
    private const float HighlightMaxAlpha = 0.8f;
    private static readonly StringName HighlightColorParam = new("highlight_color");

    private Item? _item;
    private bool _isHighlighted;
    private bool _isInteractionHighlighted;
    private ShaderMaterial? _highlightMaterial;
    private Sprite2D? _sprite;
    private float _highlightTime;
    private Area2D? _interactionArea;

    public string LootDisplayName => "On Ground";
    public int SlotCount => 1;
    public bool RemoveWhenEmpty => true;
    public string InteractionTooltip => "Pick Up";

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
        AddToGroup("ground_item");

        _sprite = new Sprite2D
        {
            Centered = true
        };
        AddChild(_sprite);

        UpdateVisual();
        SetupHighlightShader();
    }

    public override void _Process(double delta)
    {
        var shouldHighlight = _isHighlighted || _isInteractionHighlighted;
        if (!shouldHighlight || _highlightMaterial is null)
        {
            return;
        }

        _highlightTime += (float)delta * HighlightPulseSpeed;
        var alpha = Mathf.Lerp(HighlightMinAlpha, HighlightMaxAlpha,
            (Mathf.Sin(_highlightTime) + 1f) * 0.5f);
        var color = _isInteractionHighlighted
            ? new Color(0.5f, 1f, 0.5f, alpha)
            : new Color(1f, 1f, 0f, alpha);
        _highlightMaterial.SetShaderParameter(HighlightColorParam, color);
    }

    private void SetupHighlightShader()
    {
        if (_sprite is null)
        {
            return;
        }

        var shader = GD.Load<Shader>("res://shaders/highlight_outline.gdshader");
        if (shader is null)
        {
            GD.PrintErr("GroundItem: Failed to load highlight shader");
            return;
        }

        _highlightMaterial = new ShaderMaterial { Shader = shader };
        _highlightMaterial.SetShaderParameter(HighlightColorParam, new Color(1f, 1f, 0f, 0.5f));
    }

    private void UpdateHighlightVisual()
    {
        if (_sprite is null || _highlightMaterial is null)
        {
            return;
        }

        var shouldHighlight = _isHighlighted || _isInteractionHighlighted;
        _sprite.Material = shouldHighlight ? _highlightMaterial : null;
        _highlightTime = 0f;
    }

    private void UpdateVisual()
    {
        if (_sprite is null)
        {
            return;
        }

        _sprite.Texture = _item?.Icon;
    }

    /// <summary>
    /// Sets the item this ground item represents.
    /// </summary>
    public void SetItem(Item item)
    {
        _item = item;
        UpdateVisual();
    }

    public IReadOnlyList<Item?> GetItems() => [_item];

    public Item? GetItemAt(int index) => index == 0 ? _item : null;

    public bool SetItemAt(int index, Item? item)
    {
        if (index != 0)
        {
            return false;
        }

        _item = item;
        UpdateVisual();
        return true;
    }

    public Item? TakeItemAt(int index)
    {
        if (index != 0)
        {
            return null;
        }

        var item = _item;
        _item = null;
        UpdateVisual();
        return item;
    }

    public new Vector2 GetGlobalPosition() => GlobalPosition;

    public void OnBecameEmpty() => QueueFree();

    // IInteractable implementation
    public void OnInteract()
    {
        GD.Print($"GroundItem: Interacting with ground item");
        InventoryScreen.Instance?.OpenWithLootable(this);
    }

    public Vector2 GetInteractionPosition() => GlobalPosition;

    public Area2D? GetInteractionArea() => _interactionArea;

    /// <summary>
    /// Creates a ground item at the specified position with the given item.
    /// </summary>
    public static GroundItem Create(Item item, Vector2 position)
    {
        var groundItem = new GroundItem
        {
            GlobalPosition = position,
            _item = item
        };
        return groundItem;
    }
}

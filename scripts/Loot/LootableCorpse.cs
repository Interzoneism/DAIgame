namespace DAIgame.Loot;

using System.Collections.Generic;
using DAIgame.Combat;
using DAIgame.Core;
using DAIgame.Player;
using DAIgame.UI;
using Godot;

/// <summary>
/// A zombie corpse that can be looted. Contains random weapon and ammo.
/// Replaces ZombieCorpse as the corpse to spawn from zombies.
/// </summary>
public partial class LootableCorpse : CharacterBody2D, ILootable, IInteractable
{
    private const float Friction = 600f;
    private const float SettleThreshold = 5f;
    private const float HighlightPulseSpeed = 4f;
    private const float HighlightMinAlpha = 0.3f;
    private const float HighlightMaxAlpha = 0.8f;
    private const int LootSlots = 4;
    private static readonly StringName HighlightColorParam = new("highlight_color");

    /// <summary>
    /// Display name shown in the loot UI.
    /// </summary>
    [Export]
    public string DisplayName { get; set; } = "Corpse";

    private AnimatedSprite2D? _sprite;
    private bool _hasSettled;
    private InventoryItem?[] _items = new InventoryItem?[LootSlots];
    private bool _isHighlighted;
    private bool _isInteractionHighlighted;
    private ShaderMaterial? _highlightMaterial;
    private float _highlightTime;
    private bool _lootGenerated;
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

    /// <summary>
    /// Sets the initial velocity for the corpse.
    /// </summary>
    public new void SetVelocity(Vector2 velocity) => Velocity = velocity;

    public override void _Ready()
    {
        base._Ready();
        ZIndex = 3;
        AddToGroup("lootable_container");

        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (_sprite is not null)
        {
            _sprite.Centered = true;
            _sprite.FlipH = true;

            _sprite.Animation = "corpse";
            if (_sprite.SpriteFrames is not null && _sprite.SpriteFrames.HasAnimation("corpse"))
            {
                var frameCount = _sprite.SpriteFrames.GetFrameCount("corpse");
                if (frameCount > 0)
                {
                    _sprite.Frame = (int)GD.Randi() % frameCount;
                    _sprite.Pause();
                }

                var frameTexture = _sprite.SpriteFrames.GetFrameTexture("corpse", 0);
                if (frameTexture is not null)
                {
                    var size = frameTexture.GetSize();
                    var collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
                    if (collisionShape?.Shape is RectangleShape2D rect)
                    {
                        rect.Size = new Vector2(size.X * 0.9f, size.Y * 0.4f);
                    }
                }
            }
        }

        GenerateRandomLoot();
        SetupHighlightShader();

        // Look for interaction area if present
        _interactionArea = GetNodeOrNull<Area2D>("InteractionArea");
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

    public override void _PhysicsProcess(double delta)
    {
        if (_hasSettled)
        {
            return;
        }

        if (Velocity.LengthSquared() > SettleThreshold)
        {
            Velocity = Velocity.MoveToward(Vector2.Zero, Friction * (float)delta);
            MoveAndSlide();
        }
        else
        {
            ConvertToStatic();
        }
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
            GD.PrintErr("LootableCorpse: Failed to load highlight shader");
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

    private void GenerateRandomLoot()
    {
        if (_lootGenerated)
        {
            return;
        }

        _lootGenerated = true;

        // Generate a random weapon
        var weaponItem = GenerateRandomWeapon();
        if (weaponItem is not null)
        {
            _items[0] = weaponItem;
            GD.Print($"LootableCorpse: Generated weapon '{weaponItem.DisplayName}'");
        }

        // Generate random ammo
        var ammoItem = GenerateRandomAmmo();
        if (ammoItem is not null)
        {
            _items[1] = ammoItem;
            GD.Print($"LootableCorpse: Generated ammo '{ammoItem.DisplayName}' x{ammoItem.StackCount}");
        }
    }

    private static InventoryItem? GenerateRandomWeapon()
    {
        // Available weapons
        var weaponPaths = new[]
        {
            "res://data/weapons/pistol.tres",
            "res://data/weapons/shotgun.tres",
            "res://data/weapons/uzi.tres",
            "res://data/weapons/bat.tres"
        };

        var iconPaths = new Dictionary<string, string>
        {
            { "pistol", "res://assets/sprites/items/icons/icon_weapon_ranged_pistol.png" },
            { "shotgun", "res://assets/sprites/items/icons/icon_weapon_ranged_shotgun.png" },
            { "uzi", "res://assets/sprites/items/icons/icon_weapon_ranged_uzi.png" },
            { "bat", "res://assets/sprites/items/icons/icon_weapon_melee_bat.png" }
        };

        var idx = (int)(GD.Randi() % weaponPaths.Length);
        var weapon = ResourceLoader.Load<WeaponData>(weaponPaths[idx]);
        if (weapon is null)
        {
            GD.PrintErr($"LootableCorpse: Failed to load weapon at {weaponPaths[idx]}");
            return null;
        }

        // Try to load a specific icon, fallback to uzi icon
        var iconPath = iconPaths.TryGetValue(weapon.WeaponId, out var path) ? path : "res://assets/sprites/icon_uzi.png";
        var icon = ResourceLoader.Load<Texture2D>(iconPath);

        return new InventoryItem
        {
            ItemId = weapon.WeaponId,
            DisplayName = weapon.DisplayName,
            ItemType = InventoryItemType.Weapon,
            Icon = icon,
            StackCount = 1,
            MaxStack = 1,
            WeaponData = weapon
        };
    }

    private static InventoryItem? GenerateRandomAmmo()
    {
        var ammoTypes = new[]
            {
                (AmmoType.Small, "Ammo (Small)", "res://assets/sprites/items/icons/icon_ammo_small.png", 10, 30),
                (AmmoType.Rifle, "Ammo (Rifle)", "res://assets/sprites/items/icons/icon_ammo_rifle.png", 5, 20),
                (AmmoType.Shotgun, "Ammo (Shotgun)", "res://assets/sprites/items/icons/icon_ammo_shotgun.png", 4, 12)
            };
        var idx = (int)(GD.Randi() % ammoTypes.Length);
        var (ammoType, displayName, iconPath, minAmount, maxAmount) = ammoTypes[idx];

        var icon = ResourceLoader.Load<Texture2D>(iconPath);
        if (icon is null)
        {
            GD.PrintErr($"LootableCorpse: Failed to load ammo icon at {iconPath}");
            return null;
        }

        var amount = (int)(GD.Randi() % (maxAmount - minAmount + 1)) + minAmount;

        return new InventoryItem
        {
            ItemId = $"ammo_{ammoType.ToString().ToLowerInvariant()}",
            DisplayName = displayName,
            ItemType = InventoryItemType.Ammo,
            AmmoType = ammoType,
            Icon = icon,
            StackCount = amount,
            MaxStack = 300
        };
    }

    private void ConvertToStatic()
    {
        _hasSettled = true;
        SetPhysicsProcess(false);
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

    public new Vector2 GetGlobalPosition() => GlobalPosition;

    public void OnBecameEmpty()
    {
        // Corpses remain even when looted
    }

    // IInteractable implementation
    public void OnInteract()
    {
        GD.Print($"LootableCorpse: Interacting with {DisplayName}");
        InventoryScreen.Instance?.OpenWithLootable(this);
    }

    public Vector2 GetInteractionPosition() => GlobalPosition;

    public Area2D? GetInteractionArea() => _interactionArea;
}

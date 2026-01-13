namespace DAIgame.UI;

using DAIgame.Combat;
using DAIgame.Core;
using DAIgame.Player;
using Godot;

/// <summary>
/// Game HUD displaying health, stamina, cold exposure, weapon info, and other vital stats.
/// </summary>
public partial class GameHUD : CanvasLayer
{
    private ProgressBar? _healthBar;
    private ProgressBar? _staminaBar;
    private ProgressBar? _coldBar;
    private Label? _healthLabel;
    private Label? _staminaLabel;
    private Label? _coldLabel;
    private Label? _timeLabel;
    private Label? _slowMoLabel;
    private PlayerController? _player;

    // Weapon HUD elements
    private VBoxContainer? _weaponContainer;
    private Label? _weaponNameLabel;
    private Label? _ammoLabel;
    private ProgressBar? _reloadBar;
    private WeaponManager? _weaponManager;

    // Interaction tooltip
    private PanelContainer? _interactionTooltip;
    private Label? _interactionLabel;
    private InteractionManager? _interactionManager;

    public override void _Ready()
    {
        // Create the HUD elements
        CreateHUDElements();

        // Find player reference
        UpdatePlayerReference();

        // Subscribe to GameManager signals
        var gm = GameManager.Instance;
        if (gm is not null)
        {
            gm.ColdExposureChanged += OnColdExposureChanged;
        }

        // Connect to InteractionManager when available
        CallDeferred(MethodName.ConnectToInteractionManager);
    }

    private void ConnectToInteractionManager()
    {
        _interactionManager = InteractionManager.Instance;
        if (_interactionManager is not null)
        {
            _interactionManager.HoveredInteractableChanged += OnHoveredInteractableChanged;
        }
    }

    private void OnHoveredInteractableChanged(Node? interactable)
    {
        UpdateInteractionTooltip();
    }

    private void CreateHUDElements()
    {
        // Main container
        var container = new VBoxContainer
        {
            AnchorLeft = 0f,
            AnchorTop = 0f,
            AnchorRight = 0f,
            AnchorBottom = 0f,
            OffsetLeft = 10f,
            OffsetTop = 10f,
            OffsetRight = 200f,
            OffsetBottom = 120f
        };
        AddChild(container);

        // Health bar
        var healthContainer = new HBoxContainer();
        container.AddChild(healthContainer);

        var healthIcon = new Label { Text = "♥", CustomMinimumSize = new Vector2(20, 0) };
        healthContainer.AddChild(healthIcon);

        _healthBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(120, 20),
            Value = 100,
            ShowPercentage = false
        };
        healthContainer.AddChild(_healthBar);

        _healthLabel = new Label { Text = "100/100" };
        healthContainer.AddChild(_healthLabel);

        // Stamina bar
        var staminaContainer = new HBoxContainer();
        container.AddChild(staminaContainer);

        var staminaIcon = new Label { Text = "⚡", CustomMinimumSize = new Vector2(20, 0) };
        staminaContainer.AddChild(staminaIcon);

        _staminaBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(120, 16),
            Value = 100,
            ShowPercentage = false
        };
        staminaContainer.AddChild(_staminaBar);

        _staminaLabel = new Label { Text = "100%" };
        staminaContainer.AddChild(_staminaLabel);

        // Cold bar
        var coldContainer = new HBoxContainer();
        container.AddChild(coldContainer);

        var coldIcon = new Label { Text = "❄", CustomMinimumSize = new Vector2(20, 0) };
        coldContainer.AddChild(coldIcon);

        _coldBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(120, 20),
            Value = 0,
            ShowPercentage = false
        };
        coldContainer.AddChild(_coldBar);

        _coldLabel = new Label { Text = "0%" };
        coldContainer.AddChild(_coldLabel);

        // Time display
        _timeLabel = new Label
        {
            Text = "06:00",
            CustomMinimumSize = new Vector2(80, 20)
        };
        container.AddChild(_timeLabel);

        // Slow-mo indicator (bottom left)
        _slowMoLabel = new Label
        {
            Text = "",
            AnchorLeft = 0f,
            AnchorTop = 1f,
            AnchorRight = 0f,
            AnchorBottom = 1f,
            OffsetLeft = 10f,
            OffsetTop = -40f,
            OffsetRight = 200f,
            OffsetBottom = -10f,
            Modulate = new Color(1f, 0.3f, 0.3f)
        };
        AddChild(_slowMoLabel);

        // Weapon HUD (bottom right)
        CreateWeaponHUD();
    }

    private void CreateWeaponHUD()
    {
        _weaponContainer = new VBoxContainer
        {
            AnchorLeft = 1f,
            AnchorTop = 1f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -160f,
            OffsetTop = -80f,
            OffsetRight = -10f,
            OffsetBottom = -10f
        };
        AddChild(_weaponContainer);

        // Weapon name
        _weaponNameLabel = new Label
        {
            Text = "No Weapon",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _weaponContainer.AddChild(_weaponNameLabel);

        // Ammo display
        var ammoContainer = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End
        };
        _weaponContainer.AddChild(ammoContainer);

        _ammoLabel = new Label
        {
            Text = "0 / 0",
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(80, 20)
        };
        ammoContainer.AddChild(_ammoLabel);

        // Reload bar
        _reloadBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(120, 8),
            Value = 0,
            ShowPercentage = false,
            Visible = false
        };
        _weaponContainer.AddChild(_reloadBar);

        // Interaction tooltip (centered, near bottom)
        CreateInteractionTooltip();
    }

    private void CreateInteractionTooltip()
    {
        _interactionTooltip = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorTop = 1f,
            AnchorRight = 0.5f,
            AnchorBottom = 1f,
            OffsetLeft = -60f,
            OffsetTop = -80f,
            OffsetRight = 60f,
            OffsetBottom = -50f,
            Visible = false
        };
        AddChild(_interactionTooltip);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        _interactionTooltip.AddChild(margin);

        _interactionLabel = new Label
        {
            Text = "[E] Interact",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        margin.AddChild(_interactionLabel);
    }

    private void UpdateInteractionTooltip()
    {
        if (_interactionTooltip is null || _interactionLabel is null)
        {
            return;
        }

        var interactable = InteractionManager.Instance?.HoveredInteractable;
        if (interactable is null)
        {
            _interactionTooltip.Visible = false;
            return;
        }

        _interactionLabel.Text = $"[E] {interactable.InteractionTooltip}";
        _interactionTooltip.Visible = true;
    }

    public override void _Process(double delta)
    {
        UpdatePlayerReference();
        UpdateWeaponManagerReference();
        UpdateHealthDisplay();
        UpdateColdDisplay();
        UpdateTimeDisplay();
        UpdateSlowMoDisplay();
        UpdateWeaponDisplay();
        UpdateInteractionTooltip();
    }

    private void UpdatePlayerReference()
    {
        if (_player is not null && IsInstanceValid(_player))
        {
            return;
        }

        var players = GetTree().GetNodesInGroup("player");
        _player = players.Count > 0 ? players[0] as PlayerController : null;
        _weaponManager = null; // Reset weapon manager when player changes
    }

    private void UpdateWeaponManagerReference()
    {
        if (_weaponManager is not null && IsInstanceValid(_weaponManager))
        {
            return;
        }

        if (_player is null)
        {
            _weaponManager = null;
            return;
        }

        _weaponManager = _player.GetNodeOrNull<WeaponManager>("WeaponManager");
    }

    private void UpdateWeaponDisplay()
    {
        if (_weaponNameLabel is null || _ammoLabel is null || _reloadBar is null)
        {
            return;
        }

        if (_weaponManager is null)
        {
            _weaponNameLabel.Text = "No Weapon";
            _ammoLabel.Text = "- / -";
            _reloadBar.Visible = false;
            return;
        }

        var weapon = _weaponManager.CurrentWeapon;
        if (weapon is null)
        {
            _weaponNameLabel.Text = "No Weapon";
            _ammoLabel.Text = "- / -";
            _reloadBar.Visible = false;
            return;
        }

        _weaponNameLabel.Text = weapon.DisplayName;
        _ammoLabel.Text = $"{_weaponManager.CurrentAmmo} / {weapon.MagazineSize}";

        // Show reload bar if reloading
        if (_weaponManager.IsReloading)
        {
            _reloadBar.Visible = true;
            _reloadBar.Value = _weaponManager.ReloadProgress * 100f;
            _reloadBar.Modulate = new Color(1f, 0.8f, 0.2f);
        }
        else
        {
            _reloadBar.Visible = false;
        }

        // Color ammo based on remaining
        var ammoPercent = (float)_weaponManager.CurrentAmmo / weapon.MagazineSize;
        _ammoLabel.Modulate = ammoPercent > 0.3f
            ? Colors.White
            : ammoPercent > 0f
                ? new Color(1f, 0.8f, 0.2f)
                : new Color(1f, 0.3f, 0.3f);
    }

    private void UpdateHealthDisplay()
    {
        if (_player is null || _healthBar is null || _healthLabel is null)
        {
            return;
        }

        var healthPercent = _player.CurrentHealth / _player.MaxHealth * 100f;
        _healthBar.Value = healthPercent;
        _healthLabel.Text = $"{Mathf.CeilToInt(_player.CurrentHealth)}/{Mathf.CeilToInt(_player.MaxHealth)}";


        // Color the bar based on health
        _ = _healthBar.GetThemeStylebox("fill") as StyleBoxFlat;
        _healthBar.Modulate = healthPercent > 50 ? new Color(0.2f, 0.8f, 0.2f) : healthPercent > 25 ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.2f, 0.2f);

        UpdateStaminaDisplay();
    }

    private void UpdateStaminaDisplay()
    {
        if (_player is null || _staminaBar is null || _staminaLabel is null)
        {
            return;
        }

        var staminaPercent = _player.CurrentStamina / _player.MaxStamina * 100f;
        _staminaBar.Value = staminaPercent;
        _staminaLabel.Text = $"{Mathf.CeilToInt(staminaPercent)}%";

        // Color the bar based on stamina level (yellow when full, orange when low)
        _staminaBar.Modulate = staminaPercent > 50 ? new Color(1f, 0.9f, 0.2f) : staminaPercent > 25 ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.4f, 0.2f);
    }

    private void UpdateColdDisplay()
    {
        if (_coldBar is null || _coldLabel is null)
        {
            return;
        }

        var gm = GameManager.Instance;
        if (gm is null)
        {
            return;
        }

        var coldPercent = gm.GetColdExposureNormalized() * 100f;
        _coldBar.Value = coldPercent;
        _coldLabel.Text = $"{Mathf.CeilToInt(coldPercent)}%";

        // Color based on cold level
        _coldBar.Modulate = coldPercent < 33 ? new Color(0.5f, 0.8f, 1f) : coldPercent < 66 ? new Color(0.3f, 0.5f, 1f) : new Color(0.6f, 0.2f, 1f);
    }

    private void UpdateTimeDisplay()
    {
        if (_timeLabel is null)
        {
            return;
        }

        var gm = GameManager.Instance;
        if (gm is null)
        {
            return;
        }

        // Convert normalized time to 24h clock
        var totalMinutes = gm.TimeOfDay * 24f * 60f;
        var hours = Mathf.FloorToInt(totalMinutes / 60f) % 24;
        var minutes = Mathf.FloorToInt(totalMinutes % 60f);

        var dayNight = gm.IsNight ? "🌙" : "☀";
        _timeLabel.Text = $"{dayNight} {hours:D2}:{minutes:D2}";
    }

    private void UpdateSlowMoDisplay()
    {
        if (_slowMoLabel is null)
        {
            return;
        }

        var gm = GameManager.Instance;
        _slowMoLabel.Text = gm is not null && gm.IsSlowMoActive ? "◆ SLOW-MO ◆" : "";
    }

    private void OnColdExposureChanged(float exposure, float maxExposure)
    {
        // Additional feedback could go here (screen effects, sounds, etc.)
    }

    public override void _ExitTree()
    {
        var gm = GameManager.Instance;
        if (gm is not null)
        {
            gm.ColdExposureChanged -= OnColdExposureChanged;
        }

        if (_interactionManager is not null)
        {
            _interactionManager.HoveredInteractableChanged -= OnHoveredInteractableChanged;
        }
    }
}

namespace DAIgame.UI;

using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private Label? _debugStatsLabel;
    private PanelContainer? _debugVariablesPanel;
    private LineEdit? _holdOffsetXInput;
    private LineEdit? _holdOffsetYInput;
    private LineEdit? _spawnOffsetXInput;
    private LineEdit? _spawnOffsetYInput;
    private Button? _debugSaveButton;
    private PlayerController? _player;
    private PlayerStatsManager? _statsManager;
    private bool _debugMode;
    private bool _debugDirty = true;
    private float _lastDisplayedAccuracy = -1f;
    private float _lastDisplayedRecoilPenalty = -1f;
    private bool _suppressDebugInputEvents;

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

        _debugStatsLabel = new Label
        {
            Text = "",
            Visible = false,
            CustomMinimumSize = new Vector2(220, 0)
        };
        container.AddChild(_debugStatsLabel);

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

        // Debug variables window (top right)
        CreateDebugVariablesPanel();
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

    private void CreateDebugVariablesPanel()
    {
        _debugVariablesPanel = new PanelContainer
        {
            AnchorLeft = 1f,
            AnchorTop = 0f,
            AnchorRight = 1f,
            AnchorBottom = 0f,
            OffsetLeft = -260f,
            OffsetTop = 10f,
            OffsetRight = -10f,
            OffsetBottom = 170f,
            Visible = false
        };
        AddChild(_debugVariablesPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _debugVariablesPanel.AddChild(margin);

        var layout = new VBoxContainer();
        margin.AddChild(layout);

        var title = new Label { Text = "DEBUG VARIABLES" };
        layout.AddChild(title);

        var holdLabel = new Label { Text = "HoldOffset" };
        layout.AddChild(holdLabel);

        var holdRow = new HBoxContainer();
        layout.AddChild(holdRow);

        holdRow.AddChild(new Label { Text = "X" });
        _holdOffsetXInput = new LineEdit { CustomMinimumSize = new Vector2(70f, 0f) };
        _holdOffsetXInput.TextChanged += OnHoldOffsetXChanged;
        holdRow.AddChild(_holdOffsetXInput);

        holdRow.AddChild(new Label { Text = "Y" });
        _holdOffsetYInput = new LineEdit { CustomMinimumSize = new Vector2(70f, 0f) };
        _holdOffsetYInput.TextChanged += OnHoldOffsetYChanged;
        holdRow.AddChild(_holdOffsetYInput);

        var spawnLabel = new Label { Text = "SpawnOffset" };
        layout.AddChild(spawnLabel);

        var spawnRow = new HBoxContainer();
        layout.AddChild(spawnRow);

        spawnRow.AddChild(new Label { Text = "X" });
        _spawnOffsetXInput = new LineEdit { CustomMinimumSize = new Vector2(70f, 0f) };
        _spawnOffsetXInput.TextChanged += OnSpawnOffsetXChanged;
        spawnRow.AddChild(_spawnOffsetXInput);

        spawnRow.AddChild(new Label { Text = "Y" });
        _spawnOffsetYInput = new LineEdit { CustomMinimumSize = new Vector2(70f, 0f) };
        _spawnOffsetYInput.TextChanged += OnSpawnOffsetYChanged;
        spawnRow.AddChild(_spawnOffsetYInput);

        _debugSaveButton = new Button { Text = "Save" };
        _debugSaveButton.Pressed += OnSaveDebugVariablesPressed;
        layout.AddChild(_debugSaveButton);
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
        HandleDebugToggle();
        UpdatePlayerReference();
        UpdateStatsManagerReference();
        UpdateWeaponManagerReference();
        UpdateHealthDisplay();
        UpdateColdDisplay();
        UpdateTimeDisplay();
        UpdateSlowMoDisplay();
        UpdateDebugStatsDisplay();
        UpdateWeaponDisplay();
        UpdateInteractionTooltip();
    }

    private void UpdatePlayerReference()
    {
        if (_player is not null && IsInstanceValid(_player))
        {
            return;
        }

        DisconnectWeaponManagerSignals();
        if (_statsManager is not null && IsInstanceValid(_statsManager))
        {
            _statsManager.StatsRecalculated -= OnStatsRecalculated;
        }

        var players = GetTree().GetNodesInGroup("player");
        _player = players.Count > 0 ? players[0] as PlayerController : null;
        _weaponManager = null; // Reset weapon manager when player changes
        _statsManager = null; // Reset stats manager when player changes
        ResetAccuracyTracking();
        _debugDirty = true;
    }

    private void UpdateStatsManagerReference()
    {
        if (_statsManager is not null && IsInstanceValid(_statsManager))
        {
            return;
        }

        if (_player is null)
        {
            _statsManager = null;
            _debugDirty = true;
            return;
        }

        _statsManager = _player.GetNodeOrNull<PlayerStatsManager>("PlayerStatsManager");
        if (_statsManager is null)
        {
            foreach (var child in _player.GetChildren())
            {
                if (child is PlayerStatsManager manager)
                {
                    _statsManager = manager;
                    break;
                }
            }
        }
        if (_statsManager is not null)
        {
            _statsManager.StatsRecalculated += OnStatsRecalculated;
            _debugDirty = true;
        }
    }

    private void HandleDebugToggle()
    {
        if (!Input.IsActionJustPressed("DebugMode"))
        {
            return;
        }

        _debugMode = !_debugMode;
        if (_debugStatsLabel is not null)
        {
            _debugStatsLabel.Visible = _debugMode;
        }
        if (_debugVariablesPanel is not null)
        {
            _debugVariablesPanel.Visible = _debugMode;
        }
        if (_debugMode)
        {
            UpdateDebugVariableFields();
        }
        _debugDirty = true;
    }

    private void UpdateWeaponManagerReference()
    {
        if (_player is null)
        {
            DisconnectWeaponManagerSignals();
            return;
        }

        if (_weaponManager is not null && IsInstanceValid(_weaponManager))
        {
            return;
        }

        DisconnectWeaponManagerSignals();

        _weaponManager = _player.GetNodeOrNull<WeaponManager>("WeaponManager");
        if (_weaponManager is null)
        {
            foreach (var child in _player.GetChildren())
            {
                if (child is WeaponManager manager)
                {
                    _weaponManager = manager;
                    break;
                }
            }
        }

        if (_weaponManager is not null)
        {
            _weaponManager.WeaponChanged += OnWeaponChanged;
            _weaponManager.WeaponFired += OnWeaponFired;
            ResetAccuracyTracking();
            _debugDirty = true;
        }
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

    private void UpdateDebugStatsDisplay()
    {
        if (!_debugMode || _debugStatsLabel is null)
        {
            return;
        }

        var accuracyPercent = GetCurrentAccuracyPercent();
        var recoilPenalty = GetCurrentRecoilPenalty();

        if (!_debugDirty && (!Mathf.IsEqualApprox(accuracyPercent, _lastDisplayedAccuracy) || !Mathf.IsEqualApprox(recoilPenalty, _lastDisplayedRecoilPenalty)))
        {
            _debugDirty = true;
        }

        if (!_debugDirty)
        {
            return;
        }

        var weaponName = _weaponManager?.CurrentWeapon?.DisplayName ?? "No Weapon";

        // Set a smaller line spacing for the debug stats label
        _debugStatsLabel.AddThemeConstantOverride("line_spacing", -5);

        var sb = new StringBuilder();
        sb.AppendLine("DEBUG STATS");
        sb.AppendLine($"Accuracy: {accuracyPercent:F1}% ({weaponName}, recoil {recoilPenalty:F1}%)");

        if (_statsManager is null)
        {
            sb.AppendLine("(No stats manager)");
            _debugStatsLabel.Text = sb.ToString().TrimEnd();
            _lastDisplayedAccuracy = accuracyPercent;
            _lastDisplayedRecoilPenalty = recoilPenalty;
            _debugDirty = false;
            return;
        }

        foreach (StatType stat in System.Enum.GetValues<StatType>())
        {
            sb.AppendLine($"{stat}: {_statsManager.GetStat(stat):F2}");
        }

        _debugStatsLabel.Text = sb.ToString().TrimEnd();
        _lastDisplayedAccuracy = accuracyPercent;
        _lastDisplayedRecoilPenalty = recoilPenalty;
        _debugDirty = false;
    }

    private void OnColdExposureChanged(float exposure, float maxExposure)
    {
        // Additional feedback could go here (screen effects, sounds, etc.)
    }

    private void OnStatsRecalculated()
    {
        _debugDirty = true;
    }

    private void OnWeaponChanged(WeaponData? weapon)
    {
        ResetAccuracyTracking();
        UpdateDebugVariableFields();
        _debugDirty = true;
    }

    private void OnWeaponFired(WeaponData weapon, Vector2 firedDirection)
    {
        _debugDirty = true;
    }

    private float GetCurrentAccuracyPercent()
    {
        if (_weaponManager is null || !IsInstanceValid(_weaponManager))
        {
            return 100f;
        }

        return _weaponManager.GetTotalAccuracyPercent();
    }

    private float GetCurrentRecoilPenalty()
    {
        if (_weaponManager is null || !IsInstanceValid(_weaponManager))
        {
            return 0f;
        }

        return _weaponManager.CurrentRecoilPenalty;
    }

    private void DisconnectWeaponManagerSignals()
    {
        if (_weaponManager is null)
        {
            return;
        }

        if (IsInstanceValid(_weaponManager))
        {
            _weaponManager.WeaponChanged -= OnWeaponChanged;
            _weaponManager.WeaponFired -= OnWeaponFired;
        }

        _weaponManager = null;
    }

    private void ResetAccuracyTracking()
    {
        _lastDisplayedAccuracy = -1f;
        _lastDisplayedRecoilPenalty = -1f;
    }

    private void UpdateDebugVariableFields()
    {
        if (_holdOffsetXInput is null || _holdOffsetYInput is null || _spawnOffsetXInput is null || _spawnOffsetYInput is null || _debugSaveButton is null)
        {
            return;
        }

        var weapon = _weaponManager?.CurrentWeapon;
        _suppressDebugInputEvents = true;
        if (weapon is null)
        {
            _holdOffsetXInput.Text = "";
            _holdOffsetYInput.Text = "";
            _spawnOffsetXInput.Text = "";
            _spawnOffsetYInput.Text = "";
            _holdOffsetXInput.Editable = false;
            _holdOffsetYInput.Editable = false;
            _spawnOffsetXInput.Editable = false;
            _spawnOffsetYInput.Editable = false;
            _debugSaveButton.Disabled = true;
        }
        else
        {
            _holdOffsetXInput.Text = weapon.HoldOffset.X.ToString("0.###", CultureInfo.InvariantCulture);
            _holdOffsetYInput.Text = weapon.HoldOffset.Y.ToString("0.###", CultureInfo.InvariantCulture);
            _spawnOffsetXInput.Text = weapon.SpawnOffsetX.ToString("0.###", CultureInfo.InvariantCulture);
            _spawnOffsetYInput.Text = weapon.SpawnOffsetY.ToString("0.###", CultureInfo.InvariantCulture);
            _holdOffsetXInput.Editable = true;
            _holdOffsetYInput.Editable = true;
            _spawnOffsetXInput.Editable = true;
            _spawnOffsetYInput.Editable = true;
            _debugSaveButton.Disabled = false;
        }
        _suppressDebugInputEvents = false;
    }

    private void OnHoldOffsetXChanged(string text)
    {
        if (_suppressDebugInputEvents)
        {
            return;
        }

        if (TryParseFloat(text, out var value))
        {
            ApplyHoldOffsetChange(x: value);
        }
    }

    private void OnHoldOffsetYChanged(string text)
    {
        if (_suppressDebugInputEvents)
        {
            return;
        }

        if (TryParseFloat(text, out var value))
        {
            ApplyHoldOffsetChange(y: value);
        }
    }

    private void ApplyHoldOffsetChange(float? x = null, float? y = null)
    {
        var weapon = _weaponManager?.CurrentWeapon;
        if (weapon is null)
        {
            return;
        }

        var offset = weapon.HoldOffset;
        if (x.HasValue)
        {
            offset.X = x.Value;
        }
        if (y.HasValue)
        {
            offset.Y = y.Value;
        }
        weapon.HoldOffset = offset;
    }

    private void OnSpawnOffsetXChanged(string text)
    {
        if (_suppressDebugInputEvents)
        {
            return;
        }

        if (TryParseFloat(text, out var value))
        {
            ApplySpawnOffsetChange(x: value);
        }
    }

    private void OnSpawnOffsetYChanged(string text)
    {
        if (_suppressDebugInputEvents)
        {
            return;
        }

        if (TryParseFloat(text, out var value))
        {
            ApplySpawnOffsetChange(y: value);
        }
    }

    private void ApplySpawnOffsetChange(float? x = null, float? y = null)
    {
        var weapon = _weaponManager?.CurrentWeapon;
        if (weapon is null)
        {
            return;
        }

        if (x.HasValue)
        {
            weapon.SpawnOffsetX = x.Value;
        }
        if (y.HasValue)
        {
            weapon.SpawnOffsetY = y.Value;
        }
    }

    private void OnSaveDebugVariablesPressed()
    {
        if (_weaponManager?.CurrentWeapon is not WeaponData weapon)
        {
            GD.PrintErr("GameHUD: Cannot save debug variables; no weapon equipped.");
            return;
        }

        if (!TryGetHoldOffsetInput(out var holdOffset))
        {
            GD.PrintErr("GameHUD: Cannot save HoldOffset; invalid X or Y value.");
            return;
        }

        if (!TryGetSpawnOffsetInput(out var spawnOffset))
        {
            GD.PrintErr("GameHUD: Cannot save SpawnOffset; invalid X or Y value.");
            return;
        }

        weapon.HoldOffset = holdOffset;
        weapon.SpawnOffsetX = spawnOffset.X;
        weapon.SpawnOffsetY = spawnOffset.Y;

        var weaponId = weapon.WeaponId?.Trim();
        if (string.IsNullOrEmpty(weaponId))
        {
            GD.PrintErr("GameHUD: Cannot save debug variables; weapon has no WeaponId.");
            return;
        }

        var jsonPath = $"res://data/items/{weaponId}.json";
        var globalPath = ProjectSettings.GlobalizePath(jsonPath);
        if (!File.Exists(globalPath))
        {
            GD.PrintErr($"GameHUD: Weapon JSON not found at '{jsonPath}'.");
            return;
        }

        try
        {
            var json = File.ReadAllText(globalPath);
            var node = JsonNode.Parse(json) as JsonObject;
            if (node is null)
            {
                GD.PrintErr($"GameHUD: Failed to parse JSON for '{weaponId}'.");
                return;
            }

            var holdNode = new JsonObject
            {
                ["x"] = holdOffset.X,
                ["y"] = holdOffset.Y
            };
            node["HoldOffset"] = holdNode;
            node["SpawnOffsetX"] = spawnOffset.X;
            node["SpawnOffsetY"] = spawnOffset.Y;

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(globalPath, node.ToJsonString(options));
            GD.Print($"GameHUD: Saved HoldOffset ({holdOffset.X}, {holdOffset.Y}) and SpawnOffset ({spawnOffset.X}, {spawnOffset.Y}) to {jsonPath}.");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"GameHUD: Failed to save debug variables for '{weaponId}'. Error: {ex.Message}");
        }
    }

    private bool TryParseFloat(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private bool TryGetHoldOffsetInput(out Vector2 holdOffset)
    {
        holdOffset = Vector2.Zero;
        if (_holdOffsetXInput is null || _holdOffsetYInput is null)
        {
            return false;
        }

        if (!TryParseFloat(_holdOffsetXInput.Text, out var x))
        {
            return false;
        }

        if (!TryParseFloat(_holdOffsetYInput.Text, out var y))
        {
            return false;
        }

        holdOffset = new Vector2(x, y);
        return true;
    }

    private bool TryGetSpawnOffsetInput(out Vector2 spawnOffset)
    {
        spawnOffset = Vector2.Zero;
        if (_spawnOffsetXInput is null || _spawnOffsetYInput is null)
        {
            return false;
        }

        if (!TryParseFloat(_spawnOffsetXInput.Text, out var x))
        {
            return false;
        }

        if (!TryParseFloat(_spawnOffsetYInput.Text, out var y))
        {
            return false;
        }

        spawnOffset = new Vector2(x, y);
        return true;
    }

    public override void _ExitTree()
    {
        var gm = GameManager.Instance;
        if (gm is not null)
        {
            gm.ColdExposureChanged -= OnColdExposureChanged;
        }

        if (_statsManager is not null && IsInstanceValid(_statsManager))
        {
            _statsManager.StatsRecalculated -= OnStatsRecalculated;
        }

        DisconnectWeaponManagerSignals();

        if (_interactionManager is not null)
        {
            _interactionManager.HoveredInteractableChanged -= OnHoveredInteractableChanged;
        }
    }
}

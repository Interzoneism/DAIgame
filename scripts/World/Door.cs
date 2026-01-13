namespace DAIgame.World;

using DAIgame.Core;
using Godot;

/// <summary>
/// A door that can be opened or closed by the player hovering and pressing Interact (E).
/// When open, the door rotates 90 degrees clockwise from its starting rotation.
/// The door is anchored at the left side of the sprite.
/// </summary>
public partial class Door : StaticBody2D, IInteractable
{
	private const float HighlightPulseSpeed = 4f;
	private const float HighlightMinAlpha = 0.3f;
	private const float HighlightMaxAlpha = 0.8f;
	private static readonly StringName HighlightColorParam = new("highlight_color");

	/// <summary>
	/// Time in seconds to animate the door opening/closing.
	/// </summary>
	[Export]
	public float AnimationDuration { get; set; } = 0.3f;

	/// <summary>
	/// Whether the door starts in the open state.
	/// </summary>
	[Export]
	public bool StartsOpen { get; set; } = false;

	/// <summary>
	/// Current state of the door.
	/// </summary>
	public bool IsOpen { get; private set; }

	public string InteractionTooltip => IsOpen ? "Close" : "Open";

	private bool _isInteractionHighlighted;
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
			UpdateHighlightVisual();
		}
	}

	private float _targetRotation;
	private float _startRotation;
	private float _currentAnimationTime;
	private bool _isAnimating;
	private Area2D? _interactionArea;
	private ShaderMaterial? _highlightMaterial;
	private CanvasItem? _spriteNode;
	private Material? _originalMaterial;
	private float _highlightTime;

	public override void _Ready()
	{
		AddToGroup("door");

		_startRotation = Rotation;
		_targetRotation = _startRotation;

		// Set up interaction area (for hover detection only)
		_interactionArea = GetNodeOrNull<Area2D>("InteractionArea");
		if (_interactionArea is null)
		{
			GD.PrintErr("Door: InteractionArea not found! Player won't be able to interact.");
		}

		SetupHighlightShader();

		// Apply initial state if door starts open
		if (StartsOpen)
		{
			IsOpen = true;
			Rotation = _startRotation + Mathf.DegToRad(90);
			_targetRotation = Rotation;
		}
	}

	public override void _Process(double delta)
	{
		if (_isAnimating)
		{
			AnimateDoor((float)delta);
		}

		// Update highlight pulsing
		if (_isInteractionHighlighted && _highlightMaterial is not null)
		{
			_highlightTime += (float)delta * HighlightPulseSpeed;
			var alpha = Mathf.Lerp(HighlightMinAlpha, HighlightMaxAlpha,
				(Mathf.Sin(_highlightTime) + 1f) * 0.5f);
			_highlightMaterial.SetShaderParameter(HighlightColorParam, new Color(0.5f, 1f, 0.5f, alpha));
		}
	}

	private void SetupHighlightShader()
	{
		// Find the sprite node to apply the shader to
		_spriteNode = GetNodeOrNull<Sprite2D>("Sprite2D") as CanvasItem
			?? GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D") as CanvasItem;

		if (_spriteNode is null)
		{
			GD.PrintErr($"Door '{Name}': No Sprite2D or AnimatedSprite2D found for highlighting");
			return;
		}

		_originalMaterial = _spriteNode.Material;

		var shader = GD.Load<Shader>("res://shaders/highlight_outline.gdshader");
		if (shader is null)
		{
			GD.PrintErr("Door: Failed to load highlight shader");
			return;
		}

		_highlightMaterial = new ShaderMaterial { Shader = shader };
		_highlightMaterial.SetShaderParameter(HighlightColorParam, new Color(0.5f, 1f, 0.5f, 0.5f));
	}

	private void UpdateHighlightVisual()
	{
		if (_spriteNode is null || _highlightMaterial is null)
		{
			return;
		}

		_spriteNode.Material = _isInteractionHighlighted ? _highlightMaterial : _originalMaterial;
		_highlightTime = 0f;
	}

	// IInteractable implementation
	public void OnInteract()
	{
		ToggleDoor();
	}

	public Vector2 GetInteractionPosition() => GlobalPosition;

	public Area2D? GetInteractionArea() => _interactionArea;

	/// <summary>
	/// Toggles the door between open and closed states.
	/// </summary>
	public void ToggleDoor()
	{
		if (_isAnimating)
		{
			return;
		}

		IsOpen = !IsOpen;
		_isAnimating = true;
		_currentAnimationTime = 0f;

		// 90 degrees clockwise when opening
		_targetRotation = IsOpen
			? _startRotation + Mathf.DegToRad(90)
			: _startRotation;

		GD.Print($"Door: {(IsOpen ? "Opening" : "Closing")}");
	}

	/// <summary>
	/// Opens the door (if not already open).
	/// </summary>
	public void Open()
	{
		if (!IsOpen && !_isAnimating)
		{
			ToggleDoor();
		}
	}

	/// <summary>
	/// Closes the door (if not already closed).
	/// </summary>
	public void Close()
	{
		if (IsOpen && !_isAnimating)
		{
			ToggleDoor();
		}
	}

	private void AnimateDoor(float delta)
	{
		_currentAnimationTime += delta;
		var t = Mathf.Min(_currentAnimationTime / AnimationDuration, 1f);

		// Smooth easing
		var easedT = EaseOutQuad(t);
		var startAngle = IsOpen ? _startRotation : _startRotation + Mathf.DegToRad(90);
		Rotation = Mathf.LerpAngle(startAngle, _targetRotation, easedT);

		if (t >= 1f)
		{
			_isAnimating = false;
			Rotation = _targetRotation;
			GD.Print($"Door: {(IsOpen ? "Opened" : "Closed")}");
		}
	}

	private static float EaseOutQuad(float t) => 1f - ((1f - t) * (1f - t));
}

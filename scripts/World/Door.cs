namespace DAIgame.World;

using Godot;

/// <summary>
/// A door that can be opened or closed by the player pressing Interact (E).
/// When open, the door rotates 90 degrees clockwise from its starting rotation.
/// The door is anchored at the left side of the sprite.
/// </summary>
public partial class Door : StaticBody2D
{
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

	private float _targetRotation;
	private float _startRotation;
	private float _currentAnimationTime;
	private bool _isAnimating;
	private bool _playerInRange;
	private Area2D? _interactionArea;

	public override void _Ready()
	{
		AddToGroup("door");

		_startRotation = Rotation;
		_targetRotation = _startRotation;

		// Set up interaction area
		_interactionArea = GetNodeOrNull<Area2D>("InteractionArea");
		if (_interactionArea is not null)
		{
			_interactionArea.BodyEntered += OnBodyEntered;
			_interactionArea.BodyExited += OnBodyExited;
		}
		else
		{
			GD.PrintErr("Door: InteractionArea not found! Player won't be able to interact.");
		}

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

		// Check for player interaction when player is nearby
		if (_playerInRange && Input.IsActionJustPressed("Interact"))
		{
			ToggleDoor();
		}
	}

	private void OnBodyEntered(Node2D body)
	{
		if (body.IsInGroup("player"))
		{
			_playerInRange = true;
			GD.Print("Door: Player in range, press E to interact");
		}
	}

	private void OnBodyExited(Node2D body)
	{
		if (body.IsInGroup("player"))
		{
			_playerInRange = false;
		}
	}

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

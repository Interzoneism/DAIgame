namespace DAIgame.Core;

using DAIgame.Loot;
using Godot;

/// <summary>
/// Manages interaction highlighting and input for interactables in range.
/// Should be added as a child of the player.
/// Uses the same range as LootHighlighter for consistency.
/// </summary>
public partial class InteractionManager : Node
{
    /// <summary>
    /// The interactable currently under the mouse cursor (if in range).
    /// </summary>
    public IInteractable? HoveredInteractable { get; private set; }

    /// <summary>
    /// Emitted when the hovered interactable changes.
    /// </summary>
    [Signal]
    public delegate void HoveredInteractableChangedEventHandler(Node? interactable);

    private Node2D? _player;
    private IInteractable? _lastHighlighted;

    public static InteractionManager? Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        _player = GetParent<Node2D>();

        if (_player is null)
        {
            // ...existing code...
        }
    }

    public override void _Process(double delta) => UpdateHoveredInteractable();

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("Interact"))
        {
            if (HoveredInteractable is not null)
            {
                HoveredInteractable.OnInteract();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void UpdateHoveredInteractable()
    {
        if (_player is null)
        {
            return;
        }

        var playerPos = _player.GlobalPosition;
        var highlighter = LootHighlighter.Instance;
        var interactionRadius = highlighter?.HighlightRadius ?? 100f;
        var radiusSq = interactionRadius * interactionRadius;

        var mousePos = _player.GetGlobalMousePosition();
        IInteractable? newHovered = null;
        var closestDistSq = float.MaxValue;

        // Check all interactable groups
        CheckGroupForHover("door", playerPos, mousePos, radiusSq, ref newHovered, ref closestDistSq);
        CheckGroupForHover("lootable_container", playerPos, mousePos, radiusSq, ref newHovered, ref closestDistSq);
        CheckGroupForHover("ground_item", playerPos, mousePos, radiusSq, ref newHovered, ref closestDistSq);

        // Update highlighting
        if (newHovered != _lastHighlighted)
        {
            if (_lastHighlighted is not null)
            {
                _lastHighlighted.IsInteractionHighlighted = false;
            }

            if (newHovered is not null)
            {
                newHovered.IsInteractionHighlighted = true;
            }

            _lastHighlighted = newHovered;
        }

        if (HoveredInteractable != newHovered)
        {
            HoveredInteractable = newHovered;
            EmitSignal(SignalName.HoveredInteractableChanged, (Node?)newHovered);
        }
    }

    private void CheckGroupForHover(
        string groupName,
        Vector2 playerPos,
        Vector2 mousePos,
        float radiusSq,
        ref IInteractable? newHovered,
        ref float closestDistSq)
    {
        var nodes = GetTree().GetNodesInGroup(groupName);
        // ...existing code...

        foreach (var node in nodes)
        {
            if (node is not IInteractable interactable)
            {
                continue;
            }

            // Check if player is in range of the interactable
            var interactablePos = interactable.GetInteractionPosition();
            var distToPlayerSq = playerPos.DistanceSquaredTo(interactablePos);

            if (distToPlayerSq > radiusSq)
            {
                continue;
            }

            // Check if mouse is hovering over the interaction area
            var isMouseOver = IsMouseOverInteractable(interactable, mousePos);

            if (!isMouseOver)
            {
                continue;
            }

            // Pick the closest one to the mouse
            var distToMouseSq = mousePos.DistanceSquaredTo(interactablePos);
            if (distToMouseSq < closestDistSq)
            {
                closestDistSq = distToMouseSq;
                newHovered = interactable;
            }
        }
    }

    private static bool IsMouseOverInteractable(IInteractable interactable, Vector2 mousePos)
    {
        var area = interactable.GetInteractionArea();
        if (area is not null)
        {
            // Check each collision shape in the area
            foreach (var child in area.GetChildren())
            {
                if (child is not CollisionShape2D shapeNode)
                {
                    continue;
                }

                var shape = shapeNode.Shape;
                if (shape is null)
                {
                    continue;
                }

                // Transform mouse position to the shape's local space
                var localMousePos = shapeNode.GlobalTransform.AffineInverse() * mousePos;

                // Check if the point is inside the shape
                if (IsPointInShape(shape, localMousePos))
                {
                    return true;
                }
            }
            return false;
        }

        // Fallback: check if mouse is within a small radius of the position
        var pos = interactable.GetInteractionPosition();
        return mousePos.DistanceSquaredTo(pos) <= 400f; // 20 pixel radius
    }

    private static bool IsPointInShape(Shape2D shape, Vector2 point)
    {
        return shape switch
        {
            CircleShape2D circle => point.LengthSquared() <= circle.Radius * circle.Radius,
            RectangleShape2D rect => Mathf.Abs(point.X) <= rect.Size.X / 2f && Mathf.Abs(point.Y) <= rect.Size.Y / 2f,
            CapsuleShape2D capsule => IsPointInCapsule(capsule, point),
            SegmentShape2D segment => IsPointInSegment(segment, point),
            _ => false
        };
    }

    private static bool IsPointInCapsule(CapsuleShape2D capsule, Vector2 point)
    {
        // Capsule is a rectangle with rounded ends
        var halfHeight = capsule.Height / 2f;
        var radius = capsule.Radius;

        if (Mathf.Abs(point.Y) <= halfHeight - radius)
        {
            // Inside the rectangular part
            return Mathf.Abs(point.X) <= radius;
        }

        // Check the circular ends
        var capCenter = new Vector2(0, point.Y > 0 ? halfHeight - radius : -(halfHeight - radius));
        return point.DistanceSquaredTo(capCenter) <= radius * radius;
    }

    private static bool IsPointInSegment(SegmentShape2D segment, Vector2 point)
    {
        // For segments, check if point is close to the line
        var a = segment.A;
        var b = segment.B;

        // Calculate distance from point to line segment
        var ab = b - a;
        var ap = point - a;
        var projection = ap.Dot(ab) / ab.LengthSquared();
        projection = Mathf.Clamp(projection, 0f, 1f);
        var closestPoint = a + (ab * projection);

        // Consider a small threshold for hovering over a line
        const float LineThreshold = 5f;
        return point.DistanceSquaredTo(closestPoint) <= LineThreshold * LineThreshold;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

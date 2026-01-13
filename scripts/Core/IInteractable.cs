namespace DAIgame.Core;

using Godot;

/// <summary>
/// Interface for objects that can be interacted with when the player is in range and hovering over them.
/// Examples: doors, loot containers, switches, etc.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// The tooltip text shown when hovering over this interactable (e.g., "Open Door", "Interact").
    /// </summary>
    string InteractionTooltip { get; }

    /// <summary>
    /// Whether this interactable is currently highlighted (player in range and hovering).
    /// </summary>
    bool IsInteractionHighlighted { get; set; }

    /// <summary>
    /// Called when the player presses the Interact key while hovering over this interactable.
    /// </summary>
    void OnInteract();

    /// <summary>
    /// Gets the global position of this interactable for distance checks.
    /// </summary>
    Vector2 GetInteractionPosition();

    /// <summary>
    /// Gets the Area2D used for mouse hover detection.
    /// Returns null if no interaction area is defined (will use node bounds).
    /// </summary>
    Area2D? GetInteractionArea();
}

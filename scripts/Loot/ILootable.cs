namespace DAIgame.Loot;

using System.Collections.Generic;
using DAIgame.Core.Items;
using Godot;

/// <summary>
/// Interface for objects that can be looted (containers, ground items, corpses).
/// </summary>
public interface ILootable
{
    /// <summary>
    /// Display name shown in the loot UI (e.g., "Corpse", "Cabinet", "On Ground").
    /// </summary>
    string LootDisplayName { get; }

    /// <summary>
    /// Whether this lootable is currently highlighted (ViewLoot active and in range).
    /// </summary>
    bool IsHighlighted { get; set; }

    /// <summary>
    /// Gets all items in this lootable.
    /// </summary>
    IReadOnlyList<Item?> GetItems();

    /// <summary>
    /// Gets the item at a specific slot index.
    /// </summary>
    Item? GetItemAt(int index);

    /// <summary>
    /// Sets the item at a specific slot index (for placing items in containers).
    /// Returns true if successful.
    /// </summary>
    bool SetItemAt(int index, Item? item);

    /// <summary>
    /// Removes the item at a specific slot index and returns it.
    /// </summary>
    Item? TakeItemAt(int index);

    /// <summary>
    /// Gets the global position of this lootable for distance checks.
    /// </summary>
    Vector2 GetGlobalPosition();

    /// <summary>
    /// Gets the number of slots in this lootable.
    /// </summary>
    int SlotCount { get; }

    /// <summary>
    /// Whether this lootable should be removed when empty (e.g., ground items).
    /// </summary>
    bool RemoveWhenEmpty { get; }

    /// <summary>
    /// Called when this lootable becomes empty.
    /// </summary>
    void OnBecameEmpty();
}

namespace DAIgame.Combat;

using System.Collections.Generic;
using DAIgame.Core;
using Godot;
using Godot.Collections;

/// <summary>
/// Provides smart melee hit detection that respects obstacles.
/// Attacks cannot pass through walls, doors, or other damageable entities.
/// The closest target in each ray direction is hit; targets behind are ignored.
/// </summary>
public static class MeleeHitDetection
{
    /// <summary>
    /// Physics layers that block melee attacks.
    /// Layer 1 = Walls, Layer 5 = Door
    /// </summary>
    private const uint ObstacleLayerMask = (1 << 0) | (1 << 4); // Layers 1 and 5 (0-indexed: 0 and 4)

    /// <summary>
    /// Physics layers for potential targets.
    /// Layer 2 = Player, Layer 3 = Enemies
    /// </summary>
    private const uint TargetLayerMask = (1 << 1) | (1 << 2); // Layers 2 and 3 (0-indexed: 1 and 2)

    /// <summary>
    /// Combined mask for all hittable things (obstacles + targets).
    /// </summary>
    private const uint AllHittableMask = ObstacleLayerMask | TargetLayerMask;

    /// <summary>
    /// Number of rays to cast for a cone attack. More rays = more accurate but more expensive.
    /// </summary>
    private const int ConeRayCount = 7;

    /// <summary>
    /// Number of rays to cast for a swing slice. Smaller than cone since swing is narrow.
    /// </summary>
    private const int SwingSliceRayCount = 3;

    /// <summary>
    /// Result of a melee hit detection.
    /// </summary>
    public readonly struct MeleeHitResult
    {
        public readonly Node2D Target;
        public readonly Vector2 HitPosition;
        public readonly Vector2 HitNormal;
        public readonly float Distance;

        public MeleeHitResult(Node2D target, Vector2 hitPosition, Vector2 hitNormal, float distance)
        {
            Target = target;
            HitPosition = hitPosition;
            HitNormal = hitNormal;
            Distance = distance;
        }
    }

    /// <summary>
    /// Finds all valid targets in a cone that are not blocked by obstacles.
    /// Each unique target is only returned once, even if hit by multiple rays.
    /// </summary>
    /// <param name="world">The World2D to query.</param>
    /// <param name="origin">Origin point of the attack.</param>
    /// <param name="direction">Center direction of the cone (normalized).</param>
    /// <param name="range">Maximum range of the attack.</param>
    /// <param name="halfAngleRad">Half the cone angle in radians.</param>
    /// <param name="excludeRids">RIDs to exclude from detection (typically the attacker).</param>
    /// <param name="excludeNodes">Nodes to exclude from detection (the attacker and its children).</param>
    /// <returns>List of hit results for all valid targets.</returns>
    public static List<MeleeHitResult> FindTargetsInCone(
        World2D world,
        Vector2 origin,
        Vector2 direction,
        float range,
        float halfAngleRad,
        Array<Rid>? excludeRids = null,
        HashSet<Node2D>? excludeNodes = null)
    {
        var results = new List<MeleeHitResult>();
        var hitTargets = new HashSet<ulong>(); // Track unique targets by instance ID

        var spaceState = world.DirectSpaceState;
        if (spaceState is null)
        {
            GD.PrintErr("MeleeHitDetection: Could not get physics space state");
            return results;
        }

        // Cast rays across the cone
        var baseAngle = direction.Angle();

        for (var i = 0; i < ConeRayCount; i++)
        {
            // Distribute rays evenly across the cone
            var t = ConeRayCount > 1 ? (float)i / (ConeRayCount - 1) : 0.5f;
            var angleOffset = Mathf.Lerp(-halfAngleRad, halfAngleRad, t);
            var rayAngle = baseAngle + angleOffset;
            var rayDir = Vector2.FromAngle(rayAngle);

            var hit = CastRayForClosestTarget(spaceState, origin, rayDir, range, excludeRids, excludeNodes);
            if (hit.HasValue && !hitTargets.Contains(hit.Value.Target.GetInstanceId()))
            {
                hitTargets.Add(hit.Value.Target.GetInstanceId());
                results.Add(hit.Value);
            }
        }

        return results;
    }

    /// <summary>
    /// Finds valid targets in a narrow swing slice that are not blocked by obstacles.
    /// Used for incremental swing detection where we check a small arc each frame.
    /// </summary>
    /// <param name="world">The World2D to query.</param>
    /// <param name="origin">Origin point of the attack.</param>
    /// <param name="direction">Direction of the current swing position (normalized).</param>
    /// <param name="range">Maximum range of the attack.</param>
    /// <param name="sliceAngleRad">Half-angle of the slice in radians.</param>
    /// <param name="excludeRids">RIDs to exclude from detection.</param>
    /// <param name="excludeNodes">Nodes to exclude from detection.</param>
    /// <returns>List of hit results for all valid targets in the slice.</returns>
    public static List<MeleeHitResult> FindTargetsInSwingSlice(
        World2D world,
        Vector2 origin,
        Vector2 direction,
        float range,
        float sliceAngleRad,
        Array<Rid>? excludeRids = null,
        HashSet<Node2D>? excludeNodes = null)
    {
        var results = new List<MeleeHitResult>();
        var hitTargets = new HashSet<ulong>();

        var spaceState = world.DirectSpaceState;
        if (spaceState is null)
        {
            return results;
        }

        var baseAngle = direction.Angle();

        // Cast fewer rays for swing slice since it's narrower
        for (var i = 0; i < SwingSliceRayCount; i++)
        {
            var t = SwingSliceRayCount > 1 ? (float)i / (SwingSliceRayCount - 1) : 0.5f;
            var angleOffset = Mathf.Lerp(-sliceAngleRad, sliceAngleRad, t);
            var rayAngle = baseAngle + angleOffset;
            var rayDir = Vector2.FromAngle(rayAngle);

            var hit = CastRayForClosestTarget(spaceState, origin, rayDir, range, excludeRids, excludeNodes);
            if (hit.HasValue && !hitTargets.Contains(hit.Value.Target.GetInstanceId()))
            {
                hitTargets.Add(hit.Value.Target.GetInstanceId());
                results.Add(hit.Value);
            }
        }

        return results;
    }

    /// <summary>
    /// Casts a single ray and returns the closest damageable target, if any.
    /// Walls and doors block the ray - if they're closer, no target is returned.
    /// </summary>
    private static MeleeHitResult? CastRayForClosestTarget(
        PhysicsDirectSpaceState2D spaceState,
        Vector2 origin,
        Vector2 direction,
        float range,
        Array<Rid>? excludeRids,
        HashSet<Node2D>? excludeNodes)
    {
        var endPoint = origin + (direction * range);

        var query = PhysicsRayQueryParameters2D.Create(origin, endPoint, AllHittableMask);
        if (excludeRids is not null)
        {
            query.Exclude = excludeRids;
        }
        // Important: allow hitting from inside shapes (in case ray starts at entity center)
        query.HitFromInside = false;

        var result = spaceState.IntersectRay(query);
        if (result.Count == 0)
        {
            return null;
        }

        var hitCollider = result["collider"].AsGodotObject();
        var hitPosition = (Vector2)result["position"];
        var hitNormal = (Vector2)result["normal"];

        // Find the actual node we hit (might need to traverse up to parent)
        var hitNode = FindDamageableNode(hitCollider, excludeNodes);

        if (hitNode is null)
        {
            // We hit something (probably a wall/obstacle) but it's not a valid target
            // This means anything behind it is blocked
            return null;
        }

        var distance = origin.DistanceTo(hitPosition);
        return new MeleeHitResult(hitNode, hitPosition, hitNormal, distance);
    }

    /// <summary>
    /// Finds the damageable node from a collider. Handles cases where the collider
    /// is a child of the actual damageable entity.
    /// </summary>
    private static Node2D? FindDamageableNode(GodotObject collider, HashSet<Node2D>? excludeNodes)
    {
        if (collider is not Node node)
        {
            return null;
        }

        // Check if the collider itself is damageable
        if (collider is Node2D node2D and IDamageable)
        {
            if (excludeNodes is not null && excludeNodes.Contains(node2D))
            {
                return null;
            }
            return node2D;
        }

        // Traverse up to find a damageable parent (e.g., hitbox child of enemy)
        var current = node.GetParent();
        while (current is not null)
        {
            if (current is Node2D parentNode2D and IDamageable)
            {
                if (excludeNodes is not null && excludeNodes.Contains(parentNode2D))
                {
                    return null;
                }
                return parentNode2D;
            }
            current = current.GetParent();
        }

        return null;
    }

    /// <summary>
    /// Performs a simple attack in a single direction (zombie-style attack).
    /// Returns the closest damageable target that isn't blocked.
    /// </summary>
    /// <param name="world">The World2D to query.</param>
    /// <param name="origin">Origin point of the attack.</param>
    /// <param name="direction">Direction of the attack (normalized).</param>
    /// <param name="range">Maximum range of the attack.</param>
    /// <param name="excludeRids">RIDs to exclude from detection.</param>
    /// <param name="excludeNodes">Nodes to exclude from detection.</param>
    /// <returns>The closest valid target, or null if none found.</returns>
    public static MeleeHitResult? FindClosestTargetInDirection(
        World2D world,
        Vector2 origin,
        Vector2 direction,
        float range,
        Array<Rid>? excludeRids = null,
        HashSet<Node2D>? excludeNodes = null)
    {
        var spaceState = world.DirectSpaceState;
        if (spaceState is null)
        {
            return null;
        }

        return CastRayForClosestTarget(spaceState, origin, direction, range, excludeRids, excludeNodes);
    }
}

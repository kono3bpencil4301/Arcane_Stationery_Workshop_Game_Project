using Godot;

/// <summary>
/// Tile-specific collision rules shared by enemy projectiles.
/// </summary>
internal static class EnemyProjectileTileCollision
{
    private static readonly Vector2I FenceLeftTile = new(1, 7);
    private static readonly Vector2I FenceMiddleTile = new(2, 7);
    private static readonly Vector2I FenceRightTile = new(3, 7);

    public static bool IsFence(KinematicCollision2D collision)
    {
        if (collision?.GetCollider() is not TileMapLayer tileMapLayer)
            return false;

        Vector2I bodyCell = tileMapLayer.GetCoordsForBodyRid(
            collision.GetColliderRid()
        );

        if (IsFenceCell(tileMapLayer, bodyCell))
            return true;

        // TileMapLayer can merge tile physics bodies. In that case the RID may
        // not resolve to the contacted cell, so also probe immediately on both
        // sides of the collision surface.
        Vector2 contactPosition = collision.GetPosition();
        Vector2 normal = collision.GetNormal();

        return
            IsFenceAtGlobalPosition(tileMapLayer, contactPosition) ||
            IsFenceAtGlobalPosition(
                tileMapLayer,
                contactPosition - normal * 2.0f
            ) ||
            IsFenceAtGlobalPosition(
                tileMapLayer,
                contactPosition + normal * 2.0f
            );
    }

    private static bool IsFenceAtGlobalPosition(
        TileMapLayer tileMapLayer,
        Vector2 globalPosition
    )
    {
        Vector2I cell = tileMapLayer.LocalToMap(
            tileMapLayer.ToLocal(globalPosition)
        );

        return IsFenceCell(tileMapLayer, cell);
    }

    private static bool IsFenceCell(
        TileMapLayer tileMapLayer,
        Vector2I cell
    )
    {
        Vector2I atlasCoordinates = tileMapLayer.GetCellAtlasCoords(cell);

        return
            atlasCoordinates == FenceLeftTile ||
            atlasCoordinates == FenceMiddleTile ||
            atlasCoordinates == FenceRightTile;
    }

    /// <summary>
    /// MoveAndCollide may report no usable remainder after recovery from an
    /// overlap. Restoring the intended end position guarantees forward progress
    /// through a fence without disabling collision against ordinary walls.
    /// </summary>
    public static void FinishMotionThroughFence(
        CharacterBody2D projectile,
        Vector2 motionStart,
        Vector2 intendedMotion
    )
    {
        projectile.GlobalPosition = motionStart + intendedMotion;
    }
}

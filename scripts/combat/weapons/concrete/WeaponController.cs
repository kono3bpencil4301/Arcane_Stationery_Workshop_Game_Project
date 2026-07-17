using Godot;

public partial class WeaponController : Node
{
    [Export]
    public ChalkWeapon ChalkWeapon { get; set; }

    private Node2D _currentTarget;

    public override void _Process(double delta)
    {
        _currentTarget = FindNearestEnemy();

        if (_currentTarget == null)
        {
            return;
        }

        ChalkWeapon.TryFire(
            _currentTarget.GlobalPosition
        );
    }

    private Node2D FindNearestEnemy()
    {
        Node2D owner = GetParent<Node2D>();

        Node2D nearest = null;
        float nearestDistanceSquared = float.MaxValue;

        foreach (
            Node node in
            GetTree().GetNodesInGroup("enemy")
        )
        {
            if (node is not Node2D enemy)
            {
                continue;
            }

            float distanceSquared =
                owner.GlobalPosition.DistanceSquaredTo(
                    enemy.GlobalPosition
                );

            if (
                distanceSquared <
                nearestDistanceSquared
            )
            {
                nearestDistanceSquared =
                    distanceSquared;

                nearest = enemy;
            }
        }

        return nearest;
    }
}

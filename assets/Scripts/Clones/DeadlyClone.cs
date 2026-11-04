using Godot;

public partial class DeadlyClone : BasePlayerController
{
    private Area2D localKillZone;

    public override void _Ready()
    {
        base._Ready();

        localKillZone = GetNode<Area2D>("area_2d");
        localKillZone.BodyEntered += OnBodyEntered;
    }

    void OnBodyEntered(Node body)
    {
        if (body == this)
        {
            return;
        }

        if (body is BasePlayerController player)
        {
            if (player is DeadlyClone || player is ShadowClone)
            {
                return;
            }

            player.CallDeferred("Kill");
        }
    }
}

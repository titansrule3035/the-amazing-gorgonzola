using Godot;
using System;

public partial class GorgCarcass : Node2D
{
    // Public config. true = the character died facing left.
    // Can be set before OR after the carcass enters the tree; the visuals are re-applied either way.
    // (The hat's launch direction is only decided once, in _Ready, so set it before AddChild.)
    private bool _flip;
    private bool isReady = false;

    public bool flip
    {
        get => _flip;
        set
        {
            _flip = value;
            if (isReady)
            {
                ApplyFlipVisuals();
            }
        }
    }

    // Node parts (order: hat, head, torso, left leg, right leg)
    private RigidBody2D[] bodies = new RigidBody2D[5];
    private Sprite2D[] sprites = new Sprite2D[5];

    // Piece positions as laid out in the scene, and the mirrored layout used when flipped.
    private readonly Vector2[] defaultPositions = new Vector2[5];
    private static readonly Vector2[] flippedPositions =
    {
        new(20.0f, -66.0f),  // gorg_hat
        new(-2.0f, -23.0f),  // gorg_head
        new(7.0f, -42.0f),   // gorg_torso
        new(7.0f, -6.0f),    // gorg_leg_l
        new(-14.0f, -16.0f)  // gorg_leg_r
    };

    // Randomness / launch ranges
    private Random rand = new();

    private Vector2 launchMin = new(-100, -250);
    private Vector2 launchMax = new(100, -100);

    [Export] public Vector2 leftHatLaunch = new(-130, -120);
    [Export] public Vector2 rightHatLaunch = new(130, -120);

    /// <summary>
    /// Initializes body parts, applies flip adjustments and random launch velocities, and wires flush handlers.
    /// </summary>
    public override void _Ready()
    {
        bodies[0] = GetNode<RigidBody2D>("gorg_hat");
        bodies[1] = GetNode<RigidBody2D>("gorg_head");
        bodies[2] = GetNode<RigidBody2D>("gorg_torso");
        bodies[3] = GetNode<RigidBody2D>("gorg_leg_l");
        bodies[4] = GetNode<RigidBody2D>("gorg_leg_r");

        for (int i = 0; i < bodies.Length; i++)
        {
            sprites[i] = bodies[i].GetNode<Sprite2D>("sprite");
            defaultPositions[i] = bodies[i].Position;
        }

        isReady = true;
        ApplyFlipVisuals();

        for (int i = 0; i < bodies.Length; i++)
        {
            Vector2 launchVelocity;

            if (i == 0) // gorg_hat
            {
                Vector2 baseHatLaunch = flip ? rightHatLaunch : leftHatLaunch;
                float variationX = (float)(rand.NextDouble() * baseHatLaunch.X);
                float variationY = (float)(rand.NextDouble() * baseHatLaunch.Y);
                launchVelocity = baseHatLaunch + new Vector2(variationX, variationY);
            }
            else
            {
                float randX = (float)(rand.NextDouble() * (launchMax.X - launchMin.X) + launchMin.X);
                float randY = (float)(rand.NextDouble() * (launchMax.Y - launchMin.Y) + launchMin.Y);
                launchVelocity = new Vector2(randX, randY);
            }

            bodies[i].LinearVelocity = launchVelocity;
        }

        var killPanel = KillPanel.GetInstance();
        if (killPanel != null)
        {
            killPanel.OnFadeOut += Flush;
        }
    }

    /// <summary>
    /// Flips every piece's sprite and moves the pieces to the mirrored layout when flip is true,
    /// or back to the scene layout when it is false.
    /// </summary>
    private void ApplyFlipVisuals()
    {
        for (int i = 0; i < bodies.Length; i++)
        {
            sprites[i].FlipH = !flip;
            bodies[i].Position = flip ? flippedPositions[i] : defaultPositions[i];
        }
    }

    /// <summary>
    /// Flush handler to unhook events and free this node.
    /// </summary>
    void Flush()
    {
        var killPanel = KillPanel.GetInstance();
        if (killPanel != null)
        {
            killPanel.OnFadeOut -= Flush;
        }

        QueueFree();
    }
}
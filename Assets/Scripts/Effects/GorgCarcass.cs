using Godot;
using System;

public partial class GorgCarcass : Node2D
{
    // Public config
    public bool flip;

    // Node parts
    private RigidBody2D[] bodies = new RigidBody2D[5];
    private Sprite2D[] sprites = new Sprite2D[5];

    // Randomness / launch ranges
    private Random rand = new();

    private Vector2 launchMin = new(-100, -250); 
    private Vector2 launchMax = new(100, -100);

    [Export] public Vector2 leftHatLaunch = new(-130, -120);
    [Export] public Vector2 rightHatLaunch = new(130, -120);

    /// <summary>
    /// Initializes body parts, applies flip adjustments and random launch velocities, and wires flush handlers.
    /// </summary>
    public override async void _Ready()
    {
        bodies[0] = GetNode<RigidBody2D>("gorg_hat");
        bodies[1] = GetNode<RigidBody2D>("gorg_head");
        bodies[2] = GetNode<RigidBody2D>("gorg_torso");
        bodies[3] = GetNode<RigidBody2D>("gorg_leg_l");
        bodies[4] = GetNode<RigidBody2D>("gorg_leg_r");

        for (int i = 0; i < bodies.Length; i++)
        {
            sprites[i] = bodies[i].GetNode<Sprite2D>("sprite");

            if (flip)
            {
                sprites[i].FlipH = true;

                switch (bodies[i].Name)
                {
                    case "gorg_hat":
                        bodies[i].Position = new Vector2(20.0f, -66.0f);
                        break;
                    case "gorg_head":
                        bodies[i].Position = new Vector2(-2.0f, -23.0f);
                        break;
                    case "gorg_torso":
                        bodies[i].Position = new Vector2(7.0f, -42.0f);
                        break;
                    case "gorg_leg_l":
                        bodies[i].Position = new Vector2(7.0f, -6.0f);
                        break;
                    case "gorg_leg_r":
                        bodies[i].Position = new Vector2(-14.0f, -16.0f);
                        break;
                }
            }
        }

        for (int i = 0; i < bodies.Length; i++)
        {
            Vector2 launchVelocity;

            if (bodies[i].Name == "gorg_hat")
            {
                Vector2 baseHatLaunch = flip ? rightHatLaunch : leftHatLaunch;
                float variationX = (float)(rand.NextDouble() * baseHatLaunch.X); 
                float variationY = (float)(rand.NextDouble() *  baseHatLaunch.Y);
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

        while (GlobalGameManager.GetInstance() == null)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        CanvasEffects.GetInstance().OnFadeOut += Flush;
        GlobalGameManager.GetInstance().OnFlush += Flush;
    }

    /// <summary>
    /// Flush handler to unhook events and free this node.
    /// </summary>
    void Flush()
    {
        CanvasEffects.GetInstance().OnFadeOut -= Flush;
        GlobalGameManager.GetInstance().OnFlush -= Flush;
        QueueFree();
    }

    /// <summary>
    /// Overload that accepts a levelCompleted flag and delegates to Flush().
    /// </summary>
    void Flush(bool levelCompleted)
    {
        Flush();
    }
}

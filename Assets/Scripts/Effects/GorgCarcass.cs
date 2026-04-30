using Godot;
using System;

public partial class GorgCarcass : Node2D
{
    public bool flip;

    private RigidBody2D[] bodies = new RigidBody2D[5];
    private Sprite2D[] sprites = new Sprite2D[5];
    private Random rand = new Random();

    // General body part launch ranges
    private Vector2 launchMin = new Vector2(-100, -250); 
    private Vector2 launchMax = new Vector2(100, -100);

    // Hat launch values when not randomized
    [Export] public Vector2 leftHatLaunch = new Vector2(-130, -120);
    [Export] public Vector2 rightHatLaunch = new Vector2(130, -120);

    public override async void _Ready()
    {
        // Load body nodes
        bodies[0] = GetNode<RigidBody2D>("gorg_hat");
        bodies[1] = GetNode<RigidBody2D>("gorg_head");
        bodies[2] = GetNode<RigidBody2D>("gorg_torso");
        bodies[3] = GetNode<RigidBody2D>("gorg_leg_l");
        bodies[4] = GetNode<RigidBody2D>("gorg_leg_r");

        // Set positions and flip sprites if necessary
        for (int i = 0; i < bodies.Length; i++)
        {
            sprites[i] = bodies[i].GetNode<Sprite2D>("sprite");

            if (flip)
            {
                sprites[i].FlipH = true;

                // Manually set positions when flipped
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

        // Launch logic (randomized for all parts including hat)
        for (int i = 0; i < bodies.Length; i++)
        {
            Vector2 launchVelocity;

            if (bodies[i].Name == "gorg_hat")
            {
                // Use hat-specific vector as center, with slight randomness
                Vector2 baseHatLaunch = flip ? rightHatLaunch : leftHatLaunch;
                float variationX = (float)(rand.NextDouble() * baseHatLaunch.X); // +/-10 range
                float variationY = (float)(rand.NextDouble() *  baseHatLaunch.Y);
                launchVelocity = baseHatLaunch + new Vector2(variationX, variationY);
            }
            else
            {
                // Random velocity within defined min and max range
                float randX = (float)(rand.NextDouble() * (launchMax.X - launchMin.X) + launchMin.X);
                float randY = (float)(rand.NextDouble() * (launchMax.Y - launchMin.Y) + launchMin.Y);
                launchVelocity = new Vector2(randX, randY);
            }

            // Apply velocity to the body part
            bodies[i].LinearVelocity = launchVelocity;
        }

        while (GlobalGameManager.GetInstance() == null)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        GlobalGameManager.GetInstance().OnFlush += Flush;
    }

    void Flush()
    {
        QueueFree();
    }
}

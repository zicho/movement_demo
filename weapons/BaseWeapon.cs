using Constants;
using Entities;
using Enums;
using Godot;
using System;

namespace Weapons
{
    public class BaseWeapon : Node2D
    {
        private Sprite _sprite;
        private Vector2 _direction;

        public Player HeldBy { get; private set; }

        // Declare member variables here. Examples:
        // private int a = 2;
        // private string b = "text";

        // Called when the node enters the scene tree for the first time.
        public override void _Ready()
        {
            _sprite = GetNode<Sprite>("Sprite");
            HeldBy = GetParent<Player>();
        }

        // Called every frame. 'delta' is the elapsed time since the previous frame.
        public override void _Process(float delta)
        {
            if (HeldBy == null) return;

            // _sprite.Scale = GetPlayerDirection();
            // LookAt(new Vector2(800, 600));

            // Position = HeldBy.Position + (GetPlayerDirection().Normalized() * 90);

            if (!HeldBy.AimLocked)
            {
                _direction = HeldBy.GetInputDirection();
                RotationDegrees = GetWeaponRotation();
                _sprite.FlipV = RotationDegrees > 90;
            }


            GD.Print(_direction);
        }

        private int GetWeaponRotation()
        {
            if (_direction == Directions.UP) return -90;
            else if (_direction == Directions.UP_RIGHT) return -45;
            else if (_direction == Directions.RIGHT) return 0;
            else if (_direction == Directions.DOWN_RIGHT) return 45;
            else if (_direction == Directions.DOWN) return 90;
            else if (_direction == Directions.DOWN_LEFT) return 135;
            else if (_direction == Directions.LEFT) return 180;
            else if (_direction == Directions.UP_LEFT) return 225;


            return 0;
        }

        private Vector2 GetPlayerDirection()
        {
            return HeldBy.LookDirection == HorizontalDirection.Right
            ? new Vector2(1, 1)
            : new Vector2(-1, 1);
        }
    }
}


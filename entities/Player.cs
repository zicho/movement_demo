using System;
using Constants;
using Enums;
using Godot;

namespace Entities
{
    public class Player : KinematicBody2D
    {
        // movement
        private Vector2 _vel = Vector2.Zero;
        private readonly int _movementSpeed = 500;
        private readonly int _movementAcceleration = 40;
        private readonly float _groundDeceleration = 0.4f; // slowdown in percent per frame (0.01 per percent)
        private readonly float _airDeceleration = 0.02f; // slowdown in percent per frame (0.01 per percent)

        // jumping
        private readonly int _jumpPower = 800;
        private int _jumpsAvailable;
        private readonly int _maxJumps = 2;
        private readonly Timer _disableJumpTimer = new Timer();
        private readonly float _disableJumpDelayTime = 0.5f;
        private bool _canJump;
        private bool _cancelDisableJump; // this cancels the disable jump timer to avoid cancelling double jumps
        private bool _jumpBuffer; // set to true when jump is pressed while in air to "queue" the jump

        // environment
        private readonly int _gravity = 60;
        private readonly int _maxGravity = 1200;

        // states (Replace with statehandler?)
        public bool InAir => CurrentState == PlayerState.Jumping || CurrentState == PlayerState.Falling;
        // public bool Falling => _vel.y > 0;
        // public bool Jumping => _vel.y < 0;
        public bool MovementPressed => Input.IsActionPressed("ui_left") || Input.IsActionPressed("ui_right");
        public bool MovingRight => _vel.x > 0;
        public bool MovingLeft => _vel.x < 0;

        public bool AimLocked { get; private set; }

        public HorizontalDirection LookDirection;
        public PlayerState CurrentState;

        // dashing
        private bool _canDash;
        private bool _isDashing;
        private Vector2 _dashDir = Vector2.Zero;
        private readonly float _dashDuration = 0.18f;
        private readonly Timer _dashTimer = new Timer();
        private readonly int _dashSpeed = 800;
        private readonly int _slideSpeed = 20;

        // dash trail
        private readonly Timer _trailTimer = new Timer();
        private readonly float _trailDelay = 0.2f;
        private readonly PackedScene _trailEffectScene = ResourceLoader.Load("res://fx/GhostTrail.tscn") as PackedScene;

        // miscellaneous node setup
        private AnimationPlayer _animPlayer;
        private Sprite _sprite;
        private Label _stateLabel;
        private RayCast2D _floorDetectRay; // used for the "jump buffer"

        // these rays are for wall sliding
        private RayCast2D _wallDetectTopRightRay;
        private RayCast2D _wallDetectBottomRightRay;
        private RayCast2D _wallDetectTopLeftRay;
        private RayCast2D _wallDetectBottomLeftRay;

        public override void _Ready()
        {
            _jumpsAvailable = _maxJumps;

            SetupJumpTimer();
            SetupDashTimer();
            SetupTrailTimer();
            SetupRayCasts();

            _animPlayer = GetNode<AnimationPlayer>("Anim");
            _sprite = GetNode<Sprite>("Sprite");
            _stateLabel = GetNode<Label>("StateLabel");
        }

        private void SetupRayCasts()
        {
            _wallDetectTopRightRay = GetNode<RayCast2D>("RightTopRaycast");
            _wallDetectBottomRightRay = GetNode<RayCast2D>("RightBottomRaycast");
            _wallDetectTopLeftRay = GetNode<RayCast2D>("LeftTopRaycast");
            _wallDetectBottomLeftRay = GetNode<RayCast2D>("LeftBottomRaycast");

            _floorDetectRay = GetNode<RayCast2D>("DownwardRaycast");
        }

        private void SetupTrailTimer()
        {
            _trailTimer.WaitTime = _trailDelay;
            _trailTimer.Connect("timeout", this, nameof(CreateTrail));
            _trailTimer.OneShot = true;
            AddChild(_trailTimer);
        }

        private void SetupDashTimer()
        {
            _dashTimer.WaitTime = _dashDuration;
            _dashTimer.Connect("timeout", this, nameof(StopDash));
            _dashTimer.OneShot = true;
            AddChild(_dashTimer);
        }

        private void SetupJumpTimer()
        {
            _disableJumpTimer.WaitTime = _disableJumpDelayTime;
            _disableJumpTimer.Connect("timeout", this, nameof(DisableJump));
            _disableJumpTimer.OneShot = true;
            AddChild(_disableJumpTimer);
        }

        private void DisableJump()
        {
            // GD.Print("Jump disabler hit, is on floor: " + IsOnFloor() + ". Cancel jump is " + _cancelJump);
            if (_cancelDisableJump) _canJump = false;
        }

        public override void _Process(float delta)
        {
            Run();
            HandleLookDirection();
            JumpStateHandler();
            Jump();
            Dash();
            WallSliding();
            Gravity();
            StateHandler();

            // TODO: AnimationHandler(); 

            // finally, apply movement
            if (_isDashing)
            {
                CreateTrail();
                _vel = MoveAndSlide(_dashDir * _dashSpeed, Vector2.Up);
            }
            else
            {
                _vel = MoveAndSlide(_vel, Vector2.Up);
            }

            _stateLabel.Text = CurrentState.ToString();
        }

        private void HandleLookDirection()
        {
            AimLocked = Input.IsActionPressed("lock_look_direction");

            if (!Input.IsActionPressed("lock_look_direction") && MovingRight)
            {
                _sprite.FlipH = false;
                LookDirection = HorizontalDirection.Right;
            }
            if (!Input.IsActionPressed("lock_look_direction") && MovingLeft)
            {
                _sprite.FlipH = true;
                LookDirection = HorizontalDirection.Left;
            }
        }

        private void Run()
        {
            if (CurrentState == PlayerState.WallJumping) return;

            if (Input.IsActionPressed("ui_right"))
            {
                _vel.x = Math.Min(_vel.x + _movementAcceleration, _movementSpeed);
            }
            else if (Input.IsActionPressed("ui_left"))
            {
                _vel.x = Math.Max(_vel.x - _movementAcceleration, -_movementSpeed);
            }

            if (IsOnFloor() && CurrentState != PlayerState.Running)
            {
                _vel.x = Mathf.Lerp(_vel.x, 0, _groundDeceleration);
            }
            else if (!IsOnFloor() && CurrentState != PlayerState.Running)
            { // AIR FRICION
                _vel.x = Mathf.Lerp(_vel.x, 0, _airDeceleration);
            }
        }

        private void JumpStateHandler()
        {
            if (IsOnFloor())
            {
                if (!InAir) _jumpsAvailable = _maxJumps;
                // GD.Print("Jumps reset");
                _canJump = true;
                _canDash = true;
                if (!_cancelDisableJump && !InAir)
                {
                    // GD.Print("Cancel jump enabled");
                    _cancelDisableJump = true;
                }
            }
            else if (_disableJumpTimer.IsStopped() && _canJump)
            {
                _disableJumpTimer.Start();
            }

            if (!IsOnFloor() && _jumpsAvailable == 0 && Input.IsActionJustPressed("ui_accept") && _floorDetectRay.IsColliding())
            {
                // GD.Print("Jump buffer set");
                _jumpBuffer = true;
            }

            if (_jumpBuffer && IsOnFloor()) PerformJump();
        }

        private void Jump()
        {
            if (Input.IsActionJustPressed("ui_accept") && _jumpsAvailable != 0 && _canJump)// && PlayerState != State.WallSliding)
            {
                // GD.Print("Cancel jump disabled");
                PerformJump();
                _cancelDisableJump = false;
                // GD.Print(_jumpsAvailable);
            }

            // else if (Input.IsActionJustPressed("ui_accept") && PlayerState == State.WallSliding) {
            //     _vel.x -= 400;
            //     _vel.y -= 1400;
            // }
        }

        private void PerformJump()
        {
            if (!IsOnFloor() && _jumpsAvailable == _maxJumps) GD.Print("Coyote jump performed");
            _jumpBuffer = false;
            _jumpsAvailable--;
            if (InAir) _vel.y = 0;
            _vel.y = 0;
            _vel.y -= _jumpPower;
        }

        private void Dash()
        {
            if (Input.IsActionJustPressed("dash") && _canDash && !IsOnFloor())
            {
                _isDashing = true;
                _canDash = false;

                _dashDir = GetInputDirection();
                _dashTimer.Start();
            }
        }

        public Vector2 GetInputDirection()
        {
            if (Input.IsActionPressed("ui_up"))
            {
                if (Input.IsActionPressed("ui_left")) return Directions.UP_LEFT;
                else if (Input.IsActionPressed("ui_right")) return Directions.UP_RIGHT;
                else return Directions.UP;
            }
            else if (Input.IsActionPressed("ui_right"))
            {
                if (Input.IsActionPressed("ui_up")) return Directions.UP_RIGHT;
                else if (Input.IsActionPressed("ui_down")) return Directions.DOWN_RIGHT;
                else return Directions.RIGHT;
            }
            else if (Input.IsActionPressed("ui_down"))
            {
                if (Input.IsActionPressed("ui_left")) return Directions.DOWN_LEFT;
                else if (Input.IsActionPressed("ui_right")) return Directions.DOWN_RIGHT;
                else return Directions.DOWN;
            }
            else if (Input.IsActionPressed("ui_left"))
            {
                if (Input.IsActionPressed("ui_up")) return Directions.UP_LEFT;
                else if (Input.IsActionPressed("ui_down")) return Directions.DOWN_LEFT;
                else return Directions.LEFT;
            }

            return MovingRight ? Vector2.Right : Vector2.Left;
        }

        private void CreateTrail()
        {
            var ghost = _trailEffectScene.Instance() as Sprite;
            GetParent().AddChild(ghost);
            ghost.FlipH = _sprite.FlipH;
            ghost.Position = Position;
            ghost.Texture = _sprite.Texture;
        }

        private void StopDash()
        {
            _canDash = false;
            _isDashing = false;
            _vel /= 2;
            _vel.LinearInterpolate(Vector2.Zero, 0.1f);
        }

        private void Gravity()
        {
            if (CurrentState != PlayerState.WallSliding || CurrentState != PlayerState.WallJumping) _vel.y = Math.Min(_vel.y + _gravity, _maxGravity);
            else _vel.y = _slideSpeed;
        }

        private void WallSliding() {
            if(CurrentState == PlayerState.Jumping || CurrentState == PlayerState.Falling) GD.Print("Yeee");
        }

        private void StateHandler()
        {
            if (MovementPressed && IsOnFloor()) CurrentState = PlayerState.Running;
            else if (!MovementPressed && IsOnFloor()) CurrentState = PlayerState.Stopped;
            else if (!IsOnFloor() && _vel.y < 0) CurrentState = PlayerState.Jumping;
            else if (!IsOnFloor() && _vel.y > 0) CurrentState = PlayerState.Falling;

            // if (!IsOnFloor())
            // {
            //     if ((_wallDetectTopRightRay.IsColliding() || _wallDetectTopRightRay.IsColliding()) && Input.IsActionPressed("ui_right"))
            //     {
            //         _vel.y = 0;
            //         PlayerState = State.WallSliding;
            //     }
            //     else if ((_wallDetectTopLeftRay.IsColliding() || _wallDetectTopLeftRay.IsColliding()) && Input.IsActionPressed("ui_left"))
            //     {
            //         _vel.y = 0;
            //         PlayerState = State.WallSliding;
            //     }
            // }

            // if (PlayerState == State.WallSliding) _canDash = false;

            if (_isDashing) CurrentState = PlayerState.Dashing;
        }
        // private void AnimationHandler()
        // {
        //     if (Running && !InAir)
        //     {
        //         _animPlayer.Play("run");
        //     }
        //     else if (InAir)
        //     {
        //         _animPlayer.Play("jump");
        //     }
        //     else
        //     {
        //         _animPlayer.Play("stop");
        //     }
        // }
    }
}
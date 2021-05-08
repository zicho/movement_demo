using Godot;

namespace Constants
{
    public static class Directions
    {
        public static Vector2 UP = Vector2.Up;
        public static Vector2 UP_RIGHT = Vector2.Up + Vector2.Right;
        public static Vector2 RIGHT = Vector2.Right;
        public static Vector2 DOWN_RIGHT = Vector2.Down + Vector2.Right;
        public static Vector2 DOWN = Vector2.Down;
        public static Vector2 DOWN_LEFT = Vector2.Left + Vector2.Down;
        public static Vector2 LEFT = Vector2.Left;
        public static Vector2 UP_LEFT = Vector2.Up + Vector2.Left;
    }
}
using System;

namespace Cove.GodotFormat
{
  /// <summary>
  /// Represents a 3D vector with x, y, and z components.
  /// </summary>
  public class Vector3
  {
    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public static readonly Vector3 Zero = new(0, 0, 0);

    public Vector3(float x, float y, float z)
    {
      X = x;
      Y = y;
      Z = z;
    }

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3 operator *(Vector3 a, float scalar) => new(a.X * scalar, a.Y * scalar, a.Z * scalar);

    public static Vector3 operator /(Vector3 a, float scalar)
    {
      if (scalar == 0) throw new DivideByZeroException("Cannot divide by zero.");
      return new Vector3(a.X / scalar, a.Y / scalar, a.Z / scalar);
    }

    public static float Dot(Vector3 a, Vector3 b)
    {
      return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    public static Vector3 Cross(Vector3 a, Vector3 b)
    {
      return new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X
    );
    }

    public float Magnitude()
    {
      return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
    }

    public Vector3 Normalize()
    {
      float magnitude = Magnitude();
      if (magnitude == 0) throw new InvalidOperationException("Cannot normalize a zero vector.");
      return this / magnitude;
    }

    public override string ToString()
    {
      return $"({X}, {Y}, {Z})";
    }
  }

  /// <summary>
  /// Represents a 2D vector with x and y components.
  /// </summary>
  public class Vector2
  {
    public float X { get; }
    public float Y { get; }

    public static readonly Vector2 Zero = new(0, 0);

    public Vector2(float x, float y)
    {
      X = x;
      Y = y;
    }

    public float Magnitude()
    {
      return (float)Math.Sqrt(X * X + Y * Y);
    }

    public Vector2 Normalize()
    {
      float magnitude = Magnitude();
      if (magnitude == 0) throw new InvalidOperationException("Cannot normalize a zero vector.");
      return new Vector2(X / magnitude, Y / magnitude);
    }

    public float Angle()
    {
      return (float)Math.Atan2(Y, X);
    }

    public float AngleInDegrees()
    {
      return Angle() * (180f / (float)Math.PI);
    }

    public Vector2 Rotate(float angleRadians)
    {
      float cosTheta = (float)Math.Cos(angleRadians);
      float sinTheta = (float)Math.Sin(angleRadians);
      return new Vector2(
          X * cosTheta - Y * sinTheta,
          X * sinTheta + Y * cosTheta
      );
    }

    public Vector2 RotateInDegrees(float angleDegrees)
    {
      return Rotate(angleDegrees * ((float)Math.PI / 180f));
    }

    public static Vector2 operator *(Vector2 a, float scalar) => new(a.X * scalar, a.Y * scalar);

    public override string ToString()
    {
      return $"({X}, {Y})";
    }
  }

  /// <summary>
  /// Represents a quaternion for 3D rotations.
  /// </summary>
  public class Quat
  {
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public float W { get; }

    public Quat(float x, float y, float z, float w)
    {
      X = x;
      Y = y;
      Z = z;
      W = w;
    }

    public override string ToString()
    {
      return $"({X}, {Y}, {Z}, {W})";
    }
  }

  /// <summary>
  /// Represents a 3D plane in space.
  /// </summary>
  public class Plane
  {
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public float Distance { get; }

    public Plane(float x, float y, float z, float distance)
    {
      X = x;
      Y = y;
      Z = z;
      Distance = distance;
    }

    public override string ToString()
    {
      return $"Plane({X}, {Y}, {Z}, {Distance})";
    }
  }
}

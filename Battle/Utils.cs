using Microsoft.Extensions.ObjectPool;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Server.Battle
{
    public interface ICustomObjectPoolable
    {
        void Dispose();
    }
    public struct Vector2
    {
        public float X;
        public float Y;
        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator +(Vector2 a, Vector2 b)
        {
            return new Vector2(a.X + b.X, a.Y + b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator -(Vector2 a, Vector2 b)
        {
            return new Vector2(a.X - b.X, a.Y - b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator -(Vector2 a)
        {
            return new Vector2(0f - a.X, 0f - a.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator *(Vector2 a, float d)
        {
            return new Vector2(a.X * d, a.Y * d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator *(float d, Vector2 a)
        {
            return new Vector2(a.X * d, a.Y * d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 operator /(Vector2 a, float d)
        {
            return new Vector2(a.X / d, a.Y / d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector2 lhs, Vector2 rhs)
        {
            return lhs.X == rhs.X && lhs.Y == rhs.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector2 lhs, Vector2 rhs)
        {
            return !(lhs == rhs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj)
        {
            if (obj is not Vector2 v)
            {
                return false;
            }

            return X == v.X && Y == v.Y;
        }

        public readonly float Length => (float)Math.Sqrt(X * X + Y * Y);

        public readonly float LengthSquared => X * X + Y * Y;

        public readonly Vector2 Normalize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                float length = Length;
                if (length <= 0)
                {
                    return new(1, 0);
                }

                return this / length;
            }
        }

        public static Vector2 Zero => new();

        public override int GetHashCode()
        {
            return (X, Y).GetHashCode();
        }
    }
    public static class Utils
    {
        public const float FrameDeltaTime = 0.0333333f;

        public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new Vector2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        public static float Dot(Vector2 v1, Vector2 v2) => v1.X * v2.X + v1.Y * v2.Y;

        public static float Distance(Vector2 v1, Vector2 v2) => (v1 - v2).Length;

        public static float DistanceSq(Vector2 v1, Vector2 v2) => (v1 - v2).LengthSquared;
    }

    public static class CustomObjectPool<T> where T : class, ICustomObjectPoolable, new()
    {
        private static readonly ConcurrentQueue<T> _queue = new();

        public static T Get()
        {
            if (_queue.TryDequeue(out var item))
            {
                return item;
            }
            return new T();
        }

        public static void Dispose(T item)
        {
            item.Dispose();
            _queue.Enqueue(item);
        }
    }
    public static class CustomObjectPool
    {
        public static void Dispose<T>(T obj) where T : class, ICustomObjectPoolable, new()
        {
            CustomObjectPool<T>.Dispose(obj);
        }
    }
}

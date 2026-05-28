
namespace Server.Common
{
    public static partial class ExtendsMethod
    {
        public static T? GetRandomValue<T>(this IList<T> list)
        {
            return list.ElementAt(Utils.GetRandomValue(0, list.Count));
        }
    }
    public class FixedSizedQueue<T> : Queue<T>
    {
        public int Limit { get; }

        public FixedSizedQueue(int limit) : base(limit)
        {
            Limit = limit;
        }

        public new void Enqueue(T item)
        {
            // 큐가 가득 찼으면, 가장 오래된 항목을 제거
            while (Count >= Limit)
            {
                Dequeue();
            }
            base.Enqueue(item);
        }
    }

    public static class Utils
    {
        private static Random _random = new Random();
        public static float GetRandomValue(float max)
        {
            return (float)(_random.NextDouble() * max);
        }
        public static float GetRandomValue(float min, float max)
        {
            return (float)(min + (_random.NextDouble() * (max - min)));
        }

        public static int GetRandomValue(int max)
        {
            return _random.Next(max);
        }

        public static int GetRandomValue(int min, int max)
        {
            return _random.Next(min, max);
        }

        public static T GetListRandomValue<T>(IList<T> list)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentException("List cannot be null or empty.");
            }
            return list.ElementAt(GetRandomValue(0, list.Count));
        }
    }
}

using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using System.Linq;

namespace DismantledBot
{
    public static class Utilities
    {
        private static Random Random = new Random();

        public static int PMod(int x, int m)
        {
            return ((x % m) + m) % m;
        }

        public static List<string> FuzzySearch(this IEnumerable<string> self, string compare)
        {
            return self.OrderBy(x => FuzzyMatch(compare, x)).ToList();
        }

        public static int FuzzyMatch(string original, string comparedTo)
        {
            int l1 = original.Length;
            int l2 = comparedTo.Length;

            int[,] data = new int[original.Length + 1, comparedTo.Length + 1];

            for (int i = 1; i <= original.Length; i++)
                data[i, 0] = i;
            for (int i = 1; i <= comparedTo.Length; i++)
                data[0, i] = i;
            for(int x = 1; x <= original.Length; x++)
            {
                for(int y = 1; y <= comparedTo.Length; y++)
                {
                    if (original[x - 1] == comparedTo[y - 1])
                        data[x, y] = data[PMod(x - 1, l1 + 1), PMod(y - 1, l2 + 1)];
                    else
                        data[x, y] = Math.Min(data[x, PMod(y - 1, l2 + 1)] + 1, Math.Min(data[PMod(x - 1, l1 + 1), y] + 1, data[PMod(x - 1, l1 + 1), PMod(y - 1, l2 + 1)] + 1));
                }
            }

            return data[original.Length, comparedTo.Length];
        }

        public static T GetRandom<T>(this IEnumerable<T> self)
        {
            if (self.Count() == 0)
                return default;

            return self.ElementAt(Random.Next(0, self.Count() - 1));
        }

        public static string GetMethod([CallerMemberName]string method = "")
        {
            return method;
        }

        public static bool GetContains<T>(this IEnumerable<T> self, T obj, out T retrieved)
        {
            foreach(T item in self)
            {
                if(Equals(item, obj))
                {
                    retrieved = item;
                    return true;
                }
            }

            retrieved = default;
            return false;
        }

        public static bool GetContains<T>(this IEnumerable<T> self, Func<T, bool> selector, out T retrieved)
        {
            foreach (T item in self)
            {
                if (selector(item))
                {
                    retrieved = item;
                    return true;
                }
            }

            retrieved = default;
            return false;
        }
    }
}

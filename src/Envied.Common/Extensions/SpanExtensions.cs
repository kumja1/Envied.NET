using System.Runtime.CompilerServices;

namespace Envied.Common.Extensions;

public static class SpanExtensions
{
    private const int InsertionSortThreshold = 16;
    private const int StackSize = 64;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sort(this Span<string> span, IComparer<string> comparer)
    {
        if (span.Length > 1)
            HybridSort(span, comparer);
    }

    private static void HybridSort(Span<string> span, IComparer<string> comparer)
    {
        // Stack-based sorting to avoid recursion overhead
        var stack = new Stack<(int Left, int Right)>(StackSize);
        stack.Push((0, span.Length - 1));

        (int Left, int Right) range;
        while (stack.Count > 0 && (range = stack.Pop()) != default)
        {
            int left = range.Left;
            int right = range.Right;

            if (right - left + 1 <= InsertionSortThreshold)
            {
                InsertionSort(span, left, right, comparer);
                continue;
            }

            int mid = left + (right - left) / 2;
            int pivotIndex = MedianOfThree(span, left, mid, right, comparer);
            Swap(ref span[pivotIndex], ref span[right]);

            int partitionIndex = Partition(span, left, right, comparer);
            if (partitionIndex - left > right - partitionIndex)
            {
                if (left < partitionIndex - 1)
                    stack.Push((left, partitionIndex - 1));
                if (partitionIndex + 1 < right)
                    stack.Push((partitionIndex + 1, right));
            }
            else
            {
                if (partitionIndex + 1 < right)
                    stack.Push((partitionIndex + 1, right));
                if (left < partitionIndex - 1)
                    stack.Push((left, partitionIndex - 1));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MedianOfThree(Span<string> span, int a, int b, int c, IComparer<string> comparer)
    {
        int ab = comparer.Compare(span[a], span[b]);
        int ac = comparer.Compare(span[a], span[c]);
        int bc = comparer.Compare(span[b], span[c]);

        return ab < 0 ?
            (bc < 0 ? b : (ac < 0 ? c : a)) :
            (bc > 0 ? b : (ac > 0 ? c : a));
    }

    private static int Partition(Span<string> span, int left, int right, IComparer<string> comparer)
    {
        string pivot = span[right];
        int i = left;

        for (int j = left; j < right; j++)
        {
            if (comparer.Compare(span[j], pivot) <= 0)
            {
                Swap(ref span[i], ref span[j]);
                i++;
            }
        }

        Swap(ref span[i], ref span[right]);
        return i;
    }

    private static void InsertionSort(Span<string> span, int left, int right, IComparer<string> comparer)
    {
        for (int i = left + 1; i <= right; i++)
        {
            string current = span[i];
            int j = i - 1;

            while (j >= left && comparer.Compare(span[j], current) > 0)
            {
                span[j + 1] = span[j];
                j--;
            }
            span[j + 1] = current;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Swap(ref string a, ref string b) => (b, a) = (a, b);
}

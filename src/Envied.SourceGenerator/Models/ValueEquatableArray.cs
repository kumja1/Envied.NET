using System.Collections;


public readonly struct ValueEquatableArray<T>(T[] items) : IEnumerable<T>, IEquatable<ValueEquatableArray<T>> where T : IEquatable<T>
{
    private readonly T[] _items = items ?? [];

    public int Length => _items.Length;

    public ref readonly T this[int index] => ref _items[index];

    public bool Equals(ValueEquatableArray<T> other) =>
        _items.AsSpan().SequenceEqual(other._items);

    public override bool Equals(object obj) =>
        obj is ValueEquatableArray<T> other && Equals(other);


    public override int GetHashCode()
    {
        if (_items.Length == 0) return 0;
        int hash = 17;

        foreach (var item in _items)
        {
            hash = hash * 31 + (item?.GetHashCode() ?? 0);
        }
        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static implicit operator ValueEquatableArray<T>(T[] array) => new(array);
        public static implicit operator ValueEquatableArray<T>(List<T> list) => new([.. list]);  



    public static implicit operator T[](ValueEquatableArray<T> array) => array._items;
}
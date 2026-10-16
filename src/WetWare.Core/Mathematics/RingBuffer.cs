namespace WetWare.Core.Mathematics;

/// <summary>Fixed-capacity circular buffer used for oscilloscope traces and sliding windows.</summary>
public sealed class RingBuffer<T>
{
    private readonly T[] _items;
    private int _start;

    public RingBuffer(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = new T[capacity];
    }

    public int Capacity => _items.Length;

    public int Count { get; private set; }

    public void Add(T item)
    {
        int index = (_start + Count) % _items.Length;
        _items[index] = item;
        if (Count < _items.Length) Count++;
        else _start = (_start + 1) % _items.Length;
    }

    /// <summary>Element by age: 0 is the oldest retained item.</summary>
    public T this[int index] => _items[(_start + index) % _items.Length];

    public T Last => this[Count - 1];

    public void Clear()
    {
        _start = 0;
        Count = 0;
    }

    /// <summary>Copies the items (oldest first) into <paramref name="destination"/>; returns the number copied.</summary>
    public int CopyTo(T[] destination)
    {
        int n = Math.Min(Count, destination.Length);
        int skip = Count - n;
        for (int i = 0; i < n; i++) destination[i] = this[skip + i];
        return n;
    }

    public T[] ToArray()
    {
        var result = new T[Count];
        CopyTo(result);
        return result;
    }
}

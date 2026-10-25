using System;

// Compact representation of a finite numerical arithmetic sequence
// e.g. ArithSeq(1,3,5) = [1,4,7,10,13]
[Serializable]
public class ArithSeq
{
    public int Start;
    public int Stride;
    public int Count;

    public ArithSeq(int start, int stride, int count)
    {
        Start = start;
        Stride = stride;
        Count = count;
    }

    // [1,4,7,10].IndexOf(2) => null
    // [1,4,7,10].IndexOf(7) => 2
    public int? IndexOf(int query)
    {
        if (query < Start)
            return null;
        int offs = query - Start;
        if (offs % Stride != 0)
            return null;
        int strides = offs / Stride;
        if (strides < Count)
            return strides;
        return null;
    }
}

namespace MathSolver.Services.Core;

/// <summary>Reference-counted persistent 16-level tree. Snapshots share the root; edits copy one path.
/// Leaf ownership releases obsolete files even when many later snapshots remain alive.</summary>
internal sealed class InputBlockMap : IDisposable
{
    private Node? _root;
    internal int Count => _root?.Count ?? 0;
    internal IEnumerable<KeyValuePair<int, InputBlock>> Entries => Enumerate(_root, 0, 65536);
    internal IEnumerable<int> Keys => Entries.Select(entry => entry.Key);
    internal IEnumerable<InputBlock> Values => Entries.Select(entry => entry.Value);
    internal InputBlock this[int key] => TryGetValue(key, out var block) ? block : throw new KeyNotFoundException();
    internal InputBlockMap Snapshot()
    {
        var snapshot = new InputBlockMap();
        _root?.AddReference();
        snapshot._root = _root;
        return snapshot;
    }
    internal bool TryGetValue(int key, out InputBlock block)
    {
        int first = 0, width = 65536;
        Node? node = _root;
        while (node is not null && width > 1)
        {
            width /= 2;
            if (key < first + width) node = node.Left;
            else { first += width; node = node.Right; }
        }
        block = node?.Block ?? default;
        return node is not null;
    }
    internal void Set(int key, InputBlock block)
    {
        if ((uint)key >= 65536) throw new ArgumentOutOfRangeException(nameof(key));
        Node replacement = Update(_root, 0, 65536, key, block);
        var previous = _root; _root = replacement; previous?.Release();
    }
    private static Node Update(Node? previous, int first, int width, int key, InputBlock block)
    {
        if (width == 1) return new Node(block);
        width /= 2;
        Node? left, right;
        if (key < first + width)
        {
            left = Update(previous?.Left, first, width, key, block);
            right = previous?.Right; right?.AddReference();
        }
        else
        {
            right = Update(previous?.Right, first + width, width, key, block);
            left = previous?.Left; left?.AddReference();
        }
        try { return new Node(left, right); }
        catch { left?.Release(); right?.Release(); throw; }
    }
    private static IEnumerable<KeyValuePair<int, InputBlock>> Enumerate(Node? node, int first, int width)
    {
        if (node is null) yield break;
        if (width == 1) { yield return new(first, node.Block); yield break; }
        foreach (var entry in Enumerate(node.Left, first, width / 2)) yield return entry;
        foreach (var entry in Enumerate(node.Right, first + width / 2, width / 2)) yield return entry;
    }
    public void Dispose() { var root = _root; _root = null; root?.Release(); }
    private sealed class Node
    {
        private int _references = 1;
        internal Node? Left { get; }
        internal Node? Right { get; }
        internal InputBlock Block { get; }
        internal int Count { get; }
        internal Node(InputBlock block)
        {
            Block = block; Count = 1; block.Arena?.AddReference();
        }
        internal Node(Node? left, Node? right)
        {
            Left = left; Right = right; Count = (left?.Count ?? 0) + (right?.Count ?? 0);
        }
        internal void AddReference() => Interlocked.Increment(ref _references);
        internal void Release()
        {
            if (Interlocked.Decrement(ref _references) != 0) return;
            Left?.Release(); Right?.Release(); Block.Arena?.Dispose();
        }
    }
}

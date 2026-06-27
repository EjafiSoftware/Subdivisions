using Unity.Collections;
using Unity.Entities;

namespace Subdivisions.Systems.SubdivisionsToolJobs
{
    internal struct HeapItem
    {
        public float _priority;
        public float _cost;
        public Entity _node;
    }

    /// <summary>Binary min-heap keyed by priority, used for the boundary path-find frontier.</summary>
    internal struct MinHeap
    {
        private NativeList<HeapItem> _items;

        public static MinHeap Allocate(int capacity, Allocator allocator)
        {
            return new MinHeap { _items = new NativeList<HeapItem>(capacity, allocator) };
        }

        public int Count => _items.Length;

        public void Push(float priority, float cost, Entity node)
        {
            _items.Add(new HeapItem { _priority = priority, _cost = cost, _node = node });
            var i = _items.Length - 1;
            while (i > 0)
            {
                var parent = (i - 1) / 2;
                if (_items[parent]._priority <= _items[i]._priority)
                {
                    break;
                }
                (_items[parent], _items[i]) = (_items[i], _items[parent]);
                i = parent;
            }
        }

        public HeapItem Pop()
        {
            var root = _items[0];
            var last = _items.Length - 1;
            _items[0] = _items[last];
            _items.RemoveAt(last);

            var n = _items.Length;
            var i = 0;
            while (true)
            {
                var left = 2 * i + 1;
                var right = 2 * i + 2;
                var smallest = i;
                if (left < n && _items[left]._priority < _items[smallest]._priority)
                {
                    smallest = left;
                }
                if (right < n && _items[right]._priority < _items[smallest]._priority)
                {
                    smallest = right;
                }
                if (smallest == i)
                {
                    break;
                }
                (_items[i], _items[smallest]) = (_items[smallest], _items[i]);
                i = smallest;
            }
            return root;
        }

        public void Dispose()
        {
            _items.Dispose();
        }
    }
}

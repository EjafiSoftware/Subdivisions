using System.Collections.Generic;
using System.Linq;
using AutoBogus;
using AwesomeAssertions;
using NUnit.Framework;
using Subdivisions.Core;
using Unity.Entities;

namespace Subdivisions.Tests
{
    [TestFixture]
    public class MinHeapTests
    {
        [Test]
        public void Pop_DrainsItemsInAscendingPriorityWithPayloadIntact()
        {
            var entries = new AutoFaker<HeapEntry>()
                .RuleFor(e => e.Priority, f => f.Random.Float(-1000f, 1000f))
                .Generate(50);

            var heap = new MinHeap();
            for (var i = 0; i < entries.Count; i++)
            {
                heap.Push(entries[i].Priority, entries[i].Priority * 2f, Node(i + 1));
            }

            var popped = new List<HeapItem>();
            while (heap.Count > 0)
            {
                popped.Add(heap.Pop());
            }

            var expected = entries.Select((e, i) => new
            {
                _priority = e.Priority,
                _cost = e.Priority * 2f,
                _node = Node(i + 1),
            });

            popped.Should().HaveCount(entries.Count);
            popped.Should().BeInAscendingOrder(p => p._priority);
            popped.Select(p => new { p._priority, p._cost, p._node }).Should().BeEquivalentTo(expected);
        }

        [Test]
        public void Pop_ReturnsLowestPriorityFirst()
        {
            var heap = new MinHeap();
            heap.Push(5f, 0f, Node(1));
            heap.Push(1f, 0f, Node(2));
            heap.Push(3f, 0f, Node(3));

            heap.Pop()._node.Should().Be(Node(2));
        }

        [Test]
        public void Count_TracksPushesAndPops()
        {
            var heap = new MinHeap();
            heap.Count.Should().Be(0);

            heap.Push(3f, 0f, Node(1));
            heap.Push(1f, 0f, Node(2));
            heap.Count.Should().Be(2);

            heap.Pop();
            heap.Count.Should().Be(1);
        }

        [Test]
        public void Clear_EmptiesTheHeap()
        {
            var heap = new MinHeap();
            heap.Push(1f, 0f, Node(1));
            heap.Push(2f, 0f, Node(2));

            heap.Clear();

            heap.Count.Should().Be(0);
        }

        private static Entity Node(int index) => new()
            { Index = index, Version = 1 };

        private class HeapEntry
        {
            public float Priority { get; set; }
        }
    }
}

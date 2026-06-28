using System.Collections.Generic;
using Game.Areas;
using Game.Common;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;

namespace Subdivisions.Domain
{
    /// <summary>
    /// Emits the components that create an Area from a polygon (a district is an Area):
    /// a closed <see cref="Node"/> buffer, a <see cref="CreationDefinition"/> for the prefab,
    /// and an <see cref="Updated"/> tag. Area generation tessellates and commits on apply.
    /// </summary>
    public static class AreaDefinitionCreation
    {
        /// <summary>Writes the polygon as a closed ring of area nodes (last node == first).</summary>
        public static void AsDynamicBufferNodes(EntityCommandBuffer ecb, Entity entity, IReadOnlyList<float3> points)
        {
            var nodeBuffer = ecb.AddBuffer<Node>(entity);
            nodeBuffer.ResizeUninitialized(points.Count + 1);
            for (var i = 0; i < points.Count; i++)
            {
                nodeBuffer[i] = new Node(points[i], float.MinValue);
            }
            nodeBuffer[points.Count] = nodeBuffer[0];
        }

        /// <summary>Tags the entity as a creation definition for the given prefab entity.</summary>
        public static void WithCreationDefinition(EntityCommandBuffer ecb, Entity entity, Entity prefab)
        {
            var definition = default(CreationDefinition);
            definition.m_Prefab = prefab;
            ecb.AddComponent(entity, definition);
            ecb.AddComponent(entity, default(Updated));
        }
    }
}

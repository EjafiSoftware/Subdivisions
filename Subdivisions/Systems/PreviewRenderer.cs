using Colossal.Mathematics;
using Game.Rendering;
using Subdivisions.Core;
using Unity.Collections;
using UnityEngine;

namespace Subdivisions.Systems
{
    internal sealed class PreviewRenderer
    {
        private const float PointDiameter = 8f;
        private const float StartDiameter = 12f;
        private const float LineWidth = 2f;

        private static readonly Color PointColor = new(0.2f, 0.7f, 1f, 0.9f);
        private static readonly Color SnapColor = new(1f, 0.85f, 0.2f, 0.9f);
        private static readonly Color AreaColor = new(1f, 0.45f, 0.9f, 0.9f);
        private static readonly Color CloseColor = new(0.3f, 1f, 0.4f, 1f);
        private static readonly Color LineColor = new(0.2f, 0.7f, 1f, 0.5f);

        private readonly OverlayRenderSystem _overlay;

        public PreviewRenderer(OverlayRenderSystem overlay)
        {
            _overlay = overlay;
        }

        public void Draw(NativeList<SnapPoint> controlPoints, SnapPoint hover, bool canClose)
        {
            var buffer = _overlay.GetBuffer(out var deps);
            deps.Complete();

            for (var i = 0; i < controlPoints.Length; i++)
            {
                var next = i + 1 < controlPoints.Length ? controlPoints[i + 1].Position : hover.Position;
                buffer.DrawLine(LineColor, new Line3.Segment(controlPoints[i].Position, next), LineWidth, false);
            }

            for (var i = 0; i < controlPoints.Length; i++)
            {
                if (i == 0 && controlPoints.Length >= 3)
                {
                    buffer.DrawCircle(canClose ? CloseColor : PointColor, controlPoints[0].Position, StartDiameter);
                }
                else
                {
                    buffer.DrawCircle(PointColor, controlPoints[i].Position, PointDiameter);
                }
            }

            if (!canClose)
            {
                buffer.DrawCircle(CursorColor(hover), hover.Position, PointDiameter);
            }
        }

        private static Color CursorColor(SnapPoint hover)
        {
            if (hover.OnNet)
            {
                return SnapColor;
            }
            return hover.OnArea ? AreaColor : PointColor;
        }
    }
}

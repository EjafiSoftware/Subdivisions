using Game.Input;

namespace Subdivisions.Systems
{
    internal enum ToolEditAction
    {
        None,
        AddPoint,
        RemoveLast,
        Close,
    }

    /// <summary>Maps this frame's raw tool input to a single editing intent.</summary>
    internal static class ToolInputReader
    {
        public static ToolEditAction Read(IProxyAction apply, IProxyAction secondaryApply, bool canClose)
        {
            if (secondaryApply.WasPressedThisFrame())
            {
                return ToolEditAction.RemoveLast;
            }
            if (apply.WasPressedThisFrame())
            {
                return canClose ? ToolEditAction.Close : ToolEditAction.AddPoint;
            }
            return ToolEditAction.None;
        }
    }
}

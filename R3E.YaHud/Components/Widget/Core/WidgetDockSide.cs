using System.ComponentModel.DataAnnotations;

namespace R3E.YaHud.Components.Widget.Core
{
    /// <summary>
    /// Where a widget is pinned to the screen. Anything other than <see cref="Free"/> makes the
    /// widget <em>docked</em>: it is placed by its own stylesheet instead of by
    /// <see cref="BasicSettings.XPercent"/>/<see cref="BasicSettings.YPercent"/>, it takes no drag
    /// handler, and it therefore keeps its controls usable while the HUD is unlocked.
    /// </summary>
    /// <remarks>
    /// Deliberately a shared enum rather than a per-widget one: the edge rails here and the planned
    /// full-width position bar are the same concept seen from two sides, and a common enum keeps the
    /// host CSS classes (<c>dock-left</c>, <c>dock-right</c>, …) and the visibility predicate in one
    /// place.
    /// </remarks>
    public enum WidgetDockSide
    {
        /// <summary>Not docked: freely draggable and positioned by percentage, as widgets always were.</summary>
        [Display(Name = "Free")]
        Free,

        /// <summary>Pinned to the left screen edge, vertically centred.</summary>
        [Display(Name = "Left")]
        Left,

        /// <summary>Pinned to the right screen edge, vertically centred.</summary>
        [Display(Name = "Right")]
        Right
    }
}

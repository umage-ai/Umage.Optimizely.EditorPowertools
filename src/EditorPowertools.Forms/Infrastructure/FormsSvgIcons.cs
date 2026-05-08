namespace UmageAI.Optimizely.EditorPowerTools.Forms.Infrastructure;

/// <summary>
/// Inline SVG path data for Forms add-on tool icons. Each constant is the
/// inner markup of a 24x24 stroked icon (no surrounding <c>&lt;svg&gt;</c> tag).
/// </summary>
internal static class FormsSvgIcons
{
    // Document with bullet points + checkmark — represents a form.
    public const string FormsOverview =
        @"<rect x=""4"" y=""3"" width=""16"" height=""18"" rx=""2""/><line x1=""8"" y1=""8"" x2=""16"" y2=""8""/><line x1=""8"" y1=""12"" x2=""16"" y2=""12""/><line x1=""8"" y1=""16"" x2=""12"" y2=""16""/><polyline points=""14 16 15.5 17.5 18 14""/>";

    // Stacked horizontal bars + clock face — represents an event timeline.
    public const string SubmissionsTimeline =
        @"<line x1=""4"" y1=""6"" x2=""20"" y2=""6""/><line x1=""4"" y1=""12"" x2=""14"" y2=""12""/><line x1=""4"" y1=""18"" x2=""10"" y2=""18""/><circle cx=""18"" cy=""15"" r=""4""/><polyline points=""18 13 18 15 19.5 16""/>";
}

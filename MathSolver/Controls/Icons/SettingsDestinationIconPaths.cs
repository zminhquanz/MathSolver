using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>Shared SVG paths for settings menu destinations and their page headers.</summary>
public static class SettingsDestinationIconPaths
{
    // Original vector artwork also ships as editable SVG masters in Resources/Images.
    public const string Theme = "M18.6 15.8A8.4 8.4 0 0 1 8.2 5.4 8.5 8.5 0 1 0 18.6 15.8Z M17 2l.8 1.7L19.5 4l-1.7.8L17 6.5l-.8-1.7L14.5 4l1.7-.3Z";
    public const string Palette = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10h1.2c2.15 0 3.15-2.75 1.35-4.05-.97-.71-.5-2.2.8-2.2h2.3C20.4 15.75 22 14.12 22 11.5 22 6.25 17.55 2 12 2ZM7 7.1a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8Zm5-2a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8Zm5 2a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8ZM5.5 12a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8Z";
    public const string Close = "M6.4 5 12 10.6 17.6 5 19 6.4 13.4 12 19 17.6 17.6 19 12 13.4 6.4 19 5 17.6 10.6 12 5 6.4Z";
    public const string Video = "M4 5h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2Zm0 2v10h16V7H4Zm6 2 5 3-5 3V9Z";
    // Small, solid 24x24 symbols for full Settings page headings. SVG masters are editable.
    public const string Motion = "M2 5h7v2H2z M1 11h8v2H1z M2 17h7v2H2z M11 4.5 22 12 11 19.5z";
    public const string Density = "M3 3h8v8H3z M13 3h8v8h-8z M3 13h8v8H3z M13 13h8v8h-8z";
    // One connected silhouette and a counter cutout keep the crossbar seamless at 16px.
    public const string Typography = "M12 4 20 20H17.4L15.8 16.7H8.2L6.6 20H4z M12 8.8 9.15 14.7h5.7z";
    public const string Language = "M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20z M4.3 11h3.1c.1-2 .4-3.8 1.1-5.3A8.1 8.1 0 0 0 4.3 11z M4.3 13a8.1 8.1 0 0 0 4.2 5.3c-.7-1.5-1-3.3-1.1-5.3z M9.4 11h5.2c-.2-2.6-1-4.8-2.6-6.7-1.6 1.9-2.4 4.1-2.6 6.7z M9.4 13c.2 2.6 1 4.8 2.6 6.7 1.6-1.9 2.4-4.1 2.6-6.7z M15.5 5.7c.7 1.5 1 3.3 1.1 5.3h3.1a8.1 8.1 0 0 0-4.2-5.3z M16.6 13c-.1 2-.4 3.8-1.1 5.3a8.1 8.1 0 0 0 4.2-5.3z";
    public const string Result = "M4 3h16v3H4z M4 9h10v2H4z M4 14h16v2H4z M4 19h10v2H4z M17 10h2v3h-2z";
    public const string Developer = "M8 6 2 12l6 6 1.5-1.5L5 12l4.5-4.5z M16 6l-1.5 1.5L19 12l-4.5 4.5L16 18l6-6z M13.1 3 9.1 21h2.1L15.2 3z";
    public const string Reset = "M12 3a9 9 0 1 1-8.7 11.3l2.2-.5A6.8 6.8 0 1 0 7 7.7L10 11H2V3l3.5 3.5A8.9 8.9 0 0 1 12 3z";

    public static string DataManagement => Icons8LicensedPaths.For("Database") ?? OriginalDataManagement;
    private const string OriginalDataManagement = "M480-880C680-880 840-820 840-740C840-660 680-600 480-600C280-600 120-660 120-740C120-820 280-880 480-880Z M120-600C280-490 680-490 840-600V-440C840-360 680-300 480-300C280-300 120-360 120-440Z M120-300C280-190 680-190 840-300V-180C840-100 680-40 480-40C280-40 120-100 120-180Z";

    // Same 24 × 24 geometry as Android's native hardware_chip drawable.
    public const string Hardware = "M6,4H8V2H10V4H14V2H16V4H18L20,6V8H22V10H20V14H22V16H20V18L18,20H16V22H14V20H10V22H8V20H6L4,18V16H2V14H4V10H2V8H4V6Z M6,7V17L7,18H17L18,17V7L17,6H7Z M9,9H15V15H9Z";

    public const string About = "M508.5-291.5Q520-303 520-320v-160q0-17-11.5-28.5T480-520q-17 0-28.5 11.5T440-480v160q0 17 11.5 28.5T480-280q17 0 28.5-11.5Zm0-320Q520-623 520-640t-11.5-28.5Q497-680 480-680t-28.5 11.5Q440-657 440-640t11.5 28.5Q463-600 480-600t28.5-11.5ZM480-80q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Zm0-80q134 0 227-93t93-227q0-134-93-227t-227-93q-134 0-227 93t-93 227q0 134 93 227t227 93Zm0-320Z";

    public static string Settings => Icons8LicensedPaths.For("Settings") ?? OriginalSettings;
    private const string OriginalSettings = "M433-80q-27 0-46.5-18T363-142l-9-66q-13-5-24.5-12T307-235l-62 26q-25 11-50 2t-39-32l-47-82q-14-23-8-49t27-43l53-40q-1-7-1-13.5v-27q0-6.5 1-13.5l-53-40q-21-17-27-43t8-49l47-82q14-23 39-32t50 2l62 26q11-8 23-15t24-12l9-66q4-26 23.5-44t46.5-18h94q27 0 46.5 18t23.5 44l9 66q13 5 24.5 12t22.5 15l62-26q25-11 50-2t39 32l47 82q14 23 8 49t-27 43l-53 40q1 7 1 13.5v27q0 6.5-2 13.5l53 40q21 17 27 43t-8 49l-48 82q-14 23-39 32t-50-2l-60-26q-11 8-23 15t-24 12l-9 66q-4 26-23.5 44T527-80h-94Zm7-80h79l14-106q31-8 57.5-23.5T639-327l99 41 39-68-86-65q5-14 7-29.5t2-31.5q0-16-2-31.5t-7-29.5l86-65-39-68-99 42q-22-23-48.5-38.5T533-694l-13-106h-79l-14 106q-31 8-57.5 23.5T321-633l-99-41-39 68 86 64q-5 15-7 30t-2 32q0 16 2 31t7 30l-86 65 39 68 99-42q22 23 48.5 38.5T427-266l13 106Zm42-180q58 0 99-41t41-99q0-58-41-99t-99-41q-59 0-99.5 41T342-480q0 58 40.5 99t99.5 41Zm-2-140Z";
}

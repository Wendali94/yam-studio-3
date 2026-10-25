namespace YamStudio.Core;

/// <summary>One global language switch. Every label in the app is written as T(amharic, english).</summary>
public static class Loc
{
    public static string Lang = "am";
    public static bool Am => Lang == "am";
    public static string T(string am, string en) => Am ? am : en;
    public static string Money(decimal n) => $"{n:N0} {T("ብር", "ETB")}";
}

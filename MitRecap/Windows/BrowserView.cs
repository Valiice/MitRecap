namespace MitRecap.Windows;

internal static class BrowserView
{
    public const string Site = "https://valiice.github.io/MitRecap/";

    private static readonly string Page = Path.Combine(Path.GetTempPath(), "MitRecap.html");

    // Windows cuts a link off at about 2000 characters when a program opens it, and a share code is far longer.
    // So the browser is given a local page that forwards to the site with the code after the #, which is not sent to the server.
    public static void Open(string code)
    {
        File.WriteAllText(Page, $"<!DOCTYPE html><meta charset=\"utf-8\"><title>Mit Recap</title><script>location.replace(\"{Site}#{code}\")</script>");
        Dalamud.Utility.Util.OpenLink(Page);
    }

    public static void Cleanup() => File.Delete(Page);
}

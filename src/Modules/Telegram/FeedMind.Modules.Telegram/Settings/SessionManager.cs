namespace FeedMind.Modules.Telegram.Settings;

public sealed class SessionManager
{
    private readonly string _sessionPath;

    public SessionManager(TelegramSettings settings, string appHome)
    {
        _sessionPath = SetupSession(settings.WTelegramSession, appHome);
    }

    public string GetSessionPath() => _sessionPath;

    private static string SetupSession(string sessionPath, string appHome)
    {
        var fullPath = Path.IsPathRooted(sessionPath) ? sessionPath : Path.Combine(appHome, sessionPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Telegram session file not found: {fullPath}");
        }

        return fullPath;
    }
}

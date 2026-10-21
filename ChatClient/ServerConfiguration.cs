namespace ChatClient;

public static class ServerConfiguration
{
    private const string DefaultServerUrl = "http://localhost:5291";

    public static string ServerUrl { get; } = GetServerUrl();

    private static string GetServerUrl()
    {
        string? configuredUrl = Environment.GetEnvironmentVariable("MINIDISCORD_SERVER_URL");

        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        return DefaultServerUrl;
    }
}

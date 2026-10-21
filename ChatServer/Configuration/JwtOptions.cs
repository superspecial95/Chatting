namespace ChatServer.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; init; } = "";

    public string Issuer { get; init; } = "MiniDiscord";

    public string Audience { get; init; } = "MiniDiscordClient";

    public int ExpirationHours { get; init; } = 24;
}

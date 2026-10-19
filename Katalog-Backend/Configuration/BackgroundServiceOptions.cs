namespace Katalog_Backend.Configuration;

public sealed class BackgroundServiceOptions
{
    public const string SectionName = "BackgroundServices";

    public bool ConsumerEnabled { get; set; } = true;
    public bool OutboxPublisherEnabled { get; set; } = true;
}

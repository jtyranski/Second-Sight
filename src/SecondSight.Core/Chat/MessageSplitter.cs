namespace SecondSight.Core.Chat;

/// <summary>Splits long text into pieces Second Life will accept (chat and IMs max out near 1,023 bytes).</summary>
public static class MessageSplitter
{
    public const int MaxChunkLength = 900;

    public static IReadOnlyList<string> Split(string text, int max = MaxChunkLength)
    {
        var chunks = new List<string>();
        var remaining = text.Trim();
        while (remaining.Length > max)
        {
            var cut = remaining.LastIndexOf(' ', max);
            if (cut < max / 2) cut = max;
            chunks.Add(remaining[..cut].TrimEnd());
            remaining = remaining[cut..].TrimStart();
        }
        if (remaining.Length > 0) chunks.Add(remaining);
        return chunks;
    }
}

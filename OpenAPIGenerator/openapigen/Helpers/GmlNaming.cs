namespace openapigen.Helpers
{
    public sealed record GmlNaming(string Prefix = "gm")
    {
        public string Pub => $"{Prefix.ToLowerInvariant()}_";
        public string Priv => $"_{Prefix.ToLowerInvariant()}_";
        public string Mac => $"{Prefix.ToUpperInvariant()}_";
        public string StructPrefix => char.ToUpperInvariant(Prefix[0]) + Prefix[1..];
    }
}

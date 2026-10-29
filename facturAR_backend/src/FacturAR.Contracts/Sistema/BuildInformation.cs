namespace FacturAR.Contracts.Sistema;

public sealed record BuildInformation(string Version, string Commit)
{
    public const string VersionKey = "APP_VERSION";
    public const string CommitKey = "SOURCE_COMMIT";
    public const string DefaultVersion = "0.1.0";
    public const string UnknownCommit = "unknown";
}

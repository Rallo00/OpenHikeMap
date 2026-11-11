namespace OpenHikeMap.Services;

public static class ClientAssetVersion
{
    public static string Value { get; } = typeof(ClientAssetVersion).Assembly.ManifestModule.ModuleVersionId.ToString("N");
}

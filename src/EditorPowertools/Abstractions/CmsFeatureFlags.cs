namespace UmageAI.Optimizely.EditorPowerTools.Abstractions;

internal static class CmsFeatureFlags
{
#if OPTIMIZELY_CMS13
    public const bool ContractsAvailable = true;
#else
    public const bool ContractsAvailable = false;
#endif
}

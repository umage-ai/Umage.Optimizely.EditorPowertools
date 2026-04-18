namespace UmageAI.Optimizely.EditorPowerTools.Abstractions;

public sealed record ContentTypeMetadata(
    bool IsContract,
    IReadOnlyList<ContractRef> Contracts,
    IReadOnlyList<string> CompositionBehaviors)
{
    public static readonly ContentTypeMetadata Empty = new(
        IsContract: false,
        Contracts: Array.Empty<ContractRef>(),
        CompositionBehaviors: Array.Empty<string>());
}

public sealed record ContractRef(int Id, Guid Guid, string Name, string? DisplayName);

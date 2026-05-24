namespace ContextFilter.Core.Models;

/// <summary>
/// Lightweight filters applied before the expensive parameter tree is built.
/// </summary>
public sealed record ElementPreFilterOptions(
    bool IsEnabled,
    bool IncludeRebar,
    bool IncludeWalls,
    bool IncludeFloors,
    bool IncludeFoundations,
    bool IncludeGenericModels)
{
    public static ElementPreFilterOptions Default { get; } = new(
        false,
        true,
        true,
        true,
        true,
        true);
}

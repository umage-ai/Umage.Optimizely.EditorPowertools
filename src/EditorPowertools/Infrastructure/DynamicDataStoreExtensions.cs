using EPiServer.Data.Dynamic;

namespace UmageAI.Optimizely.EditorPowerTools.Infrastructure;

/// <summary>
/// Helpers for <see cref="DynamicDataStore"/> that avoid the pitfalls of its built-in bulk operations.
/// </summary>
public static class DynamicDataStoreExtensions
{
    /// <summary>
    /// Deletes every item one identity at a time instead of calling <see cref="DynamicDataStore.DeleteAll"/>,
    /// which issues a single unbatched DELETE for the whole table inside one transaction. On a table that has
    /// grown large that statement can run past the SQL command timeout, causing the delete to be rolled back
    /// entirely (leaving zero rows removed) and the exception to bubble up. Batched, per-identity deletes keep
    /// each round trip small and make partial progress if something fails partway through.
    /// </summary>
    public static void ClearInBatches<T>(this DynamicDataStore store) where T : IDynamicData
    {
        foreach (var identity in store.Items<T>().Select(item => item.Id).ToList())
        {
            store.Delete(identity);
        }
    }
}

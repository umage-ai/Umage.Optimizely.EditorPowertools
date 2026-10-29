using EPiServer.Data;
using EPiServer.Data.Dynamic;

namespace UmageAI.Optimizely.EditorPowerTools.Infrastructure;

/// <summary>
/// Helpers for <see cref="DynamicDataStore"/> that avoid the pitfalls of its built-in bulk operations.
/// </summary>
public static class DynamicDataStoreExtensions
{
    private const int BatchSize = 500;

    /// <summary>
    /// Deletes every item one identity at a time instead of calling <see cref="DynamicDataStore.DeleteAll"/>,
    /// which issues a single unbatched DELETE for the whole table inside one transaction. On a table that has
    /// grown large that statement can run past the SQL command timeout, causing the delete to be rolled back
    /// entirely (leaving zero rows removed) and the exception to bubble up. Batched, per-identity deletes keep
    /// each round trip small and make partial progress if something fails partway through.
    /// </summary>
    /// <remarks>
    /// Loads whole items rather than projecting <c>item.Id</c> in the query: the DDS LINQ provider cannot
    /// project the identity and throws <c>FormatException: Invalid formatted string: ""</c>.
    /// </remarks>
    public static void ClearInBatches<T>(this DynamicDataStore store) where T : IDynamicData
    {
        Identity? previousFirst = null;
        while (true)
        {
            var batch = store.Items<T>().Take(BatchSize).ToList();
            if (batch.Count == 0)
            {
                return;
            }

            // A batch that starts where the last one did means the deletes did not stick; stop rather than spin.
            if (previousFirst is not null && batch[0].Id.Equals(previousFirst))
            {
                throw new InvalidOperationException($"Clearing the {typeof(T).Name} store made no progress.");
            }

            previousFirst = batch[0].Id;
            foreach (var item in batch)
            {
                store.Delete(item.Id);
            }

            if (batch.Count < BatchSize)
            {
                return;
            }
        }
    }
}

namespace MonadicSharp.Persistence.Core;

/// <summary>
/// Typed error factory for all persistence failures.
/// All errors are <see cref="Error"/> values — they flow through Result pipelines
/// without causing unhandled exceptions.
/// </summary>
public static class PersistenceError
{
    /// <summary>The requested entity was not found.</summary>
    public static Error NotFound(string entityName, object id) =>
        Error.Create($"Entity '{entityName}' with id '{id}' was not found.", "PERSISTENCE_NOT_FOUND")
             .WithMetadata("EntityName", entityName)
             .WithMetadata("EntityId", id.ToString() ?? string.Empty);

    /// <summary>An entity with the same key already exists.</summary>
    public static Error Conflict(string entityName, object id) =>
        Error.Create($"Entity '{entityName}' with id '{id}' already exists.", "PERSISTENCE_CONFLICT")
             .WithMetadata("EntityName", entityName)
             .WithMetadata("EntityId", id.ToString() ?? string.Empty);

    /// <summary>A concurrency conflict occurred — the entity was modified by another process.</summary>
    public static Error ConcurrencyConflict(string entityName, object id) =>
        Error.Create(
            $"Concurrency conflict on entity '{entityName}' with id '{id}'. The entity was modified by another process.",
            "PERSISTENCE_CONCURRENCY_CONFLICT")
             .WithMetadata("EntityName", entityName)
             .WithMetadata("EntityId", id.ToString() ?? string.Empty);

    /// <summary>The underlying database threw an unexpected exception.</summary>
    public static Error DatabaseError(string operation, Exception ex) =>
        Error.FromException(ex, "PERSISTENCE_DATABASE_ERROR")
             .WithMetadata("DatabaseOperation", operation);

    /// <summary>A query produced more results than expected.</summary>
    public static Error TooManyResults(string entityName, int count) =>
        Error.Create(
            $"Query for '{entityName}' returned {count} results where exactly one was expected.",
            "PERSISTENCE_TOO_MANY_RESULTS")
             .WithMetadata("EntityName", entityName)
             .WithMetadata("ResultCount", count);
}

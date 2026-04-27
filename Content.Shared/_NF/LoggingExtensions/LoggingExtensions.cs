using Content.Shared.Stacks;

namespace Content.Shared._NF.LoggingExtensions;

public static class LoggingExtensions
{
    [Dependency] private static readonly SharedStackSystem _stack = default!;

    public static string GetExtraLogs(EntityManager entityManager, EntityUid entity)
    {
        // TODO: Fix this, entity throwing nullreference error in SharedHandsSystem.Pickup
        // Get details from the stack component to track amount of things in the stack.
        //if (entityManager.HasComponent<StackComponent>(entity))
        //{
        //    return $"(StackCount: {_stack.GetCount(entity).ToString()})";
        //}

        // Add more logging things here when needed.

        return "";
    }
}

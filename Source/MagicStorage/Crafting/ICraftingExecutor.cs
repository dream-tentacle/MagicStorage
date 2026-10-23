using System.Collections.Generic;
using Verse;

namespace MagicStorage
{
    // An executor borrows an order; its materials, progress and staged output stay in the core.
    internal interface ICraftingExecutor
    {
        Thing WorkerThing { get; }
        Building_StorageCore Core { get; }
        CraftingOrder Order { get; }
        bool AssignmentActive { get; }
        bool Allows(CraftingOrder order);
        bool CanContinue();
        void CancelAssignment();
        void ReleaseAssignment();
        float WorkSpeed(RecipeDef recipe);
        void Learn(RecipeDef recipe, int ticks);
        IEnumerable<Thing> MakeProducts(RecipeDef recipe, List<Thing> ingredients, Thing dominant);
        void NotifyCompleted(List<Thing> products);
    }
}

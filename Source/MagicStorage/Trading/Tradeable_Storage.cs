using RimWorld;
using Verse;

namespace MagicStorage
{
    internal sealed class Tradeable_Storage : Tradeable
    {
        private readonly StorageTradeSession session;
        internal Tradeable_Storage(StorageTradeSession session) { this.session = session; }

        public override int CountHeldBy(Transactor trans)
        {
            if (trans != Transactor.Colony) return base.CountHeldBy(trans);
            long count = 0;
            foreach (Thing item in thingsColony) count += session.Available(item);
            return (int)System.Math.Min(int.MaxValue, count);
        }

        public override void ResolveTrade()
        {
            if (ActionToDo == TradeAction.PlayerSells) session.Deliver(this);
            else base.ResolveTrade(); // Native purchases, including pawn handling and tutorial notifications.
        }
    }
}

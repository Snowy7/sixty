using System.Collections.Generic;

namespace Ia.Core.Update
{
    // Pooled struct for lifecycle queue
    internal struct IaLifecycleEntry
    {
        public IaBehaviour Behaviour;
        public IaLifecycleEvent Event;
        public IaUpdateGroup Group;
        public int Priority;

        // Simple pool to reduce allocations
        private static readonly Queue<List<IaLifecycleEntry>> s_listPool = new();

        public static List<IaLifecycleEntry> RentList()
        {
            return s_listPool.Count > 0 ? s_listPool.Dequeue() : new List<IaLifecycleEntry>(32);
        }

        public static void ReturnList(List<IaLifecycleEntry> list)
        {
            list.Clear();
            s_listPool.Enqueue(list);
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ia.Core.Events
{
    /// <summary>
    /// Lightweight type-based event bus for decoupled communication.
    /// </summary>
    public static class IaEventBus
    {
        private interface IEventStream
        {
            void Clear();
            int ListenerCount { get; }
        }

        private sealed class EventStream<T> : IEventStream
        {
            private readonly List<Action<T>> listeners = new List<Action<T>>(8);
            private readonly HashSet<Action<T>> lookup = new HashSet<Action<T>>();
            private readonly List<Action<T>> pendingAdds = new List<Action<T>>(4);
            private readonly List<Action<T>> pendingRemoves = new List<Action<T>>(4);
            private bool isPublishing;

            public int ListenerCount => listeners.Count;

            public void Subscribe(Action<T> handler)
            {
                if (handler == null)
                {
                    return;
                }

                if (isPublishing)
                {
                    if (pendingRemoves.Remove(handler))
                    {
                        return;
                    }

                    if (!lookup.Contains(handler) && !pendingAdds.Contains(handler))
                    {
                        pendingAdds.Add(handler);
                    }

                    return;
                }

                AddInternal(handler);
            }

            public void Unsubscribe(Action<T> handler)
            {
                if (handler == null)
                {
                    return;
                }

                if (isPublishing)
                {
                    if (pendingAdds.Remove(handler))
                    {
                        return;
                    }

                    if (!pendingRemoves.Contains(handler))
                    {
                        pendingRemoves.Add(handler);
                    }

                    return;
                }

                RemoveInternal(handler);
            }

            public void Publish(T evt)
            {
                isPublishing = true;
                try
                {
                    for (int i = 0; i < listeners.Count; i++)
                    {
                        Action<T> callback = listeners[i];
                        if (callback == null)
                        {
                            continue;
                        }

                        try
                        {
                            callback.Invoke(evt);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    }
                }
                finally
                {
                    isPublishing = false;
                    FlushPending();
                }
            }

            public void Clear()
            {
                listeners.Clear();
                lookup.Clear();
                pendingAdds.Clear();
                pendingRemoves.Clear();
                isPublishing = false;
            }

            private void AddInternal(Action<T> handler)
            {
                if (!lookup.Add(handler))
                {
                    return;
                }

                listeners.Add(handler);
            }

            private void RemoveInternal(Action<T> handler)
            {
                if (!lookup.Remove(handler))
                {
                    return;
                }

                listeners.Remove(handler);
            }

            private void FlushPending()
            {
                for (int i = 0; i < pendingRemoves.Count; i++)
                {
                    RemoveInternal(pendingRemoves[i]);
                }

                for (int i = 0; i < pendingAdds.Count; i++)
                {
                    AddInternal(pendingAdds[i]);
                }

                pendingRemoves.Clear();
                pendingAdds.Clear();
            }
        }

        private static readonly Dictionary<Type, IEventStream> Streams = new Dictionary<Type, IEventStream>(64);

        /// <summary>
        /// Subscribe to events of type T.
        /// </summary>
        public static void Subscribe<T>(Action<T> handler)
        {
            GetOrCreateStream<T>().Subscribe(handler);
        }

        /// <summary>
        /// Unsubscribe from events of type T.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            if (Streams.TryGetValue(typeof(T), out IEventStream stream) && stream is EventStream<T> typedStream)
            {
                typedStream.Unsubscribe(handler);
                if (typedStream.ListenerCount == 0)
                {
                    Streams.Remove(typeof(T));
                }
            }
        }

        /// <summary>
        /// Publish an event of type T to all subscribers.
        /// </summary>
        public static void Publish<T>(T evt)
        {
            if (!Streams.TryGetValue(typeof(T), out IEventStream stream))
            {
                return;
            }

            if (stream is EventStream<T> typedStream)
            {
                typedStream.Publish(evt);
            }
        }

        /// <summary>
        /// Returns true when at least one subscriber exists for event type T.
        /// </summary>
        public static bool HasSubscribers<T>()
        {
            return Streams.TryGetValue(typeof(T), out IEventStream stream) && stream.ListenerCount > 0;
        }

        /// <summary>
        /// Returns the current subscriber count for event type T.
        /// </summary>
        public static int GetSubscriberCount<T>()
        {
            return Streams.TryGetValue(typeof(T), out IEventStream stream) ? stream.ListenerCount : 0;
        }

        /// <summary>
        /// Clears all subscribers for event type T.
        /// </summary>
        public static void Clear<T>()
        {
            Type type = typeof(T);
            if (!Streams.TryGetValue(type, out IEventStream stream))
            {
                return;
            }

            stream.Clear();
            Streams.Remove(type);
        }

        /// <summary>
        /// Clears all subscribers (useful for tests or scene reload).
        /// </summary>
        public static void ClearAll()
        {
            foreach (IEventStream stream in Streams.Values)
            {
                stream.Clear();
            }

            Streams.Clear();
        }

        private static EventStream<T> GetOrCreateStream<T>()
        {
            Type type = typeof(T);
            if (Streams.TryGetValue(type, out IEventStream stream))
            {
                return (EventStream<T>)stream;
            }

            EventStream<T> created = new EventStream<T>();
            Streams[type] = created;
            return created;
        }
    }
}

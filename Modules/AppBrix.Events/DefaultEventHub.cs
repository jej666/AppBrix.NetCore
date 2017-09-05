// Copyright (c) MarinAtanasov. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the project root for license information.
//
using AppBrix.Lifecycle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AppBrix.Events
{
    internal sealed class DefaultEventHub : IEventHub, IApplicationLifecycle
    {
        #region IApplicationLifecycle implementation
        public void Initialize(IInitializeContext context)
        {
        }

        public void Uninitialize()
        {
            this.subscriptions.Clear();
        }
        #endregion

        #region IEventHub implementation
        public void Subscribe<T>(Action<T> handler) where T : IEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            this.SubscribeInternal(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            this.UnsubscribeInternal(handler);
        }

        public void Raise<T>(T args) where T : IEvent
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            this.RaiseInternal(args);
        }
        #endregion

        #region Private methods
        private void SubscribeInternal<T>(Action<T> handler) where T : IEvent
        {
            IList<object> handlers;
            if (!this.subscriptions.TryGetValue(typeof(T), out handlers))
            {
                handlers = new List<object>();
                this.subscriptions[typeof(T)] = handlers;
            }
            handlers.Add(handler);
        }

        private void UnsubscribeInternal<T>(Action<T> handler) where T : IEvent
        {
            if (this.subscriptions.TryGetValue(typeof(T), out var handlers))
            {
                // Optimize for unsubscribing the last element since this is the most common scenario.
                for (int i = handlers.Count - 1; i >= 0; i--)
                {
                    if (handlers[i].Equals(handler))
                    {
                        handlers.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private void RaiseInternal<T>(T args) where T : IEvent
        {
            var type = typeof(T);
            var baseType = type;
            while (baseType != null && typeof(IEvent).IsAssignableFrom(baseType))
            {
                this.RaiseEvent(args, baseType);
                baseType = baseType.BaseType;
            }

            foreach (var @interface in type.GetInterfaces())
            {
                if (typeof(IEvent).IsAssignableFrom(@interface))
                {
                    this.RaiseEvent(args, @interface);
                }
            }
        }
        
        private void RaiseEvent<T>(T args, Type eventType) where T : IEvent
        {
            if (this.subscriptions.TryGetValue(eventType, out var handlers))
            {
                for (var i = 0; i < handlers.Count; i++)
                {
                    var handler = (Action<T>)handlers[i];
                    handler(args);

                    // Check if the handler has unsubscribed itself.
                    if (i < handlers.Count && !object.ReferenceEquals(handler, handlers[i]))
                        i--;
                }
            }
        }
        #endregion

        #region Private fields and constants
        private readonly IDictionary<Type, IList<object>> subscriptions = new Dictionary<Type, IList<object>>();
        #endregion
    }
}

﻿#if NETSTANDARD2_1
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nito.Disposables.Internals
{
    /// <summary>
    /// A field containing a bound asynchronous action.
    /// </summary>
    /// <typeparam name="T">The type of context for the action.</typeparam>
    public sealed class BoundAsyncActionField<T>
    {
        private BoundAction? _field;

        /// <summary>
        /// Initializes the field with the specified action and context.
        /// </summary>
        /// <param name="action">The action delegate.</param>
        /// <param name="context">The context.</param>
        public BoundAsyncActionField(Func<T, ValueTask> action, [AllowNull] T context)
        {
            _field = new BoundAction(action, context);
        }

        /// <summary>
        /// Whether the field is empty.
        /// </summary>
        public bool IsEmpty => Interlocked.CompareExchange(ref _field, null, null) == null;

        /// <summary>
        /// Atomically retrieves the bound action from the field and sets the field to <c>null</c>. May return <c>null</c>.
        /// </summary>
        public IBoundAction? TryGetAndUnset()
        {
            return Interlocked.Exchange(ref _field, null);
        }

        /// <summary>
        /// Attempts to update the context of the bound action stored in the field. Returns <c>false</c> if the field is <c>null</c>.
        /// </summary>
        /// <param name="contextUpdater">The function used to update an existing context. This may be called more than once if more than one thread attempts to simultaneously update the context.</param>
        public bool TryUpdateContext(Func<T, T> contextUpdater)
        {
            _ = contextUpdater ?? throw new ArgumentNullException(nameof(contextUpdater));
            while (true)
            {
                var original = Interlocked.CompareExchange(ref _field, null, null);
                if (original == null)
                    return false;
                var updatedContext = new BoundAction(original, contextUpdater);
                var result = Interlocked.CompareExchange(ref _field, updatedContext, original);
                if (ReferenceEquals(original, result))
                    return true;
            }
        }

        /// <summary>
        /// An action delegate bound with its context.
        /// </summary>
        public interface IBoundAction
        {
            /// <summary>
            /// Executes the action. This should only be done after the bound action is retrieved from a field by <see cref="TryGetAndUnset"/>.
            /// </summary>
            ValueTask InvokeAsync();
        }

        private sealed class BoundAction : IBoundAction
        {
            private readonly Func<T, ValueTask>? _action;
            [AllowNull] private readonly T _context;

            public BoundAction(Func<T, ValueTask>? action, [AllowNull] T context)
            {
                _action = action;
                _context = context;
            }

            public BoundAction(BoundAction originalBoundAction, Func<T, T> contextUpdater)
            {
                _action = originalBoundAction._action;
                _context = contextUpdater(originalBoundAction._context);
            }

            public ValueTask InvokeAsync() => _action == null ? new ValueTask() : _action.Invoke(_context);
        }
    }
}
#endif
#nullable enable
using System;

namespace Refactor.Fsm
{
    public readonly struct State<TState, TContext>
        where TState : struct, Enum
    {
        public readonly TState Id;
        public readonly Action<TContext>? Initialization;
        public readonly Action<TContext>? EarlyUpdate;
        public readonly Action<TContext>? FixedUpdate;
        public readonly Action<TContext>? PostFixedUpdate;
        public readonly Action<TContext>? PreUpdate;
        public readonly Action<TContext>? Update;
        public readonly Action<TContext>? PreLateUpdate;
        public readonly Action<TContext>? PostLateUpdate;
        public readonly Action<TContext>? TimeUpdate;
        public readonly Action<TContext>? Enter;
        public readonly Action<TContext>? Exit;

        public State(
            TState id,
            Action<TContext>? initialization = null,
            Action<TContext>? earlyUpdate = null,
            Action<TContext>? fixedUpdate = null,
            Action<TContext>? postFixedUpdate = null,
            Action<TContext>? preUpdate = null,
            Action<TContext>? update = null,
            Action<TContext>? preLateUpdate = null,
            Action<TContext>? postLateUpdate = null,
            Action<TContext>? timeUpdate = null,
            Action<TContext>? enter = null,
            Action<TContext>? exit = null)
        {
            Id = id;
            Initialization = initialization;
            EarlyUpdate = earlyUpdate;
            FixedUpdate = fixedUpdate;
            PostFixedUpdate = postFixedUpdate;
            PreUpdate = preUpdate;
            Update = update;
            PreLateUpdate = preLateUpdate;
            PostLateUpdate = postLateUpdate;
            TimeUpdate = timeUpdate;
            Enter = enter;
            Exit = exit;
        }
    }

    public readonly struct Transition<TState, TContext> : IComparable<Transition<TState, TContext>>
        where TState : struct, Enum
    {
        public readonly TState From;
        public readonly TState To;
        public readonly Func<TContext, object?, bool> Condition;
        public readonly object? State;
        public readonly int Priority;

        public Transition(
            TState from, 
            TState to, 
            Func<TContext, object?, bool> condition, 
            object? state = null,
            int priority = 0)
        {
            From = from;
            To = to;
            Condition = condition;
            State = state;
            Priority = priority;
        }

        public int CompareTo(Transition<TState, TContext> other)
        {
            // Descending sort order
            return other.Priority.CompareTo(Priority);
        }
    }

    public sealed class StatefulCondition<TContext, TState>
    {
        private readonly TState _state;
        private readonly Func<TContext, TState, bool> _predicate;

        public StatefulCondition(TState state, Func<TContext, TState, bool> predicate)
        {
            _state = state;
            _predicate = predicate;
        }

        public bool Evaluate(TContext ctx) => _predicate(ctx, _state);
    }

    internal static class ConditionWrappers<TContext>
    {
        public static readonly Func<TContext, object?, bool> Default =
            static (ctx, state) => ((Func<TContext, bool>)state!)(ctx);
    }

    internal static class ConditionWrappers<TContext, TState>
    {
        public static readonly Func<TContext, object?, bool> Stateful = 
            static (ctx, state) => ((StatefulCondition<TContext, TState>)state!).Evaluate(ctx);
    }
}

using System;

namespace Refactor.Fsm
{
    public static partial class Fsms
    {
        private const int DefaultSize = 8;

        public static FsmBuilder<TState, TContext> Create<TState, TContext>(int size = DefaultSize)
            where TState : struct, Enum
            => FsmBuilder<TState, TContext>.Create(size);

        public static FsmBuilder<TState, TContext> From<TState, TContext>(Fsm<TState, TContext> existing)
            where TState : struct, Enum
            => new(existing);
    }
}
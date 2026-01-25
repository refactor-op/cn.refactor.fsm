using System;

namespace Refactor.Fsm
{
    public static class Fsms
    {
        public static FsmBuilder<TState, TContext> Create<TState, TContext>(int stateCapacity = 8, int transitionCapacity = 8) 
            where TState : struct, Enum =>
            FsmBuilder<TState, TContext>.Create(stateCapacity, transitionCapacity);

        public static FsmBuilder<TState, TContext> From<TState, TContext>(Fsm<TState, TContext> fsm) 
            where TState : struct, Enum =>
            FsmBuilder<TState, TContext>.From(fsm);
    }
}

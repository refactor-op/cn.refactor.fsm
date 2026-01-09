using System;

namespace Refactor.Fsm
{
    public abstract class StateHandler<TState, TContext> where TState : struct, Enum
    {
        public virtual void OnInitialEnter(TContext ctx, Fsm<TState, TContext> fsm)
        {
        }

        public virtual void OnEnter(TState from, TContext ctx, Fsm<TState, TContext> fsm)
        {
        }

        public virtual void OnExit(TState to, TContext ctx, Fsm<TState, TContext> fsm)
        {
        }

        public virtual void OnReenter(TContext ctx, Fsm<TState, TContext> fsm)
        {
        }

        public virtual void OnPause(TContext ctx, Fsm<TState, TContext> fsm)
        {
        }

        public virtual void OnResume(TContext ctx, Fsm<TState, TContext> fsm)
        {
        }

        public virtual void OnUpdate(TContext ctx, Fsm<TState, TContext> fsm)
        {
        }

        public virtual void OnFixedUpdate(TContext ctx, Fsm<TState, TContext> fsm)
        {
        }

        public virtual void OnLateUpdate(TContext ctx, Fsm<TState, TContext> fsm)
        {
        }
    }
}
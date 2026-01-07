using System;

namespace Refactor.Fsm
{
    public interface IEnterHandler<TState, TContext> where TState : struct, Enum
    {
        void OnEnter(TState fromState, TContext context);
    }

    public interface IExitHandler<TState, TContext> where TState : struct, Enum
    {
        void OnExit(TState toState, TContext context);
    }

    public interface IUpdateHandler<TContext>
    {
        void OnUpdate(TContext context);
    }

    public interface IFixedUpdateHandler<TContext>
    {
        void OnFixedUpdate(TContext context);
    }

    public interface ILateUpdateHandler<TContext>
    {
        void OnLateUpdate(TContext context);
    }
}

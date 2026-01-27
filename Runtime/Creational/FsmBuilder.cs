#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Refactor.Fsm
{
    internal struct FsmBuilderShared<TState, TContext>
        where TState : struct, Enum
    {
        public State<TState, TContext>[] States;
        public int StateCount;
        public Transition<TState, TContext>[] Transitions;
        public int TransitionCount;
        public TState InitialState;
        public bool HasInitialState;
        public TContext Context;
    }

    public ref struct FsmBuilder<TState, TContext>
        where TState : struct, Enum
    {
        private static readonly ArrayPool<State<TState, TContext>>
            StatePool = ArrayPool<State<TState, TContext>>.Shared;

        private static readonly ArrayPool<Transition<TState, TContext>> TransitionPool =
            ArrayPool<Transition<TState, TContext>>.Shared;

        private static readonly ArrayPool<FsmBuilderShared<TState, TContext>> SharedPool =
            ArrayPool<FsmBuilderShared<TState, TContext>>.Shared;

        internal FsmBuilderShared<TState, TContext>[] Shared;

        internal static FsmBuilder<TState, TContext> Create(int stateCapacity, int transitionCapacity)
        {
            var shared = SharedPool.Rent(1);
            shared[0] = default;
            shared[0].States = StatePool.Rent(stateCapacity);
            shared[0].Transitions = TransitionPool.Rent(transitionCapacity);
            return new FsmBuilder<TState, TContext> { Shared = shared };
        }

        internal static FsmBuilder<TState, TContext> From(Fsm<TState, TContext> fsm)
        {
            var builder = Create(fsm.RawStates.Length, fsm.RawTransitions.Length);
            ref var data = ref builder.Shared[0];
            data.Context = fsm.Context;
            data.InitialState = fsm.InitialStateId;
            data.HasInitialState = true;
            Array.Copy(fsm.RawStates, data.States, fsm.RawStates.Length);
            data.StateCount = fsm.RawStates.Length;
            Array.Copy(fsm.RawTransitions, data.Transitions, fsm.RawTransitions.Length);
            data.TransitionCount = fsm.RawTransitions.Length;
            return builder;
        }

        #region Global

        public StateBuilder<TState, TContext> AddState(TState id) => new(Shared, id, false);
        public StateBuilder<TState, TContext> ModifyState(TState id) => new(Shared, id, true);

        public FsmBuilder<TState, TContext> RemoveState(TState stateId)
        {
            var idx = GetStateIndex(stateId);
            if (idx < 0) return this;

            ref var data = ref Shared[0];
            Array.Copy(data.States, idx + 1, data.States, idx, data.StateCount - idx - 1);
            data.States[--data.StateCount] = default;

            for (var i = data.TransitionCount - 1; i >= 0; i--)
            {
                var t = data.Transitions[i];
                if (StateEquals(t.From, stateId) || StateEquals(t.To, stateId))
                    RemoveTransitionAt(i);
            }

            if (data.HasInitialState && StateEquals(data.InitialState, stateId))
                data.HasInitialState = false;

            return this;
        }

        public FsmBuilder<TState, TContext> StartWith(TState state)
        {
            ref var data = ref Shared[0];
            data.InitialState = state;
            data.HasInitialState = true;
            return this;
        }

        public FsmBuilder<TState, TContext> ContextWith(TContext context)
        {
            Shared[0].Context = context;
            return this;
        }

        public Fsm<TState, TContext> Build()
        {
            ref var data = ref Shared[0];
            if (data.StateCount == 0 || !data.HasInitialState)
                throw new InvalidOperationException("FSM invalid.");
            var rs = new State<TState, TContext>[data.StateCount];
            Array.Copy(data.States, rs, data.StateCount);
            var rt = new Transition<TState, TContext>[data.TransitionCount];
            Array.Copy(data.Transitions, rt, data.TransitionCount);
            Array.Sort(rt);
            
            return new Fsm<TState, TContext>(rs, rt, data.InitialState, data.Context);
        }

        public void Dispose()
        {
            if (Shared == null) return;

            ref var data = ref Shared[0];
            StatePool.Return(data.States, true);
            TransitionPool.Return(data.Transitions, true);
            var toReturn = Shared;
            Shared = null!;
            SharedPool.Return(toReturn, true);
        }

        #endregion

        #region Shared Helper

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool StateEquals(TState a, TState b) 
            => EqualityComparer<TState>.Default.Equals(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int GetStateIndex(TState id)
        {
            ref var data = ref Shared[0];
            for (var i = 0; i < data.StateCount; i++)
                if (StateEquals(data.States[i].Id, id))
                    return i;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveTransitionAt(int index)
        {
            ref var data = ref Shared[0];
            Array.Copy(data.Transitions, index + 1, data.Transitions, index, data.TransitionCount - index - 1);
            data.Transitions[--data.TransitionCount] = default;
        }

        internal void GrowStates()
        {
            ref var data = ref Shared[0];
            var old = data.States;
            data.States = StatePool.Rent(old.Length * 2);
            Array.Copy(old, data.States, data.StateCount);
            StatePool.Return(old, true);
        }

        internal void GrowTransitions()
        {
            ref var data = ref Shared[0];
            var old = data.Transitions;
            data.Transitions = TransitionPool.Rent(old.Length * 2);
            Array.Copy(old, data.Transitions, data.TransitionCount);
            TransitionPool.Return(old, true);
        }

        #endregion
    }

    public ref struct StateBuilder<TState, TContext>
        where TState : struct, Enum
    {
        private FsmBuilder<TState, TContext> _root;
        private readonly TState _id;

        private Action<TContext>? _init,
            _early,
            _fixed,
            _postFixed,
            _pre,
            _update,
            _preLate,
            _postLate,
            _time,
            _enter,
            _exit;

        internal StateBuilder(FsmBuilderShared<TState, TContext>[] shared, TState id, bool isPatch)
        {
            _root = new FsmBuilder<TState, TContext> { Shared = shared };
            _id = id;
            _init = _early = _fixed = _postFixed = _pre = _update = _preLate = _postLate = _time = _enter = _exit = null;

            if (!isPatch) return;

            var idx = _root.GetStateIndex(id);
            if (idx < 0) return;

            ref var s = ref _root.Shared[0].States[idx];
            _init = s.Initialization;
            _early = s.EarlyUpdate;
            _fixed = s.FixedUpdate;
            _postFixed = s.PostFixedUpdate;
            _pre = s.PreUpdate;
            _update = s.Update;
            _preLate = s.PreLateUpdate;
            _postLate = s.PostLateUpdate;
            _time = s.TimeUpdate;
            _enter = s.Enter;
            _exit = s.Exit;
        }

        #region Lifecycle

        public StateBuilder<TState, TContext> Initialization(Action<TContext> cb)
        {
            _init = cb;
            return this;
        }

        public StateBuilder<TState, TContext> EarlyUpdate(Action<TContext> cb)
        {
            _early = cb;
            return this;
        }

        public StateBuilder<TState, TContext> FixedUpdate(Action<TContext> cb)
        {
            _fixed = cb;
            return this;
        }

        public StateBuilder<TState, TContext> PostFixedUpdate(Action<TContext> cb)
        {
            _postFixed = cb;
            return this;
        }

        public StateBuilder<TState, TContext> PreUpdate(Action<TContext> cb)
        {
            _pre = cb;
            return this;
        }

        public StateBuilder<TState, TContext> Update(Action<TContext> cb)
        {
            _update = cb;
            return this;
        }

        public StateBuilder<TState, TContext> PreLateUpdate(Action<TContext> cb)
        {
            _preLate = cb;
            return this;
        }

        public StateBuilder<TState, TContext> PostLateUpdate(Action<TContext> cb)
        {
            _postLate = cb;
            return this;
        }

        public StateBuilder<TState, TContext> TimeUpdate(Action<TContext> cb)
        {
            _time = cb;
            return this;
        }

        public StateBuilder<TState, TContext> Enter(Action<TContext> cb)
        {
            _enter = cb;
            return this;
        }

        public StateBuilder<TState, TContext> Exit(Action<TContext> cb)
        {
            _exit = cb;
            return this;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Alias for PreLateUpdate.
        /// </summary>
        public StateBuilder<TState, TContext> LateUpdate(Action<TContext> cb)
        {
            _preLate = cb;
            return this;
        }

        #endregion

        #region Transition

        public TransitionBuilder<TState, TContext> To(TState targetId)
        {
            Commit();
            return new TransitionBuilder<TState, TContext>(_root.Shared, _id, targetId);
        }

        public TransitionBuilder<TState, TContext> ModifyTo(TState targetId)
        {
            Commit();
            RemoveTo(targetId);
            return new TransitionBuilder<TState, TContext>(_root.Shared, _id, targetId);
        }

        public StateBuilder<TState, TContext> RemoveAllTo()
        {
            ref var data = ref _root.Shared[0];
            for (var i = data.TransitionCount - 1; i >= 0; i--)
                if (FsmBuilder<TState, TContext>.StateEquals(data.Transitions[i].From, _id))
                    _root.RemoveTransitionAt(i);
            return this;
        }

        public StateBuilder<TState, TContext> RemoveTo(TState target)
        {
            ref var data = ref _root.Shared[0];
            for (var i = data.TransitionCount - 1; i >= 0; i--)
            {
                var t = data.Transitions[i];
                if (FsmBuilder<TState, TContext>.StateEquals(t.From, _id) &&
                    FsmBuilder<TState, TContext>.StateEquals(t.To, target))
                    _root.RemoveTransitionAt(i);
            }

            return this;
        }

        #endregion

        public FsmBuilder<TState, TContext> End()
        {
            Commit();
            return _root;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Commit()
        {
            var s = new State<TState, TContext>(_id,
                _init,
                _early,
                _fixed,
                _postFixed,
                _pre,
                _update,
                _preLate,
                _postLate,
                _time,
                _enter,
                _exit);
            ref var data = ref _root.Shared[0];
            var idx = _root.GetStateIndex(_id);
            if (idx >= 0) data.States[idx] = s;
            else
            {
                if (data.StateCount == data.States.Length) _root.GrowStates();
                data.States[data.StateCount++] = s;
                if (!data.HasInitialState)
                {
                    data.InitialState = _id;
                    data.HasInitialState = true;
                }
            }
        }
    }

    public ref struct TransitionBuilder<TState, TContext>
        where TState : struct, Enum
    {
        private FsmBuilder<TState, TContext> _root;
        private readonly TState _from, _to;

        internal TransitionBuilder(FsmBuilderShared<TState, TContext>[] shared, TState from, TState to)
        {
            _root = new FsmBuilder<TState, TContext> { Shared = shared };
            _from = from;
            _to = to;
        }

        public StateBuilder<TState, TContext> When(Func<TContext, bool> predicate, int priority = 0)
        {
            AddInternal(new Transition<TState, TContext>(_from,
                _to,
                ConditionWrappers<TContext>.Default,
                predicate,
                priority));
            return new StateBuilder<TState, TContext>(_root.Shared, _from, true);
        }

        public StateBuilder<TState, TContext> When<TS>(TS state, Func<TContext, TS, bool> predicate, int priority = 0)
        {
            var w = new StatefulCondition<TContext, TS>(state, predicate);
            AddInternal(new Transition<TState, TContext>(_from, _to, ConditionWrappers<TContext, TS>.Stateful, w, priority));
            return new StateBuilder<TState, TContext>(_root.Shared, _from, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddInternal(Transition<TState, TContext> t)
        {
            ref var data = ref _root.Shared[0];
            if (data.TransitionCount == data.Transitions.Length) _root.GrowTransitions();
            data.Transitions[data.TransitionCount++] = t;
        }
    }
}

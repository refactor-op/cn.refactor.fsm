using System;
using System.Buffers;

namespace Refactor.Fsm
{
    public ref struct FsmBuilder<TState, TContext>
        where TState : struct, Enum
    {
        private static readonly ArrayPool<Fsm<TState, TContext>.State> StatePool =
            ArrayPool<Fsm<TState, TContext>.State>.Shared;

        private Fsm<TState, TContext>.State[] _states;
        private int _stateCount; // The count of states which may be less than _states.Length due to pooling.

        private TState _initialState;
        private bool _hasInitialState;
        private TContext _context;

        internal static FsmBuilder<TState, TContext> Create(int size)
        {
            var capacity = 8;
            while (capacity < size)
                capacity <<= 1;

            var builder = new FsmBuilder<TState, TContext>();
            builder._states     = StatePool.Rent(capacity);
            builder._stateCount = 0;

            builder._initialState    = default;
            builder._hasInitialState = false;
            builder._context         = default;

            return builder;
        }

        internal FsmBuilder(Fsm<TState, TContext> existing)
        {
            var existingStates = existing.GetStates();
            _stateCount = existingStates.Length;

            var capacity = 8;
            while (capacity < _stateCount)
                capacity <<= 1;

            _states = StatePool.Rent(capacity);
            Array.Copy(existingStates, _states, _stateCount);

            _initialState    = existing.CurrentState.Id;
            _hasInitialState = true;
            _context         = existing.Context;
        }

        /// <remarks>
        ///     the first state added defaults to the initial state unless StartWith is called.
        /// </remarks>
        public void With(TState state, StateHandler<TState, TContext> handler)
        {
            var states = _states;
            if (states == null) throw new InvalidOperationException("FSM builder is not initialized.");
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            // Check if the state already exists; if so, update its handler.
            var key = Fsm<TState, TContext>.ToKey(state);
            for (var i = 0; i < _stateCount; i++)
            {
                if (Fsm<TState, TContext>.ToKey(states[i].Id) != key)
                    continue;

                states[i] = new Fsm<TState, TContext>.State(state, handler);
                return;
            }

            // if it's full, grow it.
            if (_stateCount == states.Length)
            {
                Grow();
                states = _states;
                if (states == null) throw new InvalidOperationException("FSM builder is not initialized.");
            }

            states[_stateCount++] = new Fsm<TState, TContext>.State(state, handler);

            // Automatically set the first registered state as the initial state.
            if (!_hasInitialState)
            {
                _initialState    = state;
                _hasInitialState = true;
            }
        }

        public void Without(TState state)
        {
            if (_states == null) throw new InvalidOperationException("FSM builder is not initialized.");

            var key   = Fsm<TState, TContext>.ToKey(state);
            var index = -1;
            for (var i = 0; i < _stateCount; i++)
            {
                if (Fsm<TState, TContext>.ToKey(_states[i].Id) != key)
                    continue;

                index = i;
                break;
            }

            if (index < 0)
                return;

            // swap with the last element to allow O(1) removal since order is not preserved.
            _stateCount--;
            if (index != _stateCount)
            {
                var swapped = _states[_stateCount];
                _states[index]       = swapped;
                _states[_stateCount] = default;
            }
            else
            {
                _states[_stateCount] = default;
            }

            if (_hasInitialState && Fsm<TState, TContext>.ToKey(_initialState) == key)
            {
                if (_stateCount > 0)
                    _initialState = _states[0].Id;
                else
                    _hasInitialState = false;
            }
        }

        public void StartWith(TState state)
        {
            _initialState    = state;
            _hasInitialState = true;
        }

        public void WithContext(TContext context)
        {
            _context = context;
        }

        public Fsm<TState, TContext> Build()
        {
            if (_states == null) throw new InvalidOperationException("FSM builder is not initialized.");
            if (_stateCount == 0) throw new InvalidOperationException("No states are registered.");
            if (!_hasInitialState) throw new InvalidOperationException("Initial state is not set.");

            var initialKey     = Fsm<TState, TContext>.ToKey(_initialState);
            var initialIsValid = false;
            for (var i = 0; i < _stateCount; i++)
            {
                if (Fsm<TState, TContext>.ToKey(_states[i].Id) != initialKey)
                    continue;

                if (_states[i].Handler == null)
                    throw new InvalidOperationException($"Initial state {_initialState} has no handler.");

                initialIsValid = true;
                break;
            }

            if (!initialIsValid)
                throw new InvalidOperationException($"Initial state {_initialState} is not registered.");

            // Copy to a compact array to minimize memory.
            var resultStates = new Fsm<TState, TContext>.State[_stateCount];
            if (_stateCount > 0)
                Array.Copy(_states, resultStates, _stateCount);

            return new Fsm<TState, TContext>(resultStates, _initialState, _context);
        }

        public void Dispose()
        {
            var toReturnStates = _states;
            this = default;

            if (toReturnStates != null)
                StatePool.Return(toReturnStates, true);
        }

        #region Private

        private void Grow()
        {
            var oldStates = _states;
            var newStates = StatePool.Rent(oldStates.Length * 2);
            Array.Copy(oldStates, newStates, _stateCount);
            StatePool.Return(oldStates, true);
            _states = newStates;
        }

        #endregion
    }
}
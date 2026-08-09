#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using R3;

namespace Refactor.Fsm
{
    public sealed class Fsm<TState, TContext> : IDisposable
        where TState : struct, Enum
    {
        private readonly State<TState, TContext>[] _states;
        private readonly Transition<TState, TContext>[] _rawTransitions;
        private readonly Dictionary<TState, Transition<TState, TContext>[]> _transitionMap;
        private readonly Dictionary<TState, int> _stateIndexMap;

        private State<TState, TContext> _current;
        private TContext _context;
        private CompositeDisposable? _disposables;

        internal State<TState, TContext>[] RawStates => _states;
        internal Transition<TState, TContext>[] RawTransitions => _rawTransitions;
        internal TState InitialStateId { get; }

        public ref readonly State<TState, TContext> CurrentState => ref _current;
        public ref TContext Context => ref _context;
        public TState CurrentStateId => _current.Id;

        internal Fsm(
            State<TState, TContext>[] states,
            Transition<TState, TContext>[] transitions,
            TState initialState,
            TContext context)
        {
            _states = states;
            _rawTransitions = transitions;
            InitialStateId = initialState;
            _context = context;

            _stateIndexMap = new Dictionary<TState, int>(states.Length);
            for (var i = 0; i < states.Length; i++) 
                _stateIndexMap[states[i].Id] = i;

            _transitionMap = new Dictionary<TState, Transition<TState, TContext>[]>();
            var tempMap = new Dictionary<TState, List<Transition<TState, TContext>>>();

            foreach (var t in transitions)
            {
                if (!tempMap.TryGetValue(t.From, out var list))
                {
                    list = new List<Transition<TState, TContext>>();
                    tempMap[t.From] = list;
                }
                list.Add(t);
            }

            foreach (var kvp in tempMap)
                _transitionMap[kvp.Key] = kvp.Value.ToArray();

            if (!_stateIndexMap.TryGetValue(initialState, out var index))
                throw new InvalidOperationException();

            _current = _states[index];
            _current.Enter?.Invoke(_context);

            _disposables = new CompositeDisposable();
            Observable.EveryUpdate(UnityFrameProvider.Initialization)
                .Subscribe(this, static (_, self) => self.Initialization())
                .AddTo(_disposables);
            Observable.EveryUpdate(UnityFrameProvider.EarlyUpdate)
                .Subscribe(this, static (_, self) => self.EarlyUpdate())
                .AddTo(_disposables);
            Observable.EveryUpdate(UnityFrameProvider.FixedUpdate)
                .Subscribe(this, static (_, self) => self.FixedUpdate())
                .AddTo(_disposables);
            Observable.EveryUpdate(UnityFrameProvider.PostFixedUpdate)
                .Subscribe(this, static (_, self) => self.PostFixedUpdate())
                .AddTo(_disposables);
            Observable.EveryUpdate(UnityFrameProvider.PreUpdate)
                .Subscribe(this, static (_, self) => self.PreUpdate())
                .AddTo(_disposables);
            Observable.EveryUpdate(UnityFrameProvider.Update)
                .Subscribe(this, static (_, self) => self.Update())
                .AddTo(_disposables);
            Observable.EveryUpdate(UnityFrameProvider.PreLateUpdate)
                .Subscribe(this, static (_, self) => self.PreLateUpdate())
                .AddTo(_disposables);
            Observable.EveryUpdate(UnityFrameProvider.PostLateUpdate)
                .Subscribe(this, static (_, self) => self.PostLateUpdate())
                .AddTo(_disposables);
            Observable.EveryUpdate(UnityFrameProvider.TimeUpdate)
                .Subscribe(this, static (_, self) => self.TimeUpdate())
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables?.Dispose();
            _disposables = null;
        }

        private void Initialization() => _current.Initialization?.Invoke(_context);
        private void EarlyUpdate() => _current.EarlyUpdate?.Invoke(_context);
        private void FixedUpdate() => _current.FixedUpdate?.Invoke(_context);
        private void PostFixedUpdate() => _current.PostFixedUpdate?.Invoke(_context);
        private void PreUpdate() => _current.PreUpdate?.Invoke(_context);

        private void Update()
        {
            _current.Update?.Invoke(_context);
            CheckTransitions();
        }

        private void PreLateUpdate() => _current.PreLateUpdate?.Invoke(_context);
        private void PostLateUpdate() => _current.PostLateUpdate?.Invoke(_context);
        private void TimeUpdate() => _current.TimeUpdate?.Invoke(_context);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckTransitions()
        {
            if (!_transitionMap.TryGetValue(_current.Id, out var transitions)) 
                return;

            foreach (var t in transitions)
            {
                if (!t.Condition(_context, t.State)) 
                    continue;

                TransitionTo(t.To);
                return;
            }
        }

        private void TransitionTo(TState newState)
        {
            if (StateEquals(newState, _current.Id))
                return;

            if (!_stateIndexMap.TryGetValue(newState, out var newIndex))
                throw new InvalidOperationException();

            var newStateData = _states[newIndex];

            _current.Exit?.Invoke(_context);
            _current = newStateData;
            _current.Enter?.Invoke(_context);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool StateEquals(TState a, TState b) 
            => EqualityComparer<TState>.Default.Equals(a, b);
    }
}

using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Refactor.Fsm
{
    public class Fsm<TState, TContext>
        where TState : struct, Enum
    {
        private readonly State[] _states;
        private readonly IndexMap _indexMap;

        private State _current;
        private ulong _currentKey;
        private TContext _context;

        public ref readonly State CurrentState => ref _current;
        public ref TContext Context => ref _context;
        public bool IsPaused { get; private set; }

        public readonly struct State
        {
            public readonly TState Id;
            public readonly StateHandler<TState, TContext> Handler;

            public State(TState id, StateHandler<TState, TContext> handler)
            {
                Id      = id;
                Handler = handler;
            }
        }

        private enum UnderlyingKind : byte
        {
            Byte,
            SByte,
            Short,
            UShort,
            Int,
            UInt,
            Long,
            ULong
        }

        private readonly struct IndexMap
        {
            private readonly ulong[] _keys;
            private readonly int[] _values;
            private readonly int _mask;

            private IndexMap(ulong[] keys, int[] values, int mask)
            {
                _keys   = keys;
                _values = values;
                _mask   = mask;
            }

            public static IndexMap Create(State[] states)
            {
                var count    = states.Length;
                var capacity = 1;
                while (capacity < count * 2)
                    capacity <<= 1;

                var keys   = new ulong[capacity];
                var values = new int[capacity];
                Array.Fill(values, -1);
                var mask = capacity - 1;

                for (var i = 0; i < count; i++)
                {
                    var key = ToKey(states[i].Id);
                    Insert(keys, values, mask, key, i);
                }

                return new IndexMap(keys, values, mask);
            }

            public int Lookup(ulong key)
            {
                var idx = (int)(Hash32(key) & (uint)_mask);
                while (true)
                {
                    var v = _values[idx];
                    if (v == -1) return -1;
                    if (_keys[idx] == key) return v;
                    idx = (idx + 1) & _mask;
                }
            }

            private static void Insert(ulong[] keys, int[] values, int mask, ulong key, int value)
            {
                var idx = (int)(Hash32(key) & (uint)mask);
                while (true)
                {
                    var v = values[idx];
                    if (v == -1)
                    {
                        keys[idx]   = key;
                        values[idx] = value;
                        return;
                    }

                    if (keys[idx] == key)
                    {
                        values[idx] = value;
                        return;
                    }

                    idx = (idx + 1) & mask;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint Hash32(ulong x) => (uint)((x * 11400714819323198549UL) >> 32); // 2^64 / phi.
        }

        #region Creational

        internal Fsm(State[] states, TState initialState, TContext context)
        {
            _states  = states;
            _context = context;

            _indexMap = IndexMap.Create(states);

            var initialKey = ToKey(initialState);
            var index      = _indexMap.Lookup(initialKey);
            if (index < 0)
                throw new InvalidOperationException($"State {initialState} is not registered.");
            _current    = _states[index];
            _currentKey = initialKey;

            _current.Handler.OnInitialEnter(_context, this);
        }

        internal State[] GetStates()
        {
            var copy = new State[_states.Length];
            Array.Copy(_states, copy, _states.Length);
            return copy;
        }

        #endregion

        #region Control

        /// <summary>Pauses the Update/FixedUpdate/LateUpdate loops.</summary>
        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            _current.Handler.OnPause(_context, this);
        }

        /// <summary>Resumes the Update/FixedUpdate/LateUpdate loops.</summary>
        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            _current.Handler.OnResume(_context, this);
        }

        #endregion

        #region Transition

        /// <summary>
        ///     Transitions to a new state.
        ///     <para>If the target state equals the current state, no action is taken.</para>
        /// </summary>
        public void GoTo(TState newState)
        {
            var newKey = ToKey(newState);
            if (newKey == _currentKey)
                return;

            var newIndex = _indexMap.Lookup(newKey);
            if (newIndex < 0 || _states[newIndex].Handler == null)
                throw new InvalidOperationException($"State {newState} is not registered.");

            var newStateData = _states[newIndex];
            var oldState     = _current.Id;

            _current.Handler.OnExit(newState, _context, this);

            _current    = newStateData;
            _currentKey = newKey;

            _current.Handler.OnEnter(oldState, _context, this);

            if (IsPaused)
                _current.Handler.OnPause(_context, this);
        }

        /// <summary>
        ///     Re-enters the current state (Exit => Enter).
        /// </summary>
        public void Reenter()
        {
            _current.Handler.OnReenter(_context, this);
        }

        #endregion

        #region Update

        public void Update()
        {
            if (IsPaused) return;
            _current.Handler.OnUpdate(_context, this);
        }

        public void FixedUpdate()
        {
            if (IsPaused) return;
            _current.Handler.OnFixedUpdate(_context, this);
        }

        public void LateUpdate()
        {
            if (IsPaused) return;
            _current.Handler.OnLateUpdate(_context, this);
        }

        #endregion

        #region Indexer

        private static readonly UnderlyingKind s_underlyingKind = InitializeUnderlyingKind();

        private static UnderlyingKind InitializeUnderlyingKind()
        {
            var underlying = Enum.GetUnderlyingType(typeof(TState));

            if (underlying == typeof(byte)) return UnderlyingKind.Byte;
            if (underlying == typeof(sbyte)) return UnderlyingKind.SByte;
            if (underlying == typeof(short)) return UnderlyingKind.Short;
            if (underlying == typeof(ushort)) return UnderlyingKind.UShort;
            if (underlying == typeof(int)) return UnderlyingKind.Int;
            if (underlying == typeof(uint)) return UnderlyingKind.UInt;
            if (underlying == typeof(long)) return UnderlyingKind.Long;
            if (underlying == typeof(ulong)) return UnderlyingKind.ULong;

            throw new NotSupportedException(
                $"Enum {typeof(TState).Name} uses unsupported underlying type {underlying.Name}.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ToKey(TState state) =>
            s_underlyingKind switch
            {
                UnderlyingKind.Byte   => UnsafeUtility.As<TState, byte>(ref state),
                UnderlyingKind.SByte  => unchecked((ulong)UnsafeUtility.As<TState, sbyte>(ref state)),
                UnderlyingKind.Short  => unchecked((ulong)UnsafeUtility.As<TState, short>(ref state)),
                UnderlyingKind.UShort => UnsafeUtility.As<TState, ushort>(ref state),
                UnderlyingKind.Int    => unchecked((ulong)UnsafeUtility.As<TState, int>(ref state)),
                UnderlyingKind.UInt   => UnsafeUtility.As<TState, uint>(ref state),
                UnderlyingKind.Long   => unchecked((ulong)UnsafeUtility.As<TState, long>(ref state)),
                UnderlyingKind.ULong  => UnsafeUtility.As<TState, ulong>(ref state),
                _                     => 0
            };

        #endregion
    }
}
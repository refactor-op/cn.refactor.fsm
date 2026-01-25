using System.Reflection;
using NUnit.Framework;

namespace Refactor.Fsm.Tests
{
    public class FsmTests
    {
        public enum State
        {
            Idle,
            Move,
            Attack,
            Dead
        }

        private Context _context;
        private int _initialEnterCount;
        private int _enterCount;
        private int _exitCount;
        private int _updateCount;

        [SetUp]
        public void Setup()
        {
            _context           = new Context();
            _initialEnterCount = 0;
            _enterCount        = 0;
            _exitCount         = 0;
            _updateCount       = 0;
        }

        [Test]
        public void Build_SetsInitialState_And_CallsEnter()
        {
            var fsm = Fsms.Create<State, Context>()
                .ContextWith(_context)
                .StartWith(State.Idle)
                .AddState(State.Idle)
                .Enter(c => _initialEnterCount++)
                .End()
                .Build();

            Assert.AreEqual(State.Idle, fsm.CurrentStateId);
            Assert.AreEqual(1, _initialEnterCount);

            fsm.Dispose();
        }

        [Test]
        public void TransitionTo_ValidState_TransitionsCorrectly()
        {
            _context.CanMove = true;

            var fsm = Fsms.Create<State, Context>()
                .ContextWith(_context)
                .StartWith(State.Idle)
                .AddState(State.Idle)
                .Exit(c => _exitCount++)
                .To(State.Move)
                .When(c => c.CanMove)
                .End()
                .AddState(State.Move)
                .Enter(c => _enterCount++)
                .End()
                .Build();

            ManualTick(fsm, "Update");

            Assert.AreEqual(State.Move, fsm.CurrentStateId);
            Assert.AreEqual(1, _exitCount);
            Assert.AreEqual(1, _enterCount);

            fsm.Dispose();
        }

        [Test]
        public void Update_InvokesCallback()
        {
            var fsm = Fsms.Create<State, Context>()
                .ContextWith(_context)
                .StartWith(State.Idle)
                .AddState(State.Idle)
                .Update(c => _updateCount++)
                .End()
                .Build();

            ManualTick(fsm, "Update");

            Assert.AreEqual(1, _updateCount);

            fsm.Dispose();
        }

        [Test]
        public void FixedUpdate_InvokesCallback()
        {
            var count = 0;
            var fsm = Fsms.Create<State, Context>()
                .ContextWith(_context)
                .StartWith(State.Idle)
                .AddState(State.Idle)
                .FixedUpdate(c => count++)
                .End()
                .Build();

            ManualTick(fsm, "FixedUpdate");

            Assert.AreEqual(1, count);

            fsm.Dispose();
        }

        [Test]
        public void LateUpdate_InvokesCallback()
        {
            var count = 0;
            var fsm = Fsms.Create<State, Context>()
                .ContextWith(_context)
                .StartWith(State.Idle)
                .AddState(State.Idle)
                .LateUpdate(c => count++)
                .End()
                .Build();

            ManualTick(fsm, "PreLateUpdate");

            Assert.AreEqual(1, count);

            fsm.Dispose();
        }

        private void ManualTick(object fsm, string method)
        {
            var tickMethod = fsm.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            if (tickMethod != null)
                tickMethod.Invoke(fsm, null);
        }

        public class Context
        {
            public int Value;
            public bool CanMove;
        }
    }
}
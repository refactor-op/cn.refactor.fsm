using System;
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
        private SpyHandler _idleHandler;
        private SpyHandler _moveHandler;

        [SetUp]
        public void Setup()
        {
            _context     = new Context();
            _idleHandler = new SpyHandler();
            _moveHandler = new SpyHandler();
        }

        [Test]
        public void Build_SetsInitialState_And_CallsOnEnter()
        {
            // Arrange & Act
            using var builder = Fsms.Create<State, Context>();
            builder.With(State.Idle, _idleHandler);
            builder.WithContext(_context);
            builder.StartWith(State.Idle);
            var fsm = builder.Build();

            // Assert
            Assert.AreEqual(State.Idle, fsm.CurrentState.Id);
            Assert.AreEqual(1, _idleHandler.InitialEnterCount, "Should enter initial state immediately");
        }

        [Test]
        public void GoTo_ValidState_TransitionsCorrectly()
        {
            // Arrange
            Fsm<State, Context> fsm;
            {
                using var builder = Fsms.Create<State, Context>();
                builder.With(State.Idle, _idleHandler);
                builder.With(State.Move, _moveHandler);
                builder.WithContext(_context);
                builder.StartWith(State.Idle);
                fsm = builder.Build();
            }

            // Act
            fsm.GoTo(State.Move);

            // Assert
            Assert.AreEqual(State.Move, fsm.CurrentState.Id);

            // Check Idle Exit
            Assert.AreEqual(1, _idleHandler.ExitCount);
            Assert.AreEqual(State.Move, _idleHandler.LastExitedTo);

            // Check Move Enter
            Assert.AreEqual(1, _moveHandler.EnterCount);
            Assert.AreEqual(State.Idle, _moveHandler.LastEnteredFrom);
        }

        [Test]
        public void GoTo_SameState_DoesNothing()
        {
            // Arrange
            Fsm<State, Context> fsm;
            {
                using var builder = Fsms.Create<State, Context>();
                builder.With(State.Idle, _idleHandler);
                builder.WithContext(_context);
                builder.StartWith(State.Idle);
                fsm = builder.Build();
            }

            _idleHandler.EnterCount = 0; // Reset after init

            // Act
            fsm.GoTo(State.Idle);

            // Assert
            Assert.AreEqual(0, _idleHandler.ExitCount);
            Assert.AreEqual(0, _idleHandler.EnterCount);
        }

        [Test]
        public void Reenter_TriggersExitAndEnter()
        {
            // Arrange
            Fsm<State, Context> fsm;
            {
                using var builder = Fsms.Create<State, Context>();
                builder.With(State.Idle, _idleHandler);
                builder.WithContext(_context);
                builder.StartWith(State.Idle);
                fsm = builder.Build();
            }

            _idleHandler.ReenterCount = 0;

            // Act
            fsm.Reenter();

            // Assert
            Assert.AreEqual(1, _idleHandler.ReenterCount);
        }

        [Test]
        public void Update_WhenRunning_InvokesHandler()
        {
            // Arrange
            Fsm<State, Context> fsm;
            {
                using var builder = Fsms.Create<State, Context>();
                builder.With(State.Idle, _idleHandler);
                builder.WithContext(_context);
                builder.StartWith(State.Idle);
                fsm = builder.Build();
            }

            // Act
            fsm.Update();
            fsm.FixedUpdate();
            fsm.LateUpdate();

            // Assert
            Assert.AreEqual(1, _idleHandler.UpdateCount);
            Assert.AreEqual(1, _idleHandler.FixedUpdateCount);
            Assert.AreEqual(1, _idleHandler.LateUpdateCount);
        }

        [Test]
        public void Update_WhenPaused_DoesNotInvokeHandler()
        {
            // Arrange
            Fsm<State, Context> fsm;
            {
                using var builder = Fsms.Create<State, Context>();
                builder.With(State.Idle, _idleHandler);
                builder.WithContext(_context);
                builder.StartWith(State.Idle);
                fsm = builder.Build();
            }

            // Act
            fsm.Pause();
            fsm.Update();

            // Assert
            Assert.AreEqual(0, _idleHandler.UpdateCount);
            Assert.IsTrue(fsm.IsPaused);
            Assert.AreEqual(1, _idleHandler.PauseCount);

            // Act - Resume
            fsm.Resume();
            fsm.Update();

            // Assert
            Assert.AreEqual(1, _idleHandler.UpdateCount);
            Assert.IsFalse(fsm.IsPaused);
            Assert.AreEqual(1, _idleHandler.ResumeCount);
        }

        [Test]
        public void From_ClonesAndModifies()
        {
            // Arrange
            Fsm<State, Context> fsm;
            {
                using var baseBuilder = Fsms.Create<State, Context>();
                baseBuilder.With(State.Idle, _idleHandler);
                baseBuilder.With(State.Move, _moveHandler);
                baseBuilder.WithContext(_context);
                baseBuilder.StartWith(State.Idle);
                fsm = baseBuilder.Build();
            }

            // Create a derived FSM with Attack added
            var                 attackHandler = new SpyHandler();
            Fsm<State, Context> derivedFsm;
            {
                using var derivedBuilder = Fsms.From(fsm);
                derivedBuilder.With(State.Attack, attackHandler);
                derivedFsm = derivedBuilder.Build();
            }

            // Act
            derivedFsm.GoTo(State.Attack);

            // Assert
            Assert.AreEqual(State.Attack, derivedFsm.CurrentState.Id);
            Assert.AreEqual(1, attackHandler.EnterCount);
        }

        [Test]
        public void Without_RemovesState()
        {
            // Arrange
            Fsm<State, Context> fsm;
            {
                using var baseBuilder = Fsms.Create<State, Context>();
                baseBuilder.With(State.Idle, _idleHandler);
                baseBuilder.With(State.Move, _moveHandler);
                baseBuilder.WithContext(_context);
                baseBuilder.StartWith(State.Idle);
                fsm = baseBuilder.Build();
            }

            // Create a derived FSM with Move removed
            Fsm<State, Context> derivedFsm;
            {
                using var derivedBuilder = Fsms.From(fsm);
                derivedBuilder.Without(State.Move);
                derivedFsm = derivedBuilder.Build();
            }

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => derivedFsm.GoTo(State.Move));
            Assert.That(ex!.Message, Does.Contain("not registered"));
        }

        [Test]
        public void Build_WithoutContext_UsesDefaultContext()
        {
            {
                using var builder = Fsms.Create<State, Context>();
                builder.With(State.Idle, _idleHandler);
                builder.StartWith(State.Idle);
                var fsm = builder.Build();

                Assert.AreEqual(State.Idle, fsm.CurrentState.Id);
            }
        }
    }

    public class Context
    {
    }

    public class SpyHandler : StateHandler<FsmTests.State, Context>
    {
        public int InitialEnterCount;
        public int EnterCount;
        public int ExitCount;
        public int ReenterCount;
        public int PauseCount;
        public int ResumeCount;
        public int UpdateCount;
        public int FixedUpdateCount;
        public int LateUpdateCount;

        public FsmTests.State LastEnteredFrom;
        public FsmTests.State LastExitedTo;

        public override void OnInitialEnter(Context ctx, Fsm<FsmTests.State, Context> fsm)
        {
            InitialEnterCount++;
        }

        public override void OnEnter(FsmTests.State from, Context ctx, Fsm<FsmTests.State, Context> fsm)
        {
            EnterCount++;
            LastEnteredFrom = from;
        }

        public override void OnExit(FsmTests.State to, Context ctx, Fsm<FsmTests.State, Context> fsm)
        {
            ExitCount++;
            LastExitedTo = to;
        }

        public override void OnReenter(Context ctx, Fsm<FsmTests.State, Context> fsm)
        {
            ReenterCount++;
        }

        public override void OnPause(Context ctx, Fsm<FsmTests.State, Context> fsm)
        {
            PauseCount++;
        }

        public override void OnResume(Context ctx, Fsm<FsmTests.State, Context> fsm)
        {
            ResumeCount++;
        }

        public override void OnUpdate(Context ctx, Fsm<FsmTests.State, Context> fsm)
        {
            UpdateCount++;
        }

        public override void OnFixedUpdate(Context ctx, Fsm<FsmTests.State, Context> fsm)
        {
            FixedUpdateCount++;
        }

        public override void OnLateUpdate(Context ctx, Fsm<FsmTests.State, Context> fsm)
        {
            LateUpdateCount++;
        }
    }
}
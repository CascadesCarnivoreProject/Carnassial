using Carnassial.Command;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class LowLevelTests : CarnassialTest
    {
        [ClassCleanup]
        public static void ClassCleanup()
        {
            CarnassialTest.TryRevertToDefaultCultures();
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            CarnassialTest.TryChangeToTestCulture();
        }

        /// <summary>
        /// Basic functional validation of <see cref="MostRecentlyUsedList" />.
        /// </summary>
        [TestMethod]
        public void MostRecentlyUsedList()
        {
            MostRecentlyUsedList<int> mruList = new(5);

            mruList.SetMostRecent(0);
            Assert.IsFalse(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out int mostRecent));
            Assert.IsTrue(mostRecent == 0);
            List<int> list = [.. mruList];
            Assert.IsTrue(list.Count == 1);
            Assert.IsTrue(list[0] == 0);

            mruList.SetMostRecent(1);
            Assert.IsFalse(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 1);
            list = [.. mruList];
            Assert.IsTrue(list.Count == 2);
            Assert.IsTrue(list[0] == 1);
            Assert.IsTrue(list[1] == 0);

            mruList.SetMostRecent(0);
            Assert.IsFalse(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 0);
            list = [.. mruList];
            Assert.IsTrue(list.Count == 2);
            Assert.IsTrue(list[0] == 0);
            Assert.IsTrue(list[1] == 1);

            Assert.IsTrue(mruList.TryRemove(0));
            Assert.IsFalse(mruList.IsFull());
            list = [.. mruList];
            Assert.IsTrue(list.Count == 1);
            Assert.IsTrue(list[0] == 1);

            Assert.IsFalse(mruList.TryRemove(0));
            Assert.IsTrue(mruList.TryRemove(1));
            Assert.IsFalse(mruList.IsFull());
            list = [.. mruList];
            Assert.IsTrue(list.Count == 0);

            mruList.SetMostRecent(2);
            mruList.SetMostRecent(3);
            mruList.SetMostRecent(4);
            mruList.SetMostRecent(5);
            mruList.SetMostRecent(6);
            mruList.SetMostRecent(7);
            Assert.IsTrue(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 7);
            list = [.. mruList];
            Assert.IsTrue(list.Count == 5);
            Assert.IsTrue(list[0] == 7);
            Assert.IsTrue(list[1] == 6);
            Assert.IsTrue(list[2] == 5);
            Assert.IsTrue(list[3] == 4);
            Assert.IsTrue(list[4] == 3);

            mruList.SetMostRecent(6);
            Assert.IsTrue(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 6);
            list = [.. mruList];
            Assert.IsTrue(list.Count == 5);
            Assert.IsTrue(list[0] == 6);
            Assert.IsTrue(list[1] == 7);
            Assert.IsTrue(list[2] == 5);
            Assert.IsTrue(list[3] == 4);
            Assert.IsTrue(list[4] == 3);

            mruList.SetMostRecent(3);
            Assert.IsTrue(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 3);
            list = [.. mruList];
            Assert.IsTrue(list.Count == 5);
            Assert.IsTrue(list[0] == 3);
            Assert.IsTrue(list[1] == 6);
            Assert.IsTrue(list[2] == 7);
            Assert.IsTrue(list[3] == 5);
            Assert.IsTrue(list[4] == 4);

            Assert.IsFalse(mruList.TryRemove(-1));
            Assert.IsTrue(mruList.IsFull());

            Assert.IsTrue(mruList.TryRemove(5));
            Assert.IsFalse(mruList.IsFull());
            list = [.. mruList];
            Assert.IsTrue(list.Count == 4);
            Assert.IsTrue(list[0] == 3);
            Assert.IsTrue(list[1] == 6);
            Assert.IsTrue(list[2] == 7);
            Assert.IsTrue(list[3] == 4);

            Assert.IsTrue(mruList.TryGetLeastRecent(out int leastRecent));
            Assert.IsTrue(leastRecent == 4);
        }

        /// <summary>
        /// Basic functional validation of <see cref="UndoRedoChain" />.
        /// </summary>
        [TestMethod]
        public void UndoRedoChain()
        {
            // zero states
            UndoRedoChain<int> undoRedoChain = new();
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);
            Assert.IsFalse(undoRedoChain.TryMoveToNextRedo(out _));
            Assert.IsFalse(undoRedoChain.TryMoveToNextUndo(out _));

            // basic lifecycle with two different states
            undoRedoChain.AddCommand(new TestCommand(0));
            undoRedoChain.AddCommand(new TestCommand(1));
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextUndo(out UndoableCommand<int>? state));
            state.Undo(0);
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextRedo(out state));
            state.Execute(0);
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextUndo(out state));
            state.Undo(0);
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextRedo(out state));
            state.Execute(0);
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsFalse(undoRedoChain.TryMoveToNextRedo(out _));
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextUndo(out state));
            state.Undo(0);
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextUndo(out state));
            state.Undo(0);
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);

            undoRedoChain.Clear();
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);
            Assert.IsFalse(undoRedoChain.TryMoveToNextRedo(out _));
            Assert.IsFalse(undoRedoChain.TryMoveToNextUndo(out _));

            // one state
            TestCommand firstState = new(3);
            undoRedoChain.AddCommand(firstState);
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);
            Assert.IsFalse(undoRedoChain.TryMoveToNextRedo(out _));
            Assert.IsTrue(undoRedoChain.TryMoveToNextUndo(out state));
            state.Undo(0);
            Assert.IsTrue(firstState.Equals(state));
        }

        internal class TestCommand : UndoableCommand<int>
        {
            public int ID { get; private init; }

            public TestCommand(int id)
            {
                this.ID = id;
                this.IsExecuted = true;
            }

            public override void Execute(int parameter)
            {
                this.IsExecuted = true;
            }

            public override void Undo(int parameter)
            {
                this.IsExecuted = false;
            }
        }
    }
}

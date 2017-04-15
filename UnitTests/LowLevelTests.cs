using Carnassial.Data;
using Carnassial.Editor;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class LowLevelTests : CarnassialTest
    {
        /// <summary>
        /// Basic functional validation of <see cref="MostRecentlyUsedList" />.
        /// </summary>
        [TestMethod]
        public void MostRecentlyUsedList()
        {
            MostRecentlyUsedList<int> mruList = new MostRecentlyUsedList<int>(5);

            mruList.SetMostRecent(0);
            Assert.IsFalse(mruList.IsFull());
            int mostRecent;
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 0);
            List<int> list = mruList.ToList();
            Assert.IsTrue(list.Count == 1);
            Assert.IsTrue(list[0] == 0);

            mruList.SetMostRecent(1);
            Assert.IsFalse(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 1);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 2);
            Assert.IsTrue(list[0] == 1);
            Assert.IsTrue(list[1] == 0);

            mruList.SetMostRecent(0);
            Assert.IsFalse(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 0);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 2);
            Assert.IsTrue(list[0] == 0);
            Assert.IsTrue(list[1] == 1);

            Assert.IsTrue(mruList.TryRemove(0));
            Assert.IsFalse(mruList.IsFull());
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 1);
            Assert.IsTrue(list[0] == 1);

            Assert.IsFalse(mruList.TryRemove(0));
            Assert.IsTrue(mruList.TryRemove(1));
            Assert.IsFalse(mruList.IsFull());
            list = mruList.ToList();
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
            list = mruList.ToList();
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
            list = mruList.ToList();
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
            list = mruList.ToList();
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
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 4);
            Assert.IsTrue(list[0] == 3);
            Assert.IsTrue(list[1] == 6);
            Assert.IsTrue(list[2] == 7);
            Assert.IsTrue(list[3] == 4);

            int leastRecent;
            Assert.IsTrue(mruList.TryGetLeastRecent(out leastRecent));
            Assert.IsTrue(leastRecent == 4);
        }

        [TestMethod]
        public void RequiredBinaries()
        {
            Assert.IsTrue(Dependencies.AreRequiredBinariesPresent(Constant.ApplicationName, this.GetType().Assembly));
            Assert.IsTrue(Dependencies.AreRequiredBinariesPresent(EditorConstant.ApplicationName, this.GetType().Assembly));
        }

        /// <summary>
        /// Basic functional validation of <see cref="UndoRedoChain" />.
        /// </summary>
        [TestMethod]
        public void UndoRedo()
        {
            // zero states
            UndoRedoChain undoRedoChain = new UndoRedoChain();
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);
            UndoRedoState state;
            Assert.IsFalse(undoRedoChain.TryGetRedo(out state));
            Assert.IsFalse(undoRedoChain.TryGetUndo(out state));

            // basic lifecycle with two different states
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            this.PopulateDefaultDatabase(fileDatabase);
            ImageRow firstFile = fileDatabase.Files[0];
            ImageRow secondFile = fileDatabase.Files[1];
            undoRedoChain.AddStateIfDifferent(firstFile);
            undoRedoChain.AddStateIfDifferent(secondFile);
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryGetUndo(out state));
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryGetRedo(out state));
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryGetUndo(out state));
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryGetRedo(out state));
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsFalse(undoRedoChain.TryGetRedo(out state));
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryGetUndo(out state));
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);

            Assert.IsFalse(undoRedoChain.TryGetUndo(out state));
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);

            undoRedoChain.Clear();
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);
            Assert.IsFalse(undoRedoChain.TryGetRedo(out state));
            Assert.IsFalse(undoRedoChain.TryGetUndo(out state));

            // one state
            UndoRedoState firstState = new UndoRedoState(firstFile);
            undoRedoChain.AddStateIfDifferent(firstFile);
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);
            Assert.IsFalse(undoRedoChain.TryGetRedo(out state));
            Assert.IsTrue(undoRedoChain.TryGetUndo(out state));
            Assert.IsTrue(firstState.Equals(state));

            undoRedoChain.AddStateIfDifferent(firstFile);
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.TryGetRedo(out state));
            Assert.IsTrue(undoRedoChain.TryGetUndo(out state));
            Assert.IsTrue(firstState.Equals(state));
        }
    }
}

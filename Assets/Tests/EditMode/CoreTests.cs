using GridCut.Core;
using NUnit.Framework;

namespace GridCut.Tests
{
    /// <summary>
    /// Unity Test Runner bridge. The real assertions live in GridCut.Core.TestCases so
    /// the same suite also runs in the dotnet console tool.
    /// </summary>
    public class CoreTests
    {
        private static bool _ran;
        private static System.Collections.Generic.List<TestCases.Item> _items;

        [Test]
        public void AllCoreTestCasesPass()
        {
            if (!_ran)
            {
                _items = TestCases.RunAll(97);
                _ran = true;
            }

            Assert.IsTrue(_items != null && _items.Count > 0, "Tidak ada test case berjalan");
            foreach (var item in _items)
                Assert.IsTrue(item.Pass, $"{item.Name} gagal: {item.Detail}");
        }
    }
}

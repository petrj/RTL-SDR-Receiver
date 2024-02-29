using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RTLSDR.DAB;

namespace Tests
{
    [TestClass]
    public class Tests
    {
        [TestMethod]
        public void TestFEC()
        {
            var testData = File.ReadAllBytes($"TestData{Path.DirectorySeparatorChar}RSTestData.bin");
            var corr_count = new ReedSolomonCodecControlBlock(8, 0x11D, 0, 1, 10, 135);
            Assert.AreEqual(5, corr_count);
        }
    }
}
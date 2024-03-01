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
            var rs = new ReedSolomonErrorCorrection(8, 0x11D, 0, 1, 10, 135);
            var corrPos = new int[10];
            var corr_count = rs.DecodeRSChar(testData, corrPos, 0);
            Assert.AreEqual(5, corr_count);
        }
    }
}
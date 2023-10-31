namespace Tests
{
    [TestClass]
    public class Test
    {
        [TestMethod]
        public void TestFMDemodulation()
        {
            var testData = File.ReadAllBytes($"TestData{Path.DirectorySeparatorChar}QI-DATA");
        }
    }
}
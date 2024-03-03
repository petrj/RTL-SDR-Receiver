using System;
using System.Collections.Generic;
using System.IO;
using LoggerService;
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

        [TestMethod]
        public void TestMSCCRC_CRC16_CCITT()
        {
            var testData = File.ReadAllBytes($"TestData{Path.DirectorySeparatorChar}MSCCRCTestData.bin");
            uint crc = new DABCRC(true,true, 0x1021).CalcCRC(testData);
            Assert.AreEqual((uint)26751, crc);
        }

        [TestMethod]
        public void TestMSCCRC_FIRE_CODE()
        {
            var testData = File.ReadAllBytes($"TestData{Path.DirectorySeparatorChar}MSCCRCTestData2.bin");
            // first 2 bytes CRC
            // next 9 bytes data
            var crcStored = testData[0] << 8 | testData[1];
            var data = new byte[9];
            Buffer.BlockCopy(testData, 2, data, 0, 9);

            uint crc = new DABCRC(false, false, 0x782F).CalcCRC(data);

            Assert.AreEqual((uint)60014, crc);
        }

        [TestMethod]
        public void TestFICCRC()
        {
            var testData = File.ReadAllBytes($"TestData{Path.DirectorySeparatorChar}FICCRCTestData.bin");

            //var viterbi = new Viterbi(768);
            //var fic = new FICData(new DummyLoggingService(), viterbi);
            var crcOK = FICData.CheckCRC(testData);
            Assert.IsTrue(crcOK);
        }

        [TestMethod]
        public void TestFICFIG0EXT1()
        {
            var testData = File.ReadAllBytes($"TestData{Path.DirectorySeparatorChar}FICCRCTestData.bin");

            var viterbi = new Viterbi(768);
            var fib = new FIB(new DummyLoggingService());

            var firstExpected = new DABSubChannel()
            {
                Bitrate = 96,
                Length = 72,
                ProtectionLevel = EEPProtectionLevel.EEP_1,
                StartAddr = 276,
                SubChId = 4
            };
            DABSubChannel first = null;

            List<DABSubChannel> res = new List<DABSubChannel>();
            fib.SubChannelFound += (sender, e) =>
            {
                if (e is SubChannelFoundEventArgs es)
                {
                    res.Add(es.SubChannel);
                    if (first == null)
                    {
                        first = es.SubChannel;
                    }
                }
            };
            fib.Parse(testData);

            Assert.AreEqual(7, res.Count);
            Assert.AreEqual(firstExpected.Bitrate, first.Bitrate);
            Assert.AreEqual(firstExpected.StartAddr, first.StartAddr);
            Assert.AreEqual(firstExpected.SubChId, first.SubChId);
            Assert.AreEqual(firstExpected.ProtectionLevel, first.ProtectionLevel);
        }
    }
}
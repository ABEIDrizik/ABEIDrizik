using Microsoft.VisualStudio.TestTools.UnitTesting;
using SprdFlashTool.Core.Protocol; // Make sure CrcUtils is public and in this namespace
using System;

namespace SprdFlashTool.Core.Tests
{
    [TestClass]
    public class CrcUtilsTests
    {
        // Test Methods for CalculateXmodemCrc

        [TestMethod]
        public void TestXmodemCrc_SimpleData()
        {
            byte[] data = { 0x01, 0x02, 0x03 };
            ushort expected = 0xCE3C;
            ushort actual = CrcUtils.CalculateXmodemCrc(data);
            Assert.AreEqual(expected, actual, "XMODEM CRC for {0x01, 0x02, 0x03} failed.");
        }

        [TestMethod]
        public void TestXmodemCrc_EmptyData()
        {
            byte[] data = {};
            ushort expected = 0x0000;
            ushort actual = CrcUtils.CalculateXmodemCrc(data);
            Assert.AreEqual(expected, actual, "XMODEM CRC for empty data failed.");
        }

        [TestMethod]
        public void TestXmodemCrc_SingleByte()
        {
            byte[] data = { 0x41 }; // 'A'
            ushort expected = 0xF060; 
            ushort actual = CrcUtils.CalculateXmodemCrc(data);
            Assert.AreEqual(expected, actual, "XMODEM CRC for single byte 'A' failed.");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestXmodemCrc_NullData()
        {
            CrcUtils.CalculateXmodemCrc(null);
        }

        // Test Methods for CalculateFdlCrc

        [TestMethod]
        public void TestFdlCrc_EvenLengthData()
        {
            byte[] data = { 0x01, 0x02, 0x03, 0x04 };
            ushort expected = 0xFBF9;
            ushort actual = CrcUtils.CalculateFdlCrc(data);
            Assert.AreEqual(expected, actual, "FDL CRC for {0x01, 0x02, 0x03, 0x04} failed.");
        }

        [TestMethod]
        public void TestFdlCrc_OddLengthData()
        {
            byte[] data = { 0x01, 0x02, 0x03 };
            ushort expected = 0xFEFA;
            ushort actual = CrcUtils.CalculateFdlCrc(data);
            Assert.AreEqual(expected, actual, "FDL CRC for {0x01, 0x02, 0x03} failed.");
        }

        [TestMethod]
        public void TestFdlCrc_EmptyData()
        {
            byte[] data = {};
            ushort expected = 0xFFFF; // ~0 & 0xFFFF
            ushort actual = CrcUtils.CalculateFdlCrc(data);
            Assert.AreEqual(expected, actual, "FDL CRC for empty data failed.");
        }

        [TestMethod]
        public void TestFdlCrc_SingleByte()
        {
            byte[] data = { 0x41 }; // 'A'
            ushort expected = 0xFFBE;
            ushort actual = CrcUtils.CalculateFdlCrc(data);
            Assert.AreEqual(expected, actual, "FDL CRC for single byte 'A' failed.");
        }
        
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestFdlCrc_NullData()
        {
            CrcUtils.CalculateFdlCrc(null);
        }
    }
}

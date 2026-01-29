namespace RTLSDR.DAB;

using System;
using System.Collections.Generic;
using System.Text;

public class DabPadParser
{
    private StringBuilder _dlsBuffer = new StringBuilder();
    private int _lastToggle = -1;
    private bool _isCollecting = false;

    public event EventHandler<string> OnMessageCompleted;

    /// <summary>
    /// Processes a raw AAC Audio Unit to extract and parse X-PAD data.
    /// </summary>
    /// <param name="auData">The raw AU data (including PAD, excluding CRC)</param>
    public void ProcessAudioUnit(byte[] auData)
    {
        if (auData == null || auData.Length < 4) return;

        // 1. F-PAD is always the last 2 bytes of the AU
        int fpadIndex = auData.Length - 2;
        byte fpadL = auData[fpadIndex];

        // 2. Check X-PAD Indicator (bits 4 and 5 of the first F-PAD byte)
        // 00 = No X-PAD, 01 = Short, 10 = Variable
        int xpadType = (fpadL >> 4) & 0x03;

        if (xpadType == 0x02) // Variable size X-PAD
        {
            ParseVariableXPAD(auData, fpadIndex);
        }
        else if (xpadType == 0x01) // Short X-PAD (Fixed 4 bytes)
        {
            byte[] shortData = new byte[4];
            Buffer.BlockCopy(auData, fpadIndex - 4, shortData, 0, 4);
            // Short X-PAD is rarely used for DLS nowadays, usually for status
        }
    }

    private void ParseVariableXPAD(byte[] auData, int fpadIndex)
    {
        int ciPointer = fpadIndex - 1;
        List<byte> contentIndicators = new List<byte>();

        // Read CI (Content Indicator) bytes backwards
        while (ciPointer >= 0)
        {
            byte ci = auData[ciPointer];
            contentIndicators.Add(ci);
            if ((ci & 0x80) == 0) break; // Extension bit 0 means this is the last CI
            ciPointer--;
        }

        // Calculate data start position
        int totalDataLen = 0;
        foreach (var ci in contentIndicators)
            totalDataLen += GetXpadAppLength(ci & 0x1F);

        int dataPointer = ciPointer - totalDataLen;
        if (dataPointer < 0) return;

        // Extract data for each application defined in CIs
        foreach (var ci in contentIndicators)
        {
            int appLen = GetXpadAppLength(ci & 0x1F);
            int xAppType = (ci >> 5) & 0x03;

            byte[] appData = new byte[appLen];
            Buffer.BlockCopy(auData, dataPointer, appData, 0, appLen);

            // In most muxes, DLS is mapped to X-AppType 0 or 3
            // We also verify by looking at the DLS header
            if (IsPossiblyDLS(appData))
            {
                ParseDLSSegment(appData);
            }

            dataPointer += appLen;
        }
    }

    private bool IsPossiblyDLS(byte[] data)
    {
        if (data.Length < 2) return false;
        // DLS Command ID is bits 3-0 of the first byte. 0 = Text.
        return (data[0] & 0x0F) == 0;
    }

    private void ParseDLSSegment(byte[] data)
    {
        byte h1 = data[0];
        byte h2 = data[1];

        int toggle = (h1 & 0x80) >> 7;
        bool firstSegment = (h1 & 0x40) != 0;
        bool lastSegment = (h1 & 0x20) != 0;
        int charset = (h2 >> 4) & 0x0F;

        // If toggle bit changes, the station started a completely new text (new song)
        if (toggle != _lastToggle)
        {
            _dlsBuffer.Clear();
            _lastToggle = toggle;
            _isCollecting = firstSegment;
        }

        if (firstSegment)
        {
            _dlsBuffer.Clear();
            _isCollecting = true;
        }

        if (_isCollecting)
        {
            // Text starts at index 2
            // Note: DAB+ uses specific charsets. 0 = EBU Latin, 15 = UTF-8
            string text = (charset == 15)
                ? Encoding.UTF8.GetString(data, 2, data.Length - 2)
                : DecodeEbuLatin(data, 2, data.Length - 2);

            _dlsBuffer.Append(text.Replace("\0", ""));
        }

        if (lastSegment && _isCollecting)
        {
            string result = _dlsBuffer.ToString().Trim();
            OnMessageCompleted?.Invoke(this, result);
            _isCollecting = false;
        }
    }

    private int GetXpadAppLength(int lenInd)
    {
        int[] table = { 0, 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64, 96, 128, 192 };
        return lenInd < table.Length ? table[lenInd] : 0;
    }

    private string DecodeEbuLatin(byte[] data, int offset, int count)
    {
        // Simplification: Most EBU Latin characters match ASCII/ISO-8859-1
        // For full Czech support, you'd need a custom mapping table
        return Encoding.GetEncoding("ISO-8859-1").GetString(data, offset, count);
    }
}
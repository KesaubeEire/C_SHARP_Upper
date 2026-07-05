namespace WpfScada.Services.Plc.Modbus;

public class ModbusParseResult
{
    public bool IsError { get; set; }
    public string ErrorMessage { get; set; } = "";
    public bool IsBitMode { get; set; }
    public List<BitResult> Bits { get; set; } = [];
    public List<RegisterResult> Registers { get; set; } = [];
    public byte RawFuncCode { get; set; }
}

public class BitResult
{
    public int Index { get; set; }
    public bool IsOn { get; set; }
    public byte RawByte { get; set; }
}

public class RegisterResult
{
    public int Index { get; set; }
    public ushort Value { get; set; }
}

public static class ModbusProtocol
{
    public const int MBAP_HEADER_SIZE = 7;
    public const int MAX_REGISTERS_PER_READ = 125;
    public const int MAX_COILS_PER_READ = 2000;

    public static byte[] CalcCRC(byte[] data)
    {
        ushort crc = 0xFFFF;
        for (int i = 0; i < data.Length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc >>= 1;
                    crc ^= 0xA001;
                }
                else
                {
                    crc >>= 1;
                }
            }
        }
        return [(byte)(crc & 0xFF), (byte)(crc >> 8)];
    }

    public static bool VerifyCRC(byte[] frameWithCrc)
    {
        if (frameWithCrc.Length < 3) return false;
        byte[] data = new byte[frameWithCrc.Length - 2];
        Array.Copy(frameWithCrc, 0, data, 0, data.Length);
        byte[] expected = CalcCRC(data);
        return expected[0] == frameWithCrc[frameWithCrc.Length - 2]
            && expected[1] == frameWithCrc[frameWithCrc.Length - 1];
    }

    public static byte[] BuildReadPDU(byte devAddr, byte funcCode, ushort startAddr, ushort count)
    {
        return [devAddr, funcCode, (byte)(startAddr >> 8), (byte)startAddr, (byte)(count >> 8), (byte)count];
    }

    public static byte[] BuildRTUFrame(byte[] pdu)
    {
        byte[] crc = CalcCRC(pdu);
        byte[] frame = new byte[pdu.Length + 2];
        Array.Copy(pdu, 0, frame, 0, pdu.Length);
        frame[pdu.Length] = crc[0];
        frame[pdu.Length + 1] = crc[1];
        return frame;
    }

    public static byte[] BuildTCPFrame(byte[] pdu, byte unitId, ushort transId)
    {
        byte[] frame = new byte[MBAP_HEADER_SIZE + pdu.Length];
        frame[0] = (byte)(transId >> 8);
        frame[1] = (byte)(transId & 0xFF);
        frame[2] = 0x00;
        frame[3] = 0x00;
        frame[4] = 0x00;
        frame[5] = (byte)(1 + pdu.Length);
        frame[6] = unitId;
        Array.Copy(pdu, 0, frame, MBAP_HEADER_SIZE, pdu.Length);
        return frame;
    }

    public static ModbusParseResult ParseResponse(byte[] buffer)
    {
        var result = new ModbusParseResult();

        if (buffer.Length < 3)
        {
            result.IsError = true;
            result.ErrorMessage = "响应太短，可能通信出错";
            return result;
        }

        byte funcCode = buffer[1];
        result.RawFuncCode = funcCode;

        if ((funcCode & 0x80) != 0)
        {
            result.IsError = true;
            result.ErrorMessage = GetErrorName(buffer.Length >= 3 ? buffer[2] : (byte)0);
            return result;
        }

        byte byteCount = buffer[2];
        if (3 + byteCount > buffer.Length)
        {
            result.IsError = true;
            result.ErrorMessage = $"数据不完整：byteCount={byteCount}，实际收到 {buffer.Length - 3} 字节";
            return result;
        }

        if (funcCode == 0x01 || funcCode == 0x02)
        {
            result.IsBitMode = true;
            for (int i = 0; i < byteCount; i++)
            {
                byte bits = buffer[3 + i];
                for (int bit = 0; bit < 8; bit++)
                {
                    result.Bits.Add(new BitResult { Index = i * 8 + bit, IsOn = (bits & (1 << bit)) != 0, RawByte = bits });
                }
            }
        }
        else if (funcCode == 0x03 || funcCode == 0x04)
        {
            result.IsBitMode = false;
            int regCount = byteCount / 2;
            for (int i = 0; i < regCount; i++)
            {
                ushort value = (ushort)((buffer[3 + i * 2] << 8) | buffer[3 + i * 2 + 1]);
                result.Registers.Add(new RegisterResult { Index = i, Value = value });
            }
        }

        return result;
    }

    public static int GetMaxCount(byte funcCode) => funcCode switch
    {
        0x01 or 0x02 => MAX_COILS_PER_READ,
        0x03 or 0x04 => MAX_REGISTERS_PER_READ,
        _ => 0,
    };

    public static string GetErrorName(byte errCode) => errCode switch
    {
        0x01 => "非法功能码",
        0x02 => "非法数据地址",
        0x03 => "非法数据值",
        0x04 => "从站设备故障",
        _ => $"未知错误 (0x{errCode:X2})",
    };
}

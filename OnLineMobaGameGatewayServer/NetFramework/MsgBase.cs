using System;
using System.Text;
using Newtonsoft.Json;
//#nullable disable

public class MsgBase
{
    /// <summary>
    /// 协议名
    /// </summary>
    public string protoName = "";

    /// <summary>
    /// 编码
    /// </summary>
    /// <param name="msgBase">消息</param>
    /// <returns></returns>
    public static byte[] Encode(MsgBase msgBase)
    {
        string s = JsonConvert.SerializeObject(msgBase);
        return Encoding.UTF8.GetBytes(s);
    }

    /// <summary>
    /// 解码
    /// </summary>
    /// <param name="protoName">协议名</param>
    /// <param name="bytes">字节数组</param>
    /// <param name="offset">数组起始位置</param>
    /// <param name="count">长度</param>
    /// <returns></returns>
    public static MsgBase Decode(string protoName, byte[] bytes, int offset, int count)
    {
        string s = Encoding.UTF8.GetString(bytes, offset, count);
        Type t = Type.GetType(protoName);
        return (MsgBase)JsonConvert.DeserializeObject(s, t);
    }

    /// <summary>
    /// 协议名编码，前2个字节是协议名长度和后面是协议名内容
    /// </summary>
    /// <param name="msgBase">消息</param>
    /// <returns></returns>
    public static byte[] EncodeName(MsgBase msgBase)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(msgBase.protoName);
        short len = (short)nameBytes.Length;
        byte[] bytes = new byte[2 + len];

        // bytes[0] = (byte)(len % 256);//0000 0001 0000 0001
        // bytes[1] = (byte)(len / 256);//0000 0000 1111 1111
        
        bytes[0] = (byte)(len & 0xff);//0000 0001 0000 0001
        bytes[1] = (byte)(len >> 8);//0000 0000 1111 1111
        
        Array.Copy(nameBytes, 0, bytes, 2, len);
        return bytes;
    }

    /// <summary>
    /// 协议名解码
    /// </summary>
    /// <param name="bytes">字节数组</param>
    /// <param name="offset">起始位置</param>
    /// <param name="count">要返回的解析出来的长度</param>
    /// <returns></returns>
    public static string DecodeName(byte[] bytes, int offset, out int count)
    {
        count = 0;
        if (offset + 2 > bytes.Length) return "";
        // short len = (short)(bytes[offset + 1] * 256 + bytes[offset]);
        short len = (short)(bytes[offset + 1] << 8 | bytes[offset]);
        if (len <= 0) return "";
        count = 2 + len;
        return Encoding.UTF8.GetString(bytes, offset + 2, len);
    }
}

using System;

public class ByteArray
{
    /// <summary>
    /// 默认长度
    /// </summary>
    private const int DEFAULT_SIZE = 1024;

    /// <summary>
    /// 字节数组
    /// </summary>
    public byte[] bytes;

    /// <summary>
    /// 读的位置
    /// </summary>
    public int readIndex;

    /// <summary>
    /// 写的位置
    /// </summary>
    public int writeIndex;

    /// <summary>
    /// 初始大小
    /// </summary>
    public int initSize;

    /// <summary>
    /// 数组容量
    /// </summary>
    public int capacity;

    /// <summary>
    /// 读写之间的长度
    /// </summary>
    public int Length => writeIndex - readIndex;

    /// <summary>
    /// 剩余长度
    /// </summary>
    public int Remain => capacity - writeIndex;

    /// <summary>
    /// 创建字节数组
    /// </summary>
    /// <param name="size">数组长度</param>
    public ByteArray(int size = DEFAULT_SIZE)
    {
        bytes = new byte[size];
        initSize = size;
        capacity = size;
        readIndex = 0;
        writeIndex = 0;
    }

    /// <summary>
    /// 创建字节数组
    /// </summary>
    /// <param name="defaultBytes">默认的字节数组</param>
    public ByteArray(byte[] defaultBytes)
    {
        bytes = defaultBytes;
        initSize = defaultBytes.Length;
        capacity = defaultBytes.Length;
        readIndex = 0;
        writeIndex = defaultBytes.Length;
    }

    /// <summary>
    /// 移动数据
    /// </summary>
    public void MoveBytes()
    {
        if (Length > 0)
        {
            Array.Copy(bytes, readIndex, bytes, 0, Length);
        }

        writeIndex = Length;
        readIndex = 0;
    }

    /// <summary>
    /// 扩容
    /// </summary>
    public void Resize(int size)
    {
        if (size < Length) return;
        if (size < initSize) return;
        capacity = size;
        //新长度的数组
        byte[] newBytes = new byte[capacity];
        //将原来的数据拷贝到新数组当中
        Array.Copy(bytes, readIndex, newBytes, 0, Length);
        bytes = newBytes;
        writeIndex = Length;
        readIndex = 0;
    }
}
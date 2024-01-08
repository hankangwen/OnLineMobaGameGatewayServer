using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

public static class Gateway
{
    public enum ServerType
    {
        Gateway,//网关服务器
        Fighter,//战斗服务器
    }

    /// <summary>
    /// 用于连接客户端的服务端socket
    /// </summary>
    public static Socket listenfd;

    /// <summary>
    /// 用于连接其他服务器的Socket
    /// </summary>
    public static Socket gateway;

    /// <summary>
    /// 客户端字典
    /// </summary>
    public static Dictionary<Socket, ClientState> clientStates = new Dictionary<Socket, ClientState>();

    /// <summary>
    /// 其他服务端字典
    /// </summary>
    public static Dictionary<Socket, ServerState> serverStates = new Dictionary<Socket, ServerState>();

    /// <summary>
    /// 用于检测的列表
    /// </summary>
    public static List<Socket> sockets = new List<Socket>();

    private static float _pingInterval = 2;

    /// <summary>
    /// 连接服务器
    /// </summary>
    /// <param name="ip">ip地址</param>
    /// <param name="port">端口号</param>
    public static void Connect(string ip, int port)
    {
        listenfd = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPAddress iPAddress = IPAddress.Parse(ip);
        IPEndPoint iPEndPoint = new IPEndPoint(iPAddress, port);
        listenfd.Bind(iPEndPoint);
        listenfd.Listen(0);

        Console.WriteLine("服务器启动成功");
        while (true)
        {
            sockets.Clear();
            //放服务端的Socket
            sockets.Add(listenfd);
            //放客户端的Socket
            foreach (Socket socket in clientStates.Keys)
            {
                sockets.Add(socket);
            }
            Socket.Select(sockets, null, null, 1000);
            for (int i = 0; i < sockets.Count; i++)
            {
                Socket s = sockets[i];
                if (s == listenfd)
                {
                    //有客户端要连接
                    Accept(listenfd);
                }
                else
                {
                    //客户端发消息过来了
                    Receive(s);
                }
            }
            CheckPing();
        }
    }

    /// <summary>
    /// 连接其他服务器
    /// </summary>
    /// <param name="ip">ip</param>
    /// <param name="port">端口号</param>
    public static ServerState ConnectServer(string ip, int port)
    {
        ServerState serverState = new ServerState();
        gateway = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPAddress iPAddress = IPAddress.Parse(ip);
        IPEndPoint iPEndPoint = new IPEndPoint(iPAddress, port);
        gateway.Bind(iPEndPoint);
        gateway.Listen(0);
        Console.WriteLine("网关服务器等待其他服务器连接");
        gateway.BeginAccept(AcceptServerCallback, serverState);
        return serverState;
    }

    /// <summary>
    /// 接收其他服务端连接的回调
    /// </summary>
    /// <param name="ar"></param>
    private static void AcceptServerCallback(IAsyncResult ar)
    {
        //封装连接过来的服务端对象
        ServerState serverState = (ServerState)ar.AsyncState;
        Socket socket = gateway.EndAccept(ar);
        Console.WriteLine("连接成功");
        serverState.socket = socket;

        serverStates.Add(socket, serverState);
        //接收消息
        ByteArray byteArray = serverState.readBuffer;
        socket.BeginReceive(byteArray.bytes, byteArray.writeIndex, byteArray.Remain, SocketFlags.None, ReveiceServerCallback, serverState);
    }

    /// <summary>
    /// 接收其他服务端发过来的消息
    /// </summary>
    /// <param name="ar"></param>
    private static void ReveiceServerCallback(IAsyncResult ar)
    {
        ServerState serverState = (ServerState)ar.AsyncState;
        int count = 0;
        Socket server = serverState.socket;

        ByteArray byteArray = serverState.readBuffer;
        if (byteArray.Remain <= 0)
        {
            byteArray.MoveBytes();
        }
        if(byteArray.Remain <= 0)
        {
            Console.WriteLine("Reveive fail:数组长度不足");
            //关闭服务端（如关闭战斗服的连接）
            //Close();
            return;
        }
        server.EndReceive(ar);

        try
        {
            count = server.EndReceive(ar);
        }
        catch (SocketException e)
        {
            Console.WriteLine("Reveive fail:" + e.Message);

            //关闭服务端（如关闭战斗服的连接）
            //Close();
            return;
        }

        if (count <= 0)
        {
            Console.WriteLine("Socket Close:" + serverState.socket.RemoteEndPoint.ToString());
            //关闭服务端（如关闭战斗服的连接）
            //Close();
            return;
        }

        //处理接收过来的消息
        byteArray.writeIndex += count;
        OnReceiveData(serverState);
    }

    private static void OnReceiveData(ServerState serverState)
    {

    }

    /// <summary>
    /// 接收客户端连接
    /// </summary>
    /// <param name="listenfd">服务端的Socket</param>
    private static void Accept(Socket listenfd)
    {
        try
        {
            Socket socket = listenfd.Accept();
            Console.WriteLine("Accept " + socket.RemoteEndPoint.ToString());
            //创建描述客户端的对象
            ClientState state = new ClientState();
            state.socket = socket;

            state.lastPingTime = GetTimeStamp();
            clientStates.Add(socket, state);
        }
        catch (SocketException e)
        {
            Console.WriteLine("Accept 失败" + e.Message);
        }
    }

    /// <summary>
    /// 接收客户端发送过来的消息
    /// </summary>
    /// <param name="socket">客户端的Socket</param>
    private static void Receive(Socket socket)
    {
        ClientState state = clientStates[socket];
        ByteArray readBuffer = state.readBuffer;

        if (readBuffer.Remain <= 0)
        {
            readBuffer.MoveBytes();
        }
        if (readBuffer.Remain <= 0)
        {
            Console.WriteLine("Receive 失败， 数组不够大");
            Close(state);
            return;
        }

        int count = 0;
        try
        {
            count = socket.Receive(readBuffer.bytes, readBuffer.writeIndex, readBuffer.Remain, SocketFlags.None);
        }
        catch (SocketException e)
        {
            Console.WriteLine("Receive 失败，" + e.Message);
            Close(state);
            return;
        }
        //客户端主动关闭
        if (count <= 0)
        {
            Console.WriteLine("Socket Close:" + socket.RemoteEndPoint.ToString());
            Close(state);
            return;
        }
        readBuffer.writeIndex += count;
        //处理消息
        OnReveiceData(state);
        readBuffer.MoveBytes();
    }

    /// <summary>
    /// 处理消息
    /// </summary>
    /// <param name="state">客户端对象</param>
    private static void OnReveiceData(ClientState state)
    {
        ByteArray readBuffer = state.readBuffer;
        byte[] bytes = readBuffer.bytes;
        int readIndex = readBuffer.readIndex;

        if (readBuffer.Length <= 2)
            return;

        //解析总长度
        short length = (short)(bytes[readIndex + 1] * 256 + bytes[readIndex]);
        //收到的消息没有解析出来的多
        if (readBuffer.Length < length)
            return;

        readBuffer.readIndex += 2;

        int nameCount = 0;
        string protoName = ProtobufTool.DecodeName(readBuffer.bytes, readBuffer.readIndex, out nameCount);
        if (protoName == "")
        {
            Console.WriteLine("OnReveiceData 失败，协议名为空");
            Close(state);
            return;
        }
        readBuffer.readIndex += nameCount;

        int bodyLength = length - nameCount;
        IExtensible msgBase = ProtobufTool.Decode(protoName, readBuffer.bytes, readBuffer.readIndex, bodyLength);
        readBuffer.readIndex += bodyLength;
        readBuffer.MoveBytes();

        //通过反射调用客户端发过来的协议对应的方法
        MethodInfo mi = typeof(MsgHandler).GetMethod(protoName);
        if (mi != null)
        {
            //要执行方法的参数
            object[] o = { state, msgBase };
            mi.Invoke(null, o);
        }
        else
        {
            Console.WriteLine("OnReceiveData 反射失败");
        }

        if (readBuffer.Length > 2)
        {
            OnReveiceData(state);
        }
    }

    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="state">客户端对象</param>
    /// <param name="msgBase">消息</param>
    public static void Send(ClientState state, IExtensible msgBase)
    {
        if (state == null || !state.socket.Connected)
            return;

        //编码
        byte[] nameBytes = ProtobufTool.EncodeName(msgBase);
        byte[] bodyBytes = ProtobufTool.Encode(msgBase);
        int len = nameBytes.Length + bodyBytes.Length;
        byte[] sendBytes = new byte[len + 2];
        sendBytes[0] = (byte)(len % 256);
        sendBytes[1] = (byte)(len / 256);
        Array.Copy(nameBytes, 0, sendBytes, 2, nameBytes.Length);
        Array.Copy(bodyBytes, 0, sendBytes, 2 + nameBytes.Length, bodyBytes.Length);

        try
        {
            state.socket.Send(sendBytes, 0, sendBytes.Length, 0);
        }
        catch (SocketException e)
        {
            Console.WriteLine("Send 失败" + e.Message);
        }
    }

    /// <summary>
    /// 获取时间戳
    /// </summary>
    /// <returns></returns>
    public static long GetTimeStamp()
    {
        TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return Convert.ToInt64(ts.TotalSeconds);
    }

    /// <summary>
    /// 心跳检测
    /// </summary>
    private static void CheckPing()
    {
        foreach (ClientState state in clientStates.Values)
        {
            if (GetTimeStamp() - state.lastPingTime > _pingInterval * 4)
            {
                Console.WriteLine("心跳机制，断开连接：", state.socket.RemoteEndPoint);
                //关闭客户端
                Close(state);
                return;
            }
        }
    }

    /// <summary>
    /// 关闭对应客户端
    /// </summary>
    /// <param name="state">客户端</param>
    private static void Close(ClientState state)
    {
        state.socket.Close();
        clientStates.Remove(state.socket);
    }


    ///// <summary>
    ///// 服务端socket
    ///// </summary>
    //public static Socket listenfd;

    ///// <summary>
    ///// 客户端字典
    ///// </summary>
    //public static Dictionary<Socket, ClientState> states = new Dictionary<Socket, ClientState>();

    ///// <summary>
    ///// 用于检测的列表
    ///// </summary>
    //public static List<Socket> sockets = new List<Socket>();

    //private static float _pingInterval = 2;

    ///// <summary>
    ///// 连接服务器
    ///// </summary>
    ///// <param name="ip">ip地址</param>
    ///// <param name="port">端口号</param>
    //public static void Connect(string ip, int port)
    //{
    //    listenfd = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    //    IPAddress iPAddress = IPAddress.Parse(ip);
    //    IPEndPoint iPEndPoint = new IPEndPoint(iPAddress, port);
    //    listenfd.Bind(iPEndPoint);
    //    listenfd.Listen(0);

    //    Console.WriteLine("服务器启动成功");
    //    while (true)
    //    {
    //        sockets.Clear();
    //        //放服务端的Socket
    //        sockets.Add(listenfd);
    //        //放客户端的Socket
    //        foreach (Socket socket in states.Keys)
    //        {
    //            sockets.Add(socket);
    //        }
    //        Socket.Select(sockets, null, null, 1000);
    //        for (int i = 0; i < sockets.Count; i++)
    //        {
    //            Socket s = sockets[i];
    //            if (s == listenfd)
    //            {
    //                //有客户端要连接
    //                Accept(listenfd);
    //            }
    //            else
    //            {
    //                //客户端发消息过来了
    //                Receive(s);
    //            }
    //        }
    //        CheckPing();
    //    }
    //}

    ///// <summary>
    ///// 接收客户端连接
    ///// </summary>
    ///// <param name="listenfd">服务端的Socket</param>
    //private static void Accept(Socket listenfd)
    //{
    //    try
    //    {
    //        Socket socket = listenfd.Accept();
    //        Console.WriteLine("Accept " + socket.RemoteEndPoint.ToString());
    //        //创建描述客户端的对象
    //        ClientState state = new ClientState();
    //        state.socket = socket;

    //        state.lastPingTime = GetTimeStamp();
    //        states.Add(socket, state);
    //    }
    //    catch (SocketException e)
    //    {
    //        Console.WriteLine("Accept 失败" + e.Message);
    //    }
    //}

    ///// <summary>
    ///// 接收客户端发送过来的消息
    ///// </summary>
    ///// <param name="socket">客户端的Socket</param>
    //private static void Receive(Socket socket)
    //{
    //    ClientState state = states[socket];
    //    ByteArray readBuffer = state.readBuffer;

    //    if (readBuffer.Remain <= 0)
    //    {
    //        readBuffer.MoveBytes();
    //    }
    //    if (readBuffer.Remain <= 0)
    //    {
    //        Console.WriteLine("Receive 失败， 数组不够大");
    //        return;
    //    }

    //    int count = 0;
    //    try
    //    {
    //        count = socket.Receive(readBuffer.bytes, readBuffer.writeIndex, readBuffer.Remain, SocketFlags.None);
    //    }
    //    catch (SocketException e)
    //    {
    //        Console.WriteLine("Receive 失败，" + e.Message);
    //        return;
    //    }
    //    //客户端主动关闭
    //    if (count <= 0)
    //    {
    //        Console.WriteLine("Socket Close:" + socket.RemoteEndPoint.ToString());
    //        return;
    //    }
    //    readBuffer.writeIndex += count;
    //    //处理消息
    //    OnReveiceData(state);
    //    readBuffer.MoveBytes();
    //}

    ///// <summary>
    ///// 处理消息
    ///// </summary>
    ///// <param name="state">客户端对象</param>
    //private static void OnReveiceData(ClientState state)
    //{
    //    ByteArray readBuffer = state.readBuffer;
    //    byte[] bytes = readBuffer.bytes;
    //    int readIndex = readBuffer.readIndex;

    //    if (readBuffer.Length <= 2)
    //        return;

    //    //解析总长度
    //    short length = (short)(bytes[readIndex + 1] * 256 + bytes[readIndex]);
    //    //收到的消息没有解析出来的多
    //    if (readBuffer.Length < length)
    //        return;

    //    readBuffer.readIndex += 2;

    //    int nameCount = 0;
    //    string protoName = MsgBase.DecodeName(readBuffer.bytes, readBuffer.readIndex, out nameCount);
    //    if (protoName == "")
    //    {
    //        Console.WriteLine("OnReveiceData 失败，协议名为空");
    //        return;
    //    }
    //    readBuffer.readIndex += nameCount;

    //    int bodyLength = length - nameCount;
    //    MsgBase msgBase = MsgBase.Decode(protoName, readBuffer.bytes, readBuffer.readIndex, bodyLength);
    //    readBuffer.readIndex += bodyLength;
    //    readBuffer.MoveBytes();

    //    //通过反射调用客户端发过来的协议对应的方法
    //    MethodInfo mi = typeof(MsgHandler).GetMethod(protoName);
    //    if (mi != null)
    //    {
    //        //要执行方法的参数
    //        object[] o = { state, msgBase };
    //        mi.Invoke(null, o);
    //    }
    //    else
    //    {
    //        Console.WriteLine("OnReceiveData 反射失败");
    //    }

    //    if(readBuffer.Length > 2)
    //    {
    //        OnReveiceData(state);
    //    }
    //}

    ///// <summary>
    ///// 发送消息
    ///// </summary>
    ///// <param name="state">客户端对象</param>
    ///// <param name="msgBase">消息</param>
    //public static void Send(ClientState state, MsgBase msgBase)
    //{
    //    if (state == null || !state.socket.Connected)
    //        return;

    //    //编码
    //    byte[] nameBytes = MsgBase.EncodeName(msgBase);
    //    byte[] bodyBytes = MsgBase.Encode(msgBase);
    //    int len = nameBytes.Length + bodyBytes.Length;
    //    byte[] sendBytes = new byte[len + 2];
    //    sendBytes[0] = (byte)(len % 256);
    //    sendBytes[1] = (byte)(len / 256);
    //    Array.Copy(nameBytes, 0, sendBytes, 2, nameBytes.Length);
    //    Array.Copy(bodyBytes, 0, sendBytes, 2 + nameBytes.Length, bodyBytes.Length);

    //    try
    //    {
    //        state.socket.Send(sendBytes, 0, sendBytes.Length, 0);
    //    }
    //    catch (SocketException e)
    //    {
    //        Console.WriteLine("Send 失败" + e.Message);
    //    }
    //}

    ///// <summary>
    ///// 获取时间戳
    ///// </summary>
    ///// <returns></returns>
    //public static long GetTimeStamp()
    //{
    //    TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
    //    return Convert.ToInt64(ts.TotalSeconds);
    //}

    ///// <summary>
    ///// 心跳检测
    ///// </summary>
    //private static void CheckPing()
    //{
    //    foreach (ClientState state in states.Values)
    //    {
    //        if(GetTimeStamp() - state.lastPingTime > _pingInterval * 4)
    //        {
    //            Console.WriteLine("心跳机制，断开连接：", state.socket.RemoteEndPoint);
    //            //关闭客户端
    //            Close(state);
    //            return;
    //        }
    //    }
    //}

    ///// <summary>
    ///// 关闭对应客户端
    ///// </summary>
    ///// <param name="state">客户端</param>
    //private static void Close(ClientState state)
    //{
    //    state.socket.Close();
    //    states.Remove(state.socket);
    //}
}

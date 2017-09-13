
namespace Base.Network
{
    // 网络相关的一些常数参数
    public static class NetworkCommon
    {
        // 消息包最大长度
        public const int MaxPackageSize  = 1024 * 16;

        // 最大缓存消息数
        public const int MaxCacheMessage = 5000;

        // TCP
        public const int TcpSendBuffer = 1024 * 16;
        public const int TcpRecvBuffer = 1024 * 16;

        // UDP
        public const int UdpSendBuffer = 1024 * 32;
        public const int UdpRecvBuffer = 1024 * 32;

        // KCP相关参数
        public const int KcpSendWnd = 128;  // 发送窗口
        public const int KcpRecvWnd = 128;  // 接收窗口
    }
}

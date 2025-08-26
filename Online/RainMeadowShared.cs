using System.Net;

namespace RainMeadow.Shared {
    static partial class SharedPlatform
    {
        static partial void getHeartBeatTime(ref ulong heartbeatTime)
        {
            heartbeatTime = (ulong)RainMeadow.rainMeadowOptions.UdpHeartbeat.Value;
        }
        static partial void getTimeoutTime(ref ulong TimeoutTime)
        {
            TimeoutTime = (ulong)RainMeadow.rainMeadowOptions.UdpTimeout.Value;
        }
        static partial void getTimeMS(ref ulong time)
        {
            time = (ulong)(UnityEngine.Time.realtimeSinceStartupAsDouble * 1000);
        }
    }
}

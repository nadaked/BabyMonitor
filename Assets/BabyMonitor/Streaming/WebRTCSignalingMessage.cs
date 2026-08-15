using System;

namespace BabyMonitor.Streaming
{
    [Serializable]
    public sealed class WebRTCSignalingMessage
    {
        public string type;
        public string token;
        public string sdp;
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }
}

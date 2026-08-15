using System;

namespace BabyMonitor.Pairing
{
    [Serializable]
    public class PairingPayload
    {
        public int version;
        public string address;
        public int port;
        public string token;

        public PairingPayload(
            string address,
            int port,
            string token)
        {
            version = 1;
            this.address = address;
            this.port = port;
            this.token = token;
        }
    }
}
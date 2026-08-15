using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using UnityEngine;

namespace BabyMonitor.Pairing
{
    public class PairingSession
    {
        public const int DefaultPort = 45873;

        public PairingPayload Payload { get; }

        public PairingSession()
        {
            string localIp = GetLocalIPv4();
            string token = GenerateToken();

            Payload = new PairingPayload(
                localIp,
                DefaultPort,
                token
            );
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(Payload);
        }

        private static string GenerateToken()
        {
            byte[] bytes = new byte[32];

            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            return BitConverter.ToString(bytes).Replace("-", "");
        }

        private static string GetLocalIPv4()
        {
            var candidates = new List<(string ip, int score, string interfaceName)>();

            foreach (NetworkInterface networkInterface
                     in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (networkInterface.NetworkInterfaceType ==
                    NetworkInterfaceType.Loopback)
                    continue;

                if (networkInterface.NetworkInterfaceType ==
                    NetworkInterfaceType.Tunnel)
                    continue;

                string name = networkInterface.Name.ToLowerInvariant();
                string description =
                    networkInterface.Description.ToLowerInvariant();

                // VPN / VM / Docker / sanal adaptörleri ele.
                if (IsVirtualInterface(name) ||
                    IsVirtualInterface(description))
                    continue;

                IPInterfaceProperties properties;

                try
                {
                    properties = networkInterface.GetIPProperties();
                }
                catch
                {
                    continue;
                }

                bool hasGateway =
                    properties.GatewayAddresses.Any(x =>
                        x.Address != null &&
                        x.Address.AddressFamily ==
                        AddressFamily.InterNetwork &&
                        !x.Address.Equals(IPAddress.Any));

                foreach (UnicastIPAddressInformation unicast
                         in properties.UnicastAddresses)
                {
                    IPAddress address = unicast.Address;

                    if (address.AddressFamily !=
                        AddressFamily.InterNetwork)
                        continue;

                    if (IPAddress.IsLoopback(address))
                        continue;

                    string ip = address.ToString();

                    // 169.254.x.x gibi self-assigned adresleri alma.
                    if (ip.StartsWith("169.254."))
                        continue;

                    int score = 0;

                    // Default gateway'i olan interface çok daha muhtemel.
                    if (hasGateway)
                        score += 100;

                    // Fiziksel Wi-Fi.
                    if (networkInterface.NetworkInterfaceType ==
                        NetworkInterfaceType.Wireless80211)
                    {
                        score += 100;
                    }

                    // macOS Wi-Fi genelde en0,
                    // Android genelde wlan0.
                    if (name == "en0")
                        score += 80;

                    if (name.StartsWith("wlan"))
                        score += 80;

                    if (name.Contains("wifi") ||
                        description.Contains("wi-fi") ||
                        description.Contains("wireless"))
                    {
                        score += 50;
                    }

                    candidates.Add((
                        ip,
                        score,
                        networkInterface.Name
                    ));
                }
            }

            if (candidates.Count == 0)
                throw new Exception("Local IPv4 address not found.");

            var best =
                candidates
                    .OrderByDescending(x => x.score)
                    .First();

            Debug.Log(
                $"[BabyMonitor] Selected LAN interface: " +
                $"{best.interfaceName} → {best.ip}"
            );

            return best.ip;
        }
        
        private static bool IsVirtualInterface(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            value = value.ToLowerInvariant();

            string[] blocked =
            {
                "utun",
                "tun",
                "tap",
                "vpn",
                "docker",
                "bridge",
                "vmnet",
                "vbox",
                "virtual",
                "loopback"
            };

            return blocked.Any(value.Contains);
        }
    }
}
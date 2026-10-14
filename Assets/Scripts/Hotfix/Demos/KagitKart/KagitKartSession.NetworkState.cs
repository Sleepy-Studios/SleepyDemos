using System;
using System.IO;
using UnityEngine;

namespace Hotfix.KagitKart
{
    public sealed partial class KagitKartSession
    {
        internal void WriteNetworkState(BinaryWriter writer)
        {
            writer.Write(RaceTime);
            foreach (var kart in karts) kart.WriteNetworkState(writer);
            Items.WriteNetworkState(writer);
            Race.WriteNetworkState(writer);
        }

        internal void ReadNetworkState(BinaryReader reader)
        {
            RaceTime = reader.ReadDouble();
            foreach (var kart in karts)
            {
                // 共享快照仅推进远端姿态，本地赛车由官方 Reconcile 恢复。
                if (kart == Player)
                {
                    using var saved = new MemoryStream();
                    using var writer = new BinaryWriter(saved);
                    kart.WriteNetworkState(writer);
                    kart.ReadNetworkState(reader, karts, false);
                    saved.Position = 0;
                    using var restore = new BinaryReader(saved);
                    kart.ReadNetworkState(restore, karts, false);
                }
                else
                {
                    kart.ReadNetworkState(reader, karts, true);
                }
            }
            Items.ReadNetworkState(reader);
            Race.ReadNetworkState(reader);
            lastNetworkSnapshot = Time.unscaledTime;
        }

        internal KagitKartVehicle FindNetworkKart(string id)
        {
            foreach (var kart in karts)
            {
                if (kart.Id == id) return kart;
            }
            return null;
        }
    }
}

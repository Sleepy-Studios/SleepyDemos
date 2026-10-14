using System;
using System.IO;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 固定比赛协议的紧凑状态；不将 Hotfix 类型交给 FishNet Weaver。
    internal static class KagitKartNetworkCodec
    {
        internal static void WriteControls(BinaryWriter writer, KagitKartControls value)
        {
            writer.Write(value.Steer);
            writer.Write(value.Throttle);
            writer.Write(value.Brake);
            writer.Write((byte)((value.Drift ? 1 : 0) | (value.Item ? 2 : 0) | (value.LookBack ? 4 : 0)));
        }

        internal static KagitKartControls ReadControls(BinaryReader reader)
        {
            float steer = reader.ReadSingle();
            float throttle = reader.ReadSingle();
            float brake = reader.ReadSingle();
            byte flags = reader.ReadByte();
            if (!float.IsFinite(steer) || !float.IsFinite(throttle) || !float.IsFinite(brake)
                || Math.Abs(steer) > 1 || throttle < 0 || throttle > 1 || brake < 0 || brake > 1 || flags > 7)
            {
                throw new InvalidDataException("赛车输入超出有效范围。");
            }
            return new KagitKartControls { Steer = steer, Throttle = throttle, Brake = brake,
                Drift = (flags & 1) != 0, Item = (flags & 2) != 0, LookBack = (flags & 4) != 0 };
        }

        internal static void WriteVector(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        internal static Vector3 ReadVector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

        internal static void WriteRotation(BinaryWriter writer, Quaternion value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        internal static Quaternion ReadRotation(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        internal static string ReadOptionalString(BinaryReader reader)
        {
            string value = reader.ReadString();
            return value.Length == 0 ? null : value;
        }

        internal static int ReadCount(BinaryReader reader, int maximum)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum) throw new InvalidDataException("比赛状态数量无效。");
            return count;
        }
    }
}

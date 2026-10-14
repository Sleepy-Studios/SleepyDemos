using System.IO;

namespace Hotfix.KagitKart
{
    public sealed partial class KagitKartItems
    {
        internal void NotifyNetworkEvent(KagitKartItemEvent value) => Signaled?.Invoke(value);

        internal void WriteNetworkState(BinaryWriter writer)
        {
            foreach (var box in boxes)
            {
                writer.Write(box.Active);
                writer.Write(box.TakenAt);
                writer.Write(box.TakenBy?.Id ?? string.Empty);
            }
            writer.Write(hazards.Count);
            foreach (var gum in hazards) WriteGum(writer, gum);
            writer.Write(projectiles.Count);
            foreach (var projectile in projectiles) WriteProjectile(writer, projectile);
        }

        internal void ReadNetworkState(BinaryReader reader)
        {
            foreach (var box in boxes)
            {
                box.Active = reader.ReadBoolean();
                box.TakenAt = reader.ReadDouble();
                box.TakenBy = Kart(reader.ReadString());
            }
            foreach (var gum in hazards) hazardFree.Push(gum);
            hazards.Clear();
            int count = KagitKartNetworkCodec.ReadCount(reader, 128);
            for (int i = 0; i < count; i++)
            {
                var gum = hazardFree.Count > 0 ? hazardFree.Pop() : new KagitKartGum();
                ReadGum(reader, gum);
                hazards.Add(gum);
            }
            foreach (var projectile in projectiles) projectileFree.Push(projectile);
            projectiles.Clear();
            count = KagitKartNetworkCodec.ReadCount(reader, 128);
            for (int i = 0; i < count; i++)
            {
                var projectile = projectileFree.Count > 0 ? projectileFree.Pop() : new KagitKartProjectile();
                ReadProjectile(reader, projectile);
                projectiles.Add(projectile);
            }
        }

        private KagitKartVehicle Kart(string id)
        {
            foreach (var kart in karts)
            {
                if (kart.Id == id) return kart;
            }
            return null;
        }

        private static void WriteGum(BinaryWriter writer, KagitKartGum value)
        {
            writer.Write(value.Id);
            KagitKartNetworkCodec.WriteVector(writer, value.Position);
            KagitKartNetworkCodec.WriteVector(writer, value.PreviousPosition);
            KagitKartNetworkCodec.WriteVector(writer, value.Up);
            writer.Write(value.Hint);
            writer.Write(value.Owner?.Id ?? string.Empty);
            writer.Write(value.Held?.Id ?? string.Empty);
            writer.Write(value.HeldSince);
            writer.Write(value.DroppedAt);
            writer.Write(value.HeldFor);
            writer.Write(value.Dead);
        }

        private void ReadGum(BinaryReader reader, KagitKartGum value)
        {
            value.Id = reader.ReadInt32();
            value.Position = KagitKartNetworkCodec.ReadVector(reader);
            value.PreviousPosition = KagitKartNetworkCodec.ReadVector(reader);
            value.Up = KagitKartNetworkCodec.ReadVector(reader);
            value.Hint = reader.ReadInt32();
            value.Owner = Kart(reader.ReadString());
            value.Held = Kart(reader.ReadString());
            value.HeldSince = reader.ReadDouble();
            value.DroppedAt = reader.ReadDouble();
            value.HeldFor = reader.ReadDouble();
            value.Dead = reader.ReadBoolean();
        }

        private static void WriteProjectile(BinaryWriter writer, KagitKartProjectile value)
        {
            writer.Write(value.Id);
            writer.Write(value.Kind ?? string.Empty);
            writer.Write(value.Owner?.Id ?? string.Empty);
            writer.Write(value.Target?.Id ?? string.Empty);
            KagitKartNetworkCodec.WriteVector(writer, value.Position);
            KagitKartNetworkCodec.WriteVector(writer, value.PreviousPosition);
            KagitKartNetworkCodec.WriteVector(writer, value.Up);
            writer.Write(value.Heading);
            writer.Write(value.PreviousHeading);
            writer.Write(value.Speed);
            writer.Write(value.VerticalSpeed);
            writer.Write(value.Age);
            writer.Write(value.Life);
            writer.Write(value.Bounces);
            writer.Write(value.FlipAt);
            writer.Write(value.Hint);
            writer.Write(value.Distance);
            writer.Write(value.Lateral);
            writer.Write(value.HalfWidth);
            writer.Write(value.GroundY);
            writer.Write(value.Locked);
            writer.Write(value.Straight);
            writer.Write(value.Backward);
            writer.Write(value.Falling);
            writer.Write(value.FallAt);
            writer.Write(value.TurnRate);
            writer.Write(value.ThreatKart?.Id ?? string.Empty);
            writer.Write(value.ThreatMask);
            writer.Write(value.Victims);
            writer.Write(value.Phase);
            writer.Write(value.PhaseTime);
            writer.Write(value.Progress);
            writer.Write(value.InitialLateral);
            KagitKartNetworkCodec.WriteVector(writer, value.From);
            KagitKartNetworkCodec.WriteVector(writer, value.To);
            writer.Write(value.Dead);
        }

        private void ReadProjectile(BinaryReader reader, KagitKartProjectile value)
        {
            value.Id = reader.ReadInt32();
            value.Kind = KagitKartNetworkCodec.ReadOptionalString(reader);
            value.Owner = Kart(reader.ReadString());
            value.Target = Kart(reader.ReadString());
            value.Position = KagitKartNetworkCodec.ReadVector(reader);
            value.PreviousPosition = KagitKartNetworkCodec.ReadVector(reader);
            value.Up = KagitKartNetworkCodec.ReadVector(reader);
            value.Heading = reader.ReadSingle();
            value.PreviousHeading = reader.ReadSingle();
            value.Speed = reader.ReadSingle();
            value.VerticalSpeed = reader.ReadSingle();
            value.Age = reader.ReadDouble();
            value.Life = reader.ReadDouble();
            value.Bounces = reader.ReadInt32();
            value.FlipAt = reader.ReadDouble();
            value.Hint = reader.ReadInt32();
            value.Distance = reader.ReadDouble();
            value.Lateral = reader.ReadSingle();
            value.HalfWidth = reader.ReadSingle();
            value.GroundY = reader.ReadSingle();
            value.Locked = reader.ReadBoolean();
            value.Straight = reader.ReadBoolean();
            value.Backward = reader.ReadBoolean();
            value.Falling = reader.ReadBoolean();
            value.FallAt = reader.ReadDouble();
            value.TurnRate = reader.ReadSingle();
            value.ThreatKart = Kart(reader.ReadString());
            value.ThreatMask = reader.ReadInt32();
            value.Victims = reader.ReadInt32();
            value.Phase = reader.ReadInt32();
            value.PhaseTime = reader.ReadDouble();
            value.Progress = reader.ReadDouble();
            value.InitialLateral = reader.ReadSingle();
            value.From = KagitKartNetworkCodec.ReadVector(reader);
            value.To = KagitKartNetworkCodec.ReadVector(reader);
            value.Dead = reader.ReadBoolean();
        }
    }
}

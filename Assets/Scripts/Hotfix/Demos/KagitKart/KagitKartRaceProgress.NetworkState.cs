using System.IO;

namespace Hotfix.KagitKart
{
    public sealed partial class KagitKartCountdown
    {
        internal void WriteNetworkState(BinaryWriter writer)
        {
            writer.Write(remaining);
            writer.Write(wasThrottleHeld);
            writer.Write(lastThrottlePress);
            writer.Write((int)Grade);
        }

        internal void ReadNetworkState(BinaryReader reader)
        {
            remaining = reader.ReadInt32();
            wasThrottleHeld = reader.ReadBoolean();
            lastThrottlePress = reader.ReadDouble();
            Grade = (KagitKartStartGrade)reader.ReadInt32();
        }
    }

    public sealed partial class KagitKartRaceProgress
    {
        internal void WriteNetworkState(BinaryWriter writer)
        {
            writer.Write(previousDistance);
            writer.Write(lapStartedAt);
            writer.Write(crossings);
            writer.Write(nextCheckpoint);
            writer.Write(wrongSeconds);
            writer.Write(rightSeconds);
            writer.Write(Lap);
            writer.Write(Progress);
            writer.Write(MaxProgress);
            writer.Write(Finished);
            writer.Write(FinishTime);
            writer.Write(WrongWay);
            writer.Write(lapTimes.Count);
            foreach (double lap in lapTimes) writer.Write(lap);
        }

        internal void ReadNetworkState(BinaryReader reader)
        {
            previousDistance = reader.ReadDouble();
            lapStartedAt = reader.ReadDouble();
            crossings = reader.ReadInt32();
            nextCheckpoint = reader.ReadInt32();
            wrongSeconds = reader.ReadDouble();
            rightSeconds = reader.ReadDouble();
            Lap = reader.ReadInt32();
            Progress = reader.ReadDouble();
            MaxProgress = reader.ReadDouble();
            Finished = reader.ReadBoolean();
            FinishTime = reader.ReadDouble();
            WrongWay = reader.ReadBoolean();
            lapTimes.Clear();
            int count = KagitKartNetworkCodec.ReadCount(reader, laps);
            for (int i = 0; i < count; i++) lapTimes.Add(reader.ReadDouble());
        }
    }
}

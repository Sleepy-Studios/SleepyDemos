using System.IO;

namespace Hotfix.KagitKart
{
    public sealed partial class KagitKartRace
    {
        private int[] receivedPlaces;
        internal void WriteNetworkState(BinaryWriter writer)
        {
            writer.Write((int)State);
            writer.Write(WaitLeft);
            writer.Write(BestLapTime ?? -1);
            foreach (var entry in entries)
            {
                entry.Countdown.WriteNetworkState(writer);
                entry.Progress.WriteNetworkState(writer);
                writer.Write(entry.Vehicle.Place);
                writer.Write(entry.FinishTime);
                writer.Write(entry.Estimated);
                writer.Write(entry.BestLap ?? -1);
            }
            writer.Write(results.Count);
            foreach (var result in results)
            {
                writer.Write(result.CharacterId);
                writer.Write(result.Place);
                writer.Write(result.Time);
                writer.Write(result.Estimated);
                writer.Write(result.BestLap ?? -1);
                writer.Write(result.Points);
                writer.Write(result.TotalPoints);
            }
        }

        internal void ReadNetworkState(BinaryReader reader, bool notify = true)
        {
            bool previousSuppression = suppressNetworkSignals;
            suppressNetworkSignals = !notify;
            var before = State;
            int countdown = Countdown;
            State = (KagitKartRaceState)reader.ReadInt32();
            WaitLeft = reader.ReadDouble();
            double best = reader.ReadDouble();
            BestLapTime = best < 0 ? null : best;
            if (receivedPlaces == null)
            {
                receivedPlaces = new int[entries.Length];
                for (int i = 0; i < entries.Length; i++) receivedPlaces[i] = entries[i].Vehicle.Place;
            }
            int entryIndex = 0;
            foreach (var entry in entries)
            {
                int lap = entry.Progress.Lap;
                bool finished = entry.Progress.Finished;
                bool wrongWay = entry.Progress.WrongWay;
                entry.Countdown.ReadNetworkState(reader);
                entry.Progress.ReadNetworkState(reader);
                int place = reader.ReadInt32();
                entry.FinishTime = reader.ReadDouble();
                entry.Estimated = reader.ReadBoolean();
                double bestLap = reader.ReadDouble();
                entry.BestLap = bestLap < 0 ? null : bestLap;
                if (entry.Vehicle.IsPlayer) StartGrade = entry.Countdown.Grade;
                if (receivedPlaces[entryIndex] != place) Emit("place", entry.Vehicle, place);
                receivedPlaces[entryIndex++] = place;
                if (entry.Progress.Lap > lap && entry.Progress.Lap >= 2 && !entry.Progress.Finished)
                {
                    Emit("lap", entry.Vehicle, entry.Progress.Lap, entry.Progress.LapTimes[entry.Progress.LapTimes.Count - 1]);
                    if (entry.Progress.Lap == Laps) Emit("finalLap", entry.Vehicle);
                }
                if (!finished && entry.Progress.Finished) Emit("finish", entry.Vehicle, place, entry.FinishTime);
                if (wrongWay != entry.Progress.WrongWay) Emit("wrongWay", entry.Vehicle, entry.Progress.WrongWay ? 1 : 0);
            }
            results.Clear();
            int count = KagitKartNetworkCodec.ReadCount(reader, entries.Length);
            for (int i = 0; i < count; i++)
            {
                string id = reader.ReadString();
                int place = reader.ReadInt32();
                double time = reader.ReadDouble();
                bool estimated = reader.ReadBoolean();
                double lap = reader.ReadDouble();
                results.Add(new KagitKartRaceResult { CharacterId = id, Place = place, Time = time,
                    Estimated = estimated, BestLap = lap < 0 ? null : lap, Points = reader.ReadInt32(), TotalPoints = reader.ReadInt32() });
            }
            if (countdown != Countdown) Emit("countdown", number: Countdown);
            if (before == KagitKartRaceState.Countdown && State != before) Emit("raceStart");
            if (before != KagitKartRaceState.Done && State == KagitKartRaceState.Done)
            {
                if (Mode == KagitKartRaceMode.GrandPrix && !grandPrix.RoundComplete) grandPrix.Commit(results, configuration.race.points);
                Emit("raceEnd");
            }
            suppressNetworkSignals = previousSuppression;
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Hotfix.KagitKart
{
    public sealed partial class KagitKartVehicle
    {
        internal bool SuppressSignals { get; set; }

        internal readonly struct SignalContext
        {
            private readonly int tier;
            private readonly float air, impact, boostDuration;
            private readonly string boostSource, spinCause;

            internal SignalContext(int tier, float air, float impact, float boostDuration, string boostSource, string spinCause)
            {
                this.tier = tier;
                this.air = air;
                this.impact = impact;
                this.boostDuration = boostDuration;
                this.boostSource = boostSource;
                this.spinCause = spinCause;
            }

            internal void Write(BinaryWriter writer)
            {
                writer.Write(tier);
                writer.Write(air);
                writer.Write(impact);
                writer.Write(boostDuration);
                writer.Write(boostSource ?? string.Empty);
                writer.Write(spinCause ?? string.Empty);
            }

            internal static SignalContext Read(BinaryReader reader) => new(reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                KagitKartNetworkCodec.ReadOptionalString(reader), KagitKartNetworkCodec.ReadOptionalString(reader));

            internal void Apply(KagitKartVehicle kart)
            {
                kart.DriftTier = tier;
                kart.airTime = air;
                kart.LastImpactSpeed = impact;
                kart.LastBoostDuration = boostDuration;
                kart.BoostSource = boostSource;
                kart.SpinCause = spinCause;
            }
        }

        internal SignalContext CaptureSignalContext() => new(DriftTier, airTime, LastImpactSpeed, LastBoostDuration, BoostSource, SpinCause);

        internal void NotifyNetworkSignal(KagitKartVehicleSignal signal, SignalContext context)
        {
            var current = CaptureSignalContext();
            context.Apply(this);
            try
            {
                Emit(signal);
            }
            finally
            {
                current.Apply(this);
            }
        }

        internal void WriteNetworkState(BinaryWriter writer)
        {
            KagitKartNetworkCodec.WriteControls(writer, previousControls);
            writer.Write(clock);
            writer.Write(yawHigh);
            writer.Write(verticalSpeed);
            writer.Write(airTime);
            writer.Write(steerLock);
            writer.Write(brakeHeld);
            writer.Write(outTime);
            writer.Write(onPad);
            writer.Write(wallContact);
            writer.Write(lastWallImpact);
            writer.Write(trickArmedAt);
            writer.Write(trickLeftGroundAt);
            writer.Write(trickFromRamp);
            writer.Write(trickDone);
            writer.Write(trickPending);
            writer.Write(safeDistance);
            writer.Write(safeLateral);
            writer.Write(safeTimer);
            writer.Write(safeValid);
            KagitKartNetworkCodec.WriteVector(writer, rescueFrom);
            KagitKartNetworkCodec.WriteVector(writer, rescueTo);
            writer.Write(rescueFromHeading);
            writer.Write(rescueHeading);
            writer.Write(rescueTime);
            writer.Write(touchdownAt);
            writer.Write(lastThrottleHeld);
            writer.Write(launchWindow);
            KagitKartNetworkCodec.WriteControls(writer, Controls);
            writer.Write(Place);
            writer.Write(Progress);
            writer.Write(Finished);
            writer.Write(Autopilot);
            writer.Write(Item ?? string.Empty);
            writer.Write(ItemCount);
            writer.Write(ItemHeld);
            writer.Write(Roulette);
            writer.Write(RouletteDuration);
            KagitKartNetworkCodec.WriteVector(writer, Position);
            KagitKartNetworkCodec.WriteVector(writer, PreviousPosition);
            KagitKartNetworkCodec.WriteVector(writer, Velocity);
            writer.Write(Heading);
            writer.Write(Speed);
            writer.Write(BaseTop);
            writer.Write(TopSpeed);
            writer.Write(RubberBand);
            writer.Write(Pinned);
            writer.Write(Grounded);
            writer.Write(SteerInput);
            writer.Write(VisualYaw);
            KagitKartNetworkCodec.WriteVector(writer, Up);
            KagitKartNetworkCodec.WriteRotation(writer, Rotation);
            KagitKartNetworkCodec.WriteRotation(writer, PreviousRotation);
            writer.Write(Surface ?? string.Empty);
            writer.Write(Drifting);
            writer.Write(DriftDirection);
            writer.Write(DriftCharge);
            writer.Write(DriftTier);
            writer.Write(BoostTime);
            writer.Write(LastBoostDuration);
            writer.Write(LastInkDuration);
            writer.Write(BoostStrength);
            writer.Write(BoostSource ?? string.Empty);
            writer.Write(SpinTime);
            writer.Write(SpinCause ?? string.Empty);
            writer.Write(GraceTime);
            writer.Write(InvincibleTime);
            writer.Write(InkTime);
            writer.Write(RescueStage);
            writer.Write(LastImpactSpeed);
            writer.Write(LastImpactKept);
            writer.Write(LastImpactAngle);
            writer.Write(LastHitBy?.Id ?? string.Empty);
            writer.Write(info.Index);
        }

        internal void ReadNetworkState(BinaryReader reader, IReadOnlyList<KagitKartVehicle> karts, bool interpolate)
        {
            Vector3 previous = Position;
            Quaternion rotation = Rotation;
            previousControls = KagitKartNetworkCodec.ReadControls(reader);
            clock = reader.ReadSingle();
            yawHigh = reader.ReadSingle();
            verticalSpeed = reader.ReadSingle();
            airTime = reader.ReadSingle();
            steerLock = reader.ReadSingle();
            brakeHeld = reader.ReadDouble();
            outTime = reader.ReadSingle();
            onPad = reader.ReadBoolean();
            wallContact = reader.ReadBoolean();
            lastWallImpact = reader.ReadSingle();
            trickArmedAt = reader.ReadSingle();
            trickLeftGroundAt = reader.ReadSingle();
            trickFromRamp = reader.ReadInt32();
            trickDone = reader.ReadBoolean();
            trickPending = reader.ReadBoolean();
            safeDistance = reader.ReadDouble();
            safeLateral = reader.ReadSingle();
            safeTimer = reader.ReadSingle();
            safeValid = reader.ReadBoolean();
            rescueFrom = KagitKartNetworkCodec.ReadVector(reader);
            rescueTo = KagitKartNetworkCodec.ReadVector(reader);
            rescueFromHeading = reader.ReadSingle();
            rescueHeading = reader.ReadSingle();
            rescueTime = reader.ReadDouble();
            touchdownAt = reader.ReadSingle();
            lastThrottleHeld = reader.ReadSingle();
            launchWindow = reader.ReadBoolean();
            Controls = KagitKartNetworkCodec.ReadControls(reader);
            Place = reader.ReadInt32();
            Progress = reader.ReadDouble();
            Finished = reader.ReadBoolean();
            Autopilot = reader.ReadBoolean();
            Item = KagitKartNetworkCodec.ReadOptionalString(reader);
            ItemCount = reader.ReadInt32();
            ItemHeld = reader.ReadBoolean();
            Roulette = reader.ReadDouble();
            RouletteDuration = reader.ReadDouble();
            Position = KagitKartNetworkCodec.ReadVector(reader);
            PreviousPosition = KagitKartNetworkCodec.ReadVector(reader);
            Velocity = KagitKartNetworkCodec.ReadVector(reader);
            Heading = reader.ReadSingle();
            Speed = reader.ReadSingle();
            BaseTop = reader.ReadSingle();
            TopSpeed = reader.ReadSingle();
            RubberBand = reader.ReadSingle();
            Pinned = reader.ReadBoolean();
            Grounded = reader.ReadBoolean();
            SteerInput = reader.ReadSingle();
            VisualYaw = reader.ReadSingle();
            Up = KagitKartNetworkCodec.ReadVector(reader);
            Rotation = KagitKartNetworkCodec.ReadRotation(reader);
            PreviousRotation = KagitKartNetworkCodec.ReadRotation(reader);
            Surface = KagitKartNetworkCodec.ReadOptionalString(reader);
            Drifting = reader.ReadBoolean();
            DriftDirection = reader.ReadInt32();
            DriftCharge = reader.ReadSingle();
            DriftTier = reader.ReadInt32();
            BoostTime = reader.ReadSingle();
            LastBoostDuration = reader.ReadSingle();
            LastInkDuration = reader.ReadSingle();
            BoostStrength = reader.ReadSingle();
            BoostSource = KagitKartNetworkCodec.ReadOptionalString(reader);
            SpinTime = reader.ReadSingle();
            SpinCause = KagitKartNetworkCodec.ReadOptionalString(reader);
            GraceTime = reader.ReadSingle();
            InvincibleTime = reader.ReadSingle();
            InkTime = reader.ReadSingle();
            RescueStage = reader.ReadInt32();
            LastImpactSpeed = reader.ReadSingle();
            LastImpactKept = reader.ReadSingle();
            LastImpactAngle = reader.ReadSingle();
            LastHitBy = FindKart(karts, reader.ReadString());
            track.Query(Position, reader.ReadInt32(), ref info);
            if (interpolate)
            {
                PreviousPosition = previous;
                PreviousRotation = rotation;
            }
        }

        private static KagitKartVehicle FindKart(IReadOnlyList<KagitKartVehicle> karts, string id)
        {
            foreach (var kart in karts)
            {
                if (kart.Id == id) return kart;
            }
            return null;
        }
    }
}

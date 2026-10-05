using Core.Runtime;

namespace Hotfix.DroneFlight
{
    public abstract class DroneFlightAction : IAction { }
    public sealed class DroneFlightSelectAction : DroneFlightAction
    {
        public DroneVehicleKind Kind { get; }
        public DroneFlightSelectAction(DroneVehicleKind kind) => Kind = kind;
    }
    public sealed class DroneFlightStartAction : DroneFlightAction { }
    public sealed class DroneFlightExitAction : DroneFlightAction { }
    internal sealed class DroneFlightSelectionResultAction : DroneFlightAction
    {
        internal int Version { get; }
        internal bool Succeeded { get; }
        internal string Message { get; }
        internal DroneFlightSelectionResultAction(int version, bool succeeded, string message = null) { Version=version; Succeeded=succeeded; Message=message; }
    }
    public sealed class DroneFlightControlAction : DroneFlightAction
    {
        public string Command { get; }
        public DroneFlightControlAction(string command) => Command = command;
    }
    public sealed class DroneFlightHoldAction : DroneFlightAction
    {
        public string Command { get; }
        public bool Held { get; }
        public DroneFlightHoldAction(string command, bool held) { Command=command; Held=held; }
    }
    internal sealed class DroneFlightTelemetryAction : DroneFlightAction
    {
        internal string SessionId { get; }
        internal DroneFlightUiSnapshot Snapshot { get; }
        internal DroneFlightTelemetryAction(string sessionId, DroneFlightUiSnapshot snapshot) { SessionId=sessionId; Snapshot=snapshot; }
    }
}

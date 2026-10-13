using FishNet.Broadcast;
using UnityEngine;

namespace Core.Runtime.Networking
{
    // 字段由 FishNet 在 AOT 编译时生成序列化，业务身份只从连接读取。
    public struct SessionHello : IBroadcast
    {
        public ushort Protocol;
        public string ContentVersion;
        public string DisplayName;
    }

    public struct SessionWelcome : IBroadcast
    {
        public ushort Protocol;
        public string ContentVersion;
        public int Capacity;
        public int LocalId;
        public int AuthorityId;
        public SessionMember[] Members;
        public string Error;
    }

    public struct SessionMember
    {
        public int Id;
        public string DisplayName;
    }

    public struct SessionCommand : IBroadcast
    {
        public ushort Channel;
        public ulong Sequence;
        public byte[] Payload;
    }

    public struct SessionSnapshot : IBroadcast
    {
        public ushort Channel;
        public ulong Sequence;
        public byte[] Payload;
        public bool Reliable;
    }

    public struct SessionAvatar : IBroadcast
    {
        public int Id;
        public Vector3 Position;
        public Quaternion Rotation;
        public float HeadPitch;
    }
}

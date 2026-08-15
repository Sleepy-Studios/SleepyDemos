using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>把渔叉连续碰撞回调转交给装备模块。</summary>
    public sealed class DroneHarpoonProjectile : MonoBehaviour
    {
        [SerializeField] private DroneHarpoonModule module;

        internal void Configure(DroneHarpoonModule owner)
        {
            module = owner;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount > 0)
            {
                module?.NotifyProjectileHit(collision.collider, collision.GetContact(0).point);
            }
        }
    }
}

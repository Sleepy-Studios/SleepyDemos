using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>把地面标记转换为起落架脚底留有净空的无人机根姿态。</summary>
    internal static class DroneSpawnPlacement
    {
        internal const float DefaultGroundClearanceMeters = 0.01f;

        internal static bool TryPlaceOnGround(
            GameObject drone,
            Transform groundMarker,
            float clearanceMeters,
            out float footMinimumY)
        {
            footMinimumY = float.NaN;
            if (drone == null || groundMarker == null)
            {
                return false;
            }

            var root = drone.transform;
            root.localScale = Vector3.one;
            root.SetPositionAndRotation(
                new Vector3(groundMarker.position.x, groundMarker.position.y, groundMarker.position.z),
                groundMarker.rotation);

            var found = false;
            var minimum = float.PositiveInfinity;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || !collider.enabled || collider.gameObject.name != "Foot")
                {
                    continue;
                }

                if (!TryGetMinimumWorldY(collider, out var candidate))
                {
                    continue;
                }

                found = true;
                minimum = Mathf.Min(minimum, candidate);
            }

            if (!found || !float.IsFinite(minimum))
            {
                return false;
            }

            var targetMinimum = groundMarker.position.y + Mathf.Max(0f, clearanceMeters);
            root.position += Vector3.up * (targetMinimum - minimum);
            footMinimumY = targetMinimum;
            return true;
        }

        private static bool TryGetMinimumWorldY(Collider collider, out float minimumY)
        {
            minimumY = float.PositiveInfinity;
            switch (collider)
            {
                case BoxCollider box:
                {
                    var half = box.size * 0.5f;
                    for (var x = -1; x <= 1; x += 2)
                    for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var point = box.center + Vector3.Scale(half, new Vector3(x, y, z));
                        minimumY = Mathf.Min(minimumY, box.transform.TransformPoint(point).y);
                    }

                    return true;
                }
                case SphereCollider sphere:
                {
                    var scale = sphere.transform.lossyScale;
                    var radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    minimumY = sphere.transform.TransformPoint(sphere.center).y - radius;
                    return true;
                }
                default:
                    return false;
            }
        }
    }
}

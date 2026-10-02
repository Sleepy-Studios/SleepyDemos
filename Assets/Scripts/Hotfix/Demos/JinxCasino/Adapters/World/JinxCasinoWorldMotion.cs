using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    // 仅供赌场场景效果使用；不修改角色输入、钱包或开奖结果。
    internal static class JinxCasinoWorldMotion
    {
        private const float Skin = 0.04f;
        private const float MaximumStep = 0.35f;

        internal static bool MoveActor(Transform target, Vector3 displacement, int collisionMask)
        {
            displacement.y = 0;
            if (target == null || displacement.sqrMagnitude < 0.000001f) return false;
            Physics.SyncTransforms();
            float distance = displacement.magnitude;
            Vector3 direction = displacement / distance;
            GetCapsule(target, target.position, out var bottom, out var top, out float radius);
            float allowed = distance;
            foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, direction, distance + Skin, collisionMask, QueryTriggerInteraction.Ignore))
                if (!BelongsTo(hit.collider, target)) allowed = Mathf.Min(allowed, Mathf.Max(0, hit.distance - Skin));
            if (allowed < 0.001f) return false;
            Vector3 destination = target.position + direction * allowed;
            if (!HasGround(target, destination, collisionMask, out float groundY)) return false;
            var controller = target.GetComponent<CharacterController>();
            if (controller != null && controller.enabled)
                controller.Move(direction * allowed);
            else target.position = new Vector3(destination.x, groundY + 0.03f, destination.z);
            return true;
        }

        internal static bool TeleportActor(Transform target, Vector3 destination, int collisionMask)
        {
            if (target == null) return false;
            Physics.SyncTransforms();
            if (!HasGround(target, destination, collisionMask, out float groundY)) return false;
            destination.y = groundY + 0.03f;
            GetCapsule(target, destination, out var bottom, out var top, out float radius);
            foreach (var collider in Physics.OverlapCapsule(bottom, top, radius, collisionMask, QueryTriggerInteraction.Ignore))
                if (!BelongsTo(collider, target)) return false;
            var character = target.GetComponent<CharacterController>();
            bool enabled = character != null && character.enabled;
            if (enabled) character.enabled = false;
            target.position = destination;
            if (enabled) character.enabled = true;
            return true;
        }

        internal static bool MoveTable(Transform table, Collider body, Vector3 destination, int collisionMask)
        {
            if (table == null || body == null) return false;
            Physics.SyncTransforms();
            Vector3 displacement = destination - table.position;
            displacement.y = 0;
            if (displacement.sqrMagnitude < 0.000001f) return true;
            float distance = displacement.magnitude;
            Vector3 direction = displacement / distance;
            float allowed = distance;
            var bounds = body.bounds;
            // 水平使用完整实际板体；缩小板体再留Skin会让宽桌侵入玩家碰撞体。
            foreach (var hit in Physics.BoxCastAll(bounds.center, bounds.extents, direction, Quaternion.identity, distance + Skin, collisionMask, QueryTriggerInteraction.Ignore))
                if (!BelongsTo(hit.collider, table))
                {
                    var character = hit.collider as CharacterController;
                    float clearance = Skin;
                    if (character != null) clearance += character.skinWidth * Mathf.Max(Mathf.Abs(character.transform.lossyScale.x), Mathf.Abs(character.transform.lossyScale.z));
                    allowed = Mathf.Min(allowed, Mathf.Max(0, hit.distance - clearance));
                }
            if (allowed < 0.001f) return false;
            Vector3 candidate = table.position + direction * allowed;
            if (!HasGround(table, candidate, collisionMask, out _)) return false;
            // PhysX对CharacterController的查询胶囊可能略小于实际bounds；以真实包围盒作最终保守占位检查。
            // 保留水平Skin间隔，不能靠查询命中的缩小胶囊允许桌板进入角色。场景当前只有少量角色。
            var occupied = bounds;
            occupied.center += direction * allowed;
            occupied.Expand(new Vector3(Skin * 2, 0, Skin * 2));
            foreach (var character in Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
                if (character.enabled && character.gameObject.activeInHierarchy && (collisionMask & (1 << character.gameObject.layer)) != 0 &&
                    !BelongsTo(character, table) && occupied.Intersects(character.bounds)) return false;
            table.position = candidate;
            return allowed >= distance - 0.001f;
        }

        private static bool HasGround(Transform target, Vector3 foot, int collisionMask, out float groundY)
        {
            groundY = foot.y;
            bool found = false;
            float difference = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(foot + Vector3.up * 1.2f, Vector3.down, 2.4f, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (BelongsTo(hit.collider, target) || hit.normal.y < 0.6f) continue;
                float candidate = Mathf.Abs(hit.point.y - foot.y);
                if (candidate > MaximumStep || candidate >= difference) continue;
                found = true; difference = candidate; groundY = hit.point.y;
            }
            return found;
        }

        private static void GetCapsule(Transform target, Vector3 foot, out Vector3 bottom, out Vector3 top, out float radius)
        {
            var controller = target.GetComponent<CharacterController>();
            float height = 1.8f;
            radius = 0.32f;
            if (controller != null)
            {
                radius = controller.radius * Mathf.Max(Mathf.Abs(target.lossyScale.x), Mathf.Abs(target.lossyScale.z));
                height = controller.height * Mathf.Abs(target.lossyScale.y);
            }
            radius = Mathf.Max(0.1f, radius);
            height = Mathf.Max(radius * 2, height);
            bottom = foot + Vector3.up * (radius + Skin);
            top = foot + Vector3.up * (height - radius + Skin);
        }

        private static bool BelongsTo(Collider collider, Transform target)
            => collider != null && (collider.transform == target || collider.transform.IsChildOf(target));
    }
}

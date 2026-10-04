using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 单人船只的浮力与驾驶，载物依靠甲板碰撞而不是跟随玩家瞬移。
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HowToFishBoat : MonoBehaviour
    {
        [SerializeField] private Transform seat;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private Vector3[] buoyancyPoints =
        {
            new Vector3(-0.6f, -0.35f, 1.1f), new Vector3(0.6f, -0.35f, 1.1f),
            new Vector3(-0.6f, -0.35f, -1.1f), new Vector3(0.6f, -0.35f, -1.1f)
        };
        [SerializeField] private float speed = 8;
        [SerializeField] private float turningSpeed = 42;
        [SerializeField] private GameObject[] motors;
        [SerializeField] private Transform[] propellers;
        [SerializeField] private GameObject radar;
        [SerializeField] private TextMeshProUGUI[] radarIslands;
        private IReadOnlyList<HowToFishIsland> islands;
        private HowToFishSaveData state;
        private int motorTier;
        private float startupRemaining;
        private Rigidbody body;
        private HowToFishInput input;
        private bool driving;

        public Transform Seat => seat;
        public Vector3 ExitPosition => exitPoint.position;
        public float Speed => new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
        public float StartupRemaining => startupRemaining;
        public int MotorTier => motorTier;
        public bool HasRadar => radar != null && radar.activeSelf;
        /// 当前船体外观；空值为默认，独立于玩家的皮肤解锁记录。
        public string SkinId => state?.boatSkinId;

        /// <summary>应用船只升级和对应的持久视觉。</summary>
        /// <param name="state">当前会话存档。</param>
        public void ApplyUpgrades(HowToFishSaveData state)
        {
            this.state = state;
            motorTier = state.boatMotorTier;
            if (motors != null)
                for (int i = 0; i < motors.Length; i++) motors[i].SetActive(i == motorTier);
            if (radar != null) radar.SetActive(state.hasBoatRadar);
            var visual = transform.Find("Visual");
            if (visual != null) visual.GetComponent<HowToFishSkinView>()?.SetSkin(SkinId);
        }

        /// <summary>绑定船载雷达使用的实际岛屿坐标。</summary>
        /// <param name="islandMarkers">当前世界的岛屿标记。</param>
        public void BindIslands(IReadOnlyList<HowToFishIsland> islandMarkers) => islands = islandMarkers;

        private void Update()
        {
            if (!HasRadar || state == null || islands == null || Time.timeScale == 0) return;
            foreach (var island in islands)
            {
                var dot = radarIslands[island.Index];
                dot.gameObject.SetActive(island.Index <= state.unlockedIsland);
                if (!dot.gameObject.activeSelf) continue;
                var local = Quaternion.Euler(0, -transform.eulerAngles.y, 0) * (island.Position - transform.position);
                dot.rectTransform.anchoredPosition = Vector2.ClampMagnitude(new Vector2(local.x, local.z) * .055f, 70);
                dot.text = (island.Index + 1).ToString();
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.centerOfMass = Vector3.down * 0.4f;
        }

        /// <summary>接管或释放船舵。</summary>
        /// <param name="controls">会话输入，离开驾驶位时传 null。</param>
        public void SetDriver(HowToFishInput controls)
        {
            input = controls;
            driving = controls != null;
            startupRemaining = driving ? motorTier == 0 ? 1.6f : .7f : 0;
        }

        private void FixedUpdate()
        {
            foreach (var local in buoyancyPoints)
            {
                var point = transform.TransformPoint(local);
                float wave = Mathf.Sin(Time.time * 1.3f + point.x * 0.15f + point.z * 0.1f) * 0.035f;
                float depth = wave - point.y;
                if (depth <= 0) continue;
                float lift = Mathf.Clamp(depth * 55 - body.GetPointVelocity(point).y * 8, 0, 35);
                body.AddForceAtPosition(Vector3.up * (lift * body.mass / buoyancyPoints.Length), point);
            }
            if (!driving) return;
            startupRemaining = Mathf.Max(0, startupRemaining - Time.fixedDeltaTime);
            if (startupRemaining > 0) return;
            var movement = input.ReadMove(0.15f);
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var lateral = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            // 源推力不是实测速率；以平方根换算并保留初始船速作为手感校准参数。
            float force = motorTier == 0 ? 800 : motorTier == 1 ? 1400 : 2000;
            body.AddForce((forward * movement.y * speed * Mathf.Sqrt(force / 800) - lateral) * 1.7f, ForceMode.Acceleration);
            body.MoveRotation(Quaternion.AngleAxis(movement.x * turningSpeed * Time.fixedDeltaTime, Vector3.up) * body.rotation);
            if (propellers != null)
                foreach (var propeller in propellers)
                    if (propeller.gameObject.activeInHierarchy) propeller.Rotate(0, 0, movement.y * 1100 * Time.fixedDeltaTime, Space.Self);
        }
    }
}

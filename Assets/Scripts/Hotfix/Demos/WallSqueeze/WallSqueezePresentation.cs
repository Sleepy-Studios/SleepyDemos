using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.WallSqueeze
{
    /// 保存的世界模板实例化与轴向碎片反馈，不参与碰撞规则。
    public sealed class WallSqueezePresentation : MonoBehaviour
    {
        [SerializeField] private Transform wallsRoot;
        [SerializeField] private Transform bodiesRoot;
        [SerializeField] private GameObject wallTemplate;
        [SerializeField] private GameObject monsterTemplate;
        [SerializeField] private GameObject armoredTemplate;
        [SerializeField] private GameObject slipperTemplate;
        [SerializeField] private GameObject residentTemplate;
        [SerializeField] private GameObject fragmentTemplate;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip crushSound;
        private readonly List<GameObject> walls = new();
        private readonly List<GameObject> bodies = new();
        private readonly List<Transform> fragments = new();
        private float fragmentClock;

        /// <summary>从保存模板重建当前关卡并清除旧演出。</summary>
        /// <param name="simulation">已复位的规则状态。</param>
        public void Build(WallSqueezeSimulation simulation)
        {
            foreach (var item in walls)
            {
                Destroy(item);
            }
            foreach (var item in bodies)
            {
                Destroy(item);
            }
            walls.Clear();
            bodies.Clear();
            ClearFragments();
            foreach (var wall in simulation.Walls)
            {
                var item = Instantiate(wallTemplate, wallsRoot);
                item.SetActive(true);
                item.transform.Find("Handle").gameObject.SetActive(!wall.IsFixed);
                item.transform.Find("ArrowHorizontal").gameObject.SetActive(!wall.IsFixed && wall.Axis == 0);
                item.transform.Find("ArrowVertical").gameObject.SetActive(!wall.IsFixed && wall.Axis == 1);
                walls.Add(item);
            }
            foreach (var body in simulation.Bodies)
            {
                var template = body.Resident ? residentTemplate : body.Type switch
                {
                    WallSqueezeMonsterType.Armored => armoredTemplate,
                    WallSqueezeMonsterType.Slipper => slipperTemplate,
                    _ => monsterTemplate
                };
                var item = Instantiate(template, bodiesRoot);
                item.SetActive(true);
                bodies.Add(item);
            }
            Render(simulation, 0, 0);
        }

        /// <summary>只同步纯色形状和冻结时钟；每次死亡批次反馈一次。</summary>
        /// <param name="simulation">当前规则状态。</param>
        /// <param name="selected">选中墙索引。</param>
        /// <param name="seconds">未暂停的演出时长。</param>
        public void Render(WallSqueezeSimulation simulation, int selected, float seconds)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                var wall = simulation.Walls[i];
                walls[i].transform.position = wall.Center;
                walls[i].transform.Find("Shape").localScale = wall.Size;
                walls[i].transform.Find("Selection").gameObject.SetActive(!wall.IsFixed && i == selected);
            }
            bool feedback = false;
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = simulation.Bodies[i];
                if (body.Dead && bodies[i].activeSelf)
                {
                    feedback = true;
                    bodies[i].SetActive(false);
                    for (int f = 0; f < 6; f++)
                    {
                        var fragment = Instantiate(fragmentTemplate, bodiesRoot).transform;
                        fragment.gameObject.SetActive(true);
                        fragment.position = body.Center + new Vector2((f % 3 - 1) * .15f, (f / 3 - .5f) * .18f);
                        fragment.GetComponent<SpriteRenderer>().color = body.Resident ? new Color(.114f, .247f, .549f) : new Color(.816f, .188f, .165f);
                        fragments.Add(fragment);
                    }
                    fragmentClock = .3f;
                }
                bodies[i].transform.position = body.Center;
                bodies[i].transform.localScale = body.Size;
            }
            if (feedback && audioSource != null && crushSound != null)
            {
                audioSource.PlayOneShot(crushSound);
            }
            if (fragmentClock > 0)
            {
                fragmentClock -= seconds;
                for (int i = 0; i < fragments.Count; i++)
                {
                    fragments[i].position += (i % 2 == 0 ? Vector3.right : Vector3.left) * seconds;
                }
                if (fragmentClock <= 0)
                {
                    ClearFragments();
                }
            }
        }

        private void ClearFragments()
        {
            foreach (var fragment in fragments)
            {
                Destroy(fragment.gameObject);
            }
            fragments.Clear();
            fragmentClock = 0;
        }
    }
}

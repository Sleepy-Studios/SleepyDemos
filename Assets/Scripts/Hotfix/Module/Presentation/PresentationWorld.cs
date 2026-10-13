using System;
using System.Runtime.ExceptionServices;
using Core.Runtime;
using Core.Runtime.TimeControls;
using UnityEngine;

namespace Hotfix
{
    /// 公共效果体验用制作世界，不持有 Demo 玩法或全局输入。
    public sealed class PresentationWorld : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private AudioListener listener;
        [SerializeField] private Transform movingCube;
        [SerializeField] private Transform movingSphere;
        private float elapsed;
        private Vector3 cubePosition;
        private Vector3 spherePosition;

        /// 制作相机保持全屏，由公共 Root 接管 UI 栈与显示服务。
        public Camera WorldCamera => worldCamera;
        public AudioListener Listener => listener;

        /// 缺少制作引用时由加载事务报错，不能在首帧运动中产生循环异常。
        public void Validate()
        {
            if (worldCamera == null || listener == null || movingCube == null || movingSphere == null)
            {
                throw new InvalidOperationException("公共体验世界缺少制作相机、监听器或运动目标。");
            }
        }

        private void Awake()
        {
            cubePosition = movingCube.localPosition;
            spherePosition = movingSphere.localPosition;
        }

        private void Update()
        {
            elapsed += GameTimeService.DeltaTime;
            movingCube.localRotation = Quaternion.Euler(0, elapsed * 35, 0);
            movingCube.localPosition = cubePosition + Vector3.up * (Mathf.Sin(elapsed * 1.4f) * 0.15f);
            movingSphere.localPosition = spherePosition + Vector3.right * (Mathf.Sin(elapsed) * 0.6f);
        }

        /// 从固定构图重新开始运动，供遮幕中点与恢复初始调用。
        public void ResetMotion()
        {
            elapsed = 0;
            movingCube.localRotation = Quaternion.identity;
            movingCube.localPosition = cubePosition;
            movingSphere.localPosition = spherePosition;
        }
    }

    /// 体验页拥有的资源与相机租约；失败、关闭和销毁都可以重复归还。
    public sealed class PresentationWorldLease : IDisposable
    {
        private IResourceLoader loader;
        private GameObject instance;
        private Camera previousCamera;
        private AudioListener previousListener;
        private bool previousCameraEnabled;
        private bool previousListenerEnabled;
        private bool active;

        public PresentationWorld World { get; }

        /// <summary>接收本次资源加载所有权，不能传入其它页面或场景持有的实例。</summary>
        /// <param name="loader">只加载本次体验世界的 Loader，Dispose 时归还。</param>
        /// <param name="instance">制作的 PresentationWorld Prefab 实例。</param>
        public PresentationWorldLease(IResourceLoader loader, GameObject instance)
        {
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
            World = instance.GetComponent<PresentationWorld>();
            if (World == null || World.WorldCamera == null || World.Listener == null)
            {
                throw new InvalidOperationException("公共体验世界缺少制作 Camera、AudioListener 或 World 引用。");
            }
            World.Validate();
            instance.SetActive(false);
        }

        /// 进入体验世界；相机绑定失败会立即恢复原 Hub 状态。
        public void Activate()
        {
            if (instance == null)
            {
                throw new ObjectDisposedException(nameof(PresentationWorldLease));
            }
            if (active)
            {
                return;
            }
            previousCamera = UIRootManager.Instance.BaseCamera;
            if (previousCamera == null)
            {
                throw new InvalidOperationException("公共体验需要已接入 Hub 的原相机绑点。");
            }
            previousListener = previousCamera != null ? previousCamera.GetComponent<AudioListener>() : null;
            previousCameraEnabled = previousCamera != null && previousCamera.enabled;
            previousListenerEnabled = previousListener != null && previousListener.enabled;
            active = true;
            try
            {
                instance.SetActive(true);
                World.WorldCamera.enabled = true;
                World.Listener.enabled = true;
                if (previousCamera != null)
                {
                    previousCamera.enabled = false;
                }
                if (previousListener != null)
                {
                    previousListener.enabled = false;
                }
                UIRootManager.Instance.BindToBaseCamera(World.WorldCamera);
            }
            catch
            {
                Deactivate();
                throw;
            }
        }

        /// 恢复仍然属于本租约的 Hub 绑点；导航已更换相机时不抢回新场景。
        public void Deactivate()
        {
            if (!active)
            {
                return;
            }
            active = false;
            bool restoreHub = UIRootManager.Instance.BaseCamera == World.WorldCamera
                || UIRootManager.Instance.BaseCamera == previousCamera;
            try
            {
                if (restoreHub && previousCamera != null)
                {
                    UIRootManager.Instance.BindToBaseCamera(previousCamera);
                }
            }
            finally
            {
                if (restoreHub && previousCamera != null)
                {
                    previousCamera.enabled = previousCameraEnabled;
                }
                if (restoreHub && previousListener != null)
                {
                    previousListener.enabled = previousListenerEnabled;
                }
                if (instance != null)
                {
                    World.WorldCamera.enabled = false;
                    World.Listener.enabled = false;
                    instance.SetActive(false);
                }
            }
        }

        /// 归还本次世界资源；不销毁或改写原 Hub 对象。
        public void Dispose()
        {
            if (loader == null)
            {
                return;
            }
            var released = loader;
            var ownedInstance = instance;
            loader = null;
            Exception failure = null;
            try { Deactivate(); }
            catch (Exception exception) { failure = exception; }
            instance = null;
            try
            {
                if (ownedInstance != null) released.ReleaseInstance(ownedInstance);
            }
            catch (Exception exception)
            {
                if (failure == null) failure = exception;
                else Debug.LogException(exception);
            }
            try { released.Dispose(); }
            catch (Exception exception)
            {
                if (failure == null) failure = exception;
                else Debug.LogException(exception);
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

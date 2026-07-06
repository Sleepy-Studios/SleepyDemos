using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 基础 UI 图片加载器。按资源路径加载 Sprite，并写入目标 Image。
    public sealed class UIImageLoader : MonoBehaviour
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private bool setInactiveWhenEmpty = true;

        private IResourceLoader loader;
        private Sprite currentSprite;
        private int imageRequestSequence;

        /// 实际承载 Sprite 的 Image；未显式配置时会从子节点查找。
        public Image TargetImage => targetImage;

        /// <summary>
        /// 按资源路径设置图片。
        /// </summary>
        /// <param name="key">Sprite 资源路径；为空时清空图片。</param>
        /// <param name="setNativeSize">加载成功后是否调用 <see cref="Image.SetNativeSize"/>。</param>
        /// <param name="isAsync">是否使用异步加载；false 时走同步加载。</param>
        public void SetImage(string key, bool setNativeSize = true, bool isAsync = false)
        {
            if (isAsync)
            {
                SetImageAsync(key, setNativeSize).Forget();
                return;
            }

            var image = ResolveImage();
            if (image == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                Clear();
                return;
            }

            var requestId = ++imageRequestSequence;
            var sprite = Loader.LoadAsset<Sprite>(key);
            if (requestId != imageRequestSequence)
            {
                if (sprite != null)
                {
                    Loader.ReleaseAsset(sprite);
                }

                return;
            }

            ApplySprite(image, sprite, key, setNativeSize);
        }

        private async Cysharp.Threading.Tasks.UniTaskVoid SetImageAsync(string key, bool setNativeSize)
        {
            var image = ResolveImage();
            if (image == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                Clear();
                return;
            }

            var requestId = ++imageRequestSequence;
            var sprite = await Loader.LoadAssetAsync<Sprite>(key);
            if (this == null || image == null)
            {
                if (sprite != null)
                {
                    Loader.ReleaseAsset(sprite);
                }

                return;
            }

            if (requestId != imageRequestSequence)
            {
                if (sprite != null)
                {
                    Loader.ReleaseAsset(sprite);
                }

                return;
            }

            ApplySprite(image, sprite, key, setNativeSize);
        }

        /// 清空当前图片，并按配置决定是否隐藏目标 Image 对象。
        public void Clear()
        {
            imageRequestSequence++;
            ApplyFallback(ResolveImage());
        }

        private IResourceLoader Loader => loader ??= ResourceServices.CreateLoader();

        private Image ResolveImage()
        {
            if (targetImage == null)
            {
                targetImage = GetComponentInChildren<Image>(true);
            }

            return targetImage;
        }

        private void ApplySprite(Image image, Sprite sprite, string key, bool setNativeSize)
        {
            if (sprite == null)
            {
                Debug.LogWarning($"[UIImageLoader] 加载图片失败: {key}");
                ApplyFallback(image);
                return;
            }

            ReleaseCurrentSprite();
            currentSprite = sprite;
            image.sprite = sprite;
            image.rectTransform.localScale = Vector3.one;
            image.gameObject.SetActive(true);
            if (setNativeSize)
            {
                image.SetNativeSize();
            }
        }

        private void ApplyFallback(Image image)
        {
            if (image == null)
            {
                return;
            }

            ReleaseCurrentSprite();
            image.sprite = null;
            image.rectTransform.localScale = Vector3.one;
            image.gameObject.SetActive(!setInactiveWhenEmpty);
        }

        private void ReleaseCurrentSprite()
        {
            if (currentSprite != null && loader != null)
            {
                loader.ReleaseAsset(currentSprite);
                currentSprite = null;
            }
        }

        private void OnDestroy()
        {
            ReleaseCurrentSprite();
            loader?.Dispose();
            loader = null;
        }
    }
}

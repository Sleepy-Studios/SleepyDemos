using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class ItemImageLoader : MonoBehaviour
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private bool setInactiveWhenEmpty = true;

        private int imageRequestSequence;

        public Image TargetImage => targetImage;

        public void SetImage(string spritePath, float scale = 1f)
        {
            SetImageAsync(spritePath, scale);
        }

        public void SetImage(Sprite sprite)
        {
            var image = ResolveImage();
            if (image == null)
            {
                return;
            }

            if (sprite == null)
            {
                Clear();
                return;
            }

            image.sprite = sprite;
            image.rectTransform.localScale = Vector3.one;
            image.gameObject.SetActive(true);
        }

        public async void SetImageAsync(string spritePath, float scale = 1f)
        {
            var image = ResolveImage();
            if (image == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(spritePath))
            {
                Clear();
                return;
            }

            var requestId = ++imageRequestSequence;
            var result = await ResourceServices.Default.LoadAssetAsync<Sprite>(spritePath);
            if (!result.Success || this == null || image == null)
            {
                ApplyFallback(image);
                return;
            }

            if (this == null || image == null || requestId != imageRequestSequence)
            {
                return;
            }

            image.sprite = result.Asset;
            image.rectTransform.localScale = Vector3.one * Mathf.Max(scale, 0.0001f);
            image.gameObject.SetActive(true);
        }

        public void Clear()
        {
            ApplyFallback(ResolveImage());
        }

        private Image ResolveImage()
        {
            if (targetImage == null)
            {
                targetImage = GetComponentInChildren<Image>(true);
            }

            return targetImage;
        }

        private void ApplyFallback(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.rectTransform.localScale = Vector3.one;
            image.gameObject.SetActive(!setInactiveWhenEmpty);
        }
    }
}

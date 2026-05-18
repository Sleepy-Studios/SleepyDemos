using UnityEngine;

namespace Core.Runtime
{
    public readonly struct ResourceLoadResult<T> where T : Object
    {
        public ResourceLoadResult(T asset, string address, string error)
        {
            Asset = asset;
            Address = address;
            Error = error ?? string.Empty;
        }

        public T Asset { get; }
        public string Address { get; }
        public string Error { get; }
        public bool Success => Asset != null && string.IsNullOrEmpty(Error);

        public static ResourceLoadResult<T> SuccessResult(T asset, string address)
        {
            return new ResourceLoadResult<T>(asset, address, string.Empty);
        }

        public static ResourceLoadResult<T> Failure(string address, string error)
        {
            return new ResourceLoadResult<T>(null, address, error);
        }
    }
}

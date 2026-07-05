using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.Runtime
{
    internal interface ILoopScrollItemSource
    {
        LoopScrollItemRecord GetItem(int index, Transform parent);
        bool CanReuse(LoopScrollItemRecord record, int index);
        void ProvideData(LoopScrollItemRecord record, int index);
        void ReturnItem(LoopScrollItemRecord record, Transform poolRoot);
        bool ContainsVisible(IReadOnlyList<LoopScrollItemRecord> records, int index);
        void Destroy();
    }

    public sealed class LoopScrollItemRecord
    {
        public ItemView View;
        public RectTransform RectTransform;
        public Button Button;
        public UnityAction ButtonAction;
        public int ItemTypeId;
    }

    internal sealed class LoopScrollItemSource<TView> : ILoopScrollItemSource where TView : ItemView, new()
    {
        private readonly Stack<LoopScrollItemRecord> pooledRecords = new Stack<LoopScrollItemRecord>();
        private readonly Dictionary<RectTransform, LoopScrollItemRecord> allRecords = new Dictionary<RectTransform, LoopScrollItemRecord>();
        private readonly GameObject prefab;
        private readonly Func<int, GameObject> objectFactory;
        private Action<TView, int> onProvide;
        private Action<TView, int> onReturn;
        private Action<TView, int> onClick;

        public LoopScrollItemSource(GameObject prefab = null, Func<int, GameObject> objectFactory = null)
        {
            this.prefab = prefab;
            this.objectFactory = objectFactory;
        }

        public void SetProvide(Action<TView, int> callback)
        {
            onProvide = callback;
        }

        public void SetReturn(Action<TView, int> callback)
        {
            onReturn = callback;
        }

        public void SetClick(Action<TView, int> callback)
        {
            onClick += callback;
        }

        public LoopScrollItemRecord GetItem(int index, Transform parent)
        {
            LoopScrollItemRecord record;
            if (pooledRecords.Count > 0)
            {
                record = pooledRecords.Pop();
            }
            else
            {
                record = CreateRecord(index);
                allRecords[record.RectTransform] = record;
            }

            record.RectTransform.SetParent(parent, false);
            record.RectTransform.localScale = Vector3.one;
            record.RectTransform.gameObject.SetActive(true);
            BindClick(record, index);
            return record;
        }

        public bool CanReuse(LoopScrollItemRecord record, int index)
        {
            return record?.View is TView;
        }

        public void ProvideData(LoopScrollItemRecord record, int index)
        {
            if (record?.View is not TView view)
            {
                return;
            }

            view.SetIndex(index);
            BindClick(record, index);
            onProvide?.Invoke(view, index);
        }

        public void ReturnItem(LoopScrollItemRecord record, Transform poolRoot)
        {
            if (record?.RectTransform == null)
            {
                return;
            }

            if (record.View is TView view)
            {
                onReturn?.Invoke(view, view.Index);
                view.SetIndex(-1);
            }

            record.RectTransform.SetParent(poolRoot, false);
            record.RectTransform.gameObject.SetActive(false);
            pooledRecords.Push(record);
        }

        public bool ContainsVisible(IReadOnlyList<LoopScrollItemRecord> records, int index)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].View.Index == index)
                {
                    return true;
                }
            }

            return false;
        }

        public void Destroy()
        {
            foreach (var pair in allRecords)
            {
                if (pair.Value?.RectTransform != null)
                {
                    UnityEngine.Object.Destroy(pair.Value.RectTransform.gameObject);
                }
            }

            allRecords.Clear();
            pooledRecords.Clear();
        }

        private LoopScrollItemRecord CreateRecord(int index)
        {
            var itemObject = objectFactory?.Invoke(index);
            if (itemObject == null && prefab != null)
            {
                itemObject = UnityEngine.Object.Instantiate(prefab);
            }

            if (itemObject == null)
            {
                itemObject = new GameObject(typeof(TView).Name, typeof(RectTransform));
            }

            if (itemObject.GetComponent<RectTransform>() == null)
            {
                itemObject.AddComponent<RectTransform>();
            }

            var view = new TView();
            view.Init(itemObject, index);
            return new LoopScrollItemRecord
            {
                View = view,
                RectTransform = itemObject.GetComponent<RectTransform>(),
                Button = itemObject.GetComponentInChildren<Button>(true),
                ItemTypeId = 0
            };
        }

        private void BindClick(LoopScrollItemRecord record, int index)
        {
            if (record.Button == null)
            {
                return;
            }

            if (record.ButtonAction != null)
            {
                record.Button.onClick.RemoveListener(record.ButtonAction);
            }

            record.ButtonAction = () =>
            {
                record.View.TriggerClick();
                onClick?.Invoke((TView)record.View, index);
            };
            record.Button.onClick.AddListener(record.ButtonAction);
        }
    }

    internal sealed class LoopScrollMultiItemSource : ILoopScrollItemSource
    {
        private readonly Dictionary<int, Stack<LoopScrollItemRecord>> pooledRecords = new Dictionary<int, Stack<LoopScrollItemRecord>>();
        private readonly Dictionary<RectTransform, LoopScrollItemRecord> allRecords = new Dictionary<RectTransform, LoopScrollItemRecord>();
        private IList<int> itemTypeList;
        private IDictionary<int, Type> itemTypeToViewType;
        private IDictionary<int, GameObject> itemTypeToPrefab;
        private Action<ItemView, int> onProvide;
        private Action<ItemView, int> onReturn;
        private Action<ItemView, int> onClick;

        public LoopScrollMultiItemSource(
            IList<int> itemTypeList,
            IDictionary<int, Type> itemTypeToViewType,
            IDictionary<int, GameObject> itemTypeToPrefab = null)
        {
            SetMultiListData(itemTypeList, itemTypeToViewType, itemTypeToPrefab);
        }

        public void SetMultiListData(
            IList<int> nextItemTypeList,
            IDictionary<int, Type> nextItemTypeToViewType,
            IDictionary<int, GameObject> nextItemTypeToPrefab = null)
        {
            itemTypeList = nextItemTypeList ?? Array.Empty<int>();
            itemTypeToViewType = nextItemTypeToViewType ?? new Dictionary<int, Type>();
            itemTypeToPrefab = nextItemTypeToPrefab;
        }

        public void SetProvide(Action<ItemView, int> callback)
        {
            onProvide = callback;
        }

        public void SetReturn(Action<ItemView, int> callback)
        {
            onReturn = callback;
        }

        public void SetClick(Action<ItemView, int> callback)
        {
            onClick = callback;
        }

        public LoopScrollItemRecord GetItem(int index, Transform parent)
        {
            var typeId = GetTypeId(index);
            if (!pooledRecords.TryGetValue(typeId, out var pool))
            {
                pool = new Stack<LoopScrollItemRecord>();
                pooledRecords[typeId] = pool;
            }

            LoopScrollItemRecord record;
            if (pool.Count > 0)
            {
                record = pool.Pop();
            }
            else
            {
                record = CreateRecord(typeId, index);
                allRecords[record.RectTransform] = record;
            }

            record.ItemTypeId = typeId;
            record.RectTransform.SetParent(parent, false);
            record.RectTransform.localScale = Vector3.one;
            record.RectTransform.gameObject.SetActive(true);
            BindClick(record, index);
            return record;
        }

        public bool CanReuse(LoopScrollItemRecord record, int index)
        {
            return record != null && record.ItemTypeId == GetTypeId(index);
        }

        public void ProvideData(LoopScrollItemRecord record, int index)
        {
            if (record == null)
            {
                return;
            }

            var expectedTypeId = GetTypeId(index);
            if (record.ItemTypeId != expectedTypeId)
            {
                Debug.LogError($"[LoopScroll] 多类型列表复用类型不匹配：record={record.ItemTypeId}, expected={expectedTypeId}");
                return;
            }

            record.View.SetIndex(index);
            BindClick(record, index);
            onProvide?.Invoke(record.View, index);
        }

        public void ReturnItem(LoopScrollItemRecord record, Transform poolRoot)
        {
            if (record?.RectTransform == null)
            {
                return;
            }

            onReturn?.Invoke(record.View, record.View.Index);
            record.View.SetIndex(-1);
            record.RectTransform.SetParent(poolRoot, false);
            record.RectTransform.gameObject.SetActive(false);

            if (!pooledRecords.TryGetValue(record.ItemTypeId, out var pool))
            {
                pool = new Stack<LoopScrollItemRecord>();
                pooledRecords[record.ItemTypeId] = pool;
            }

            pool.Push(record);
        }

        public bool ContainsVisible(IReadOnlyList<LoopScrollItemRecord> records, int index)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].View.Index == index)
                {
                    return true;
                }
            }

            return false;
        }

        public void Destroy()
        {
            foreach (var pair in allRecords)
            {
                if (pair.Value?.RectTransform != null)
                {
                    UnityEngine.Object.Destroy(pair.Value.RectTransform.gameObject);
                }
            }

            allRecords.Clear();
            pooledRecords.Clear();
        }

        private LoopScrollItemRecord CreateRecord(int typeId, int index)
        {
            var itemObject = CreateGameObject(typeId);
            var viewType = GetViewType(typeId);
            var view = (ItemView)Activator.CreateInstance(viewType);
            view.Init(itemObject, index);

            return new LoopScrollItemRecord
            {
                View = view,
                RectTransform = itemObject.GetComponent<RectTransform>(),
                Button = itemObject.GetComponentInChildren<Button>(true),
                ItemTypeId = typeId
            };
        }

        private GameObject CreateGameObject(int typeId)
        {
            GameObject itemObject = null;
            if (itemTypeToPrefab != null &&
                itemTypeToPrefab.TryGetValue(typeId, out var prefab) &&
                prefab != null)
            {
                itemObject = UnityEngine.Object.Instantiate(prefab);
            }

            if (itemObject == null)
            {
                itemObject = new GameObject(GetViewType(typeId).Name, typeof(RectTransform));
            }

            if (itemObject.GetComponent<RectTransform>() == null)
            {
                itemObject.AddComponent<RectTransform>();
            }

            return itemObject;
        }

        private int GetTypeId(int index)
        {
            if (itemTypeList.Count == 0)
            {
                return 0;
            }

            var safeIndex = ((index % itemTypeList.Count) + itemTypeList.Count) % itemTypeList.Count;
            return itemTypeList[safeIndex];
        }

        private Type GetViewType(int typeId)
        {
            if (itemTypeToViewType.TryGetValue(typeId, out var viewType) &&
                typeof(ItemView).IsAssignableFrom(viewType))
            {
                return viewType;
            }

            throw new InvalidOperationException($"LoopScroll 多类型列表缺少合法 ItemView 类型映射：typeId={typeId}");
        }

        private void BindClick(LoopScrollItemRecord record, int index)
        {
            if (record.Button == null)
            {
                return;
            }

            if (record.ButtonAction != null)
            {
                record.Button.onClick.RemoveListener(record.ButtonAction);
            }

            record.ButtonAction = () =>
            {
                record.View.TriggerClick();
                onClick?.Invoke(record.View, index);
            };
            record.Button.onClick.AddListener(record.ButtonAction);
        }
    }
}

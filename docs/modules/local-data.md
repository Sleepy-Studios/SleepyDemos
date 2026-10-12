# 公共本地存储

`Core.Runtime.LocalDataManager` 统一本机偏好与 JSON 文件读写，不持有业务对象缓存或场景资源。Hotfix 的键和文件名集中在 `LocalDataKeys`，业务 DTO、默认值、快照恢复及领域校验留在各 Demo。

## 调用入口

- `LoadData<T>(key, defaultValue, validate)`：读取独立对象；读取、反序列化或业务校验失败统一返回默认值，不区分失败原因，也不自动写入默认记录。
- `SaveData<T>(key, value, validate)`：先校验和序列化，再立即 `PlayerPrefs.Save`；失败恢复该键的内存原值并抛出。没有 Flush 或延迟保存步骤。
- `LoadFile(relativePath, directory)`：默认根目录是 persistentDataPath，测试可注入独立目录；返回文件文本，读取失败或空内容返回 null。业务只在这里得到的文本上解析并校验一次。
- `SaveFile(relativePath, json, directory)`：业务先校验和序列化完整候选；公共层写完临时文件再替换目标。保存失败抛出，由业务显示一次提示，成功才提交内存中的保存状态。
- `GetFileWriteTimeUtc`：供槽摘要展示保存时间。

文件路径只能位于指定根目录内。JSON 最大深度 64，不反序列化外部提供的类型名；不人为限制本机偏好或存档体积。

纸艺卡丁车的计时记录使用 persistentDataPath/KagitKart/Records，领域入口见 [纸艺卡丁车模块](../demos/kagit_kart/module.md)。只维护当前数据结构，读取失败或完整记录校验失败都视为没有数据，不逐字段修补损坏记录。不创建备份、不搜索旧目录、不迁移旧版本。

读取后的业务对象由调用方持有；预览使用独立副本，明确保存成功才提交保存状态。ClearData 只清本场内存状态，不删除本机记录。

## 验证

公共读写使用 `Tests.Module.LocalDataManagerTests`，领域使用对应业务的存档和偏好测试。重点检查正常读写、存储隔离、读取失败回默认、保存失败不提交新状态及目录越界拒绝。

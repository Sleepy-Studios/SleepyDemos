# 公共本地存储

`Core.Runtime.LocalDataManager` 统一本机偏好与 JSON 文件读写，不持有业务对象缓存或场景资源。Hotfix 的键和文件名集中在 `LocalDataKeys`，业务 DTO、默认值、快照恢复及领域校验留在各 Demo。

## 调用入口

- `LoadData<T>(key, defaultValue, validate)`：读取独立对象；缺失、损坏或领域校验失败返回默认值。需要提示时使用带 `out warning` 的重载。
- `SaveData<T>(key, value, validate)`：先校验和序列化，再立即 `PlayerPrefs.Save`；失败恢复该键的内存原值并抛出。没有 Flush 或延迟保存步骤。
- `LoadFile(relativePath, directory)`：默认根目录是 persistentDataPath，测试可注入独立目录；无有效 JSON 返回 null，实际 IO 与权限失败抛出。
- `SaveFile(relativePath, json, directory)`：领域先校验候选；公共层限制大小、校验 JSON、写入并 flush 临时文件，再原子替换主档。失败保留主档并清理本次临时文件。
- `GetFileWriteTimeUtc`：供槽摘要展示保存时间。

文件路径只能位于指定根目录内。偏好限制 128 KiB 字符，文件限制 16 MiB，JSON 最大深度 64；不反序列化外部提供的类型名。

钓鱼使用 persistentDataPath/HowToFish，赌场旅程使用 persistentDataPath/JinxCasino，永久档案使用其 Profile 子目录。只维护当前数据结构；损坏进度按空槽、损坏永久档案按新用户处理，读取不自动覆盖记录。不创建或读取备份，不搜索旧目录，不维护版本迁移。

读取后的业务对象由调用方持有；预览使用独立副本，明确保存成功才提交保存状态。ClearData 只清本场内存状态，不删除本机记录。

## 验证

公共读写使用 `Tests.Module.LocalDataManagerTests`，领域使用两个 Demo 的存档和偏好测试。重点检查三槽与跨 Demo 隔离、候选合法性、损坏回默认、原子写入失败保护及目录越界拒绝。

2026-10-09：公共存储 6/6、两个 Demo 的偏好/存档/永久档案及画面设置直接相关用例通过。Unity Editor 编译无错误；UI 导航 51/51、生命周期 23/23、过渡 9/9、跨场景过渡 13/13 通过。未执行全量测试、真机与 IL2CPP 构建。

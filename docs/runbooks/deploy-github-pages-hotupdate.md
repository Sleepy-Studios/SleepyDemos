# 用 GitHub Pages 发布热更资源

## 当前环境与目录约定

公开仓库 [H-Bole/H-Bole.io](https://github.com/H-Bole/H-Bole.io) 已开启 Pages，发布源为 `main` 根目录。Android 已完成首次发布与文件下载校验；PC 仍只有 `.gitkeep`。

| 窗口配置 | 值 |
|---|---|
| Pages 本地仓库路径 | `D:/Unity/Unity_Project/H-Bole.io` |
| 网站根 URL | `https://h-bole.github.io/H-Bole.io` |
| Unity 项目目录名 | `SleepyDemos` |
| PC 资源 URL | `https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/hotupdate/PC` |
| Android 资源 URL | `https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/hotupdate/Android` |

固定目录约定为 `unity/<项目>/hotupdate/<平台>/`：Unity 的 `StandaloneWindows64` 使用 `PC`，`Android` 使用 `Android`，其他平台使用 `BuildTarget` 名称。当前目录为：

```text
unity/SleepyDemos/hotupdate/
├── PC/.gitkeep
└── Android/（.gitkeep 与已发布资源）
```

空目录用 `.gitkeep` 保留，不代表其中已有资源。其他 Unity 项目并列放在 `unity/` 下；站点根目录可容纳其他用途，但不为未知用途提前建目录。网站根目录保留 `.nojekyll` 和 `index.html`。

2026-10-14 首次 Android 发布测试使用 `E:/Project/SleepyDemos-Andriod_Bulid/Bundles/Android/DefaultPackage/2026-10-14-86`，资源版本为 `2026-10-14-86`，清单 hash 为 `2a7e1ce5`；包含 126 个 Bundle 与 3 个版本 / 清单 / hash 文件，共 129 个下载文件、118,122,677 字节。站点提交为 [8a1cd59](https://github.com/H-Bole/H-Bole.io/commit/8a1cd59f44632209e1bce26946a4c67208d3fe1f)，此前的目录骨架一并发布，[Pages 部署工作流](https://github.com/H-Bole/H-Bole.io/actions/runs/38440318335)成功。从表中的 Android URL 下载全部 129 个文件，SHA256 全部与源文件一致；用当前部署 helper 再次上传同包，HEAD 不变、未产生新提交。此次未清空任何目录。

首次发布核对时，Android 源工作树为 `HostPlayMode`，但 `BaseServerURL` / `RemoteBaseServerURL` 为空。随后已修正源工作树的 Pages 设置：网站根为 `https://h-bole.github.io/H-Bole.io`，发布项目名为 `SleepyDemos`，两个运行时 URL 均为 `https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/hotupdate/Android`；保留 `HostPlayMode` 与已有 `2026-10-14-86` 构建。APK 未重建、设备实际热更未验证，也尚未比较源配置与已安装 APK 内配置；不能由源配置修正或上传成功断言已安装 APK 已连接 Pages。

## 窗口发布步骤

**点击上传或确认清空会执行 Git 提交、推送流程，将内容发布到公开仓库**；相同包无内容变化时不会新建提交。

1. 安装 `git`、`gh` 并完成 GitHub CLI 登录。确认发布仓库为 `main`、工作区干净且远端没有领先或分歧；工具不自动合并。若存在发布范围外的未推送文件，工具会拒绝。仓库 `Settings > Pages` 核对 `Deploy from a branch`、`main`、`/(root)`，见 [GitHub 官方发布源说明](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site)。
2. 打开 `Tools/UI Framework/Hotfix Build`，选择 `GitHub Pages` 上传方式，填写表中的仓库、网站根 URL 和项目目录名，关闭本地模拟。网站根 URL 不包含 `unity/<项目>/hotupdate/<平台>`；项目目录名使用发布逻辑名 `SleepyDemos`，而非工作树文件夹名 `SleepyDemos-Andriod_Bulid`，避免自动拼接出重复路径或错误项目目录。窗口按 Unity 当前构建平台显示目标目录，并自动生成 `RemoteBaseServerURL`；关闭本地模拟时 `BaseServerURL` 使用同一地址。Pages 参数由 Core.Editor 通过 `EditorPrefs` 按 Unity 项目和配置保存，不是运行时部署配置。切回 `SSH` 时保留其手工远端 URL 与既有连接设置。
3. 用最新源码与资源生成当前平台构建，核对窗口的当前 Bundle 路径，如 PC 的 `Bundles/StandaloneWindows64/DefaultPackage/<版本号>` 或 Android 的 `Bundles/Android/DefaultPackage/<版本号>`，再执行 Pages 上传。工具读取版本文件，只复制本包的 `.version`、版本对应 `.bytes` / `.hash` 及全部 `.bundle`，只更新内容有变化的文件，保留旧包；不复制源码、SSH 私钥、`.report`、日志和 `link.xml`。Git 操作通过异步进程执行；窗口会防止重复部署，Unity 脚本重载会延后到传输结束。等待窗口完成结果，再到仓库 `Actions` 查看 Pages 部署。

日常 `PlayMode` 仍保持 `EditorSimulateMode`；它不会证明远端下载可用。发布成功后如需实际远端验收，临时切换 `HostPlayMode`，从正式 `Assets/Scenes/AppEntrance.unity` 启动，结束或失败后恢复原模式；边界见 [热更新模块](../modules/hotfix.md#验证重点)。

## 清空当前平台目录

需要删除已发布的当前平台资源时，使用独立的“清空当前平台目录”按钮，核对确认框中的完整路径后执行。该操作保留 `.gitkeep`，删除当前项目、当前平台目录中的其他文件，并提交、推送；可能使该平台客户端无法下载资源。

只允许操作 `unity/<项目>/hotupdate/<当前平台>/`，不清理其他项目、平台或网站内容。目标目录含子目录或链接时拒绝操作；先人工检查，不能扩大删除范围或绕过校验。清空不自动重新构建或上传新包。

## 失败时核查

先查看窗口报错，再在发布仓库执行只读核查；推送失败不等于本地修改或提交已回退，不要直接重复点击。

```powershell
$pagesRepo = 'D:/Unity/Unity_Project/H-Bole.io'
gh auth status
git -C $pagesRepo status --short --branch
git -C $pagesRepo log -3 --oneline
git -C $pagesRepo ls-remote origin refs/heads/main
```

- 登录、工具缺失、工作区不干净或远端已有未同步提交：解决对应问题后再操作；工具不自动合并、重置或覆盖其他工作。
- 推送失败：核对本地 `HEAD` 与远端 `main`，确定哪些提交仍未发布。部署使用命令级 `credential.helper=!gh auth git-credential`，HTTP 参数同样仅作用于本次命令，避免本机旧凭据 helper 或过大上传缓冲影响；不修改全局 Git 配置。
- 推送成功但资源仍旧：检查 Pages 工作流与缓存。Pages 部署成功前，不将 Git 推送成功当作资源已经更新。

## 下载验证

以下验证仅在发布最新资源且 Pages 部署成功后执行；空目录骨架没有版本、清单或 Bundle 可下载。

- 从对应平台 URL 下载 `DefaultPackage.version`，与本轮本地版本一致；再下载 `DefaultPackage_<版本>.hash`、`DefaultPackage_<版本>.bytes`，hash 文本必须与本地最新构建一致。
- 下载 `.bytes` 与至少一个 `.bundle`，用 SHA256 与本地原文件比较。只检查 HTTP 200 不够，HTML 错误页也可能被保存成文件。下面以 PC 清单为例，先替换 `<版本号>`，Bundle 验证更换文件名即可。

  ```powershell
  $bundleSource = 'D:/Unity/Unity_Project/SleepyDemos/Bundles/StandaloneWindows64/DefaultPackage/<版本号>'
  $packageVersion = (Get-Content -LiteralPath "$bundleSource/DefaultPackage.version" -Raw).Trim()
  $remoteRoot = 'https://h-bole.github.io/H-Bole.io/unity/SleepyDemos/hotupdate/PC'
  $verifyFile = "DefaultPackage_$packageVersion.bytes"
  $temporaryDownload = [System.IO.Path]::GetTempFileName()
  try {
      Invoke-WebRequest -Uri "$remoteRoot/$verifyFile" -OutFile $temporaryDownload
      (Get-FileHash -LiteralPath "$bundleSource/$verifyFile" -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $temporaryDownload -Algorithm SHA256).Hash
  }
  finally {
      Remove-Item -LiteralPath $temporaryDownload
  }
  ```

比较结果必须为 `True`。文件不存在先查大小写、`unity/<项目>/hotupdate/<平台>` 层级和 Pages 部署状态。记录下载验证与 HostPlayMode 实际运行结果，不将前者替代后者；Editor 会复用已加载 Hotfix 程序集，不能由此证明远端 DLL 独立加载或 IL2CPP 正常。

## 容量边界

发布前核对文件大小与累计容量：Pages 发布站点上限为 **1 GB**，带宽 **100 GB/月**是软限制，见 [Pages 限制](https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits)。普通 Git 会阻止大于 **100 MiB** 的单文件，浏览器上传最多 **25 MiB**，见 [大文件限制](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github)。默认保留旧包会持续占用容量；容量接近限制或需要持续大量分发时改用适合的资源托管服务。

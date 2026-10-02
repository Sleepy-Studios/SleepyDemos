# 倒霉蛋俱乐部原创资源源文件

这些文件属于本Demo的源资源，不进入运行时资源采集。角色、机台、道具、面具、帽子和区域装饰由本目录的Blender脚本重新制作；音频由标准库脚本合成，没有采样参考游戏的图像、模型或声音。

## 可复现入口

- `build_models.py`：在独立空Blender场景生成模型、语义Pivot、统一调色板、源`.blend`、FBX、透明机台/道具图标及资源manifest。需要Blender 5.2.1 LTS，具体参数及输出以脚本入口为准。先输出到`Library/JinxCasino/Staging`，通过源检查及Unity门禁再导入Demo资源。
- `build_axis_gate.py`：独立Front/Right/Up源候选，用真实Unity ModelImporter验证坐标合同。
- `build_audio.py`：生成14段24kHz、单声道、16位原创合成WAV及校验manifest。
- `build_endings.py`：在独立场景制作三幅1280×720原创结局演出，保留`jinx_casino_endings.blend`，用Cycles CPU渲染，不烘焙中文文案。

源前方-Y、上+Z、右-X；Unity前方+Z、上+Y、右+X。不得用运行时旋转、负缩放或扩大物理体来掩盖导出问题。具体合同见`docs/demos/jinx_casino/architecture/model-contract.md`。

多层Empty与静态网格导出在本脚本的独立进程中按坐标矩阵共轭转换，防止FBX空间烘焙重复施加轴变换；不修改Blender安装目录或Unity姿态。导出前显式三角化、清除零面积面，保留可动语义节点。六人拉杆的六组表盘与拉杆独立，单人规则仅启用玩家0与NPC1的提示。

## 交付验证

FBX模型导入必须通过`JinxCasinoFormalModelContractsTests`，覆盖全部65资源的坐标、根与内部姿态、语义Pivot、bounds、三角面数、持久URP材质与SHA；模型不能夹带Camera、Light、AudioListener或Collider。图标由现有模型原生渲染，导入Sprite保持透明；音频使用本场景音源及现有唯一监听器。

Unity正式装配入口为`Tools/SleepyDemos/整蛊赌场/生成P4正式离线冒险`，负责保存场景、HUD绑定、URP材质及适合两端的Importer设置。源输出和工具执行回显不代表最终视觉或Player通过，验收以Demo进度文档与实际运行证据为准。

项目已有中文字体、Unity/TMP/URP及其他依赖沿用各自原许可证；本目录的原创资源不改变第三方资源授权。资源来源审核只针对本Demo新增内容，不把原项目其他Demo当成本作原创资源。

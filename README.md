# Plink

<img src="plink-preview.png" width="96" align="right" />

Windows 操作声音反馈工具。复制到剪贴板、删除文件到回收站、键盘输入、插拔电源时发出提示音。

## 功能

- 检测剪贴板变化，播放复制提示音
- 检测文件移入回收站，播放删除提示音
- 可选监听键盘输入，播放打字提示音
- 检测充电器接通/断开，播放对应提示音
- 自定义音效（支持任意 .wav 文件）
- 跟随系统亮色/暗色主题
- 开机自启动
- 单文件，无需安装，无外部依赖

## 使用

双击 `Plink.exe` 运行，图标出现在系统托盘。右键点击图标即可切换功能或自定义音效。

键盘输入音效默认关闭。开启后，Plink 只监听“有按键按下”这一事件来播放声音，不保存、不上传、不记录具体按键内容。

内置打字机按键音效来自 Freesound：[`typewriter.wav` by BMacZero](https://freesound.org/s/160678/)，许可为 Creative Commons 0。

## 构建

需要 .NET Framework 4.0+（Windows 自带）。

```powershell
powershell -ExecutionPolicy Bypass -File make-icon.ps1
powershell -ExecutionPolicy Bypass -File build.ps1
```

## 系统要求

- Windows 10 / 11
- .NET Framework 4.0+

## License

MIT

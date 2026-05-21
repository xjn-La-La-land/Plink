# Plink

<img src="plink-preview.png" width="96" align="right" />

Windows 操作声音反馈工具。复制到剪贴板时发出提示音，删除文件到回收站时发出提示音。

## 功能

- 检测剪贴板变化，播放复制提示音
- 检测文件移入回收站，播放删除提示音
- 自定义音效（支持任意 .wav 文件）
- 跟随系统亮色/暗色主题
- 开机自启动
- 单文件，无需安装，无外部依赖

## 使用

双击 `Plink.exe` 运行，图标出现在系统托盘。右键点击图标即可切换功能或自定义音效。

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

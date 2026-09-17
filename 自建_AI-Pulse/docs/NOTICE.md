# 许可与第三方组件

[English](NOTICE.en.md) · [首页](../README.md)

AI Pulse 自有代码、程序图标和随附插画采用 [MIT 许可](../LICENSE)，版权为 2026 miaomiao tools。素材来源说明见 [assets/SOURCES.md](../assets/SOURCES.md)。服务名称用于标识检测目标，不表示与服务方有关联或获得其背书。

便携 EXE 包含 Microsoft .NET 8.0.31 的 Windows x64 运行时及桌面组件。项目没有额外的应用 NuGet `PackageReference`。自包含分发仍须保留上游许可与第三方通知，不能只保留本项目 MIT：

| 组件 | 随包原文 |
| --- | --- |
| .NET Runtime 与 app host | [MIT LICENSE](licenses/dotnet-runtime-LICENSE.txt)、[完整第三方通知](licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt) |
| Windows Desktop Runtime | [LICENSE](licenses/dotnet-windowsdesktop-LICENSE.txt) |
| 对应该桌面运行时的 WPF | [完整第三方通知](licenses/dotnet-wpf-THIRD-PARTY-NOTICES.txt) |
| 对应该桌面运行时的 Windows Forms | [完整第三方通知](licenses/dotnet-winforms-THIRD-PARTY-NOTICES.txt) |

许可文件直接取自相应官方包；WPF / Windows Forms 通知来自 Windows Desktop 8.0.31 依赖清单锁定的提交。来源、提交和 SHA-256 记录于 [licenses/SOURCES.json](licenses/SOURCES.json)。完整上游通知可能覆盖上游仓库更广的内容，保留它们不表示本应用使用了每一项功能或测试素材。

界面调用 Windows 已安装的 Segoe UI、Microsoft YaHei UI 与 Segoe MDL2 Assets；发布包不包含字体文件。官方组件沿用各自许可。随包未提供代码签名证书。

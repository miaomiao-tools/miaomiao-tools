# 来源与第三方声明

本包仅包含蘑菇鼠标助手的源码、编译程序及文档。发布者：miaomiao tools。

| 项目 | 使用方式及来源 | 分发情况 |
| --- | --- | --- |
| 双箭头托盘图标、面板箭头、关闭符号 | 本项目 `Program.cs` 的几何绘图代码，在运行时生成 | 无外部图标文件、游戏美术或品牌头像 |
| Microsoft YaHei UI | 从用户 Windows 系统字体调用；Microsoft 与 Beijing Founder Electronics 的字体权益见官方页面 | 不包含、嵌入、转换或再分发字体文件 |
| .NET Framework：mscorlib、System、System.Core、System.Drawing、System.Windows.Forms | Microsoft 系统运行库，动态引用 | 不包含运行库二进制或安装器 |
| Windows user32.dll、kernel32.dll、CLR 启动库 | 调用 Windows API / 系统加载器 | 不包含系统 DLL |
| C# 编译器 csc.exe | 从构建者自己的 Windows .NET Framework 环境调用 | 不分发编译器 |
| Shroom and Gloom、Steam | 名称用于说明兼容对象；Steam URI 用于调用用户已安装的客户端 | 无游戏代码、补丁、音频、字体、图像或可执行文件 |

本轮核对没有发现 NuGet 依赖、外部源码包、嵌入式第三方资源或现成第三方许可证文件。Windows 与 .NET Framework 仍适用各自条款，本项目的许可不授予这些组件的再分发权。几何图形与本项目代码的许可随自有代码许可确定；没有给未知第三方材料指定许可证。

参考资料（2026-09-16 核对）：

- [Microsoft YaHei 与 Microsoft YaHei UI 字体信息](https://learn.microsoft.com/en-us/typography/font-list/microsoft-yahei)
- [Windows 字体使用与再分发说明](https://learn.microsoft.com/en-us/typography/fonts/font-faq)：系统字体可供 Windows 应用绘制界面；本包不分发字体。
- [.NET Framework 官方安装说明](https://learn.microsoft.com/en-us/dotnet/framework/install/)
- [Shroom and Gloom 官方 Steam 商店页](https://store.steampowered.com/app/3271280/Shroom_and_Gloom/)

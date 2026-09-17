# 构建、检查与打包

**中文** · [English](BUILD.en.md)

对应 **0.1.0-experimental.3 实验版**。需要 Windows x64、.NET Framework 4.8 / 4.8.1、PowerShell 7；不下载构建依赖，不需要游戏或管理员权限。普通玩家运行 EXE 不需要 PowerShell。

在工具源码目录或完整解压后的源码 ZIP 根目录运行：

```powershell
$build = Join-Path $env:TEMP ('ShroomMouse 构建 ' + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $env:TEMP ('ShroomMouse 打包 ' + [Guid]::NewGuid().ToString('N'))
pwsh -NoProfile -File .\scripts\build.ps1 -OutputDirectory $build
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$report = Join-Path $build 'test-results.txt'
$test = Start-Process -FilePath (Join-Path $build '蘑菇鼠标助手.exe') -ArgumentList ('--test "' + $report + '"') -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0) { throw 'Self-test failed.' }
Get-Content -LiteralPath $report
pwsh -NoProfile -File .\scripts\pack.ps1 -BuildDirectory $build -OutputDirectory $package
if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
Get-ChildItem -LiteralPath $package
```

遵守现有脚本执行策略，无需永久修改系统策略。默认构建目录为工具 `bin`，默认打包目录为工具 `dist`；两个参数均支持中文、空格和其他新目录。

## 文件从哪里来

- `build.ps1` 使用 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，按明确列表编译优化的 x64 程序，警告视作错误，不生成 PDB。
- 完整构建复制便携包所需的**两份快速说明、许可、docs/ 使用与来源说明、assets/ 两张图片**。开发和历史资料仅进源码包。图片尚未提供时可用 `-CodeOnly` 做阶段构建，只生成 EXE、运行配置和本地构建记录，不伪造截图。
- `pack.ps1` 的 EXE/config 只从 `-BuildDirectory` 读取；说明、源码及图片始终从脚本所属的**当前工具目录**读取，忽略 bin 中陈旧文档。它检查版本、源码和程序哈希是否匹配 `build-record.json`；源码或程序变过就必须重建。说明更新不要求重编译，但必须重打包。
- 便携 ZIP 根目录只有 EXE、运行配置、两份 README 和 LICENSE；docs/ 提供两份使用说明和一份双语 NOTICE，assets/ 保存两图。源码 ZIP 另含版本、构建、更新与验证记录。两份 ZIP 分别校验相对链接闭环。白名单不收集 controls.ini、game-path.txt、日志、PDB、临时文件或嵌套旧解压目录。缺图、相对链接失效或重解析点会使打包失败。
- `build-record.json` 只用于本机构建核对，既不进入 ZIP，也不作为公开机器信息记录。

## 产物与校验

输出目录只生成两份 ZIP、`release-files.json` 和 `SHA256SUMS.txt`。JSON 清单记录两个压缩包及各自每个成员的相对路径、大小和 SHA-256；校验文本记录 ZIP 和 JSON 的哈希，不自引用。ZIP 成员固定排序和时间戳，没有个人作者或绝对路径。

该流程能从源码重建同功能程序，但旧 C# 编译器写入时间戳和模块标识，**不保证重建 EXE 逐字节相同**。每次生成新清单，不把某次哈希当作跨机器重建保证。脚本不会提交、打标签、连接 GitHub 或上传。

## 验证边界

`--test` 为离线自测，不装鼠标钩子、不发真实按键。它会在报告所在目录建立隔离临时文件并清理；结果末行应为 `ALL CHECKS PASSED`。

`--demo` 为**真实桌面输入测试**：打开助手与训练窗口，安装鼠标钩子、发送 W/S，在 EXE 旁写 `demo-events.txt`。该模式固定连接训练窗口，重新选择入口禁用；不能把它当成离线测试或游戏实测。点面板 ×、托盘退出或关闭训练窗口可结束。普通模式可测试原生选择、重新选择和持久化，需要真实桌面操作。构建脚本和 `--test` 不操作真实桌面；维护者的桌面验证范围见 [验证记录](VALIDATION.md)。

发布包沿用已验收的 .3 程序，仅更新说明与下载入口；未重新编译。历史 .1 标签和附件保持原样。应用界面仍为中文。

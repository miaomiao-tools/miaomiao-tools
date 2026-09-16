# 构建、检查与打包

**中文** · [English](BUILD.en.md)

环境：Windows x64、.NET Framework 4.8/4.8.1，PowerShell 7。不下载依赖，不需要游戏安装或管理员权限完成构建、自测和打包。

获取源码包并完整解压，在源码包根目录打开 PowerShell：

```powershell
pwsh -NoProfile -File .\scripts\build.ps1
$report = Join-Path $env:TEMP ('shroommouse-test-' + [Guid]::NewGuid().ToString('N') + '.txt')
$exe = (Resolve-Path '.\bin\蘑菇鼠标助手.exe').Path
$test = Start-Process -FilePath $exe -ArgumentList ('--test "' + $report + '"') -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0) { throw 'Self-test failed; inspect the report.' }
Get-Content -LiteralPath $report
pwsh -NoProfile -File .\scripts\pack.ps1
```

按本机现有脚本策略执行，不需要永久更改执行策略。本轮构建与打包使用 PowerShell 7；Windows PowerShell 5.1 没有完成运行验证。检查通过时报告末行为 `ALL CHECKS PASSED`。普通玩家运行已编译程序不需要 PowerShell。

`build.ps1` 从 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319` 调用本机编译器，按固定列表编译 C# 源码，目标为优化的 x64 Windows 程序，不生成 PDB。默认输出到 `bin`；可用 `-OutputDirectory` 指定另一个新的目录。脚本不会运行普通 GUI 或启动游戏。

`pack.ps1` 使用白名单写入 `dist` 中的便携 ZIP、源码 ZIP；压缩内容没有用户配置或测试报告。运行前须先构建；重新构建或改动文档后应重新打包。自有代码以 MIT 许可提供。脚本不连接 GitHub 或上传任何文件。

根目录 `release-files.json` 列出发布文件的相对路径、字节数和 SHA-256；`SHA256SUMS.txt` 还包含该清单本身的校验值。校验文件本身不作自引用哈希。两份 ZIP 中的成员有固定顺序和固定时间戳，不包含绝对路径、机器名或个人作者字段。

此流程可从源码重新构建同功能程序。Windows 自带旧版 C# 编译器会写入 PE 时间戳和模块标识，同源码重建不保证 EXE 或 ZIP 逐字节相同；编译器、系统程序集版本也可能影响结果。比较新产物应使用它自身新生成的校验清单，不将本次哈希宣称为跨机器可重现值。

运行包无需源码或 PowerShell。`--demo` 仅供开发者手动测试：它会创建测试窗口、安装真实鼠标钩子并发送真实按键，且在 EXE 旁记录 `demo-events.txt`；不是隔离测试。本次候选验证没有运行此模式。普通无参数运行也会接入桌面输入，应在适合的环境自行体验。

2026-09-17 的双语更新仅为线上文档更新。现有打包脚本仍使用首次发布的中文文档白名单；已发布 ZIP 和标签没有替换。英文文档可在当前仓库中阅读。

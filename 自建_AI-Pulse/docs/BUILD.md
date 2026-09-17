# 独立构建与打包

[English](BUILD.en.md) · [首页](../README.md)

需要 Windows x64、PowerShell 7 和 .NET SDK **8.0.405 或同一 8.0.4xx 功能带的后续补丁**。`global.json` 固定功能带，项目固定自包含运行时 8.0.31。源码包可单独解压构建，不依赖本仓库其他项目，也不需要账号或 API Key。

在源码包根目录打开 PowerShell：

```powershell
pwsh -File ./scripts/build.ps1
pwsh -File ./scripts/test.ps1 -Executable './_build/publish/AI Pulse.exe' -OutputDirectory './_test/run-1' -IncludeMini
pwsh -File ./scripts/pack.ps1 -PublishDirectory './_build/publish' -BuildRecord './_build/work/build-record.json' -OutputDirectory './_package/release-1'
```

构建默认仅从 `NuGet.Config` 指定的官方 NuGet 源还原依赖，首次需联网下载。输出目录必须为空；重复构建时用新的 `-OutputDirectory` 和 `-WorkDirectory`，避免误覆盖文件。`scripts/build.ps1` 也支持 `-OfflinePackagesDirectory`，指向自行验证并准备好的官方 `.nupkg` 目录。离线源不是发行附件；它需要包含还原所需的运行时包和 SDK 工具包。

发布输出为自包含单文件 `AI Pulse.exe`。构建记录保存源码输入摘要、SDK / 运行时版本、包哈希和 EXE 哈希；代码不附带 PDB，不把本机构建路径写入源码映射。运行时可能将原生组件展开到 Windows 用户临时目录，这是 .NET 单文件应用的运行行为。

`test.ps1` 的 104 项逻辑/安全检查和 32 项迷你检查使用临时目录与本机回环服务，不向真实 AI 服务探测。迷你检查包含模拟状态渲染，不是外网可用性证明。请保留生成的原始报告在本机；不要直接放入公开包。旧的 `--smoke` 自动联网入口在本版本中被禁用，调用只会退出。

`pack.ps1` 使用 `PackageFiles.ps1` 的明确文件清单，检查源码与构建记录一致、相对文档链接完整，并拒绝符号链接、私人数据和调试日志。生成便携 ZIP、源码 ZIP、`SHA256SUMS.txt` 和逐文件 `release-files.json`。压缩包不包含构建目录、个人 `data`、测试报告或其他项目文件。

验证文件完整性可用 `Get-FileHash -Algorithm SHA256` 与发布页附带的 `SHA256SUMS.txt` 比对。`SHA256SUMS.txt` 包含两个 ZIP 与 `release-files.json` 的摘要；它本身不做循环自校验。发布前还需正常启动与人工界面验收，自动检查不能覆盖所有屏幕布局。

自有代码许可见 [LICENSE](../LICENSE)，随运行时分发的许可与通知见 [NOTICE](NOTICE.md)。实际验证范围见 [VALIDATION](VALIDATION.md)。

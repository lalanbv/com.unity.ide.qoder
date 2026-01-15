# com.unity.ide.qoder

Unity 编辑器集成插件，支持将 Qoder 作为 Unity 的外部代码编辑器。提供 `.csproj` 文件生成（用于 IntelliSense）、自动发现安装路径、一键打开脚本等功能。

## 目录

- [使用指南](#使用指南)
  - [前置条件](#前置条件)
  - [安装方式](#安装方式)
  - [配置使用](#配置使用)
  - [使用示例](#使用示例)
- [修改说明](#修改说明)
  - [项目背景](#项目背景)
  - [修改内容](#修改内容)
  - [技术原理](#技术原理)
  - [架构设计](#架构设计)
- [移植指导](#移植指导)
  - [移植到其他编辑器](#移植到其他编辑器)
  - [快速上手指南](#快速上手指南)
  - [注意事项](#注意事项)
- [项目维护](#项目维护)
  - [版本管理](#版本管理)
  - [依赖项管理](#依赖项管理)
  - [常见问题](#常见问题)
  - [更新升级](#更新升级)

---

## 使用指南

### 前置条件

| 要求 | 说明 |
|------|------|
| Unity 版本 | Unity 2019.4 或更高版本 |
| Qoder | 已安装 Qoder 编辑器 |
| 操作系统 | Windows / macOS / Linux |

### 安装方式

#### 方式一：通过 Git URL 安装（推荐）

1. 打开 Unity 编辑器
2. 进入 **Window > Package Manager**
3. 点击左上角 **+** 按钮，选择 **Add package from git URL...**
4. 输入以下地址：
   ```
   https://github.com/lalanbv/com.unity.ide.qoder.git
   ```
5. 点击 **Add** 等待安装完成

#### 方式二：通过 manifest.json 安装

编辑项目的 `Packages/manifest.json` 文件，在 `dependencies` 中添加：

```json
{
  "dependencies": {
    "com.unity.ide.qoder": "https://github.com/lalanbv/com.unity.ide.qoder.git",
    ...
  }
}
```

#### 方式三：本地安装

1. 下载或克隆本仓库
2. 将整个文件夹复制到项目的 `Packages/` 目录下
3. 文件夹名称应为 `com.unity.ide.qoder`

### 配置使用

1. **打开偏好设置**
   - Windows/Linux: `Edit > Preferences > External Tools`
   - macOS: `Unity > Preferences > External Tools`

2. **选择外部脚本编辑器**
   - 在 **External Script Editor** 下拉菜单中选择 **Qoder**
   - 如果 Qoder 没有自动出现，点击 **Browse...** 手动选择 Qoder 可执行文件

3. **自动发现路径**
   
   插件会自动搜索以下常见安装位置：
   
   **Windows:**
   ```
   C:\Program Files\Qoder\Qoder.exe
   %LOCALAPPDATA%\Programs\Qoder\Qoder.exe
   %USERPROFILE%\.qoder\Qoder.exe
   %APPDATA%\Qoder\Qoder.exe
   ```
   
   **macOS:**
   ```
   /Applications/Qoder.app
   ~/Applications/Qoder.app
   /usr/local/bin/qoder
   ~/.qoder/bin/qoder
   ```
   
   **Linux:**
   ```
   /usr/bin/qoder
   /usr/local/bin/qoder
   ~/.qoder/bin/qoder
   /snap/bin/qoder
   ```

### 使用示例

#### 打开脚本文件

- **方式一**：在 Project 窗口中双击任意 `.cs` 文件
- **方式二**：在 Console 窗口中双击错误/警告信息
- **方式三**：通过菜单 `Assets > Open C# Project` 打开整个项目

#### 自动生成项目文件

插件会在以下情况自动生成/更新 `.sln` 和 `.csproj` 文件：

- 首次设置 Qoder 为外部编辑器时
- 添加/删除/移动脚本文件时
- 手动点击 `Regenerate project files` 按钮时

#### 配置项目生成选项

在 `Edit > Preferences > External Tools` 中可以配置：

- **Embedded packages** - 是否为嵌入包生成项目文件
- **Local packages** - 是否为本地包生成项目文件
- **Registry packages** - 是否为注册表包生成项目文件
- **Git packages** - 是否为 Git 包生成项目文件
- **Built-in packages** - 是否为内置包生成项目文件

---

## 修改说明

### 项目背景

本项目基于 Unity 官方的 `com.unity.ide.visualstudio` (v2.0.26) 进行修改，参考了以下开源实现：

| 项目 | 说明 |
|------|------|
| [com.unity.ide.visualstudio](https://github.com/needle-mirror/com.unity.ide.visualstudio) | Unity 官方 Visual Studio 集成（主要参考） |
| [com.unity.ide.cursor](https://github.com/boxqkrtm/com.unity.ide.cursor) | Cursor 编辑器集成（架构参考） |

### 修改内容

#### 1. 新增文件：`VisualStudioQoderInstallation.cs`

**位置**：`Editor/VisualStudioQoderInstallation.cs`

**作用**：Qoder 编辑器的安装发现与打开逻辑

**主要功能**：
- `IsCandidateForDiscovery()` - 判断给定路径是否为 Qoder 可执行文件
- `TryDiscoverInstallation()` - 尝试从路径发现 Qoder 安装
- `GetVisualStudioInstallations()` - 枚举所有可能的 Qoder 安装位置
- `Open()` - 使用 Qoder 打开指定文件并定位到行列
- `CreateExtraFiles()` - 创建 `.qoder/settings.json` 配置文件

```csharp
// 核心打开逻辑
public override bool Open(string path, int line, int column, string solution)
{
    // 使用 VSCode 风格的命令行参数
    // qoder "项目目录" -g "文件路径":行号:列号
    ProcessRunner.Start(ProcessStartInfoFor(application, 
        $"\"{directory}\" -g \"{path}\":{line}:{column}"));
    return true;
}
```

#### 2. 修改文件：`Discovery.cs`

**位置**：`Editor/Discovery.cs`

**修改内容**：在三个方法中添加 Qoder 支持

```csharp
// GetVisualStudioInstallations() 中添加：
foreach (var installation in VisualStudioQoderInstallation.GetVisualStudioInstallations())
    yield return installation;

// TryDiscoverInstallation() 中添加：
if (VisualStudioQoderInstallation.TryDiscoverInstallation(editorPath, out installation))
    return true;

// Initialize() 中添加：
VisualStudioQoderInstallation.Initialize();
```

#### 3. 修改文件：`package.json`

| 字段 | 原值 | 新值 |
|------|------|------|
| name | com.unity.ide.visualstudio | com.unity.ide.qoder |
| displayName | Visual Studio Editor | Qoder Editor |
| version | 2.0.26 | 1.0.0 |
| unity | 2021.3 | 2019.4 |
| repository.url | (Unity 内部) | https://github.com/lalanbv/com.unity.ide.qoder.git |

#### 4. 重命名文件：`com.unity.ide.qoder.asmdef`

- 原文件名：`com.unity.ide.visualstudio.asmdef`
- 新文件名：`com.unity.ide.qoder.asmdef`
- 程序集名称：`Unity.VisualStudio.Editor` → `Unity.Qoder.Editor`

### 技术原理

#### Unity 外部编辑器集成机制

```
┌─────────────────────────────────────────────────────────────┐
│                    Unity Editor                              │
├─────────────────────────────────────────────────────────────┤
│  Unity.CodeEditor.IExternalCodeEditor 接口                   │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  - Installations        // 返回已发现的编辑器安装    │    │
│  │  - OpenProject()        // 打开项目/文件            │    │
│  │  - SyncIfNeeded()       // 同步项目文件             │    │
│  │  - OnGUI()              // 绘制设置界面             │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              VisualStudioEditor : IExternalCodeEditor        │
├─────────────────────────────────────────────────────────────┤
│  [InitializeOnLoad] 静态构造函数注册到 CodeEditor            │
│                                                              │
│  Discovery.GetVisualStudioInstallations()                    │
│  ├── VisualStudioForWindowsInstallation  (Windows IDE)      │
│  ├── VisualStudioCodeInstallation        (VS Code)          │
│  └── VisualStudioQoderInstallation       (Qoder) ← 新增     │
└─────────────────────────────────────────────────────────────┘
```

#### 编辑器发现流程

```
1. Unity 启动时
   └── [InitializeOnLoad] VisualStudioEditor 静态构造函数执行
       └── Discovery.Initialize() 初始化各编辑器
       └── CodeEditor.Register() 注册到 Unity

2. 用户打开 Preferences > External Tools
   └── IExternalCodeEditor.Installations 被调用
       └── Discovery.GetVisualStudioInstallations()
           └── 各 Installation 类枚举可能的安装路径
           └── 返回找到的编辑器列表

3. 用户双击脚本文件
   └── IExternalCodeEditor.OpenProject() 被调用
       └── Discovery.TryDiscoverInstallation() 验证编辑器
       └── IVisualStudioInstallation.Open() 启动编辑器
```

#### 项目文件生成

插件支持两种项目文件格式：

| 格式 | 类 | 特点 |
|------|-----|------|
| SDK-Style | `SdkStyleProjectGeneration` | 现代 .NET SDK 格式，更简洁 |
| Legacy | `LegacyStyleProjectGeneration` | 传统 MSBuild 格式，兼容性好 |

Qoder 使用 SDK-Style 格式：
```csharp
private static readonly IGenerator _generator = GeneratorFactory.GetInstance(GeneratorStyle.SDK);
```

### 架构设计

```
com.unity.ide.qoder/
├── Editor/
│   ├── VisualStudioEditor.cs          # 主入口，实现 IExternalCodeEditor
│   ├── Discovery.cs                    # 编辑器发现调度器 [已修改]
│   ├── VisualStudioInstallation.cs    # 安装基类
│   ├── VisualStudioQoderInstallation.cs    # Qoder 实现 [新增]
│   ├── VisualStudioCodeInstallation.cs     # VS Code 实现
│   ├── VisualStudioForWindowsInstallation.cs # VS IDE 实现
│   ├── ProjectGeneration/
│   │   ├── ProjectGeneration.cs       # 项目文件生成核心
│   │   ├── SdkStyleProjectGeneration.cs
│   │   └── LegacyStyleProjectGeneration.cs
│   └── ...
├── package.json                        # 包配置 [已修改]
└── com.unity.ide.qoder.asmdef         # 程序集定义 [已重命名]
```

---

## 移植指导

### 移植到其他编辑器

如果你想将此项目移植到支持其他编辑器（如 Windsurf、Zed 等），请按以下步骤操作：

#### 步骤 1：创建新的 Installation 类

复制 `VisualStudioQoderInstallation.cs` 并重命名，例如 `VisualStudioWindsurfInstallation.cs`：

```csharp
internal class VisualStudioWindsurfInstallation : VisualStudioInstallation
{
    // 1. 修改可执行文件匹配规则
    private static bool IsCandidateForDiscovery(string path)
    {
#if UNITY_EDITOR_WIN
        return File.Exists(path) && Regex.IsMatch(path, ".*[Ww]indsurf.*.exe$");
#elif UNITY_EDITOR_OSX
        return Directory.Exists(path) && Regex.IsMatch(path, ".*[Ww]indsurf.*.app$");
#else
        return File.Exists(path) && Regex.IsMatch(path, ".*[Ww]indsurf$");
#endif
    }

    // 2. 修改安装路径搜索列表
    public static IEnumerable<IVisualStudioInstallation> GetVisualStudioInstallations()
    {
        var candidates = new List<string>();
#if UNITY_EDITOR_WIN
        candidates.Add(IOPath.Combine(programFiles, "Windsurf", "Windsurf.exe"));
        // ... 添加其他可能的路径
#endif
        // ...
    }

    // 3. 修改显示名称
    installation = new VisualStudioWindsurfInstallation()
    {
        Name = "Windsurf" + (version != null ? $" [{version}]" : ""),
        // ...
    };

    // 4. 修改打开命令（如果命令行参数不同）
    public override bool Open(string path, int line, int column, string solution)
    {
        // 根据目标编辑器的命令行参数格式调整
        ProcessRunner.Start(ProcessStartInfoFor(application, 
            $"\"{directory}\" -g \"{path}\":{line}:{column}"));
        return true;
    }
}
```

#### 步骤 2：修改 Discovery.cs

在三个方法中添加新编辑器的支持：

```csharp
// GetVisualStudioInstallations()
foreach (var installation in VisualStudioWindsurfInstallation.GetVisualStudioInstallations())
    yield return installation;

// TryDiscoverInstallation()
if (VisualStudioWindsurfInstallation.TryDiscoverInstallation(editorPath, out installation))
    return true;

// Initialize()
VisualStudioWindsurfInstallation.Initialize();
```

#### 步骤 3：更新包配置

修改 `package.json`：
- `name`: `com.unity.ide.windsurf`
- `displayName`: `Windsurf Editor`

修改 `asmdef` 文件名和内容：
- 文件名: `com.unity.ide.windsurf.asmdef`
- `name`: `Unity.Windsurf.Editor`

### 快速上手指南

| 步骤 | 操作 | 时间估计 |
|------|------|---------|
| 1 | Fork 本仓库 | 1 分钟 |
| 2 | 全局替换 "Qoder" → "新编辑器名" | 5 分钟 |
| 3 | 修改安装路径列表 | 10 分钟 |
| 4 | 修改可执行文件匹配正则 | 5 分钟 |
| 5 | 调整命令行参数（如需要） | 10 分钟 |
| 6 | 测试验证 | 10 分钟 |

### 注意事项

#### 命令行参数兼容性

不同编辑器的命令行参数格式可能不同：

| 编辑器 | 打开文件并定位 |
|--------|---------------|
| VS Code | `code "目录" -g "文件":行:列` |
| Qoder | `qoder "目录" -g "文件":行:列` |
| Cursor | `cursor "目录" -g "文件":行:列` |
| Sublime | `subl "文件":行:列` |
| Atom | `atom "文件":行:列` |

#### 跨平台差异

```csharp
#if UNITY_EDITOR_WIN
    // Windows: 可执行文件是 .exe
    return File.Exists(path) && path.EndsWith(".exe");
#elif UNITY_EDITOR_OSX
    // macOS: 应用程序是 .app 目录
    return Directory.Exists(path) && path.EndsWith(".app");
#else
    // Linux: 通常没有扩展名
    return File.Exists(path);
#endif
```

#### 版本检测

Windows 下可通过 `FileVersionInfo` 获取版本：
```csharp
var versionInfo = FileVersionInfo.GetVersionInfo(editorPath);
Version.TryParse(versionInfo.ProductVersion, out version);
```

macOS/Linux 可能需要其他方式（读取 plist、执行 --version 等）。

---

## 项目维护

### 版本管理

#### 版本号规范

遵循 [语义化版本](https://semver.org/lang/zh-CN/)：

```
主版本号.次版本号.修订号
   │        │       └── 向后兼容的 Bug 修复
   │        └────────── 向后兼容的新功能
   └─────────────────── 不兼容的 API 变更
```

#### 发布新版本

1. 更新 `package.json` 中的 `version` 字段
2. 更新 CHANGELOG（如有）
3. 创建 Git tag：`git tag v1.0.1`
4. 推送 tag：`git push origin v1.0.1`

### 依赖项管理

当前项目无外部依赖，完全基于 Unity 内置 API。

#### 与上游同步

本项目基于 `com.unity.ide.visualstudio`，如需同步上游更新：

```bash
# 添加上游远程仓库
git remote add upstream https://github.com/needle-mirror/com.unity.ide.visualstudio.git

# 获取上游更新
git fetch upstream

# 查看上游更新内容
git log upstream/master --oneline

# 合并特定提交（推荐）
git cherry-pick <commit-hash>

# 或合并整个分支（谨慎）
git merge upstream/master
```

**注意**：合并后需要重新应用 Qoder 相关修改。

### 常见问题

#### Q: Qoder 没有出现在编辑器列表中

**原因**：Qoder 未安装在预设路径，或可执行文件名不匹配

**解决方案**：
1. 检查 Qoder 安装路径
2. 在 Preferences 中手动 Browse 选择可执行文件
3. 或修改 `VisualStudioQoderInstallation.cs` 添加自定义路径

#### Q: 双击脚本没有反应

**原因**：编辑器路径配置错误

**解决方案**：
1. 检查 Preferences > External Tools 中的编辑器路径
2. 确认路径指向有效的可执行文件
3. 尝试手动运行该可执行文件

#### Q: 项目文件未生成

**原因**：项目文件生成配置问题

**解决方案**：
1. 点击 `Regenerate project files` 按钮
2. 检查 Console 是否有错误信息
3. 确认磁盘空间和写入权限

#### Q: IntelliSense 不工作

**原因**：`.csproj` 文件缺失或过期

**解决方案**：
1. 重新生成项目文件
2. 在 Qoder 中重新加载项目
3. 检查 `.sln` 文件是否存在

### 更新升级

#### 从 Git URL 安装的项目

Package Manager 会缓存包内容。更新方式：

1. 删除 `Library/PackageCache` 中的缓存
2. 重新打开 Unity

或者在 `manifest.json` 中指定版本/commit：

```json
{
  "dependencies": {
    "com.unity.ide.qoder": "https://github.com/lalanbv/com.unity.ide.qoder.git#v1.0.1"
  }
}
```

#### 本地安装的项目

直接用新版本覆盖 `Packages/com.unity.ide.qoder/` 目录。

---

## 许可证

本项目基于 MIT 许可证开源。

原始代码版权归 Unity Technologies 和 Microsoft Corporation 所有。

---

## 贡献

欢迎提交 Issue 和 Pull Request！

- 报告 Bug：[Issues](https://github.com/lalanbv/com.unity.ide.qoder/issues)
- 贡献代码：[Pull Requests](https://github.com/lalanbv/com.unity.ide.qoder/pulls)

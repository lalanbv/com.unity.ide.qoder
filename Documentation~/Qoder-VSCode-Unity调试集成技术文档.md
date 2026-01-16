# Qoder 与 VS Code 的 Unity 调试集成技术文档

## 目录

- [1. 调试功能对比分析](#1-调试功能对比分析)
- [2. Unity 集成设置](#2-unity-集成设置)
- [3. 项目文件生成](#3-项目文件生成)
- [4. 调试协议支持](#4-调试协议支持)
- [5. 断点设置和调试流程](#5-断点设置和调试流程)
- [6. IntelliSense 和代码补全](#6-intellisense-和代码补全)
- [7. 解决方案文件处理](#7-解决方案文件处理)
- [8. 命令行参数](#8-命令行参数)
- [9. 平台兼容性](#9-平台兼容性)
- [10. 性能优化](#10-性能优化)
- [附录 A: 故障排除指南](#附录-a-故障排除指南)
- [附录 B: 最佳实践](#附录-b-最佳实践)

---

## 1. 调试功能对比分析

### 1.1 Qoder 与 VS Code 在 Unity 项目中的调试能力差异

| 特性 | Qoder | VS Code | 说明 |
|-----|-------|---------|------|
| **基础架构** | 基于 VS Code | 原生 | Qoder 继承 VS Code 的所有底层能力 |
| **调试协议** | DAP (Debug Adapter Protocol) | DAP | 两者使用相同的调试协议 |
| **Unity 调试支持** | 需要扩展 | 需要扩展 | 都需要安装 Unity 调试扩展 |
| **配置文件** | 支持 `.vscode/launch.json` | 支持 `.vscode/launch.json` | Qoder 默认兼容 VS Code 配置 |
| **Mono 调试器** | 支持 | 支持 | 通过 `vstuc` 调试适配器 |
| **断点管理** | 完整支持 | 完整支持 | 条件断点、日志断点、函数断点 |
| **变量查看** | 完整支持 | 完整支持 | 局部变量、监视、调用堆栈 |
| **热重载** | 受限 | 受限 | Unity 不支持 C# 热重载 |
| **性能分析** | 通过 Unity Profiler | 通过 Unity Profiler | 依赖 Unity 内置工具 |

### 1.2 核心差异分析

#### Qoder 的特殊之处

1. **AI 增强功能**：Qoder 在 VS Code 基础上增加了 AI 辅助编码能力
2. **配置兼容性**：完全兼容 VS Code 的 `.vscode/` 配置目录
3. **扩展生态**：可使用所有 VS Code 扩展（包括 Unity 调试扩展）

#### VS Code 的优势

1. **社区支持**：更大的用户基础和社区资源
2. **官方支持**：Microsoft 官方维护的 Unity 调试扩展
3. **文档完整**：详尽的官方文档和教程

### 1.3 调试能力边界

**重要概念**：Unity IDE 集成包（`com.unity.ide.qoder`、`com.unity.ide.vscode`）本身**不提供**调试功能，它们的职责是：

```
┌─────────────────────────────────────────────────────────────┐
│           Unity Editor (Unity 集成包的职责)                  │
├─────────────────────────────────────────────────────────────┤
│  ✅ 生成项目文件 (.sln, .csproj)                             │
│  ✅ 生成配置文件 (settings.json, launch.json)                │
│  ✅ 启动外部编辑器并传递参数                                  │
│  ✅ 计算调试端口 (56000 + PID % 1000)                        │
│  ✅ 通过 UDP 消息与编辑器通信                                 │
│  ❌ 不实现调试协议                                            │
│  ❌ 不处理断点逻辑                                            │
│  ❌ 不管理调试会话                                            │
└─────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│        外部编辑器 (Qoder/VS Code 的职责)                     │
├─────────────────────────────────────────────────────────────┤
│  ✅ 实现 DAP 客户端                                           │
│  ✅ 连接到 Unity 调试端口                                     │
│  ✅ 管理断点、变量、堆栈跟踪                                  │
│  ✅ 提供调试 UI (继续、暂停、单步等)                          │
│  ✅ 解析调试符号 (.pdb)                                       │
└─────────────────────────────────────────────────────────────┘
```

**结论**：真正的调试功能由编辑器端的扩展提供，Unity 集成包只是"桥梁"。

---

## 2. Unity 集成设置

### 2.1 Qoder 集成配置

#### 步骤 1: 安装 Qoder Unity 集成包

**方法一：通过 Git URL 安装（推荐）**

```bash
# Unity Package Manager
# Window > Package Manager > + > Add package from git URL
https://github.com/lalanbv/com.unity.ide.qoder.git
```

**方法二：通过 manifest.json**

编辑 `Packages/manifest.json`：

```json
{
  "dependencies": {
    "com.unity.ide.qoder": "https://github.com/lalanbv/com.unity.ide.qoder.git",
    "com.unity.nuget.newtonsoft-json": "3.0.2"
  }
}
```

#### 步骤 2: 设置 Qoder 为外部编辑器

1. 打开 Unity 偏好设置：
   - **Windows/Linux**: `Edit > Preferences > External Tools`
   - **macOS**: `Unity > Preferences > External Tools`

2. 在 **External Script Editor** 下拉菜单中选择 **Qoder**

3. 如果 Qoder 未自动出现，点击 **Browse...** 手动选择可执行文件：
   - **Windows**: `C:\Program Files\Qoder\Qoder.exe`
   - **macOS**: `/Applications/Qoder.app`
   - **Linux**: `/usr/bin/qoder`

#### 步骤 3: 配置项目文件生成选项

在 `External Tools` 面板中勾选需要的选项：

```
☑ Embedded packages     # 嵌入式包
☑ Local packages        # 本地包
☐ Registry packages     # 注册表包（通常不需要）
☐ Git packages          # Git 包（按需）
☐ Built-in packages     # 内置包（通常不需要）
```

#### 步骤 4: 生成配置文件

Unity 会在以下时机自动生成配置文件：

1. 首次设置 Qoder 为外部编辑器
2. 双击任意 `.cs` 文件
3. 手动点击 `Regenerate project files`

**生成的文件结构**：

```
YourUnityProject/
├── .vscode/
│   ├── settings.json       # Unity 文件排除规则
│   └── launch.json         # 调试配置（需手动创建或通过扩展生成）
├── .qoder/
│   └── settings.json       # 同 .vscode/settings.json
├── Assembly-CSharp.csproj
├── YourProject.sln
└── Assets/
```

### 2.2 VS Code 集成配置

#### 步骤 1: 安装 VS Code Unity 集成包

Unity 内置 VS Code 支持（`com.unity.ide.vscode`），通常已默认安装。

**验证安装**：

```bash
# Unity Package Manager
# Window > Package Manager > Packages: Unity Registry
# 搜索 "Visual Studio Code Editor"
```

#### 步骤 2: 安装 VS Code 扩展

在 VS Code 中安装以下扩展：

1. **C# (Microsoft)**：提供 C# 语言支持
   ```
   ext install ms-dotnettools.csharp
   ```

2. **Unity Code Snippets**：Unity 代码片段
   ```
   ext install kleber-swf.unity-code-snippets
   ```

3. **Debugger for Unity（可选）**：Unity 调试器
   ```
   ext install Unity.unity-debug
   ```

#### 步骤 3: 设置 VS Code 为外部编辑器

同 Qoder 的步骤 2，在下拉菜单中选择 **Visual Studio Code**。

### 2.3 自动发现路径机制

Unity 集成包会自动搜索以下路径来发现编辑器安装：

#### Qoder 发现路径

```csharp
// VisualStudioQoderInstallation.cs 中的实现

#if UNITY_EDITOR_WIN
var candidates = new List<string>
{
    IOPath.Combine(programFiles, "Qoder", "Qoder.exe"),
    IOPath.Combine(localAppData, "Programs", "Qoder", "Qoder.exe"),
    IOPath.Combine(userProfile, ".qoder", "Qoder.exe"),
    IOPath.Combine(appData, "Qoder", "Qoder.exe")
};

#elif UNITY_EDITOR_OSX
var candidates = new List<string>
{
    "/Applications/Qoder.app",
    IOPath.Combine(home, "Applications", "Qoder.app"),
    "/usr/local/bin/qoder",
    IOPath.Combine(home, ".qoder", "bin", "qoder")
};

#else // Linux
var candidates = new List<string>
{
    "/usr/bin/qoder",
    "/usr/local/bin/qoder",
    IOPath.Combine(home, ".qoder", "bin", "qoder"),
    "/snap/bin/qoder"
};
#endif
```

#### VS Code 发现路径

VS Code 的发现路径类似，在 `VisualStudioCodeInstallation.cs` 中定义：

```csharp
#if UNITY_EDITOR_WIN
// 常见路径：
// %LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe
// C:\Program Files\Microsoft VS Code\Code.exe

#elif UNITY_EDITOR_OSX
// /Applications/Visual Studio Code.app
// ~/Applications/Visual Studio Code.app

#else // Linux
// /usr/bin/code
// /usr/share/code/bin/code
// /snap/bin/code
#endif
```

### 2.4 手动配置路径

如果自动发现失败，可以手动指定编辑器路径：

1. 在 Unity 中点击 **Browse...**
2. 选择编辑器可执行文件：
   - **Windows**: `Qoder.exe` 或 `Code.exe`
   - **macOS**: `Qoder.app` 或 `Visual Studio Code.app`
   - **Linux**: `qoder` 或 `code`

3. Unity 会记住此路径在 `EditorPrefs` 中

---

## 3. 项目文件生成

### 3.1 项目文件生成机制

Unity IDE 集成包负责生成 `.sln` 和 `.csproj` 文件，以支持编辑器的 IntelliSense 和代码导航。

#### 生成时机

```csharp
// VisualStudioEditor.cs 中的 SyncIfNeeded() 方法

public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, ...)
{
    if (addedFiles.Any() || deletedFiles.Any() || ...)
    {
        SyncSolution();  // 触发项目文件重新生成
    }
}
```

**触发条件**：

1. 添加/删除/移动 `.cs` 文件
2. 添加/删除/启用/禁用包 (Package)
3. 更改 Assembly Definition (.asmdef) 文件
4. 手动点击 `Regenerate project files`
5. Unity 编辑器启动时（如果文件不存在）

### 3.2 两种项目文件格式

Unity 集成包支持两种 `.csproj` 格式：

#### SDK-Style（现代格式，推荐）

```xml
<!-- Assembly-CSharp.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net4.8</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <RootNamespace>DefaultNamespace</RootNamespace>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <Deterministic>false</Deterministic>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Assets\Scripts\PlayerController.cs" />
    <Compile Include="Assets\Scripts\GameManager.cs" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="UnityEngine">
      <HintPath>C:\Program Files\Unity\Editor\Data\Managed\UnityEngine.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

**特点**：
- 更简洁的语法
- 自动包含目录下所有文件（可选）
- 支持新的 .NET SDK 特性

#### Legacy-Style（传统格式）

```xml
<!-- Assembly-CSharp.csproj -->
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <OutputPath>Temp\bin\Debug\</OutputPath>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="UnityEngine">
      <HintPath>C:\Program Files\Unity\Editor\Data\Managed\UnityEngine.dll</HintPath>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <Compile Include="Assets\Scripts\PlayerController.cs" />
    <Compile Include="Assets\Scripts\GameManager.cs" />
  </ItemGroup>

  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

**特点**：
- 更冗长的语法
- 需要显式列出所有文件
- 兼容旧版本的 MSBuild

### 3.3 Qoder 与 VS Code 的项目文件配置差异

**核心原理**：Qoder 和 VS Code 使用**相同的项目文件**（`.sln` 和 `.csproj`），但可能使用不同的配置目录。

#### 配置文件位置

| 编辑器 | 配置目录 | 配置文件 | 说明 |
|-------|---------|---------|------|
| **VS Code** | `.vscode/` | `settings.json`, `launch.json`, `tasks.json` | 标准 VS Code 配置 |
| **Qoder** | `.vscode/` 或 `.qoder/` | 同上 | 兼容 VS Code 配置，可选自定义目录 |

#### settings.json 生成代码

```csharp
// VisualStudioQoderInstallation.cs 中的 CreateSettingsFile()

private void CreateSettingsFile(string configDirectory)
{
    var settingsFile = IOPath.Combine(configDirectory, "settings.json");
    if (File.Exists(settingsFile))
        return;

    string content = @"{
    ""files.exclude"": {
        ""**/.DS_Store"": true,
        ""**/.git"": true,
        ""**/.gitmodules"": true,
        ""**/*.booproj"": true,
        ""**/*.pidb"": true,
        ""**/*.suo"": true,
        ""**/*.user"": true,
        ""**/*.userprefs"": true,
        ""**/*.unityproj"": true,
        ""**/*.dll"": true,
        ""**/*.exe"": true,
        ""**/*.pdf"": true,
        ""**/*.mid"": true,
        ""**/*.midi"": true,
        ""**/*.wav"": true,
        ""**/*.gif"": true,
        ""**/*.ico"": true,
        ""**/*.jpg"": true,
        ""**/*.jpeg"": true,
        ""**/*.png"": true,
        ""**/*.psd"": true,
        ""**/*.tga"": true,
        ""**/*.tif"": true,
        ""**/*.tiff"": true,
        ""**/*.3ds"": true,
        ""**/*.3DS"": true,
        ""**/*.fbx"": true,
        ""**/*.FBX"": true,
        ""**/*.lxo"": true,
        ""**/*.LXO"": true,
        ""**/*.ma"": true,
        ""**/*.MA"": true,
        ""**/*.obj"": true,
        ""**/*.OBJ"": true,
        ""**/*.asset"": true,
        ""**/*.cubemap"": true,
        ""**/*.flare"": true,
        ""**/*.mat"": true,
        ""**/*.meta"": true,
        ""**/*.prefab"": true,
        ""**/*.unity"": true,
        ""build/"": true,
        ""Build/"": true,
        ""Library/"": true,
        ""library/"": true,
        ""obj/"": true,
        ""Obj/"": true,
        ""ProjectSettings/"": true,
        ""temp/"": true,
        ""Temp/"": true
    }
}";
    File.WriteAllText(settingsFile, content);
}
```

**说明**：此方法会在 `.qoder/settings.json` 中生成文件排除规则，避免编辑器索引大量无关文件（如 `.meta`、`Library/` 等）。

### 3.4 项目文件生成核心类

#### ProjectGeneration.cs

```csharp
// Editor/ProjectGeneration/ProjectGeneration.cs

internal class ProjectGeneration
{
    private readonly IGenerator _generator;
    
    public ProjectGeneration()
    {
        // 选择项目文件格式
        _generator = GeneratorFactory.GetInstance(GeneratorStyle.SDK);
    }

    public void Sync()
    {
        // 生成 .sln 文件
        GenerateSolutionFile();
        
        // 生成所有 .csproj 文件
        foreach (var assembly in GetAssemblies())
        {
            GenerateProjectFile(assembly);
        }
    }

    private void GenerateSolutionFile()
    {
        var solutionFile = IOPath.Combine(ProjectDirectory, $"{ProjectName}.sln");
        var content = _generator.SolutionText(GetAssemblies());
        File.WriteAllText(solutionFile, content);
    }

    private void GenerateProjectFile(Assembly assembly)
    {
        var projectFile = IOPath.Combine(ProjectDirectory, $"{assembly.name}.csproj");
        var content = _generator.ProjectText(assembly);
        File.WriteAllText(projectFile, content);
    }
}
```

#### 关键方法解析

1. **GetAssemblies()**: 枚举 Unity 项目中所有的 Assembly（程序集）
   - `Assembly-CSharp.csproj`: 主程序集
   - `Assembly-CSharp-Editor.csproj`: 编辑器程序集
   - `自定义 .asmdef 对应的程序集`

2. **SolutionText()**: 生成 `.sln` 文件内容
   ```csharp
   Microsoft Visual Studio Solution File, Format Version 12.00
   # Visual Studio Version 17
   Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Assembly-CSharp", "Assembly-CSharp.csproj", "{GUID}"
   EndProject
   ```

3. **ProjectText()**: 生成 `.csproj` 文件内容
   - 列出所有源文件
   - 添加程序集引用（UnityEngine.dll、UnityEditor.dll 等）
   - 配置编译选项（LangVersion、DefineConstants 等）

---

## 4. 调试协议支持

### 4.1 Debug Adapter Protocol (DAP)

**DAP** 是由 Microsoft 开发的标准调试协议，用于在编辑器和调试器之间进行通信。

```
┌─────────────────────────────────────────────────────────────┐
│                     编辑器 (Qoder/VS Code)                   │
│  ┌─────────────────────────────────────────────────────┐    │
│  │             DAP 客户端                              │    │
│  │  - 发送调试请求 (launch, attach, setBreakpoints)   │    │
│  │  - 接收调试事件 (stopped, output, terminated)      │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                          │ JSON-RPC over stdio/socket
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                   调试适配器 (Debug Adapter)                 │
│  ┌─────────────────────────────────────────────────────┐    │
│  │           vstuc (Unity Debugger Adapter)            │    │
│  │  - 解析 DAP 请求                                    │    │
│  │  - 转换为 Mono Soft Debugger 协议                   │    │
│  │  - 返回 DAP 响应                                    │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                          │ Mono Soft Debugger Wire Protocol
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                      Unity Editor                            │
│  ┌─────────────────────────────────────────────────────┐    │
│  │           Mono Soft Debugger                        │    │
│  │  - 监听调试端口 (56000 + PID % 1000)                │    │
│  │  - 处理断点、单步、变量查看                         │    │
│  │  - 返回调试信息                                     │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 Unity 调试端口计算

Unity 使用动态端口来避免多个 Unity 实例之间的冲突：

```csharp
// VisualStudioIntegration.cs 中的 DebuggingPort()

private static int DebuggingPort()
{
    return 56000 + (System.Diagnostics.Process.GetCurrentProcess().Id % 1000);
}
```

**计算规则**：
- **基础端口**: 56000
- **偏移量**: 当前进程 ID 对 1000 取模
- **端口范围**: 56000 - 56999

**示例**：
- Unity 进程 ID: 12345 → 调试端口: 56000 + 345 = 56345
- Unity 进程 ID: 67890 → 调试端口: 56000 + 890 = 56890

### 4.3 Mono Soft Debugger Wire Protocol

Unity 的调试功能基于 **Mono Soft Debugger**，这是 Mono 运行时内置的调试器。

**协议特性**：
- 基于 TCP socket 通信
- 二进制协议（非 JSON）
- 支持断点、单步、变量查看、堆栈跟踪、表达式求值等

**调试器初始化**：

Unity 在启动时会启动 Mono Soft Debugger 并监听调试端口：

```csharp
// Unity 内部代码（伪代码）
MonoDebugger.Listen(56000 + (ProcessId % 1000));
```

### 4.4 launch.json 配置

#### 基本配置

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Attach to Unity Editor",
            "type": "vstuc",
            "request": "attach"
        }
    ]
}
```

**字段说明**：

- `name`: 调试配置的显示名称
- `type`: 调试适配器类型（`vstuc` = Visual Studio Tools for Unity C#）
- `request`: 调试模式
  - `attach`: 附加到已运行的 Unity 进程
  - `launch`: 启动 Unity 并附加（通常不使用）

#### 高级配置

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Attach to Unity Editor",
            "type": "vstuc",
            "request": "attach",
            "endPoint": "127.0.0.1:56345",  // 显式指定端口
            "path": "${workspaceFolder}"
        },
        {
            "name": "Attach to Unity (Auto-detect)",
            "type": "vstuc",
            "request": "attach"
            // 不指定 endPoint，自动搜索 Unity 进程
        }
    ]
}
```

**高级字段**：

- `endPoint`: 手动指定 Unity 调试端口（格式：`IP:端口`）
- `path`: 项目路径（通常为 `${workspaceFolder}`）

### 4.5 调试适配器安装

#### VS Code 扩展

安装 **Debugger for Unity** 扩展：

```bash
ext install Unity.unity-debug
```

此扩展会安装 `vstuc` 调试适配器。

#### Qoder 扩展

Qoder 可以使用相同的 VS Code 扩展，因为它继承了 VS Code 的扩展系统。

安装方式：
1. 在 Qoder 中打开扩展面板
2. 搜索 "Unity Debug"
3. 安装 "Debugger for Unity"

### 4.6 调试消息通信

Unity 集成包通过 **UDP 消息系统** 与编辑器通信。

#### 消息类型

```csharp
// Messaging/MessageType.cs

internal enum MessageType
{
    Ping,               // 编辑器发送心跳包
    Pong,               // Unity 响应心跳包
    Play,               // 进入播放模式
    Stop,               // 退出播放模式
    Pause,              // 暂停
    Unpause,            // 继续
    Version,            // 查询包版本
    ProjectPath,        // 查询项目路径
    ExecuteTests,       // 执行测试
    RetrieveTestList,   // 获取测试列表
    ShowUsage,          // 显示使用统计
}
```

#### 消息通信端口

```csharp
// VisualStudioIntegration.cs

private static int MessagingPort()
{
    return DebuggingPort() + 2;  // 调试端口 + 2
}

// 示例：
// 调试端口: 56345
// 消息端口: 56347
```

#### 消息处理示例

```csharp
// VisualStudioIntegration.cs 中的 ProcessIncoming()

private static void ProcessIncoming(Message message)
{
    switch (message.Type)
    {
        case MessageType.Ping:
            Answer(message, MessageType.Pong);
            break;
        case MessageType.Play:
            EditorApplication.isPlaying = true;
            break;
        case MessageType.Stop:
            EditorApplication.isPlaying = false;
            break;
        case MessageType.ProjectPath:
            Answer(message, MessageType.ProjectPath, 
                Path.Combine(Application.dataPath, ".."));
            break;
    }
}
```

**工作流程**：

1. 编辑器启动时向 Unity 发送 `Ping` 消息
2. Unity 响应 `Pong` 消息，建立连接
3. 编辑器可发送 `Play`、`Stop`、`Pause` 等控制命令
4. Unity 处理命令并执行相应操作

---

## 5. 断点设置和调试流程

### 5.1 断点类型

| 断点类型 | 说明 | 使用场景 |
|---------|------|---------|
| **行断点** | 在代码行上设置 | 最常用的断点类型 |
| **条件断点** | 满足条件时才触发 | `i == 100` 时暂停 |
| **日志断点** | 不暂停，仅输出日志 | 避免频繁中断执行 |
| **函数断点** | 在函数入口处触发 | 跟踪特定函数调用 |
| **异常断点** | 抛出异常时触发 | 调试异常处理逻辑 |

### 5.2 调试流程

#### 步骤 1: 启动 Unity 并进入播放模式

1. 在 Unity Editor 中打开项目
2. 点击 **Play** 按钮（或按 `Ctrl+P`）
3. Unity 会启动 Mono Soft Debugger 并监听调试端口

#### 步骤 2: 在编辑器中设置断点

1. 在 Qoder/VS Code 中打开 `.cs` 文件
2. 点击行号左侧设置断点（红色圆点）
3. 断点会立即同步到 Unity 调试器

#### 步骤 3: 附加调试器到 Unity

1. 在编辑器中按 `F5` 或点击 **Run and Debug** 面板
2. 选择 **Attach to Unity Editor** 配置
3. 点击 **Start Debugging** (绿色三角按钮)

**自动化脚本**：

可以创建 `.vscode/tasks.json` 自动化此流程：

```json
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "Start Unity Play Mode",
            "type": "shell",
            "command": "unity-messaging-client",
            "args": ["play"],
            "problemMatcher": []
        }
    ]
}
```

#### 步骤 4: 触发断点

当 Unity 执行到断点所在行时：

1. Unity 暂停执行
2. 调试器发送 `stopped` 事件到编辑器
3. 编辑器高亮显示当前行
4. 显示局部变量、调用堆栈等信息

#### 步骤 5: 调试操作

| 操作 | 快捷键 | 说明 |
|-----|--------|------|
| **继续** | `F5` | 继续执行到下一个断点 |
| **单步跳过** | `F10` | 执行当前行，不进入函数内部 |
| **单步进入** | `F11` | 进入函数内部 |
| **单步跳出** | `Shift+F11` | 跳出当前函数 |
| **重启** | `Ctrl+Shift+F5` | 停止并重新附加调试器 |
| **停止** | `Shift+F5` | 断开调试器连接 |

### 5.3 调试面板功能

#### 变量面板 (Variables)

显示当前作用域的变量：

```
Variables
├─ Local
│  ├─ health: 100 (int)
│  ├─ player: PlayerController (PlayerController)
│  └─ deltaTime: 0.016 (float)
├─ this
│  ├─ name: "Player" (string)
│  ├─ transform: Transform (Transform)
│  └─ rigidbody: Rigidbody (Rigidbody)
└─ Static
   └─ Time.time: 12.34 (float)
```

**操作**：
- 双击变量查看详细信息
- 右键 → **Add to Watch** 添加到监视列表
- 修改变量值（部分类型支持）

#### 监视面板 (Watch)

监视自定义表达式：

```
Watch
├─ player.health + 10: 110
├─ Vector3.Distance(a, b): 5.23
└─ enemies.Count > 0: true
```

**添加监视**：
- 点击 **+ 添加表达式**
- 输入 C# 表达式（如 `player.health`、`enemies.Count`）

#### 调用堆栈 (Call Stack)

显示当前调用链：

```
Call Stack
├─ PlayerController.TakeDamage(int amount) Line 45
├─ Enemy.Attack() Line 78
├─ Enemy.Update() Line 23
└─ UnityEngine.MonoBehaviour.Update()
```

**操作**：
- 点击堆栈帧查看对应代码位置
- 查看每个堆栈帧的局部变量

#### 断点面板 (Breakpoints)

管理所有断点：

```
Breakpoints
☑ PlayerController.cs:45
☑ GameManager.cs:123 (条件: score > 1000)
☐ Enemy.cs:78 (已禁用)
```

**操作**：
- 勾选/取消勾选：启用/禁用断点
- 右键 → **Edit Breakpoint**：编辑条件
- 右键 → **Remove Breakpoint**：删除断点

### 5.4 条件断点示例

**场景**：只在玩家生命值小于 10 时暂停

```csharp
void TakeDamage(int amount)
{
    health -= amount;  // 在此行设置条件断点
    if (health <= 0)
    {
        Die();
    }
}
```

**设置条件断点**：

1. 右键点击断点 → **Edit Breakpoint**
2. 输入条件表达式：`health < 10`
3. 现在只有当 `health < 10` 时才会触发断点

### 5.5 日志断点示例

**场景**：记录每次伤害值，但不暂停执行

**设置日志断点**：

1. 右键点击断点 → **Edit Breakpoint**
2. 勾选 **Log Message**
3. 输入日志消息：`Damage taken: {amount}, Health remaining: {health}`
4. 取消勾选 **Pause Execution**

**效果**：

每次执行到此行时，调试控制台会输出：

```
Damage taken: 25, Health remaining: 75
Damage taken: 10, Health remaining: 65
```

### 5.6 异常断点

**启用异常断点**：

1. 打开 **Breakpoints** 面板
2. 勾选 **All Exceptions** 或 **Uncaught Exceptions**

**效果**：

当代码抛出异常时，调试器会自动暂停，即使没有设置行断点。

---

## 6. IntelliSense 和代码补全

### 6.1 IntelliSense 工作原理

IntelliSense 依赖于编辑器的 **语言服务器** (Language Server) 来分析 `.csproj` 文件和源代码。

```
┌─────────────────────────────────────────────────────────────┐
│                  编辑器 (Qoder/VS Code)                      │
│  ┌─────────────────────────────────────────────────────┐    │
│  │            C# 扩展 (OmniSharp)                      │    │
│  │  - 解析 .csproj 和 .sln 文件                       │    │
│  │  - 分析源代码和程序集引用                          │    │
│  │  - 提供代码补全、跳转定义、查找引用等功能           │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                  Unity 项目文件                              │
│  ├─ Assembly-CSharp.csproj                                  │
│  │    - 列出所有 .cs 文件                                   │
│  │    - 引用 UnityEngine.dll, UnityEditor.dll              │
│  └─ YourProject.sln                                         │
│       - 包含所有 .csproj 项目                               │
└─────────────────────────────────────────────────────────────┘
```

### 6.2 Qoder 与 VS Code 的 IntelliSense 配置

#### OmniSharp 配置

OmniSharp 是 C# 语言服务器，Qoder 和 VS Code 都使用它。

**配置文件**: `omnisharp.json`（项目根目录）

```json
{
    "MsBuild": {
        "UseBundledOnly": true
    },
    "RoslynExtensionsOptions": {
        "EnableAnalyzersSupport": true,
        "LocationPaths": []
    },
    "FormattingOptions": {
        "EnableEditorConfigSupport": true,
        "OrganizeImports": true
    }
}
```

**关键配置项**：

- `UseBundledOnly`: 使用编辑器内置的 MSBuild（推荐设为 `true`）
- `EnableAnalyzersSupport`: 启用 Roslyn 分析器（代码质量检查）
- `EnableEditorConfigSupport`: 支持 `.editorconfig` 格式化规则

### 6.3 IntelliSense 功能对比

| 功能 | Qoder | VS Code | 说明 |
|-----|-------|---------|------|
| **代码补全** | ✅ | ✅ | 输入时自动建议 |
| **参数提示** | ✅ | ✅ | 显示函数参数和重载 |
| **跳转到定义** | ✅ | ✅ | `F12` 或 `Ctrl+点击` |
| **查找所有引用** | ✅ | ✅ | `Shift+F12` |
| **重命名符号** | ✅ | ✅ | `F2` |
| **快速修复** | ✅ | ✅ | `Ctrl+.` 显示代码修复建议 |
| **代码片段** | ✅ | ✅ | 输入 `prop` 生成属性模板 |
| **悬停提示** | ✅ | ✅ | 鼠标悬停显示文档注释 |
| **AI 辅助补全** | ✅ | ❌ | Qoder 独有功能 |

### 6.4 Unity 特定的 IntelliSense

#### Unity API 补全

在项目文件中包含 Unity 程序集引用后，可以获得 Unity API 的智能提示：

```csharp
using UnityEngine;

public class Example : MonoBehaviour
{
    void Start()
    {
        transform.  // 自动补全：position, rotation, localScale, ...
        GameObject.  // 自动补全：Find, Instantiate, Destroy, ...
        Debug.  // 自动补全：Log, LogWarning, LogError, ...
    }
}
```

#### Unity 消息方法补全

OmniSharp 可以识别 Unity 的特殊方法（如 `Awake`, `Start`, `Update`）并提供补全：

```csharp
public class PlayerController : MonoBehaviour
{
    // 输入 "void Start" 后按 Tab，自动生成：
    void Start()
    {
        
    }

    // 输入 "void Update" 后按 Tab，自动生成：
    void Update()
    {
        
    }
}
```

#### 序列化字段补全

Unity 序列化系统的智能提示：

```csharp
public class Example : MonoBehaviour
{
    [SerializeField]  // 自动补全特性
    private int health;

    [Range(0, 100)]  // 自动补全并显示参数提示
    public float speed;
}
```

### 6.5 IntelliSense 故障排除

#### 问题 1: IntelliSense 不工作

**可能原因**：
- `.csproj` 或 `.sln` 文件缺失或过期
- OmniSharp 未正确加载项目

**解决方案**：

1. 在 Unity 中重新生成项目文件：
   - `Edit > Preferences > External Tools`
   - 点击 **Regenerate project files**

2. 在编辑器中重新加载 OmniSharp：
   - **VS Code**: `Ctrl+Shift+P` → `OmniSharp: Restart OmniSharp`
   - **Qoder**: 同 VS Code（Qoder 兼容 VS Code 命令）

3. 检查 OmniSharp 日志：
   - **VS Code**: `View > Output` → 选择 **OmniSharp Log**
   - 查找错误信息（如缺少依赖、项目文件解析失败）

#### 问题 2: Unity API 没有智能提示

**可能原因**：
- 项目文件中缺少 Unity 程序集引用
- OmniSharp 使用了错误的 Unity 安装路径

**解决方案**：

1. 检查 `.csproj` 文件是否包含 Unity 引用：
   ```xml
   <ItemGroup>
     <Reference Include="UnityEngine">
       <HintPath>C:\Program Files\Unity\Editor\Data\Managed\UnityEngine.dll</HintPath>
     </Reference>
   </ItemGroup>
   ```

2. 如果路径不正确，手动修改或重新生成项目文件

#### 问题 3: 代码补全速度慢

**可能原因**：
- 项目包含大量文件
- OmniSharp 索引了不必要的文件（如 `Library/`）

**解决方案**：

1. 使用 `.vscode/settings.json` 排除不必要的目录：
   ```json
   {
       "files.exclude": {
           "Library/": true,
           "Temp/": true,
           "obj/": true
       }
   }
   ```

2. 禁用部分 Roslyn 分析器：
   ```json
   {
       "omnisharp.enableRoslynAnalyzers": false
   }
   ```

---

## 7. 解决方案文件处理

### 7.1 .sln 文件结构

`.sln` 文件是 Visual Studio 解决方案文件，包含项目列表和构建配置。

**示例**：

```
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1

Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Assembly-CSharp", "Assembly-CSharp.csproj", "{12345678-1234-1234-1234-123456789012}"
EndProject

Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Assembly-CSharp-Editor", "Assembly-CSharp-Editor.csproj", "{87654321-4321-4321-4321-210987654321}"
EndProject

Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
    
    GlobalSection(ProjectConfigurationPlatforms) = postSolution
        {12345678-1234-1234-1234-123456789012}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {12345678-1234-1234-1234-123456789012}.Debug|Any CPU.Build.0 = Debug|Any CPU
        {87654321-4321-4321-4321-210987654321}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        {87654321-4321-4321-4321-210987654321}.Debug|Any CPU.Build.0 = Debug|Any CPU
    EndGlobalSection
EndGlobal
```

**关键部分**：

1. **文件头**：声明格式版本和 Visual Studio 版本
2. **Project 节**：列出所有 `.csproj` 项目
3. **Global 节**：定义解决方案级别的配置（如 Debug/Release）

### 7.2 .csproj 文件结构

`.csproj` 文件定义单个项目的构建规则。

**SDK-Style 示例**：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net4.8</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <RootNamespace>DefaultNamespace</RootNamespace>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <Deterministic>false</Deterministic>
    <NoWarn>CS0649</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Assets\Scripts\PlayerController.cs" />
    <Compile Include="Assets\Scripts\GameManager.cs" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="UnityEngine">
      <HintPath>C:\Program Files\Unity\Editor\Data\Managed\UnityEngine.dll</HintPath>
    </Reference>
    <Reference Include="UnityEditor">
      <HintPath>C:\Program Files\Unity\Editor\Data\Managed\UnityEditor.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

**关键节**：

1. **PropertyGroup**: 编译器配置
   - `TargetFramework`: .NET 框架版本（Unity 使用 .NET 4.8 或 .NET Standard 2.1）
   - `LangVersion`: C# 语言版本（Unity 2021+ 支持 C# 9.0）
   - `NoWarn`: 抑制特定警告（如 CS0649：字段未赋值）

2. **ItemGroup (Compile)**: 源文件列表
   - 列出所有 `.cs` 文件
   - Unity 自动生成此列表

3. **ItemGroup (Reference)**: 程序集引用
   - Unity 引擎 DLL（UnityEngine.dll）
   - Unity 编辑器 DLL（UnityEditor.dll）
   - 第三方插件 DLL

### 7.3 Unity 程序集定义 (.asmdef)

Unity 支持通过 **Assembly Definition** 文件将代码分割为多个程序集。

**示例**: `Scripts/MyFeature.asmdef`

```json
{
    "name": "MyFeature",
    "rootNamespace": "MyFeature",
    "references": [
        "Unity.InputSystem",
        "Unity.TextMeshPro"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**字段说明**：

- `name`: 程序集名称（会生成 `MyFeature.csproj`）
- `references`: 依赖的其他程序集
- `includePlatforms`: 仅在指定平台编译（空数组表示所有平台）
- `allowUnsafeCode`: 是否允许 `unsafe` 代码

**对应的 .csproj**：

Unity 会为每个 `.asmdef` 生成一个 `.csproj` 文件：

```
YourProject/
├── Assembly-CSharp.csproj        # 默认程序集（没有 .asmdef 的脚本）
├── MyFeature.csproj              # MyFeature.asmdef 对应的项目
└── YourProject.sln               # 包含所有项目
```

### 7.4 项目文件同步机制

Unity 在以下情况会重新生成项目文件：

1. **添加/删除 .cs 文件**：自动触发
2. **修改 .asmdef 文件**：自动触发
3. **添加/删除 Unity 包**：自动触发
4. **手动触发**：点击 `Regenerate project files`

**同步流程**：

```
用户操作 (添加 .cs 文件)
    ↓
Unity 检测到文件系统变化
    ↓
AssetDatabase.Refresh()
    ↓
CodeEditor.SyncIfNeeded(addedFiles, deletedFiles, ...)
    ↓
ProjectGeneration.Sync()
    ↓
生成/更新 .sln 和 .csproj 文件
    ↓
编辑器自动重新加载项目
```

### 7.5 解决方案文件的平台兼容性

#### Windows

- 使用反斜杠路径分隔符 `\`
- 可执行文件扩展名：`.exe`

#### macOS

- 使用正斜杠路径分隔符 `/`
- 应用程序后缀：`.app`

#### Linux

- 使用正斜杠路径分隔符 `/`
- 可执行文件无扩展名

**跨平台处理**：

Unity 集成包使用 `IOPath.Combine()` 和 `.NormalizePathSeparators()` 处理路径：

```csharp
// FileUtility.cs

public static string NormalizePathSeparators(this string path)
{
    return path.Replace('\\', IOPath.DirectorySeparatorChar)
               .Replace('/', IOPath.DirectorySeparatorChar);
}
```

---

## 8. 命令行参数

### 8.1 Qoder 启动命令

Qoder 使用与 VS Code 兼容的命令行参数。

**基本语法**：

```bash
qoder [目录] [选项] [文件]
```

**常用参数**：

| 参数 | 说明 | 示例 |
|-----|------|------|
| `<目录>` | 打开指定目录 | `qoder "C:\MyProject"` |
| `-g <文件>:<行>:<列>` | 打开文件并定位到行列 | `qoder -g "script.cs":10:5` |
| `-n` | 打开新窗口 | `qoder -n` |
| `-r` | 在当前窗口重新打开 | `qoder -r` |
| `--wait` | 等待文件关闭后返回 | `qoder --wait "file.cs"` |

### 8.2 Unity 集成包的启动命令

Unity 使用以下代码启动 Qoder：

```csharp
// VisualStudioQoderInstallation.cs 中的 Open() 方法

public override bool Open(string path, int line, int column, string solution)
{
    var directory = solution != null 
        ? Directory.GetParent(solution).FullName 
        : Directory.GetParent(path).FullName;

    var arguments = $"\"{directory}\" -g \"{path}\":{line}:{column}";
    
    ProcessRunner.Start(ProcessStartInfoFor(application, arguments));
    return true;
}
```

**生成的命令示例**：

```bash
qoder "C:\MyUnityProject" -g "C:\MyUnityProject\Assets\Scripts\PlayerController.cs":45:12
```

**参数解析**：

1. `"C:\MyUnityProject"`: 项目根目录（包含 `.sln` 文件）
2. `-g`: "Go to" 标志，表示跳转到文件位置
3. `"...\PlayerController.cs"`: 要打开的文件
4. `:45:12`: 行号 45，列号 12

### 8.3 VS Code 启动命令

VS Code 使用相同的命令行参数格式：

```bash
code "C:\MyUnityProject" -g "C:\MyUnityProject\Assets\Scripts\PlayerController.cs":45:12
```

### 8.4 其他编辑器的命令行参数对比

| 编辑器 | 打开文件并定位 | 说明 |
|-------|---------------|------|
| **Qoder** | `qoder "dir" -g "file":line:col` | 与 VS Code 相同 |
| **VS Code** | `code "dir" -g "file":line:col` | 标准格式 |
| **Cursor** | `cursor "dir" -g "file":line:col` | 与 VS Code 相同 |
| **Sublime Text** | `subl "file":line:col` | 不需要目录参数 |
| **Atom** | `atom "file":line:col` | 不需要目录参数 |
| **Visual Studio** | `devenv /edit "file" /command "Edit.Goto line"` | 需要两步操作 |

### 8.5 ProcessRunner 实现

Unity 集成包使用 `ProcessRunner` 启动外部进程。

```csharp
// ProcessRunner.cs

internal static class ProcessRunner
{
    public static void Start(ProcessStartInfo startInfo)
    {
        try
        {
            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to start process: {ex.Message}");
        }
    }
}
```

**ProcessStartInfo 配置**：

```csharp
private ProcessStartInfo ProcessStartInfoFor(string application, string arguments)
{
    return new ProcessStartInfo
    {
        FileName = application,          // qoder 可执行文件路径
        Arguments = arguments,            // 命令行参数
        UseShellExecute = false,         // 不使用系统 Shell
        CreateNoWindow = true,           // 不创建控制台窗口
        WorkingDirectory = projectPath   // 工作目录
    };
}
```

---

## 9. 平台兼容性

### 9.1 跨平台差异总结

| 特性 | Windows | macOS | Linux |
|-----|---------|-------|-------|
| **可执行文件** | `.exe` | `.app` 或无扩展名 | 无扩展名 |
| **路径分隔符** | `\` | `/` | `/` |
| **默认安装路径** | `C:\Program Files\` | `/Applications/` | `/usr/bin/` |
| **用户数据目录** | `%APPDATA%` | `~/Library/` | `~/.config/` |
| **命令行参数** | 相同 | 相同 | 相同 |
| **调试协议** | DAP | DAP | DAP |
| **Mono Debugger** | 支持 | 支持 | 支持 |

### 9.2 Windows 平台特性

#### 可执行文件发现

```csharp
#if UNITY_EDITOR_WIN
private static bool IsCandidateForDiscovery(string path)
{
    return File.Exists(path) && Regex.IsMatch(path, ".*[Qq]oder.*.exe$");
}
#endif
```

**常见安装路径**：

```csharp
var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

var candidates = new List<string>
{
    IOPath.Combine(programFiles, "Qoder", "Qoder.exe"),
    IOPath.Combine(localAppData, "Programs", "Qoder", "Qoder.exe")
};
```

#### 版本信息提取

```csharp
#if UNITY_EDITOR_WIN
var versionInfo = FileVersionInfo.GetVersionInfo(editorPath);
Version.TryParse(versionInfo.ProductVersion, out version);
#endif
```

### 9.3 macOS 平台特性

#### 应用程序包结构

macOS 应用程序是目录（`.app` 后缀），不是单个文件。

```
Qoder.app/
├── Contents/
│   ├── MacOS/
│   │   └── Qoder            # 实际可执行文件
│   ├── Resources/
│   │   └── ...
│   └── Info.plist           # 应用程序信息
```

#### 可执行文件发现

```csharp
#if UNITY_EDITOR_OSX
private static bool IsCandidateForDiscovery(string path)
{
    return Directory.Exists(path) && Regex.IsMatch(path, ".*[Qq]oder.*.app$");
}
#endif
```

**常见安装路径**：

```csharp
var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

var candidates = new List<string>
{
    "/Applications/Qoder.app",
    IOPath.Combine(home, "Applications", "Qoder.app")
};
```

#### 版本信息提取

从 `Info.plist` 读取版本：

```csharp
#if UNITY_EDITOR_OSX
var plistPath = IOPath.Combine(editorPath, "Contents", "Info.plist");
if (File.Exists(plistPath))
{
    var plist = File.ReadAllText(plistPath);
    // 解析 CFBundleShortVersionString 或 CFBundleVersion
    // 示例：<key>CFBundleShortVersionString</key><string>1.0.0</string>
}
#endif
```

### 9.4 Linux 平台特性

#### 可执行文件发现

```csharp
#if !UNITY_EDITOR_WIN && !UNITY_EDITOR_OSX
private static bool IsCandidateForDiscovery(string path)
{
    return File.Exists(path) && Regex.IsMatch(path, ".*[Qq]oder$");
}
#endif
```

**常见安装路径**：

```csharp
var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

var candidates = new List<string>
{
    "/usr/bin/qoder",
    "/usr/local/bin/qoder",
    IOPath.Combine(home, ".qoder", "bin", "qoder"),
    "/snap/bin/qoder"           // Snap 包管理器
};
```

#### 版本信息提取

执行 `qoder --version` 获取版本：

```csharp
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = editorPath,
        Arguments = "--version",
        RedirectStandardOutput = true,
        UseShellExecute = false
    }
};
process.Start();
var output = process.StandardOutput.ReadToEnd();
// 解析输出：Qoder 1.0.0
```

### 9.5 路径处理的跨平台兼容性

#### 路径规范化

```csharp
// FileUtility.cs

public static string NormalizePathSeparators(this string path)
{
    // 将所有路径分隔符统一为当前平台的格式
    return path.Replace('\\', IOPath.DirectorySeparatorChar)
               .Replace('/', IOPath.DirectorySeparatorChar);
}
```

**使用示例**：

```csharp
var path = "Assets/Scripts/PlayerController.cs";  // Unix 风格
var normalized = path.NormalizePathSeparators();
// Windows: Assets\Scripts\PlayerController.cs
// macOS/Linux: Assets/Scripts/PlayerController.cs
```

#### 绝对路径构建

```csharp
// FileUtility.cs

public static string GetAbsolutePath(string relativePath)
{
    return IOPath.GetFullPath(relativePath).NormalizePathSeparators();
}
```

**使用示例**：

```csharp
var projectPath = GetAbsolutePath(Path.Combine(Application.dataPath, ".."));
// Windows: C:\MyUnityProject
// macOS: /Users/username/MyUnityProject
// Linux: /home/username/MyUnityProject
```

---

## 10. 性能优化

### 10.1 大型 Unity 项目的调试性能优化建议

#### 问题 1: 项目文件生成速度慢

**原因**：项目包含数千个 `.cs` 文件，生成 `.csproj` 需要时间。

**优化方案**：

1. **使用 Assembly Definition**：将代码分割为多个小程序集
   ```
   Assets/
   ├── Scripts/
   │   ├── Core/
   │   │   ├── Core.asmdef          # 核心程序集
   │   │   └── ...
   │   ├── Gameplay/
   │   │   ├── Gameplay.asmdef      # 游戏逻辑程序集
   │   │   └── ...
   │   └── UI/
   │       ├── UI.asmdef            # UI 程序集
   │       └── ...
   ```

   **好处**：
   - 减少单个 `.csproj` 的文件数量
   - 提高编译速度（只重新编译修改的程序集）
   - 改善 IntelliSense 响应速度

2. **排除不必要的包**：在 `External Tools` 中取消勾选不需要的包类型
   ```
   ☐ Registry packages    # 通常不需要修改这些包
   ☐ Built-in packages    # Unity 内置包
   ```

#### 问题 2: IntelliSense 响应慢

**原因**：OmniSharp 索引了大量文件，包括生成的代码和第三方库。

**优化方案**：

1. **排除不必要的目录**：在 `.vscode/settings.json` 中配置
   ```json
   {
       "files.exclude": {
           "**/.git": true,
           "**/.DS_Store": true,
           "**/Library": true,
           "**/Temp": true,
           "**/obj": true,
           "**/build": true
       },
       "omnisharp.excludePaths": [
           "**/Library",
           "**/Temp",
           "**/obj"
       ]
   }
   ```

2. **禁用不必要的 Roslyn 分析器**：
   ```json
   {
       "omnisharp.enableRoslynAnalyzers": false,
       "omnisharp.enableEditorConfigSupport": false
   }
   ```

3. **限制 OmniSharp 的内存使用**：
   ```json
   {
       "omnisharp.maxProjectResults": 250,
       "omnisharp.maxFindSymbolsItems": 1000
   }
   ```

#### 问题 3: 调试器附加速度慢

**原因**：调试器需要加载所有程序集的调试符号（`.pdb` 文件）。

**优化方案**：

1. **禁用不需要调试的程序集**：在 `launch.json` 中配置
   ```json
   {
       "configurations": [
           {
               "name": "Attach to Unity Editor",
               "type": "vstuc",
               "request": "attach",
               "justMyCode": true,  // 仅调试自己的代码，忽略第三方库
               "symbolOptions": {
                   "searchPaths": [
                       "${workspaceFolder}/Library/ScriptAssemblies"
                   ],
                   "searchMicrosoftSymbolServer": false  // 不搜索 Microsoft 符号服务器
               }
           }
       ]
   }
   ```

2. **使用增量调试**：不要每次都重新附加调试器
   - 保持调试会话活跃
   - 使用 **热重载**（如果可用）而不是停止/重新附加

#### 问题 4: 断点设置/删除延迟

**原因**：调试器需要与 Unity 通信并同步断点状态。

**优化方案**：

1. **减少断点数量**：避免设置过多断点
   - 使用**条件断点**代替多个普通断点
   - 使用**日志断点**代替频繁的 Debug.Log

2. **使用断点组**：在调试面板中禁用暂时不需要的断点组

### 10.2 网络调试端口优化

#### 问题：多个 Unity 实例导致端口冲突

**原因**：Unity 使用 `56000 + (PID % 1000)` 计算端口，可能冲突。

**优化方案**：

1. **手动指定端口**：在 `launch.json` 中明确指定端口
   ```json
   {
       "configurations": [
           {
               "name": "Attach to Unity Editor (Port 56123)",
               "type": "vstuc",
               "request": "attach",
               "endPoint": "127.0.0.1:56123"
           }
       ]
   }
   ```

2. **使用多个配置**：为不同的 Unity 实例创建不同的配置
   ```json
   {
       "configurations": [
           {
               "name": "Attach to Unity Editor 1",
               "type": "vstuc",
               "request": "attach",
               "endPoint": "127.0.0.1:56100"
           },
           {
               "name": "Attach to Unity Editor 2",
               "type": "vstuc",
               "request": "attach",
               "endPoint": "127.0.0.1:56200"
           }
       ]
   }
   ```

### 10.3 项目文件生成优化

#### 使用 SDK-Style 格式

SDK-Style 项目文件更简洁，解析速度更快。

```csharp
// ProjectGeneration.cs

private static readonly IGenerator _generator = 
    GeneratorFactory.GetInstance(GeneratorStyle.SDK);  // 推荐
```

#### 延迟生成项目文件

避免频繁重新生成项目文件。

```csharp
// VisualStudioEditor.cs

public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, ...)
{
    // 只在必要时生成
    if (addedFiles.Length > 0 || deletedFiles.Length > 0)
    {
        // 延迟 500ms 后生成，避免短时间内多次生成
        EditorApplication.delayCall += () => SyncSolution();
    }
}
```

### 10.4 编辑器启动优化

#### 减少不必要的插件

禁用不需要的 Unity 插件和编辑器扩展，减少启动时间。

#### 使用轻量级编辑器配置

在 Qoder/VS Code 中禁用不需要的扩展。

**VS Code 工作区推荐扩展**：`.vscode/extensions.json`

```json
{
    "recommendations": [
        "ms-dotnettools.csharp",           // C# 支持（必需）
        "unity.unity-debug"                // Unity 调试器（必需）
    ],
    "unwantedRecommendations": [
        "ms-vscode.vscode-typescript-tslint-plugin"  // Unity 不需要
    ]
}
```

### 10.5 调试会话管理

#### 使用 preLaunchTask

在附加调试器前自动执行任务（如清理缓存）。

```json
{
    "configurations": [
        {
            "name": "Attach to Unity Editor",
            "type": "vstuc",
            "request": "attach",
            "preLaunchTask": "clear-temp"
        }
    ]
}
```

**对应的 tasks.json**：

```json
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "clear-temp",
            "type": "shell",
            "command": "rm",
            "args": ["-rf", "${workspaceFolder}/Temp"],
            "problemMatcher": []
        }
    ]
}
```

---

## 附录 A: 故障排除指南

### A.1 调试器无法附加

#### 症状

点击 **Start Debugging** 后，显示错误：

```
Unable to attach to Unity. Make sure Unity is running and debugging is enabled.
```

#### 可能原因

1. Unity 未在运行
2. Unity 未进入播放模式
3. 调试端口不正确
4. 防火墙阻止连接

#### 解决步骤

1. **确认 Unity 在运行并进入播放模式**：
   - 打开 Unity Editor
   - 点击 **Play** 按钮

2. **查找正确的调试端口**：
   ```csharp
   // 在 Unity 控制台中运行此代码（通过 Test Runner 或临时脚本）
   Debug.Log($"Debug Port: {56000 + (System.Diagnostics.Process.GetCurrentProcess().Id % 1000)}");
   ```

3. **更新 launch.json**：
   ```json
   {
       "endPoint": "127.0.0.1:<刚才查询到的端口>"
   }
   ```

4. **检查防火墙**：
   - Windows: `控制面板 > 系统和安全 > Windows Defender 防火墙 > 允许应用`
   - 确保 Unity 和 Qoder/VS Code 被允许

### A.2 断点不生效

#### 症状

设置断点后，调试器附加成功，但断点显示为灰色（未绑定）。

#### 可能原因

1. 代码未重新编译
2. 调试符号（`.pdb`）缺失或过期
3. 代码优化导致断点位置失效

#### 解决步骤

1. **重新编译代码**：
   - 在 Unity 中修改并保存脚本
   - 等待编译完成（Unity 控制台右下角显示 "Compiling"）

2. **检查调试符号**：
   ```bash
   # 检查 Library/ScriptAssemblies/ 目录是否有 .pdb 文件
   ls Library/ScriptAssemblies/*.pdb
   ```

3. **禁用代码优化**：
   - `Edit > Project Settings > Player > Other Settings`
   - 确保 **Optimization** 设置为 **Debug** 模式

### A.3 IntelliSense 不显示 Unity API

#### 症状

输入 `transform.` 后没有智能提示。

#### 可能原因

1. 项目文件未生成或过期
2. OmniSharp 未正确加载 Unity 程序集引用

#### 解决步骤

1. **重新生成项目文件**：
   - Unity: `Edit > Preferences > External Tools`
   - 点击 **Regenerate project files**

2. **检查 .csproj 文件**：
   ```bash
   # 查找 UnityEngine 引用
   grep "UnityEngine" *.csproj
   ```

   应该看到：
   ```xml
   <Reference Include="UnityEngine">
     <HintPath>C:\Program Files\Unity\Editor\Data\Managed\UnityEngine.dll</HintPath>
   </Reference>
   ```

3. **重启 OmniSharp**：
   - `Ctrl+Shift+P` → `OmniSharp: Restart OmniSharp`

### A.4 编辑器无法打开脚本

#### 症状

双击 Unity 中的脚本文件，编辑器没有打开。

#### 可能原因

1. 外部编辑器路径配置错误
2. 编辑器可执行文件不存在

#### 解决步骤

1. **检查外部编辑器设置**：
   - Unity: `Edit > Preferences > External Tools`
   - 查看 **External Script Editor** 是否正确选择

2. **手动选择编辑器**：
   - 点击 **Browse...**
   - 选择 Qoder 可执行文件（如 `C:\Program Files\Qoder\Qoder.exe`）

3. **检查编辑器是否可运行**：
   ```bash
   # Windows
   & "C:\Program Files\Qoder\Qoder.exe" --version

   # macOS/Linux
   /Applications/Qoder.app/Contents/MacOS/Qoder --version
   ```

### A.5 项目文件频繁重新生成

#### 症状

每次打开 Unity 或修改脚本，`.sln` 和 `.csproj` 文件都会重新生成。

#### 可能原因

1. 项目文件被 Git 忽略（不应该被忽略）
2. Unity 检测到文件系统变化

#### 解决步骤

1. **不要忽略项目文件**：
   在 `.gitignore` 中**不要**添加：
   ```gitignore
   *.csproj   # 不要忽略！
   *.sln      # 不要忽略！
   ```

   应该添加（忽略 Unity 生成的临时文件）：
   ```gitignore
   /Library/
   /Temp/
   /obj/
   /Logs/
   ```

2. **提交项目文件到版本控制**：
   ```bash
   git add *.csproj *.sln
   git commit -m "Add project files"
   ```

---

## 附录 B: 最佳实践

### B.1 项目结构最佳实践

#### 使用 Assembly Definition 组织代码

```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── Core.asmdef
│   │   ├── GameManager.cs
│   │   └── DataManager.cs
│   ├── Gameplay/
│   │   ├── Gameplay.asmdef (references: Core)
│   │   ├── PlayerController.cs
│   │   └── EnemyAI.cs
│   ├── UI/
│   │   ├── UI.asmdef (references: Core)
│   │   └── MenuController.cs
│   └── Editor/
│       ├── Editor.asmdef (Editor-only)
│       └── CustomInspector.cs
```

**好处**：
- 更快的编译速度（只重新编译修改的程序集）
- 更清晰的依赖关系
- 更好的 IntelliSense 性能

#### 使用 .editorconfig 统一代码风格

在项目根目录创建 `.editorconfig`：

```ini
# EditorConfig: https://editorconfig.org

root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true

[*.cs]
indent_style = space
indent_size = 4

# C# 代码风格
csharp_new_line_before_open_brace = all
csharp_indent_case_contents = true
csharp_space_after_cast = false
```

### B.2 调试最佳实践

#### 使用条件断点

避免频繁中断执行：

```csharp
void Update()
{
    health -= Time.deltaTime;  // 条件断点: health < 10
}
```

#### 使用日志断点

记录信息而不暂停执行：

```csharp
void TakeDamage(int amount)
{
    health -= amount;  // 日志断点: "Damage: {amount}, Health: {health}"
}
```

#### 使用 Watch 表达式

监视复杂表达式：

```
Watch:
├─ player.transform.position.magnitude  // 玩家到原点的距离
├─ enemies.Where(e => e.health > 0).Count()  // 存活的敌人数量
└─ Physics.Raycast(transform.position, Vector3.down, 1f)  // 是否在地面
```

### B.3 性能调试最佳实践

#### 使用 Unity Profiler

1. 打开 Profiler：`Window > Analysis > Profiler`
2. 启用 **Deep Profiling** 查看所有函数调用
3. 找出性能瓶颈（CPU、内存、渲染）

#### 使用 Frame Debugger

1. 打开 Frame Debugger：`Window > Analysis > Frame Debugger`
2. 逐帧查看渲染流程
3. 找出过度绘制和不必要的 Draw Call

### B.4 版本控制最佳实践

#### .gitignore 配置

```gitignore
# Unity 生成的文件（应该忽略）
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/

# Unity 项目文件（不应该忽略）
# *.csproj   # 不要取消注释！
# *.sln      # 不要取消注释！

# 编辑器配置（应该提交）
.vscode/settings.json
.vscode/launch.json

# 用户特定配置（应该忽略）
.vscode/*.log
```

#### 提交项目文件

```bash
git add *.csproj *.sln .vscode/
git commit -m "Add project and editor configuration"
```

### B.5 团队协作最佳实践

#### 共享编辑器配置

在 `.vscode/settings.json` 中配置团队共享的设置：

```json
{
    "files.exclude": {
        "**/.git": true,
        "**/Library": true,
        "**/Temp": true
    },
    "omnisharp.useModernNet": true,
    "omnisharp.enableRoslynAnalyzers": true
}
```

#### 使用工作区推荐扩展

在 `.vscode/extensions.json` 中推荐必要的扩展：

```json
{
    "recommendations": [
        "ms-dotnettools.csharp",
        "unity.unity-debug",
        "kleber-swf.unity-code-snippets"
    ]
}
```

团队成员打开项目时，编辑器会提示安装这些扩展。

---

## 结语

本文档详细介绍了 Qoder 和 VS Code 在 Unity 开发中的调试集成技术，涵盖了从基础配置到高级优化的所有方面。

**关键要点总结**：

1. **调试功能边界**：Unity 集成包只负责生成配置和启动编辑器，真正的调试功能由编辑器扩展提供
2. **DAP 协议**：Qoder 和 VS Code 都使用 Debug Adapter Protocol 与 Unity 的 Mono Soft Debugger 通信
3. **项目文件生成**：`.sln` 和 `.csproj` 文件是 IntelliSense 的基础，需要正确生成和维护
4. **跨平台兼容性**：通过路径规范化和条件编译实现 Windows/macOS/Linux 平台支持
5. **性能优化**：使用 Assembly Definition、排除不必要的文件、优化调试器配置

**参考资源**：

- Unity 官方文档：https://docs.unity3d.com/Manual/ScriptingTools.html
- DAP 协议规范：https://microsoft.github.io/debug-adapter-protocol/
- OmniSharp 文档：https://www.omnisharp.net/
- Qoder 官网：https://qoder.com/

**项目仓库**：

- com.unity.ide.qoder：https://github.com/lalanbv/com.unity.ide.qoder
- com.unity.ide.visualstudio：https://github.com/needle-mirror/com.unity.ide.visualstudio
- com.unity.ide.cursor：https://github.com/boxqkrtm/com.unity.ide.cursor

---

**文档版本**: 1.0.0  
**最后更新**: 2026-01-16  
**适用于**: Unity 2019.4+, Qoder 1.0+, VS Code 1.85+

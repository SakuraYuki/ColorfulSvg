# ColorfulSvg

将 SVG 文件、SVG 目录或 SVG 文本转换为 WPF 可直接使用的 `ResourceDictionary` XAML 资源。

当前实现基于 `SharpVectors.Wpf`，输出项为 `DrawingImage`，适合通过 `StaticResource` 绑定到 `Image.Source`。

除了核心转换能力，仓库还提供：

- `ColorfulSvg.Demo`：演示 `DrawingImage` 在 WPF 中的使用方式
- `ColorfulSvg.Cli`：命令行批量转换工具
- `ColorfulSvg.IconSearcher`：图标搜索与导出工具，适合从 yesicon 浏览、挑选并导出图标资源

## Build

```powershell
dotnet build .\ColorfulSvg.slnx
```

## Demo

```powershell
dotnet run --project .\ColorfulSvg.Demo
```

Demo 工程会在 `App.xaml` 中合并 `Resources/DemoResources.xaml`，并展示：

- `StaticResource` 直接绑定到 `Image.Source`
- `DynamicResource` 在界面中的使用方式
- 将 `DrawingImage` 作为 ViewModel 数据绑定到列表
- 选择单个 SVG 文件并调用核心库立即转换、落盘并预览

## IconSearcher

```powershell
dotnet run --project .\ColorfulSvg.IconSearcher
```

`IconSearcher` 是一个面向 WPF 资源导出的桌面工具，主要功能包括：

- 内嵌 yesicon 浏览器，直接搜索、浏览和预览图标
- 按住 `Ctrl` 可开启快速选取模式，直接点击图标加入导出列表
- 右侧导出工作区支持修改资源键、移除图标、批量导出
- 支持导出新文件或追加到现有 XAML 资源字典
- 会记住上次浏览到的 yesicon 页面，下次启动自动恢复
- 菜单中的“浏览导出 XAML...”可以打开已导出的资源文件，以宫格方式查看图标并点击复制 `Key`

推荐使用方式：

1. 在顶部输入关键词并打开 yesicon 搜索页。
2. 普通点击图标进入详情页，确认右侧预览内容。
3. 按住 `Ctrl` 直接点图标，或点击“添加当前图标”加入导出列表。
4. 在工作区中调整资源键、选择导出模式和输出文件。
5. 点击“导出选中资源”生成可直接用于 WPF 的 `ResourceDictionary`。

## CLI

```powershell
dotnet run --project .\ColorfulSvg.Cli -- --file .\icon.svg --out .\IconResources.xaml
dotnet run --project .\ColorfulSvg.Cli -- --dir .\icons --out .\IconResources.xaml
dotnet run --project .\ColorfulSvg.Cli -- --svg "<svg viewBox='0 0 16 16' xmlns='http://www.w3.org/2000/svg'><path fill='#FF5722' d='M2 2h12v12H2z'/></svg>" --key Square --out .\InlineResources.xaml
```

## WPF Usage

```xml
<ResourceDictionary Source="IconResources.xaml" />
```

```xml
<Image Width="24"
       Height="24"
       Source="{StaticResource folder/open}" />
```

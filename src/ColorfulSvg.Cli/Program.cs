using ColorfulSvg.Core;

return await ProgramEntry.RunAsync(args);

internal static class ProgramEntry
{
    public static Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = CliArguments.Parse(args);
            if (options.ShowHelp)
            {
                Console.WriteLine(CliArguments.HelpText);
                return Task.FromResult(0);
            }

            var converter = new SvgResourceConverter();
            var conversionOptions = new SvgConversionOptions
            {
                BaseDirectory = options.BaseDirectory,
                ContinueOnError = true
            };

            var result = options.Mode switch
            {
                InputMode.File => converter.ConvertFile(options.Input!, options.Key, conversionOptions),
                InputMode.Directory => converter.ConvertDirectory(options.Input!, conversionOptions),
                InputMode.Content => converter.ConvertContent(options.Input!, options.Key!, conversionOptions),
                _ => throw new InvalidOperationException("Unsupported input mode.")
            };

            converter.SaveResult(result, options.OutputPath!);

            Console.WriteLine($"Wrote {result.Resources.Count} resource(s) to '{Path.GetFullPath(options.OutputPath!)}'.");
            foreach (var resource in result.Resources)
            {
                Console.WriteLine($"  {resource.Key} <= {resource.SourceName}");
            }

            foreach (var issue in result.Issues)
            {
                Console.Error.WriteLine($"{(issue.IsError ? "ERROR" : "WARN")} [{issue.Scope}] {issue.Message}");
            }

            return Task.FromResult(result.HasErrors ? 1 : 0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliArguments.HelpText);
            return Task.FromResult(1);
        }
    }
}

internal enum InputMode
{
    None,
    File,
    Directory,
    Content
}

internal sealed class CliArguments
{
    public static string HelpText =>
"""
Usage:
  ColorfulSvg.Cli --file <path> --out <path> [--key <resourceKey>]
  ColorfulSvg.Cli --dir <path> --out <path>
  ColorfulSvg.Cli --svg <content> --key <resourceKey> --out <path> [--base-dir <path>]

Options:
  --file <path>       Convert a single SVG file.
  --dir <path>        Convert all .svg files in a directory recursively.
  --svg <content>     Convert inline SVG content.
  --key <name>        Resource key for inline SVG content or single-file override.
  --out <path>        Output ResourceDictionary XAML path.
  --base-dir <path>   Base directory used to resolve relative references for inline SVG content.
  --help              Show this help text.
""";

    public InputMode Mode { get; private init; }

    public string? Input { get; private init; }

    public string? OutputPath { get; private init; }

    public string? Key { get; private init; }

    public string? BaseDirectory { get; private init; }

    public bool ShowHelp { get; private init; }

    public static CliArguments Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliArguments { ShowHelp = true };
        }

        string? file = null;
        string? directory = null;
        string? svg = null;
        string? key = null;
        string? output = null;
        string? baseDirectory = null;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--file":
                    file = ReadValue(args, ref index, arg);
                    break;
                case "--dir":
                    directory = ReadValue(args, ref index, arg);
                    break;
                case "--svg":
                    svg = ReadValue(args, ref index, arg);
                    break;
                case "--key":
                    key = ReadValue(args, ref index, arg);
                    break;
                case "--out":
                    output = ReadValue(args, ref index, arg);
                    break;
                case "--base-dir":
                    baseDirectory = ReadValue(args, ref index, arg);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        if (showHelp)
        {
            return new CliArguments { ShowHelp = true };
        }

        var selectedInputs = new[]
        {
            (Mode: InputMode.File, Value: file),
            (Mode: InputMode.Directory, Value: directory),
            (Mode: InputMode.Content, Value: svg)
        }.Where(static item => !string.IsNullOrWhiteSpace(item.Value)).ToArray();

        if (selectedInputs.Length != 1)
        {
            throw new ArgumentException("Specify exactly one of --file, --dir, or --svg.");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new ArgumentException("Missing required argument: --out.");
        }

        var selected = selectedInputs[0];
        if (selected.Mode == InputMode.Content && string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Inline SVG conversion requires --key.");
        }

        return new CliArguments
        {
            Mode = selected.Mode,
            Input = selected.Value,
            OutputPath = output,
            Key = key,
            BaseDirectory = baseDirectory
        };
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        var valueIndex = index + 1;
        if (valueIndex >= args.Length)
        {
            throw new ArgumentException($"Missing value for {optionName}.");
        }

        index = valueIndex;
        return args[valueIndex];
    }
}

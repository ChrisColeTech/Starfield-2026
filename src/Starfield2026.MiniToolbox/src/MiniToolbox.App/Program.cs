using MiniToolbox.App.Commands;

// Parse command
if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

string command = args[0].ToLowerInvariant();
string[] commandArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();

return command switch
{
    "trpak" => TrpakCommand.Run(commandArgs),
    "gdb1" => Gdb1Command.Run(commandArgs),
    "garc" => GarcCommand.Run(commandArgs),
    "text" => TextDecodeCommand.Run(commandArgs),
    "help" or "--help" or "-h" => PrintUsage(),
    "version" or "--version" or "-v" => PrintVersion(),
    _ => HandleLegacyArgs(args)
};

int PrintUsage()
{
    Console.WriteLine("MiniToolbox - Game asset extraction utility");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  minitoolbox <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  trpak    Extract from Pokemon Scarlet/Violet archives (.trpfs/.trpak)");
    Console.WriteLine("  gdb1     Extract from Star Fox Zero/Guard resources (.modelgdb/.texturegdb)");
    Console.WriteLine("  garc     Extract from 3DS Pokemon GARC archives (Sun/Moon, X/Y)");
    Console.WriteLine("  text     Decode Sun/Moon game text files (XOR encrypted)");
    Console.WriteLine("  help     Show this help message");
    Console.WriteLine("  version  Show version information");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  minitoolbox trpak --arc ./romfs --list");
    Console.WriteLine("  minitoolbox trpak --arc ./romfs --model pokemon/pm0025/pm0025_00.trmdl -o ./pikachu");
    Console.WriteLine("  minitoolbox gdb1 --input ./Resources --scan");
    Console.WriteLine("  minitoolbox gdb1 --input ./Resources --all -o ./output");
    Console.WriteLine();
    Console.WriteLine("Output Formats:");
    Console.WriteLine("  -f obj   Wavefront OBJ (default for GDB1)");
    Console.WriteLine("  -f dae   COLLADA DAE (default for TRPAK)");
    Console.WriteLine();
    Console.WriteLine("Animation Modes:");
    Console.WriteLine("  --split  Export animations as separate clip files (default)");
    Console.WriteLine("  --baked  Export animations embedded in model files");
    Console.WriteLine();
    Console.WriteLine("Run 'minitoolbox <command> --help' for command-specific help.");
    return 0;
}

int PrintVersion()
{
    Console.WriteLine("MiniToolbox v1.0.0");
    Console.WriteLine("Supported formats:");
    Console.WriteLine("  - TRPAK (Pokemon Scarlet/Violet) -> DAE, OBJ");
    Console.WriteLine("  - GDB1 (Star Fox Zero/Guard) -> OBJ");
    Console.WriteLine("  - GARC (3DS Pokemon Sun/Moon, X/Y) -> DAE, OBJ");
    return 0;
}

// Handle legacy --arc style arguments for backwards compatibility
int HandleLegacyArgs(string[] args)
{
    // Check if this looks like legacy TRPAK args
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i].Equals("--arc", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("NOTE: Using legacy argument style. Consider using 'minitoolbox trpak --arc ...' instead.");
            return TrpakCommand.Run(args);
        }
    }

    Console.Error.WriteLine($"Unknown command: {args[0]}");
    Console.Error.WriteLine("Run 'minitoolbox help' for usage information.");
    return 1;
}

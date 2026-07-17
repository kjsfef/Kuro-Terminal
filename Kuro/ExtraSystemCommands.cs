using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;

namespace Kuro;

public sealed partial class ShellEngine
{
    private void RegisterExtraSystemCommands()
    {
        RegisterMegaProductivityCommands();
        RegisterMegaResearchCommands();
        RegisterCommunityToolHubCommands();

        Register("now", "System+", "now", "Show local and UTC date/time together.", NowCommand, "datetime");
        Register("calendar", "System+", "calendar [month] [year]", "Draw a monthly calendar.", CalendarCommand, "cal");
        Register("week", "System+", "week", "Show the current ISO week number.", WeekCommand);
        Register("timezone", "System+", "timezone", "Show local time-zone information.", TimeZoneCommand, "tz");
        Register("culture", "System+", "culture", "Show current language and formatting culture.", CultureCommand, "locale");
        Register("cpu", "System+", "cpu", "Show processor architecture and core counts.", CpuCommand);
        Register("machine", "System+", "machine", "Show machine and process architecture details.", MachineCommand);
        Register("admin", "System+", "admin", "Show whether Kuro is elevated.", (_, _) => Ok(IsAdministrator ? "administrator" : "standard user"), "isadmin");
        Register("pid", "System+", "pid", "Show Kuro's process ID.", (_, _) => Ok(Environment.ProcessId.ToString(CultureInfo.InvariantCulture)));
        Register("appdir", "System+", "appdir", "Show the running Kuro application directory.", AppDirectoryCommand);
        Register("shellpath", "System+", "shellpath", "Show the running Kuro executable path.", (_, _) => Ok(Environment.ProcessPath ?? "unknown"));
        Register("specialfolders", "System+", "specialfolders", "List useful Windows special folders.", SpecialFoldersCommand);
        Register("pathlist", "System+", "pathlist", "List every directory in the Windows PATH.", PathListCommand);
        Register("homepath", "System+", "homepath", "Show the current user's home directory.", (_, _) => Ok(HomeDirectory));
        Register("downloads", "System+", "downloads", "Change to the user's Downloads folder.", (_, _) => ChangeToSpecialFolder(Path.Combine(HomeDirectory, "Downloads"), "downloads"));
        Register("desktop", "System+", "desktop", "Change to the user's Desktop folder.", (_, _) => ChangeToSpecialFolder(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "desktop"));
        Register("documents", "System+", "documents", "Change to the user's Documents folder.", (_, _) => ChangeToSpecialFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "documents"));
        Register("clipboard", "System+", "clipboard [get|set <text>|clear]", "Read or update the Windows clipboard.", ClipboardCommand, "clip");
        Register("screen", "System+", "screen", "Show the primary desktop dimensions.", ScreenCommand);
        Register("beep", "System+", "beep [frequency] [milliseconds]", "Play a short bounded system beep.", BeepCommand);
        Register("wait", "System+", "wait <milliseconds>", "Pause Kuro for up to five seconds.", WaitCommand, "sleep");
        Register("gc", "System+", "gc", "Request a .NET garbage collection and show memory change.", GarbageCollectCommand);
        Register("timeit", "System+", "timeit <program> [arguments]", "Run an external program and report elapsed time.", TimeExternalCommand);
        Register("openconfig", "System+", "openconfig", "Open Kuro's configuration folder.", OpenConfigCommand);
        Register("configdir", "System+", "configdir", "Show Kuro's configuration directory.", (_, _) => Ok(ShellConfig.ConfigDirectory));
        Register("envsize", "System+", "envsize", "Show counts for environment variables and PATH entries.", EnvironmentSizeCommand);
    }

    private ShellResult NowCommand(string[] args, string raw)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        return Ok($"Local: {now:dddd, MMMM d, yyyy HH:mm:ss zzz}\nUTC:   {now.UtcDateTime:dddd, MMMM d, yyyy HH:mm:ss 'UTC'}\nUnix:  {now.ToUnixTimeSeconds()}");
    }

    private ShellResult CalendarCommand(string[] args, string raw)
    {
        int month = DateTime.Now.Month;
        int year = DateTime.Now.Year;
        if (args.Length >= 1 && (!int.TryParse(args[0], out month) || month is < 1 or > 12))
            return Error("calendar: month must be from 1 to 12");
        if (args.Length >= 2 && (!int.TryParse(args[1], out year) || year is < 1 or > 9999))
            return Error("calendar: year must be from 1 to 9999");

        DateTime first = new(year, month, 1);
        int days = DateTime.DaysInMonth(year, month);
        StringBuilder output = new();
        string title = first.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        output.AppendLine(title.PadLeft((20 + title.Length) / 2));
        output.AppendLine("Su Mo Tu We Th Fr Sa");
        output.Append(new string(' ', (int)first.DayOfWeek * 3));
        for (int day = 1; day <= days; day++)
        {
            output.Append(day.ToString(CultureInfo.InvariantCulture).PadLeft(2));
            bool endOfWeek = ((int)first.DayOfWeek + day) % 7 == 0;
            output.Append(endOfWeek ? Environment.NewLine : " ");
        }
        return Ok(output.ToString().TrimEnd());
    }

    private ShellResult WeekCommand(string[] args, string raw)
    {
        DateTime today = DateTime.Today;
        int week = ISOWeek.GetWeekOfYear(today);
        return Ok($"{today:yyyy-MM-dd} is ISO week {week} of {ISOWeek.GetYear(today)}.");
    }

    private ShellResult TimeZoneCommand(string[] args, string raw)
    {
        TimeZoneInfo zone = TimeZoneInfo.Local;
        return Ok($"ID:            {zone.Id}\nDisplay name:  {zone.DisplayName}\nStandard name: {zone.StandardName}\nDaylight name: {zone.DaylightName}\nUTC offset:    {zone.GetUtcOffset(DateTime.Now)}\nDST now:       {zone.IsDaylightSavingTime(DateTime.Now)}");
    }

    private ShellResult CultureCommand(string[] args, string raw)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo ui = CultureInfo.CurrentUICulture;
        return Ok($"Culture:       {culture.Name} ({culture.DisplayName})\nUI culture:    {ui.Name} ({ui.DisplayName})\nDate pattern:  {culture.DateTimeFormat.ShortDatePattern}\nTime pattern:  {culture.DateTimeFormat.ShortTimePattern}\nNumber format: {culture.NumberFormat.NumberDecimalSeparator}");
    }

    private ShellResult CpuCommand(string[] args, string raw) =>
        Ok($"Logical processors: {Environment.ProcessorCount}\nOS architecture:    {RuntimeInformation.OSArchitecture}\nProcess architecture:{RuntimeInformation.ProcessArchitecture}\n64-bit OS:           {Environment.Is64BitOperatingSystem}\n64-bit process:      {Environment.Is64BitProcess}");

    private ShellResult MachineCommand(string[] args, string raw) =>
        Ok($"Machine:       {Environment.MachineName}\nUser domain:   {Environment.UserDomainName}\nUser:          {Environment.UserName}\nOS:            {RuntimeInformation.OSDescription}\nFramework:     {RuntimeInformation.FrameworkDescription}\nPage size:     {Environment.SystemPageSize:n0} bytes");

    private ShellResult AppDirectoryCommand(string[] args, string raw)
    {
        string? path = Environment.ProcessPath;
        return Ok(path is null ? AppContext.BaseDirectory : Path.GetDirectoryName(path) ?? AppContext.BaseDirectory);
    }

    private ShellResult SpecialFoldersCommand(string[] args, string raw)
    {
        (string Name, Environment.SpecialFolder Folder)[] folders =
        [
            ("Home", Environment.SpecialFolder.UserProfile),
            ("Desktop", Environment.SpecialFolder.DesktopDirectory),
            ("Documents", Environment.SpecialFolder.MyDocuments),
            ("Pictures", Environment.SpecialFolder.MyPictures),
            ("Music", Environment.SpecialFolder.MyMusic),
            ("Videos", Environment.SpecialFolder.MyVideos),
            ("ApplicationData", Environment.SpecialFolder.ApplicationData),
            ("LocalApplicationData", Environment.SpecialFolder.LocalApplicationData),
            ("ProgramFiles", Environment.SpecialFolder.ProgramFiles),
            ("Windows", Environment.SpecialFolder.Windows),
            ("System", Environment.SpecialFolder.System)
        ];
        return Ok(string.Join(Environment.NewLine,
            folders.Select(item => $"{item.Name,-22} {Environment.GetFolderPath(item.Folder)}")) +
            $"\n{"Downloads",-22} {Path.Combine(HomeDirectory, "Downloads")}");
    }

    private ShellResult PathListCommand(string[] args, string raw)
    {
        string[] entries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Ok(string.Join(Environment.NewLine, entries.Select((entry, index) => $"{index + 1,3}. {entry}")));
    }

    private ShellResult ChangeToSpecialFolder(string path, string command)
    {
        if (!Directory.Exists(path))
            return Error($"{command}: folder not found: {path}");
        SetCurrentDirectory(path);
        return Ok(path);
    }

    private ShellResult ClipboardCommand(string[] args, string raw)
    {
        string action = args.Length == 0 ? "get" : args[0].ToLowerInvariant();
        try
        {
            switch (action)
            {
                case "get":
                    return Clipboard.ContainsText() ? Ok(Clipboard.GetText()) : Ok("Clipboard does not contain text.");
                case "set":
                    if (args.Length < 2)
                        return Error("clipboard: use clipboard set <text>");
                    Clipboard.SetText(string.Join(' ', args.Skip(1)));
                    return Ok("Clipboard text updated.");
                case "clear":
                    Clipboard.Clear();
                    return Ok("Clipboard cleared.");
                default:
                    return Error("clipboard: use get, set, or clear");
            }
        }
        catch (Exception ex)
        {
            return Error($"clipboard: {ex.Message}");
        }
    }

    private ShellResult ScreenCommand(string[] args, string raw) =>
        Ok($"Primary width:  {SystemParameters.PrimaryScreenWidth:0}\nPrimary height: {SystemParameters.PrimaryScreenHeight:0}\nVirtual width:  {SystemParameters.VirtualScreenWidth:0}\nVirtual height: {SystemParameters.VirtualScreenHeight:0}");

    private ShellResult BeepCommand(string[] args, string raw)
    {
        int frequency = 700;
        int duration = 140;
        if (args.Length > 0 && int.TryParse(args[0], out int parsedFrequency))
            frequency = Math.Clamp(parsedFrequency, 37, 32767);
        if (args.Length > 1 && int.TryParse(args[1], out int parsedDuration))
            duration = Math.Clamp(parsedDuration, 20, 1500);
        try
        {
            Console.Beep(frequency, duration);
            return ShellResult.Empty;
        }
        catch (PlatformNotSupportedException)
        {
            return Error("beep: not supported on this system");
        }
    }

    private ShellResult WaitCommand(string[] args, string raw)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int milliseconds))
            return Error("wait: enter milliseconds");
        milliseconds = Math.Clamp(milliseconds, 0, 5000);
        Thread.Sleep(milliseconds);
        return Ok($"Waited {milliseconds} ms.");
    }

    private ShellResult GarbageCollectCommand(string[] args, string raw)
    {
        long before = GC.GetTotalMemory(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long after = GC.GetTotalMemory(true);
        return Ok($"Managed memory before: {FormatSize(before)}\nManaged memory after:  {FormatSize(after)}\nDifference:            {before - after:n0} bytes");
    }

    private ShellResult TimeExternalCommand(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("timeit: missing external program");
        Stopwatch timer = Stopwatch.StartNew();
        ShellResult result = RunExternalExplicit(args);
        timer.Stop();
        return new ShellResult(result.Output + $"\n\nElapsed: {timer.Elapsed.TotalMilliseconds:0.###} ms", result.IsError);
    }

    private ShellResult OpenConfigCommand(string[] args, string raw)
    {
        Directory.CreateDirectory(ShellConfig.ConfigDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", QuoteArgument(ShellConfig.ConfigDirectory)) { UseShellExecute = true });
        return ShellResult.Empty;
    }

    private ShellResult EnvironmentSizeCommand(string[] args, string raw)
    {
        int variables = Environment.GetEnvironmentVariables().Count;
        int paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Length;
        return Ok($"Environment variables: {variables}\nPATH entries:          {paths}\nKuro variables:        {Config.Variables.Count}");
    }
}

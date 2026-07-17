using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Kuro;

public sealed partial class ShellEngine
{
    private void RegisterSystemCommands()
    {
        Register("date", "System", "date", "Show the current date.", (_, _) => Ok(DateTime.Now.ToString("dddd, MMMM d, yyyy")));
        Register("time", "System", "time", "Show the current local time.", (_, _) => Ok(DateTime.Now.ToString("HH:mm:ss.fff")));
        Register("whoami", "System", "whoami", "Show your Windows username.", (_, _) => Ok(Environment.UserName));
        Register("hostname", "System", "hostname", "Show the computer name.", (_, _) => Ok(Environment.MachineName));
        Register("uptime", "System", "uptime", "Show Windows uptime.", Uptime);
        Register("sysinfo", "System", "sysinfo", "Show detailed runtime and system information.", SystemInfo, "fetch", "kurofetch", "neofetch", "arch");
        Register("os", "System", "os", "Show operating-system information.", OperatingSystemInfo);
        Register("memory", "System", "memory", "Show Kuro process memory information.", MemoryInfo, "mem");
        Register("processes", "System", "processes [filter]", "List running processes.", Processes, "ps");
        Register("kill", "System", "kill <pid>", "Stop a process by numeric ID.", KillProcess);
        Register("open", "System", "open [path|url]", "Open a path or URL with Windows.", OpenTarget, "start");
        Register("explorer", "System", "explorer [path]", "Open a folder in File Explorer.", OpenExplorer);
        Register("edit", "System", "edit <file>", "Open a file in Notepad.", EditFile);
        Register("exec", "System", "exec <program> [arguments]", "Run a console program and capture its output.", ExecuteProgram, "run");
        Register("netinfo", "Network", "netinfo", "Show active network adapters and addresses.", NetworkInfo, "ip");
        Register("ping", "Network", "ping <host> [count]", "Send up to ten ICMP echo requests.", PingHost);
        Register("dns", "Network", "dns <host>", "Resolve a host name to IP addresses.", ResolveDns, "resolve");
        Register("ports", "Network", "ports", "List active TCP listeners and connections.", Ports);
    }

    private ShellResult Uptime(string[] args, string raw)
    {
        TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return Ok($"{(int)uptime.TotalDays} days, {uptime.Hours} hours, {uptime.Minutes} minutes, {uptime.Seconds} seconds");
    }

    private ShellResult SystemInfo(string[] args, string raw)
    {
        using Process process = Process.GetCurrentProcess();
        TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        string admin = IsAdministrator ? "yes" : "no";

        StringBuilder output = new();
        output.AppendLine($"          _..._               {UserName}@kuro");
        output.AppendLine("       .-'     '-.            -----------------------------");
        output.AppendLine($"     .'  .-.-.    '.          shell        Kuro {Version}");
        output.AppendLine($"    /   /     \\     \\         builder      {Builder}");
        output.AppendLine($"   |   |       |     |        user         {Environment.UserName}");
        output.AppendLine($"    \\   \\     /     /         host         {Environment.MachineName}");
        output.AppendLine($"     '.  '---'    .'          os           {RuntimeInformation.OSDescription}");
        output.AppendLine($"       '-._____.-'            architecture {RuntimeInformation.OSArchitecture}");
        output.AppendLine($"                              runtime      .NET {Environment.Version}");
        output.AppendLine($"                              cpu cores    {Environment.ProcessorCount}");
        output.AppendLine($"                              uptime       {(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m");
        output.AppendLine($"                              process RAM  {FormatSize(process.WorkingSet64)}");
        output.AppendLine($"                              admin        {admin}");
        output.AppendLine($"                              theme        {Config.Theme}");
        output.AppendLine($"                              commands     {CommandCount}");
        output.AppendLine($"                              directory    {CurrentDirectory}");
        return Ok(output.ToString().TrimEnd());
    }

    private ShellResult OperatingSystemInfo(string[] args, string raw) =>
        Ok($"Description:  {RuntimeInformation.OSDescription}\nArchitecture: {RuntimeInformation.OSArchitecture}\nFramework:    {RuntimeInformation.FrameworkDescription}\nProcess arch: {RuntimeInformation.ProcessArchitecture}\n64-bit OS:    {Environment.Is64BitOperatingSystem}\n64-bit app:   {Environment.Is64BitProcess}");

    private ShellResult MemoryInfo(string[] args, string raw)
    {
        Process process = Process.GetCurrentProcess();
        GCMemoryInfo gc = GC.GetGCMemoryInfo();
        return Ok($"Working set:       {FormatSize(process.WorkingSet64)}\nPrivate memory:    {FormatSize(process.PrivateMemorySize64)}\nVirtual memory:    {FormatSize(process.VirtualMemorySize64)}\nManaged heap:      {FormatSize(GC.GetTotalMemory(false))}\nAvailable to .NET: {FormatSize(gc.TotalAvailableMemoryBytes)}");
    }

    private ShellResult Processes(string[] args, string raw)
    {
        string filter = args.Length > 0 ? args[0] : string.Empty;
        Process[] processes = Process.GetProcesses()
            .Where(p => string.IsNullOrEmpty(filter) || p.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(SafeWorkingSet)
            .Take(150)
            .ToArray();

        StringBuilder output = new();
        output.AppendLine($"{"PID",7}  {"MEMORY",10}  NAME");
        output.AppendLine(new string('─', 45));
        foreach (Process process in processes)
        {
            output.AppendLine($"{process.Id,7}  {FormatSize(SafeWorkingSet(process)),10}  {process.ProcessName}");
            process.Dispose();
        }

        return Ok(output.ToString().TrimEnd());
    }

    private static long SafeWorkingSet(Process process)
    {
        try { return process.WorkingSet64; }
        catch { return 0; }
    }

    private ShellResult KillProcess(string[] args, string raw)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int processId))
            return Error("kill: enter a numeric process ID");
        if (processId == Environment.ProcessId)
            return Error("kill: refusing to terminate Kuro itself; use exit");

        try
        {
            using Process process = Process.GetProcessById(processId);
            string name = process.ProcessName;
            process.Kill(true);
            return Ok($"Stopped {name} ({processId}).");
        }
        catch (ArgumentException)
        {
            return Error($"kill: process not found: {processId}");
        }
    }

    private ShellResult OpenTarget(string[] args, string raw)
    {
        string target = args.Length == 0 ? CurrentDirectory : args[0];
        if (!Uri.TryCreate(target, UriKind.Absolute, out Uri? uri) || uri.IsFile)
        {
            string possiblePath = ResolvePath(target);
            if (File.Exists(possiblePath) || Directory.Exists(possiblePath))
                target = possiblePath;
        }

        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        return ShellResult.Empty;
    }

    private ShellResult OpenExplorer(string[] args, string raw)
    {
        string path = args.Length == 0 ? CurrentDirectory : ResolvePath(args[0]);
        if (!Directory.Exists(path) && !File.Exists(path))
            return Error($"explorer: path not found: {path}");

        string arguments = File.Exists(path) ? $"/select,{QuoteArgument(path)}" : QuoteArgument(path);
        Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
        return ShellResult.Empty;
    }

    private ShellResult EditFile(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("edit: missing file name");

        string path = ResolvePath(args[0]);
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);
        if (!File.Exists(path))
            File.WriteAllText(path, string.Empty);

        Process.Start(new ProcessStartInfo("notepad.exe", QuoteArgument(path)) { UseShellExecute = true });
        return ShellResult.Empty;
    }

    private ShellResult ExecuteProgram(string[] args, string raw) => RunExternalExplicit(args);

    private ShellResult NetworkInfo(string[] args, string raw)
    {
        StringBuilder output = new();
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()
                     .Where(n => n.OperationalStatus == OperationalStatus.Up))
        {
            output.AppendLine(adapter.Name);
            output.AppendLine($"  type:   {adapter.NetworkInterfaceType}");
            output.AppendLine($"  speed:  {FormatNetworkSpeed(adapter.Speed)}");
            output.AppendLine($"  status: {adapter.OperationalStatus}");

            foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses
                         .Where(a => a.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
                output.AppendLine($"  ip:     {address.Address}");

            output.AppendLine();
        }

        return Ok(output.Length == 0 ? "No active network adapters." : output.ToString().TrimEnd());
    }

    private static string FormatNetworkSpeed(long bitsPerSecond)
    {
        if (bitsPerSecond <= 0)
            return "unknown";
        double mbps = bitsPerSecond / 1_000_000d;
        return $"{mbps:0.##} Mbps";
    }

    private ShellResult PingHost(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("ping: missing host");

        int count = 4;
        if (args.Length > 1 && int.TryParse(args[1], out int parsed))
            count = Math.Clamp(parsed, 1, 10);

        using Ping ping = new();
        StringBuilder output = new();
        long total = 0;
        int received = 0;

        for (int index = 0; index < count; index++)
        {
            PingReply reply = ping.Send(args[0], 2_000);
            if (reply.Status == IPStatus.Success)
            {
                received++;
                total += reply.RoundtripTime;
                output.AppendLine($"Reply from {reply.Address}: time={reply.RoundtripTime}ms TTL={reply.Options?.Ttl}");
            }
            else
            {
                output.AppendLine($"Request {index + 1}: {reply.Status}");
            }
        }

        output.AppendLine($"Sent={count}, Received={received}, Lost={count - received}");
        if (received > 0)
            output.AppendLine($"Average={total / received}ms");
        return new ShellResult(output.ToString().TrimEnd(), received == 0);
    }

    private ShellResult ResolveDns(string[] args, string raw)
    {
        if (args.Length == 0)
            return Error("dns: missing host name");

        IPAddress[] addresses = Dns.GetHostAddresses(args[0]);
        return addresses.Length == 0
            ? Error("dns: no addresses returned")
            : Ok(string.Join(Environment.NewLine, addresses.Select(a => a.ToString())));
    }

    private ShellResult Ports(string[] args, string raw)
    {
        IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
        StringBuilder output = new();
        output.AppendLine("TCP LISTENERS");
        foreach (IPEndPoint endpoint in properties.GetActiveTcpListeners().OrderBy(e => e.Port).Take(200))
            output.AppendLine($"  {endpoint.Address,-39} {endpoint.Port,5}");

        output.AppendLine();
        output.AppendLine("TCP CONNECTIONS");
        foreach (TcpConnectionInformation connection in properties.GetActiveTcpConnections()
                     .OrderBy(c => c.LocalEndPoint.Port).Take(200))
        {
            output.AppendLine($"  {connection.LocalEndPoint,-24} -> {connection.RemoteEndPoint,-24} {connection.State}");
        }
        return Ok(output.ToString().TrimEnd());
    }
}

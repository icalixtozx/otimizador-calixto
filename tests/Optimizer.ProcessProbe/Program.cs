using System.Diagnostics;

using var process = Process.GetCurrentProcess();
var startedAtUtc = process.StartTime.ToUniversalTime();
var executablePath = process.MainModule?.FileName ?? "unavailable";

Console.WriteLine("Calixto Optimizer disposable process-priority probe");
Console.WriteLine($"PID: {process.Id}");
Console.WriteLine($"Created UTC: {startedAtUtc:O}");
Console.WriteLine($"Path: {executablePath}");
Console.WriteLine("This probe never changes its own priority. Press Enter to exit.");
Console.ReadLine();

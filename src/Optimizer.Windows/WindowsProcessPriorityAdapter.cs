using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Optimizer.Core.Processes;

namespace Optimizer.Windows;

/// <summary>Reads process metadata and exposes one individually guarded priority setter.</summary>
public sealed class WindowsProcessPriorityAdapter : IProcessPriorityAdapter
{
    public ProcessInventoryResult ListProcesses()
    {
        var readAt = DateTimeOffset.UtcNow;
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception exception) when (IsAccessOrPlatformError(exception))
        {
            return new(readAt, [], [$"A lista de processos não pôde ser lida ({exception.GetType().Name})."]);
        }

        var observations = new List<ProcessObservation>(processes.Length);
        var warnings = new List<string>();
        foreach (var process in processes)
        {
            using (process)
            {
                var item = Inspect(process, process.Id);
                observations.Add(item);
                if (item.Status is ProcessInspectionStatus.AccessDenied or ProcessInspectionStatus.Exited or ProcessInspectionStatus.Failed)
                    warnings.Add($"PID {item.ProcessId}: {item.Detail}");
            }
        }

        return new(readAt,
            Array.AsReadOnly(observations.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ProcessId).ToArray()),
            Array.AsReadOnly(warnings.ToArray()));
    }

    public ProcessObservation Inspect(int processId)
    {
        if (processId < 0)
            return new(processId, null, null, null, ProcessInspectionStatus.NotFound, "PID inválido.");

        try
        {
            using var process = Process.GetProcessById(processId);
            return Inspect(process, processId);
        }
        catch (ArgumentException)
        {
            return new(processId, null, null, null, ProcessInspectionStatus.NotFound, "O PID não existe mais.");
        }
        catch (Exception exception) when (IsAccessOrPlatformError(exception))
        {
            return new(processId, null, null, null, MapError(exception),
                $"A inspeção do processo falhou ({exception.GetType().Name}).");
        }
    }

    public ProcessPriorityWriteResult SetPriority(
        ProcessIdentity expectedIdentity,
        ProcessPriorityLevel expectedCurrentPriority,
        ProcessPriorityLevel targetPriority)
    {
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        if (!ProcessPriorityTargets.Allowed.Contains(targetPriority))
            return new(ProcessPriorityWriteStatus.Failed, false, "A prioridade solicitada não está na lista permitida.");

        Process process;
        try
        {
            process = Process.GetProcessById(expectedIdentity.ProcessId);
        }
        catch (ArgumentException)
        {
            return new(ProcessPriorityWriteStatus.ProcessExited, false, "O processo terminou antes da chamada de escrita.");
        }
        catch (Exception exception) when (IsAccessOrPlatformError(exception))
        {
            return new(MapWriteError(exception), false,
                $"Não foi possível abrir o processo para a escrita ({exception.GetType().Name}).");
        }

        using (process)
        {
            // Reading creation time acquires the Process object's OS handle. Every check and the
            // setter below use this same object/handle, so a later process cannot inherit this target.
            var current = Inspect(process, expectedIdentity.ProcessId);
            var comparison = expectedIdentity.CompareTo(current.Identity);
            if (current.Status is ProcessInspectionStatus.Exited or ProcessInspectionStatus.NotFound)
                return new(ProcessPriorityWriteStatus.ProcessExited, false, "O processo terminou antes da chamada de escrita.");
            if (comparison == ProcessIdentityComparison.DifferentProcess)
                return new(ProcessPriorityWriteStatus.IdentityMismatch, false, "O PID passou a identificar outra instância; a escrita foi recusada.");
            if (comparison == ProcessIdentityComparison.Unverifiable)
                return new(ProcessPriorityWriteStatus.IdentityUnverifiable, false, "A identidade completa do processo não pôde ser confirmada; a escrita foi recusada.");
            if (!current.CanReviewPriority)
                return new(current.Status == ProcessInspectionStatus.AccessDenied
                        ? ProcessPriorityWriteStatus.AccessDenied : ProcessPriorityWriteStatus.Failed,
                    false, $"Não foi possível ler a identidade e a prioridade ({current.Detail}).");
            if (current.CurrentPriority != expectedCurrentPriority)
                return new(ProcessPriorityWriteStatus.PriorityConflict, false,
                    "A prioridade mudou antes da escrita; nenhuma escrita foi iniciada.");

            try
            {
                process.PriorityClass = ToWindowsPriority(targetPriority);
                return new(ProcessPriorityWriteStatus.Applied, true,
                    "A chamada de prioridade retornou; a leitura posterior ainda precisa confirmar o estado.");
            }
            catch (Exception exception) when (IsAccessOrPlatformError(exception))
            {
                // The setter was called. Its failure can leave the result uncertain, so the caller
                // persists a failed/unverified operation and never rolls back automatically.
                return new(MapWriteError(exception), true,
                    $"A chamada de escrita falhou ({exception.GetType().Name}); o resultado exige leitura e revisão.");
            }
        }
    }

    private static ProcessObservation Inspect(Process process, int processId)
    {
        string? name = null;
        ProcessIdentity? identity = null;
        ProcessPriorityLevel? priority = null;
        var details = new List<string>();

        try
        {
            name = process.ProcessName;
        }
        catch (InvalidOperationException)
        {
            return new(processId, name, null, null, ProcessInspectionStatus.Exited, "O processo terminou durante a consulta.");
        }
        catch (Exception exception) when (IsAccessOrPlatformError(exception))
        {
            details.Add($"Nome indisponível ({exception.GetType().Name})");
        }

        DateTimeOffset? createdAtUtc = null;
        try
        {
            createdAtUtc = new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (InvalidOperationException)
        {
            return new(processId, name, null, null, ProcessInspectionStatus.Exited, "O processo terminou durante a consulta.");
        }
        catch (Exception exception) when (IsAccessOrPlatformError(exception))
        {
            details.Add($"Criação indisponível ({exception.GetType().Name})");
        }

        string? path = null;
        if (createdAtUtc is not null)
        {
            try
            {
                path = process.MainModule?.FileName;
            }
            catch (InvalidOperationException)
            {
                return new(processId, name, null, null, ProcessInspectionStatus.Exited, "O processo terminou durante a consulta.");
            }
            catch (Exception exception) when (IsAccessOrPlatformError(exception))
            {
                details.Add($"Caminho indisponível ({exception.GetType().Name})");
            }

            identity = new ProcessIdentity(processId, createdAtUtc.Value, path);
        }

        try
        {
            process.Refresh();
            priority = FromWindowsPriority(process.PriorityClass);
        }
        catch (InvalidOperationException)
        {
            return new(processId, name, null, null, ProcessInspectionStatus.Exited, "O processo terminou durante a consulta.");
        }
        catch (Exception exception) when (IsAccessOrPlatformError(exception))
        {
            details.Add($"Prioridade indisponível ({exception.GetType().Name})");
        }

        if (identity is null || priority is null)
        {
            var accessDenied = details.Any(item => item.Contains(nameof(Win32Exception), StringComparison.Ordinal) ||
                item.Contains(nameof(SecurityException), StringComparison.Ordinal));
            return new(processId, name, identity, priority,
                accessDenied ? ProcessInspectionStatus.AccessDenied : ProcessInspectionStatus.Failed,
                details.Count == 0 ? "Identidade ou prioridade não pôde ser lida." : string.Join("; ", details));
        }

        return new(processId, name, identity, priority,
            details.Count == 0 ? ProcessInspectionStatus.Available : ProcessInspectionStatus.Partial,
            details.Count == 0 ? "Identidade, prioridade e caminho disponíveis." : string.Join("; ", details));
    }

    private static ProcessPriorityClass ToWindowsPriority(ProcessPriorityLevel priority) => priority switch
    {
        ProcessPriorityLevel.Idle => ProcessPriorityClass.Idle,
        ProcessPriorityLevel.BelowNormal => ProcessPriorityClass.BelowNormal,
        ProcessPriorityLevel.Normal => ProcessPriorityClass.Normal,
        ProcessPriorityLevel.AboveNormal => ProcessPriorityClass.AboveNormal,
        ProcessPriorityLevel.High => ProcessPriorityClass.High,
        _ => throw new ArgumentOutOfRangeException(nameof(priority))
    };

    private static ProcessPriorityLevel FromWindowsPriority(ProcessPriorityClass priority) => priority switch
    {
        ProcessPriorityClass.Idle => ProcessPriorityLevel.Idle,
        ProcessPriorityClass.BelowNormal => ProcessPriorityLevel.BelowNormal,
        ProcessPriorityClass.Normal => ProcessPriorityLevel.Normal,
        ProcessPriorityClass.AboveNormal => ProcessPriorityLevel.AboveNormal,
        ProcessPriorityClass.High => ProcessPriorityLevel.High,
        ProcessPriorityClass.RealTime => ProcessPriorityLevel.Realtime,
        _ => ProcessPriorityLevel.Unknown
    };

    private static bool IsAccessOrPlatformError(Exception exception) =>
        exception is Win32Exception or SecurityException or NotSupportedException;

    private static ProcessInspectionStatus MapError(Exception exception) =>
        exception is Win32Exception { NativeErrorCode: 5 } or SecurityException
            ? ProcessInspectionStatus.AccessDenied
            : ProcessInspectionStatus.Failed;

    private static ProcessPriorityWriteStatus MapWriteError(Exception exception) =>
        exception is Win32Exception { NativeErrorCode: 5 } or SecurityException
            ? ProcessPriorityWriteStatus.AccessDenied
            : ProcessPriorityWriteStatus.Failed;
}

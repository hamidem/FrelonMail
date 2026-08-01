using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Frelon.Mail;

internal sealed class AnalysisWorkerProcess(
    Process process,
    Stream standardInput,
    Stream standardOutput,
    Stream standardError,
    IDisposable? platformIsolation = null,
    IDisposable? platformIdentity = null,
    WindowsAnalysisProcessHandle? windowsProcessHandle = null) : IDisposable
{
    private readonly IDisposable? _platformIsolation = platformIsolation;
    private readonly IDisposable? _platformIdentity = platformIdentity;
    private readonly WindowsAnalysisProcessHandle? _windowsProcessHandle =
        windowsProcessHandle;

    public Process Process { get; } = process;

    public Stream StandardInput { get; } = standardInput;

    public Stream StandardOutput { get; } = standardOutput;

    public Stream StandardError { get; } = standardError;

    public int ExitCode => _windowsProcessHandle?.ExitCode ?? Process.ExitCode;

    internal bool HasVerifiedWindowsResourceLimits()
        => OperatingSystem.IsWindows()
            && _platformIsolation is WindowsAnalysisJob job
            && job.Contains(Process)
            && job.HasVerifiedLimits();

    public static AnalysisWorkerProcess Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        return OperatingSystem.IsWindows()
            ? WindowsRestrictedAnalysisWorker.Start(startInfo)
            : StartPortable(startInfo);
    }

    public void Dispose()
    {
        _platformIsolation?.Dispose();
        _platformIdentity?.Dispose();
        StandardInput.Dispose();
        StandardOutput.Dispose();
        StandardError.Dispose();
        Process.Dispose();
        _windowsProcessHandle?.Dispose();
    }

    private static AnalysisWorkerProcess StartPortable(ProcessStartInfo startInfo)
    {
        var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new IOException(
                    "Le processus d'analyse isolé n'a pas pu démarrer.");
            }

            return new AnalysisWorkerProcess(
                process,
                process.StandardInput.BaseStream,
                process.StandardOutput.BaseStream,
                process.StandardError.BaseStream);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }
}

internal sealed class WindowsAnalysisProcessHandle : IDisposable
{
    private readonly SafeProcessHandle _handle;

    public WindowsAnalysisProcessHandle(IntPtr handle)
    {
        _handle = new SafeProcessHandle(handle, ownsHandle: true);
    }

    public int ExitCode
    {
        get
        {
            if (!GetExitCodeProcess(_handle, out var exitCode))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Le code de sortie du processus d'analyse est illisible.");
            }

            return unchecked((int)exitCode);
        }
    }

    public void Dispose() => _handle.Dispose();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(
        SafeProcessHandle process,
        out uint exitCode);
}

[SupportedOSPlatform("windows")]
internal sealed class WindowsAnalysisJob : IDisposable
{
    internal const ulong MemoryLimitBytes = 256UL * 1024 * 1024;

    private const uint JobObjectLimitActiveProcess = 0x00000008;
    private const uint JobObjectLimitProcessMemory = 0x00000100;
    private const uint JobObjectLimitJobMemory = 0x00000200;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const int JobObjectExtendedLimitInformationClass = 9;
    private const uint ExpectedLimitFlags =
        JobObjectLimitActiveProcess
        | JobObjectLimitProcessMemory
        | JobObjectLimitJobMemory
        | JobObjectLimitKillOnJobClose;

    private readonly SafeJobHandle _handle;

    private WindowsAnalysisJob(SafeJobHandle handle)
    {
        _handle = handle;
    }

    public static WindowsAnalysisJob CreateConfigured()
    {
        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(
                error,
                "Windows n'a pas pu créer la limite de ressources du processus d'analyse.");
        }

        try
        {
            var limits = new JobObjectExtendedLimitInformation
            {
                BasicLimitInformation =
                {
                    LimitFlags = ExpectedLimitFlags,
                    ActiveProcessLimit = 1,
                },
                ProcessMemoryLimit = (nuint)MemoryLimitBytes,
                JobMemoryLimit = (nuint)MemoryLimitBytes,
            };
            if (!SetInformationJobObject(
                    handle,
                    JobObjectExtendedLimitInformationClass,
                    ref limits,
                    Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Windows n'a pas pu appliquer les limites de ressources du processus d'analyse.");
            }

            var job = new WindowsAnalysisJob(handle);
            if (!job.HasVerifiedLimits())
            {
                throw new InvalidOperationException(
                    "Windows n'a pas confirmé les limites de ressources du processus d'analyse.");
            }

            return job;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public void AssignAndVerify(IntPtr processHandle)
    {
        if (!AssignProcessToJobObject(_handle, processHandle))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows n'a pas pu enfermer le processus d'analyse dans ses limites de ressources.");
        }

        if (!IsProcessInJob(processHandle, _handle, out var isInJob))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows n'a pas pu vérifier l'isolation des ressources du processus d'analyse.");
        }

        if (!isInJob)
        {
            throw new InvalidOperationException(
                "Windows n'a pas confirmé l'isolation des ressources du processus d'analyse.");
        }
    }

    public bool Contains(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!IsProcessInJob(process.Handle, _handle, out var isInJob))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "L'appartenance du processus d'analyse à sa limite de ressources est illisible.");
        }

        return isInJob;
    }

    public bool HasVerifiedLimits()
    {
        var limits = default(JobObjectExtendedLimitInformation);
        if (!QueryInformationJobObject(
                _handle,
                JobObjectExtendedLimitInformationClass,
                ref limits,
                Marshal.SizeOf<JobObjectExtendedLimitInformation>(),
                IntPtr.Zero))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Les limites de ressources du processus d'analyse sont illisibles.");
        }

        return (limits.BasicLimitInformation.LimitFlags & ExpectedLimitFlags)
                == ExpectedLimitFlags
            && limits.BasicLimitInformation.ActiveProcessLimit == 1
            && limits.ProcessMemoryLimit == (nuint)MemoryLimitBytes
            && limits.JobMemoryLimit == (nuint)MemoryLimitBytes;
    }

    public void Dispose() => _handle.Dispose();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeJobHandle CreateJobObject(
        IntPtr jobAttributes,
        string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        SafeJobHandle job,
        int jobObjectInformationClass,
        ref JobObjectExtendedLimitInformation jobObjectInformation,
        int jobObjectInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(
        SafeJobHandle job,
        int jobObjectInformationClass,
        ref JobObjectExtendedLimitInformation jobObjectInformation,
        int jobObjectInformationLength,
        IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(
        SafeJobHandle job,
        IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(
        IntPtr process,
        SafeJobHandle job,
        [MarshalAs(UnmanagedType.Bool)] out bool result);

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeJobHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle()
            => CloseHandle(handle);
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

[SupportedOSPlatform("windows")]
internal static class WindowsRestrictedAnalysisWorker
{
    private const uint TokenAssignPrimary = 0x0001;
    private const uint TokenDuplicate = 0x0002;
    private const uint TokenQuery = 0x0008;
    private const uint TokenAdjustDefault = 0x0080;
    private const uint DisableMaxPrivilege = 0x0001;
    private const uint LuaToken = 0x0004;
    private const uint SeGroupIntegrity = 0x00000020;
    private const int TokenIntegrityLevel = 25;
    private const int TokenIsAppContainer = 29;
    private const int TokenCapabilities = 30;
    private const int LowIntegrityRid = 0x1000;
    private const int ErrorInsufficientBuffer = 122;
    private const uint StartfUseStdHandles = 0x00000100;
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateNoWindow = 0x08000000;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint ResumeThreadFailed = uint.MaxValue;
    private static readonly nuint ProcThreadAttributeHandleList = 0x00020002;
    private static readonly nuint ProcThreadAttributeSecurityCapabilities = 0x00020009;

    public static AnalysisWorkerProcess Start(ProcessStartInfo startInfo)
    {
        ValidateStartInfo(startInfo);

        using var restrictedToken = CreateVerifiedRestrictedToken();
        var standardInput = new AnonymousPipeServerStream(
            PipeDirection.Out,
            HandleInheritability.Inheritable);
        var standardOutput = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        var standardError = new AnonymousPipeServerStream(
            PipeDirection.In,
            HandleInheritability.Inheritable);
        WindowsAnalysisJob? job = null;
        WindowsAppContainerProfile? appContainer = null;
        var ownershipTransferred = false;
        try
        {
            appContainer = WindowsAppContainerProfile.CreateEphemeral();
            appContainer.GrantReadAndExecute(GetWorkerCodeDirectory(startInfo));
            job = WindowsAnalysisJob.CreateConfigured();
            var startupInfo = new StartupInfoEx
            {
                StartupInfo =
                {
                    Size = Marshal.SizeOf<StartupInfoEx>(),
                    Flags = StartfUseStdHandles,
                    StandardInput = standardInput.ClientSafePipeHandle.DangerousGetHandle(),
                    StandardOutput = standardOutput.ClientSafePipeHandle.DangerousGetHandle(),
                    StandardError = standardError.ClientSafePipeHandle.DangerousGetHandle(),
                },
            };
            using var attributes = CreateProcessAttributes(
                appContainer,
                startupInfo.StartupInfo.StandardInput,
                startupInfo.StartupInfo.StandardOutput,
                startupInfo.StartupInfo.StandardError);
            startupInfo.AttributeList = attributes.DangerousGetHandle();

            var commandLine = BuildCommandLine(startInfo);
            var processInformation = default(ProcessInformation);
            WindowsAnalysisProcessHandle? processHandle = null;
            try
            {
                if (!CreateProcessAsUser(
                        restrictedToken,
                        Path.IsPathFullyQualified(startInfo.FileName)
                            ? startInfo.FileName
                            : null,
                        commandLine,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        inheritHandles: true,
                        CreateSuspended | CreateNoWindow | ExtendedStartupInfoPresent,
                        IntPtr.Zero,
                        string.IsNullOrWhiteSpace(startInfo.WorkingDirectory)
                            ? null
                            : startInfo.WorkingDirectory,
                        ref startupInfo,
                        out processInformation))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "Windows n'a pas pu démarrer le processus d'analyse avec des droits réduits.");
                }

                job.AssignAndVerify(processInformation.Process);
                var previousSuspendCount = ResumeThread(processInformation.Thread);
                if (previousSuspendCount == ResumeThreadFailed)
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "Windows n'a pas pu reprendre le processus d'analyse protégé.");
                }

                if (previousSuspendCount != 1)
                {
                    throw new InvalidOperationException(
                        "L'état suspendu du processus d'analyse n'a pas pu être vérifié.");
                }

                standardInput.DisposeLocalCopyOfClientHandle();
                standardOutput.DisposeLocalCopyOfClientHandle();
                standardError.DisposeLocalCopyOfClientHandle();

                processHandle = new WindowsAnalysisProcessHandle(
                    processInformation.Process);
                processInformation.Process = IntPtr.Zero;
                var process = Process.GetProcessById(
                    checked((int)processInformation.ProcessId));
                ownershipTransferred = true;
                return new AnalysisWorkerProcess(
                    process,
                    standardInput,
                    standardOutput,
                    standardError,
                    job,
                    appContainer,
                    processHandle);
            }
            catch
            {
                processHandle?.Dispose();
                if (processInformation.Process != IntPtr.Zero)
                {
                    _ = TerminateProcess(processInformation.Process, 13);
                }

                throw;
            }
            finally
            {
                if (processInformation.Thread != IntPtr.Zero)
                {
                    _ = CloseHandle(processInformation.Thread);
                }

                if (processInformation.Process != IntPtr.Zero)
                {
                    _ = CloseHandle(processInformation.Process);
                }
            }
        }
        finally
        {
            if (!ownershipTransferred)
            {
                job?.Dispose();
                appContainer?.Dispose();
                standardInput.Dispose();
                standardOutput.Dispose();
                standardError.Dispose();
            }
        }
    }

    internal static bool CurrentTokenCanBeReducedAndVerified()
    {
        using var token = CreateVerifiedRestrictedToken();
        return ReadIntegrityRid(token) == LowIntegrityRid;
    }

    internal static bool ProcessHasVerifiedLowIntegrity(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!OpenProcessToken(
                process.Handle,
                TokenQuery,
                out var processToken))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Le jeton Windows du worker est inaccessible.");
        }

        using (processToken)
        {
            return ReadIntegrityRid(processToken) == LowIntegrityRid;
        }
    }

    private static void ValidateStartInfo(ProcessStartInfo startInfo)
    {
        if (startInfo.UseShellExecute
            || !startInfo.RedirectStandardInput
            || !startInfo.RedirectStandardOutput
            || !startInfo.RedirectStandardError)
        {
            throw new InvalidOperationException(
                "Le processus d'analyse exige trois flux redirigés sans shell.");
        }

        if (!string.IsNullOrEmpty(startInfo.Arguments))
        {
            throw new InvalidOperationException(
                "Les arguments du processus d'analyse doivent être structurés.");
        }
    }

    internal static bool ProcessHasVerifiedNetworkIsolation(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!OpenProcessToken(
                process.Handle,
                TokenQuery,
                out var processToken))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Le jeton Windows du worker est inaccessible.");
        }

        using (processToken)
        {
            return ReadTokenBoolean(processToken, TokenIsAppContainer)
                && ReadTokenGroupCount(processToken, TokenCapabilities) == 0;
        }
    }

    private static string GetWorkerCodeDirectory(ProcessStartInfo startInfo)
    {
        var executableName = Path.GetFileNameWithoutExtension(startInfo.FileName);
        var codePath = string.Equals(
                executableName,
                "dotnet",
                StringComparison.OrdinalIgnoreCase)
            ? startInfo.ArgumentList.FirstOrDefault()
            : startInfo.FileName;
        if (string.IsNullOrWhiteSpace(codePath)
            || !Path.IsPathFullyQualified(codePath))
        {
            throw new InvalidOperationException(
                "Le dossier de code du processus d'analyse est indisponible.");
        }

        return Path.GetDirectoryName(codePath)
            ?? throw new InvalidOperationException(
                "Le dossier de code du processus d'analyse est invalide.");
    }

    private static SafeAccessTokenHandle CreateVerifiedRestrictedToken()
    {
        if (!OpenProcessToken(
                GetCurrentProcess(),
                TokenAssignPrimary
                    | TokenDuplicate
                    | TokenQuery
                    | TokenAdjustDefault,
                out var currentToken))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Le jeton Windows courant est inaccessible.");
        }

        using (currentToken)
        {
            if (!CreateRestrictedToken(
                    currentToken,
                    DisableMaxPrivilege | LuaToken,
                    0,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    out var restrictedToken))
            {
                var error = Marshal.GetLastWin32Error();
                throw new Win32Exception(
                    error,
                    $"Le jeton Windows du processus d'analyse n'a pas pu être restreint ({error}).");
            }

            try
            {
                SetLowIntegrity(restrictedToken);
                if (ReadIntegrityRid(restrictedToken) != LowIntegrityRid)
                {
                    throw new InvalidOperationException(
                        "Windows n'a pas confirmé les droits réduits du processus d'analyse.");
                }

                return restrictedToken;
            }
            catch
            {
                restrictedToken.Dispose();
                throw;
            }
        }
    }

    private static void SetLowIntegrity(SafeAccessTokenHandle token)
    {
        var sid = new SecurityIdentifier("S-1-16-4096");
        var sidBytes = new byte[sid.BinaryLength];
        sid.GetBinaryForm(sidBytes, 0);

        var labelSize = Marshal.SizeOf<TokenMandatoryLabel>();
        var buffer = Marshal.AllocHGlobal(labelSize + sidBytes.Length);
        try
        {
            var sidAddress = IntPtr.Add(buffer, labelSize);
            Marshal.Copy(sidBytes, 0, sidAddress, sidBytes.Length);
            Marshal.StructureToPtr(
                new TokenMandatoryLabel
                {
                    Label = new SidAndAttributes
                    {
                        Sid = sidAddress,
                        Attributes = SeGroupIntegrity,
                    },
                },
                buffer,
                fDeleteOld: false);

            if (!SetTokenInformation(
                    token,
                    TokenIntegrityLevel,
                    buffer,
                    labelSize + sidBytes.Length))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Le niveau d'intégrité bas du processus d'analyse n'a pas pu être appliqué.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int ReadIntegrityRid(SafeAccessTokenHandle token)
    {
        _ = GetTokenInformation(
            token,
            TokenIntegrityLevel,
            IntPtr.Zero,
            0,
            out var requiredBytes);
        var error = Marshal.GetLastWin32Error();
        if (requiredBytes <= 0 || error != ErrorInsufficientBuffer)
        {
            throw new Win32Exception(
                error,
                "Le niveau d'intégrité Windows n'a pas pu être lu.");
        }

        var buffer = Marshal.AllocHGlobal(requiredBytes);
        try
        {
            if (!GetTokenInformation(
                    token,
                    TokenIntegrityLevel,
                    buffer,
                    requiredBytes,
                    out _))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Le niveau d'intégrité Windows n'a pas pu être vérifié.");
            }

            var label = Marshal.PtrToStructure<TokenMandatoryLabel>(buffer);
            var countAddress = GetSidSubAuthorityCount(label.Label.Sid);
            if (countAddress == IntPtr.Zero)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Le SID d'intégrité Windows est invalide.");
            }

            var count = Marshal.ReadByte(countAddress);
            if (count == 0)
            {
                throw new InvalidOperationException(
                    "Le SID d'intégrité Windows est vide.");
            }

            var ridAddress = GetSidSubAuthority(
                label.Label.Sid,
                checked((uint)(count - 1)));
            if (ridAddress == IntPtr.Zero)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Le niveau d'intégrité Windows est invalide.");
            }

            return Marshal.ReadInt32(ridAddress);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool ReadTokenBoolean(
        SafeAccessTokenHandle token,
        int informationClass)
    {
        var value = 0;
        if (!GetTokenInformation(
                token,
                informationClass,
                ref value,
                sizeof(int),
                out var returnLength)
            || returnLength != sizeof(int))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "L'isolation AppContainer du processus d'analyse est illisible.");
        }

        return value != 0;
    }

    private static int ReadTokenGroupCount(
        SafeAccessTokenHandle token,
        int informationClass)
    {
        _ = GetTokenInformation(
            token,
            informationClass,
            IntPtr.Zero,
            0,
            out var requiredBytes);
        var error = Marshal.GetLastWin32Error();
        if (requiredBytes < sizeof(int) || error != ErrorInsufficientBuffer)
        {
            throw new Win32Exception(
                error,
                "Les capabilities AppContainer du processus d'analyse sont illisibles.");
        }

        var buffer = Marshal.AllocHGlobal(requiredBytes);
        try
        {
            if (!GetTokenInformation(
                    token,
                    informationClass,
                    buffer,
                    requiredBytes,
                    out _))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Les capabilities AppContainer du processus d'analyse n'ont pas pu être vérifiées.");
            }

            return Marshal.ReadInt32(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static SafeProcThreadAttributeList CreateProcessAttributes(
        WindowsAppContainerProfile appContainer,
        params IntPtr[] handles)
    {
        nuint requiredBytes = 0;
        _ = InitializeProcThreadAttributeList(
            IntPtr.Zero,
            2,
            0,
            ref requiredBytes);
        if (requiredBytes == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "La liste des flux autorisés du processus d'analyse est indisponible.");
        }

        var attributes = new SafeProcThreadAttributeList(requiredBytes);
        if (!InitializeProcThreadAttributeList(
                attributes.DangerousGetHandle(),
                2,
                0,
                ref requiredBytes))
        {
            attributes.Dispose();
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "La liste des flux autorisés du processus d'analyse n'a pas pu être créée.");
        }

        attributes.MarkInitialized();
        var handleBytes = checked(IntPtr.Size * handles.Length);
        var handleList = Marshal.AllocHGlobal(handleBytes);
        try
        {
            Marshal.Copy(handles, 0, handleList, handles.Length);
            if (!UpdateProcThreadAttribute(
                    attributes.DangerousGetHandle(),
                    0,
                    ProcThreadAttributeHandleList,
                    handleList,
                    checked((nuint)handleBytes),
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                attributes.Dispose();
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Les flux autorisés du processus d'analyse n'ont pas pu être fixés.");
            }

            attributes.OwnValueBuffer(handleList);
            handleList = IntPtr.Zero;

            var securityCapabilities = Marshal.AllocHGlobal(
                Marshal.SizeOf<WindowsAppContainerProfile.SecurityCapabilities>());
            try
            {
                Marshal.StructureToPtr(
                    appContainer.CreateSecurityCapabilities(),
                    securityCapabilities,
                    fDeleteOld: false);
                if (!UpdateProcThreadAttribute(
                        attributes.DangerousGetHandle(),
                        0,
                        ProcThreadAttributeSecurityCapabilities,
                        securityCapabilities,
                        checked((nuint)Marshal.SizeOf<
                            WindowsAppContainerProfile.SecurityCapabilities>()),
                        IntPtr.Zero,
                        IntPtr.Zero))
                {
                    attributes.Dispose();
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "L'AppContainer sans réseau du processus d'analyse n'a pas pu être fixé.");
                }

                attributes.OwnSecurityCapabilitiesBuffer(securityCapabilities);
                securityCapabilities = IntPtr.Zero;
                return attributes;
            }
            finally
            {
                if (securityCapabilities != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(securityCapabilities);
                }
            }
        }
        finally
        {
            if (handleList != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(handleList);
            }
        }
    }

    private static StringBuilder BuildCommandLine(ProcessStartInfo startInfo)
    {
        var command = new StringBuilder();
        AppendQuotedArgument(command, startInfo.FileName);
        foreach (var argument in startInfo.ArgumentList)
        {
            command.Append(' ');
            AppendQuotedArgument(command, argument);
        }

        return new StringBuilder(command.ToString(), command.Length + 1);
    }

    private static void AppendQuotedArgument(
        StringBuilder destination,
        string argument)
    {
        destination.Append('"');
        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                destination.Append('\\', (backslashes * 2) + 1);
                destination.Append('"');
                backslashes = 0;
                continue;
            }

            destination.Append('\\', backslashes);
            destination.Append(character);
            backslashes = 0;
        }

        destination.Append('\\', backslashes * 2);
        destination.Append('"');
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(
        IntPtr processHandle,
        uint desiredAccess,
        out SafeAccessTokenHandle tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateRestrictedToken(
        SafeAccessTokenHandle existingTokenHandle,
        uint flags,
        uint disableSidCount,
        IntPtr sidsToDisable,
        uint deletePrivilegeCount,
        IntPtr privilegesToDelete,
        uint restrictedSidCount,
        IntPtr sidsToRestrict,
        out SafeAccessTokenHandle newTokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetTokenInformation(
        SafeAccessTokenHandle tokenHandle,
        int tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        SafeAccessTokenHandle tokenHandle,
        int tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        SafeAccessTokenHandle tokenHandle,
        int tokenInformationClass,
        ref int tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint subAuthority);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr attributeList,
        int attributeCount,
        uint flags,
        ref nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr attributeList,
        uint flags,
        nuint attribute,
        IntPtr value,
        nuint size,
        IntPtr previousValue,
        IntPtr returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(
        IntPtr attributeList);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessAsUser(
        SafeAccessTokenHandle token,
        string? applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(
        IntPtr processHandle,
        uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr threadHandle);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenMandatoryLabel
    {
        public SidAndAttributes Label;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public uint Flags;
        public short ShowWindow;
        public short Reserved2Bytes;
        public IntPtr Reserved2;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public uint ProcessId;
        public uint ThreadId;
    }

    private sealed class SafeProcThreadAttributeList : SafeHandleZeroOrMinusOneIsInvalid
    {
        private bool _initialized;
        private IntPtr _valueBuffer;
        private IntPtr _securityCapabilitiesBuffer;

        public SafeProcThreadAttributeList(nuint size)
            : base(ownsHandle: true)
        {
            SetHandle(Marshal.AllocHGlobal(checked((int)size)));
        }

        public void MarkInitialized() => _initialized = true;

        public void OwnValueBuffer(IntPtr valueBuffer)
            => _valueBuffer = valueBuffer;

        public void OwnSecurityCapabilitiesBuffer(IntPtr valueBuffer)
            => _securityCapabilitiesBuffer = valueBuffer;

        protected override bool ReleaseHandle()
        {
            if (_initialized)
            {
                DeleteProcThreadAttributeList(handle);
            }

            if (_valueBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_valueBuffer);
            }

            if (_securityCapabilitiesBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_securityCapabilitiesBuffer);
            }

            Marshal.FreeHGlobal(handle);
            return true;
        }
    }
}

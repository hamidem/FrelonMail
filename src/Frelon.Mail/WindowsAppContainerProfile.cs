using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace Frelon.Mail;

[SupportedOSPlatform("windows")]
internal sealed class WindowsAppContainerProfile : IDisposable
{
    private const string ProfileNamePrefix = "Moralement.NET.Frelon.Worker.";
    private const string AccessControlMutexName =
        @"Local\Moralement.NET.Frelon.WorkerAcl";
    private static readonly TimeSpan AccessControlMutexTimeout =
        TimeSpan.FromSeconds(5);

    private readonly string _name;
    private readonly SafeAppContainerSidHandle _sid;
    private readonly SecurityIdentifier _securityIdentifier;
    private readonly List<DirectoryAccessGrant> _accessGrants = [];
    private bool _disposed;

    private WindowsAppContainerProfile(
        string name,
        SafeAppContainerSidHandle sid)
    {
        _name = name;
        _sid = sid;
        _securityIdentifier = new SecurityIdentifier(sid.DangerousGetHandle());
    }

    public static WindowsAppContainerProfile CreateEphemeral()
    {
        var name = $"{ProfileNamePrefix}{Guid.NewGuid():N}";
        var result = CreateAppContainerProfile(
            name,
            "Frelon analysis worker",
            "Ephemeral sandbox for one untrusted email analysis.",
            IntPtr.Zero,
            0,
            out var sid);
        if (result < 0)
        {
            sid?.Dispose();
            throw new Win32Exception(
                result & 0xFFFF,
                "Windows n'a pas pu créer l'AppContainer du processus d'analyse.");
        }

        return new WindowsAppContainerProfile(name, sid);
    }

    public SecurityCapabilities CreateSecurityCapabilities()
        => new()
        {
            AppContainerSid = _sid.DangerousGetHandle(),
            Capabilities = IntPtr.Zero,
            CapabilityCount = 0,
            Reserved = 0,
        };

    public void GrantReadAndExecute(string directoryPath)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(directoryPath));
        var rule = new FileSystemAccessRule(
            _securityIdentifier,
            FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow);
        WithAccessControlLock(() =>
        {
            var security = directory.GetAccessControl(AccessControlSections.Access);
            security.AddAccessRule(rule);
            directory.SetAccessControl(security);
        });

        _accessGrants.Add(new DirectoryAccessGrant(directory, rule));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var grant in _accessGrants)
        {
            try
            {
                WithAccessControlLock(() =>
                {
                    var security = grant.Directory.GetAccessControl(
                        AccessControlSections.Access);
                    security.RemoveAccessRuleSpecific(grant.Rule);
                    grant.Directory.SetAccessControl(security);
                });
            }
            catch (SystemException)
            {
                // Une règle résiduelle ne donne qu'un accès en lecture à une
                // identité éphémère qui ne sera jamais réutilisée.
            }
        }

        _sid.Dispose();

        // Un profil resté verrouillé après un arrêt brutal n'est jamais réutilisé :
        // chaque analyse reçoit un nom aléatoire distinct.
        _ = DeleteAppContainerProfile(_name);
    }

    private static void WithAccessControlLock(Action action)
    {
        using var mutex = new Mutex(
            initiallyOwned: false,
            AccessControlMutexName);
        var lockTaken = false;
        try
        {
            try
            {
                lockTaken = mutex.WaitOne(AccessControlMutexTimeout);
            }
            catch (AbandonedMutexException)
            {
                lockTaken = true;
            }

            if (!lockTaken)
            {
                throw new InvalidOperationException(
                    "Le verrou d'accès au code du worker est indisponible.");
            }

            action();
        }
        finally
        {
            if (lockTaken)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    private static extern int CreateAppContainerProfile(
        string appContainerName,
        string displayName,
        string description,
        IntPtr capabilities,
        uint capabilityCount,
        out SafeAppContainerSidHandle appContainerSid);

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    private static extern int DeleteAppContainerProfile(string appContainerName);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SecurityCapabilities
    {
        public IntPtr AppContainerSid;
        public IntPtr Capabilities;
        public uint CapabilityCount;
        public uint Reserved;
    }

    private sealed record DirectoryAccessGrant(
        DirectoryInfo Directory,
        FileSystemAccessRule Rule);

    private sealed class SafeAppContainerSidHandle
        : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeAppContainerSidHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle()
            => FreeSid(handle) == IntPtr.Zero;
    }

    [DllImport("advapi32.dll")]
    private static extern IntPtr FreeSid(IntPtr sid);
}

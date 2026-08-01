using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Text;
using Frelon.Cli;
using Frelon.Mail;
using Xunit;

namespace Frelon.Cli.Tests;

public sealed class IsolatedEmailAnalysisProcessTests
{
    [Fact]
    [Trait("Category", "WindowsIsolation")]
    public void WindowsBuildsAVerifiedLowIntegrityToken()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.True(
            WindowsRestrictedAnalysisWorker.CurrentTokenCanBeReducedAndVerified());
    }

    [Fact]
    [Trait("Category", "WindowsIsolation")]
    public void WindowsStartsTheChildWithVerifiedLowIntegrity()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var worker = AnalysisWorkerProcess.Start(CreateCliWorkerStartInfo());

        Assert.True(
            WindowsRestrictedAnalysisWorker.ProcessHasVerifiedLowIntegrity(
                worker.Process));
    }

    [Fact]
    [Trait("Category", "WindowsIsolation")]
    public void WindowsStartsTheChildWithoutNetworkCapabilities()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var worker = AnalysisWorkerProcess.Start(CreateCliWorkerStartInfo());

        Assert.True(
            WindowsRestrictedAnalysisWorker.ProcessHasVerifiedNetworkIsolation(
                worker.Process));
    }

    [Fact]
    [Trait("Category", "WindowsIsolation")]
    public void WindowsEnforcesVerifiedWorkerResourceLimits()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var worker = AnalysisWorkerProcess.Start(CreateCliWorkerStartInfo());

        Assert.True(worker.HasVerifiedWindowsResourceLimits());
        Assert.Equal(256UL * 1024 * 1024, WindowsAnalysisJob.MemoryLimitBytes);
    }

    [Fact]
    [Trait("Category", "WindowsIsolation")]
    [SupportedOSPlatform("windows")]
    public async Task WindowsRemovesConcurrentAppContainerAccessGrants()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const int concurrentProfiles = 8;
        var directory = Directory.CreateTempSubdirectory(
            "frelon-appcontainer-acl-");
        using var grantsReady = new CountdownEvent(concurrentProfiles);
        using var releaseGrants = new ManualResetEventSlim();
        try
        {
            var tasks = Enumerable.Range(0, concurrentProfiles)
                .Select(_ => Task.Run(() =>
                {
                    using var profile =
                        WindowsAppContainerProfile.CreateEphemeral();
                    profile.GrantReadAndExecute(directory.FullName);
                    grantsReady.Signal();
                    releaseGrants.Wait(TestContext.Current.CancellationToken);
                }))
                .ToArray();

            var allGrantsReady = grantsReady.Wait(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken);
            releaseGrants.Set();
            await Task.WhenAll(tasks);
            Assert.True(allGrantsReady);

            var accessRules = directory
                .GetAccessControl(AccessControlSections.Access)
                .GetAccessRules(
                    includeExplicit: true,
                    includeInherited: false,
                    typeof(System.Security.Principal.SecurityIdentifier));
            Assert.DoesNotContain(
                accessRules.Cast<FileSystemAccessRule>(),
                rule => rule.IdentityReference.Value.StartsWith(
                    "S-1-15-2-",
                    StringComparison.Ordinal));
        }
        finally
        {
            releaseGrants.Set();
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "WindowsIsolation")]
    public void WindowsClosesTheWorkerWithItsResourceBoundary()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var process = new Process
        {
            StartInfo = CreateCliWorkerStartInfo(),
        };
        Assert.True(process.Start());
        try
        {
            using (var job = WindowsAnalysisJob.CreateConfigured())
            {
                job.AssignAndVerify(process.Handle);
            }

            Assert.True(process.WaitForExit(5_000));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    [Fact]
    public async Task ParentAnalyzesEmailThroughTheRestrictedChildProcess()
    {
        var analyzer = IsolatedEmailAnalysis.CreateAnalyzer(
            CreateCliWorkerStartInfo,
            TimeSpan.FromSeconds(10));
        await using var email = new MemoryStream(MinimalEmail());

        var incident = await analyzer.AnalyzeAsync(
            email,
            "preuve.eml",
            TestContext.Current.CancellationToken);

        Assert.Equal("preuve.eml", incident.Evidence.FileName);
        Assert.NotEqual(Guid.Empty, incident.IncidentId);
    }

    [Fact]
    public async Task ParentEnforcesTheTimeQuotaAndStopsTheChildProcess()
    {
        var analyzer = IsolatedEmailAnalysis.CreateAnalyzer(
            CreateCliWorkerStartInfo,
            TimeSpan.FromMilliseconds(1));
        await using var email = new MemoryStream(MinimalEmail());

        await Assert.ThrowsAsync<EmailAnalysisTimeoutException>(
            () => analyzer.AnalyzeAsync(
                email,
                "preuve.eml",
                TestContext.Current.CancellationToken));
    }

    private static ProcessStartInfo CreateCliWorkerStartInfo()
    {
        var cliAssemblyPath = typeof(CliApplication).Assembly.Location;
        var startInfo = new ProcessStartInfo(
            OperatingSystem.IsWindows()
                ? Path.ChangeExtension(cliAssemblyPath, ".exe")
                : "dotnet")
        {
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (!OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add(cliAssemblyPath);
        }

        startInfo.ArgumentList.Add(IsolatedEmailAnalysis.WorkerArgument);
        return startInfo;
    }

    private static byte[] MinimalEmail()
        => Encoding.UTF8.GetBytes(
            "From: sender@example.test\r\n" +
            "To: analyst@example.test\r\n" +
            "Subject: Isolation\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "Visit https://example.test/path\r\n");
}

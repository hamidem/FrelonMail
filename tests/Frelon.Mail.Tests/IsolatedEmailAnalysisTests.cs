using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frelon.Core;
using Xunit;

namespace Frelon.Mail.Tests;

public sealed class IsolatedEmailAnalysisTests
{
    [Fact]
    public void WorkerInvocationRequiresTheSinglePrivateArgument()
    {
        Assert.True(IsolatedEmailAnalysis.IsWorkerInvocation(
            [IsolatedEmailAnalysis.WorkerArgument]));
        Assert.False(IsolatedEmailAnalysis.IsWorkerInvocation([]));
        Assert.False(IsolatedEmailAnalysis.IsWorkerInvocation(
            [IsolatedEmailAnalysis.WorkerArgument, "unexpected"]));
    }

    [Fact]
    public async Task WorkerReturnsAStructuredIncidentWithoutWritingDiagnostics()
    {
        await using var input = CreateRequest(
            "preuve.eml",
            MinimalEmail());
        await using var output = new MemoryStream();

        var exitCode = await IsolatedEmailAnalysis.RunWorkerAsync(
            input,
            output,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        var incident = JsonSerializer.Deserialize<FraudIncident>(
            output.ToArray(),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() },
            });
        Assert.NotNull(incident);
        Assert.Equal("preuve.eml", incident.Evidence.FileName);
        Assert.NotEqual(Guid.Empty, incident.IncidentId);
    }

    [Fact]
    public async Task WorkerRejectsAnIncompleteProtocolWithoutReturningItsContent()
    {
        var sensitiveContent = Encoding.UTF8.GetBytes("secret-never-returned");
        await using var input = new MemoryStream(sensitiveContent);
        await using var output = new MemoryStream();

        var exitCode = await IsolatedEmailAnalysis.RunWorkerAsync(
            input,
            output,
            TestContext.Current.CancellationToken);

        Assert.NotEqual(0, exitCode);
        Assert.Empty(output.ToArray());
    }

    private static MemoryStream CreateRequest(string fileName, byte[] content)
    {
        var fileNameBytes = Encoding.UTF8.GetBytes(fileName);
        var request = new MemoryStream();
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, fileNameBytes.Length);
        request.Write(length);
        request.Write(fileNameBytes);
        request.Write(content);
        request.Position = 0;
        return request;
    }

    private static byte[] MinimalEmail()
        => Encoding.UTF8.GetBytes(
            "From: sender@example.test\r\n" +
            "To: analyst@example.test\r\n" +
            "Subject: Isolation\r\n" +
            "Message-ID: <isolation@example.test>\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n" +
            "\r\n" +
            "Visit https://example.test/path\r\n");
}

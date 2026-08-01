using Frelon.Cli;
using Frelon.Mail;

if (IsolatedEmailAnalysis.IsWorkerInvocation(args))
{
    return await IsolatedEmailAnalysis
        .RunWorkerAsync(Console.OpenStandardInput(), Console.OpenStandardOutput())
        .ConfigureAwait(false);
}

return await CliApplication.CreateIsolated(Console.Out, Console.Error)
    .RunAsync(args)
    .ConfigureAwait(false);

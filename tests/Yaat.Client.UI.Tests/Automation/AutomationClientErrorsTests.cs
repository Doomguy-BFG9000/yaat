using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging;
using Xunit;
using Yaat.Client.Automation;
using Yaat.Client.Automation.Protocol;
using Yaat.Client.Logging;
using Yaat.Client.UI.Tests.Helpers;

namespace Yaat.Client.UI.Tests.Automation;

/// <summary>
/// The client errors an automation pipe answer carries: an error the client logs while a call runs comes back in that
/// call's <c>clientErrors</c>, and a call during which nothing is logged has none.
/// </summary>
public sealed class AutomationClientErrorsTests : AutomationHostFixture
{
    private const string ProbeCategory = "AutomationClientErrorsProbe";

    /// <summary>
    /// A roots provider that serves <see cref="Windows"/> and runs <paramref name="change"/> once, on its second call: the
    /// selector engine calls it per evaluation, so the second call is inside the wait, after the dispatcher read its start.
    /// </summary>
    private Func<IEnumerable<TopLevel>> RootsRunning(Action change)
    {
        int calls = 0;
        return () =>
        {
            if (++calls == 2)
            {
                change();
            }

            return Windows;
        };
    }

    [AvaloniaFact]
    public async Task WaitFor_ErrorLoggedDuringTheCall_ComesBackInClientErrors_AndAQuietCallHasNone()
    {
        ILogger probe = AppLog.CreateLogger(ProbeCategory);
        var status = new TextBlock { Name = "Status", Text = "loading" };
        ShowWindow("WaitWindow", status, null);
        bool changed = false;
        using AutomationHost host = StartHost(
            RootsRunning(() =>
            {
                probe.LogError(new InvalidOperationException("probe failure"), "Probe broke for {Callsign}", "AAL1");
                status.Text = "ready";
                changed = true;
            })
        );
        await using AutomationPipeTestClient client = await Connect();

        JsonElement noisy = await Send(
            client,
            ProtocolMethods.WaitFor,
            new
            {
                selector = "#Status",
                condition = "text_equals",
                text = "ready",
            }
        );
        Assert.True(changed, $"wait_for answered before the probe logged: {noisy}");
        JsonElement quiet = await Send(client, ProtocolMethods.WaitFor, new { selector = "#Status", condition = "exists" });

        Result(noisy);
        JsonElement errors = noisy.GetProperty("clientErrors");
        Assert.Equal(1, errors.GetArrayLength());
        JsonElement entry = errors[0];
        Assert.Equal("Error", entry.GetProperty("level").GetString());
        Assert.Equal(ProbeCategory, entry.GetProperty("category").GetString());
        Assert.Equal("Probe broke for AAL1", entry.GetProperty("message").GetString());
        Assert.Equal("System.InvalidOperationException: probe failure", entry.GetProperty("exception").GetString());

        Result(quiet);
        Assert.False(quiet.TryGetProperty("clientErrors", out JsonElement unexpected), $"A quiet call carried client errors: {unexpected}");
    }

    [AvaloniaFact]
    public async Task Click_HandlerThrows_AnswersWithAnInternalError_CarryingTheDispatchersOwnLogEntry()
    {
        var button = new Button { Name = "Boom", Content = "Boom" };
        button.Click += (_, _) => throw new InvalidOperationException("click handler broke");
        ShowWindow("BoomWindow", button, null);
        using AutomationHost host = StartHost(() => Windows);
        await using AutomationPipeTestClient client = await Connect();

        JsonElement response = await Send(client, ProtocolMethods.Click, new { selector = "#Boom" });

        Error(response, AutomationErrorCodes.Internal);
        JsonElement entry = Assert.Single(
            response.GetProperty("clientErrors").EnumerateArray(),
            e => e.GetProperty("category").GetString() == "AutomationDispatcher"
        );
        Assert.Equal("Error", entry.GetProperty("level").GetString());
        Assert.Equal($"Automation method {ProtocolMethods.Click} (request {ProtocolMethods.Click}) failed", entry.GetProperty("message").GetString());
        Assert.Equal("System.InvalidOperationException: click handler broke", entry.GetProperty("exception").GetString());
    }

    [AvaloniaFact]
    public async Task WaitFor_MoreErrorsThanTheCap_AnswersWithTheOldestTwenty()
    {
        ILogger probe = AppLog.CreateLogger(ProbeCategory);
        var status = new TextBlock { Name = "Status", Text = "loading" };
        ShowWindow("WaitWindow", status, null);
        bool changed = false;
        using AutomationHost host = StartHost(
            RootsRunning(() =>
            {
                for (int i = 0; i < 25; i++)
                {
                    probe.LogError("Probe error {Index}", i);
                }

                status.Text = "ready";
                changed = true;
            })
        );
        await using AutomationPipeTestClient client = await Connect();

        JsonElement response = await Send(
            client,
            ProtocolMethods.WaitFor,
            new
            {
                selector = "#Status",
                condition = "text_equals",
                text = "ready",
            }
        );

        Assert.True(changed, $"wait_for answered before the probe logged: {response}");
        Result(response);
        List<string?> messages = [.. response.GetProperty("clientErrors").EnumerateArray().Select(e => e.GetProperty("message").GetString())];
        Assert.Equal(20, AutomationResponse.MaxClientErrors);
        Assert.Equal([.. Enumerable.Range(0, AutomationResponse.MaxClientErrors).Select(i => $"Probe error {i}")], messages);
        Assert.Equal(5, response.GetProperty("clientErrorsOmitted").GetInt32());
    }
}

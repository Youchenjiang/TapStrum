using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TapStrum.Services;
using Xunit;

namespace TapStrum.Core.Tests;

public class AutoTyperServiceTests
{
    private sealed class MockClipboardService : IClipboardService
    {
        public string? Content { get; set; }

        public Task<string?> GetTextAsync() => Task.FromResult(Content);
        public Task SetTextAsync(string text)
        {
            Content = text;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RunAsync_NullOrEmptyText_ThrowsInvalidOperationExceptionAsync()
    {
        var service = new AutoTyperService();
        var options = new AutoTyperOptions { Text = "" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunAsync(options, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_KeystrokeMode_TypesAllCharactersInOrderAsync()
    {
        var sb = new StringBuilder();
        var progressReports = new List<(string Msg, double Pct)>();

        var service = new AutoTyperService(
            typeAction: c => sb.Append(c),
            tapAction: k => { },
            pasteAction: () => { },
            isEscPressedFunc: () => false,
            sleepAction: (ms, pd, token) => { });

        var options = new AutoTyperOptions
        {
            Text = "Hello World!",
            Mode = AutoTyperMode.Keystroke,
            IntervalMs = 5
        };

        await service.RunAsync(options, CancellationToken.None,
            reportProgress: (msg, pct) => progressReports.Add((msg, pct)));

        Assert.Equal("Hello World!", sb.ToString());
        Assert.Equal(12, progressReports.Count);
        Assert.Equal(100.0, progressReports[^1].Pct);
    }

    [Fact]
    public async Task RunAsync_KeystrokeMode_AppliesTrailingEnterOrTabAsync()
    {
        var tappedKeys = new List<string>();

        var service = new AutoTyperService(
            typeAction: c => { },
            tapAction: k => tappedKeys.Add(k),
            pasteAction: () => { },
            isEscPressedFunc: () => false,
            sleepAction: (ms, pd, token) => { });

        var options = new AutoTyperOptions
        {
            Text = "test",
            Mode = AutoTyperMode.Keystroke,
            TrailingKey = "Enter"
        };

        await service.RunAsync(options, CancellationToken.None);

        Assert.Single(tappedKeys);
        Assert.Equal("enter", tappedKeys[0]);
    }

    [Fact]
    public async Task RunAsync_ClipboardMode_SetsContentPastesAndRestoresAsync()
    {
        var mockClipboard = new MockClipboardService { Content = "OriginalClipboard" };
        bool pasteCalled = false;
        var progressReports = new List<(string Msg, double Pct)>();

        var service = new AutoTyperService(
            typeAction: c => { },
            tapAction: k => { },
            pasteAction: () =>
            {
                pasteCalled = true;
                Assert.Equal("PayloadToPaste", mockClipboard.Content);
            },
            isEscPressedFunc: () => false,
            sleepAction: (ms, pd, token) => { });

        var options = new AutoTyperOptions
        {
            Text = "PayloadToPaste",
            Mode = AutoTyperMode.Clipboard
        };

        await service.RunAsync(
            options,
            CancellationToken.None,
            reportProgress: (msg, pct) => progressReports.Add((msg, pct)),
            clipboardService: mockClipboard);

        Assert.True(pasteCalled);
        // Verify original clipboard content was restored
        Assert.Equal("OriginalClipboard", mockClipboard.Content);
        Assert.Equal(2, progressReports.Count);
        Assert.Equal(100.0, progressReports[^1].Pct);
    }

    [Fact]
    public async Task RunAsync_PanicDetected_ThrowsOperationCanceledExceptionAsync()
    {
        int escCall = 0;
        bool[] escSequence = [true, false, true, false, true];
        var panicDetector = new PanicDetector(
            isEscPressedFunc: () => escSequence[Math.Min(escCall++, escSequence.Length - 1)]);

        var service = new AutoTyperService(
            typeAction: c => { },
            tapAction: k => { },
            pasteAction: () => { },
            isEscPressedFunc: () => false,
            sleepAction: (ms, pd, token) =>
            {
                if (pd is not null && pd.CheckPanic(out var reason))
                {
                    throw new OperationCanceledException(reason ?? PanicDetector.EmergencyPanicMessage);
                }
            });

        var options = new AutoTyperOptions
        {
            Text = "LongTextToBeAborted",
            Mode = AutoTyperMode.Keystroke
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.RunAsync(options, CancellationToken.None, panicDetector: panicDetector));
    }
}

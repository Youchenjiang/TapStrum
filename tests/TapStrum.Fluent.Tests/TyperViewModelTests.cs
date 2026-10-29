namespace TapStrum.Fluent.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using TapStrum;
using TapStrum.Services;
using TapStrum.Fluent.ViewModels;
using Xunit;

public class TyperViewModelTests
{
    private sealed class MockTyperService : IAutoTyperService
    {
        public AutoTyperOptions? LastOptions { get; private set; }
        public bool WasRunCalled { get; private set; }

        public Task RunAsync(
            AutoTyperOptions options,
            CancellationToken cancellationToken,
            Action<string, double>? reportProgress = null,
            IClipboardService? clipboardService = null,
            PanicDetector? panicDetector = null)
        {
            WasRunCalled = true;
            LastOptions = options;
            reportProgress?.Invoke("Typing progress", 75.0);
            return Task.CompletedTask;
        }
    }

    private sealed class MockClipboardService : IClipboardService
    {
        public string? Content { get; set; } = "Sample clipboard payload";

        public Task<string?> GetTextAsync() => Task.FromResult(Content);

        public Task SetTextAsync(string text)
        {
            Content = text;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Constructor_InitializesDefaultValues()
    {
        var vm = new TyperViewModel();

        Assert.Equal(string.Empty, vm.Text);
        Assert.False(vm.IsClipboardMode);
        Assert.Equal(0, vm.TrailingKeyIndex);
        Assert.Equal(0.03, vm.IntervalSeconds);
        Assert.False(vm.JitterEnabled);
        Assert.Equal(3.0, vm.DelaySeconds);
        Assert.False(vm.IsRunning);
        Assert.Empty(vm.History);
    }

    [Fact]
    public void ClearText_EmptiesTextProperty()
    {
        var vm = new TyperViewModel { Text = "hello world" };
        vm.ClearText();
        Assert.Equal(string.Empty, vm.Text);
    }

    [Fact]
    public async Task PasteTextAsync_RetrievesFromClipboardServiceAsync()
    {
        var mockClipboard = new MockClipboardService { Content = "Clipboard text" };
        var vm = new TyperViewModel(clipboardService: mockClipboard);

        await vm.PasteTextAsync();
        Assert.Equal("Clipboard text", vm.Text);
    }

    [Fact]
    public async Task StartAsync_ValidationFails_WhenTextIsEmptyAsync()
    {
        var mockService = new MockTyperService();
        var vm = new TyperViewModel(mockService)
        {
            Text = "   ",
            DelaySeconds = 0
        };

        await vm.StartAsync();

        Assert.False(vm.IsRunning);
        Assert.False(mockService.WasRunCalled);
        Assert.False(string.IsNullOrWhiteSpace(vm.StatusText));
    }

    [Fact]
    public async Task StartAsync_ExecutesServiceAndAddsToHistoryAsync()
    {
        var mockService = new MockTyperService();
        var vm = new TyperViewModel(mockService)
        {
            Text = "echo 123",
            IsClipboardMode = true,
            TrailingKeyIndex = 1,
            IntervalSeconds = 0.05,
            DelaySeconds = 0
        };

        await vm.StartAsync();

        Assert.True(mockService.WasRunCalled);
        Assert.NotNull(mockService.LastOptions);
        Assert.Equal("echo 123", mockService.LastOptions.Text);
        Assert.Equal(AutoTyperMode.Clipboard, mockService.LastOptions.Mode);
        Assert.Equal("enter", mockService.LastOptions.TrailingKey);
        Assert.Single(vm.History);
        Assert.Contains("Pasted 8 chars", vm.History[0]);
    }
}

using System.Threading.Channels;
using System.Windows.Threading;
using HorizonRadioOverlay.Models;

namespace HorizonRadioOverlay.Services;

public sealed class OverlayAnimationQueue : IDisposable
{
    private readonly Channel<(TrackInfo Track, long Id)> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processTask;
    private long _sequenceId;
    private readonly object _gate = new();
    private CancellationTokenSource? _currentRequest;

    public OverlayAnimationQueue(Dispatcher dispatcher, Func<TrackInfo, long, CancellationToken, Task> handler)
    {
        _channel = Channel.CreateBounded<(TrackInfo Track, long Id)>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _processTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var requestItem in _channel.Reader.ReadAllAsync(_cts.Token))
                {
                    if (requestItem.Id != Interlocked.Read(ref _sequenceId)) continue;
                    using var request = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                    lock (_gate)
                    {
                        if (requestItem.Id != Interlocked.Read(ref _sequenceId)) continue;
                        _currentRequest = request;
                    }
                    try
                    {
                        await dispatcher.InvokeAsync(async () => await handler(requestItem.Track, requestItem.Id, request.Token)).Task.Unwrap();
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                    finally
                    {
                        lock (_gate)
                        {
                            if (ReferenceEquals(_currentRequest, request)) _currentRequest = null;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    public void Enqueue(TrackInfo track)
    {
        long id = Interlocked.Increment(ref _sequenceId);
        lock (_gate) _currentRequest?.Cancel();
        _channel.Writer.TryWrite((track, id));
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        lock (_gate) _currentRequest?.Cancel();
        _ = _processTask.ContinueWith(_ => _cts.Dispose(), TaskScheduler.Default);
    }
}

using Hiker.Presenters;
using Hiker.Services;

namespace Hiker.Tests;

// Dobles de lo que la logica usa del movil (ver Services/PlatformContracts.cs).

/// <summary>Dialogos con respuestas preparadas; apunta lo que se pregunto.</summary>
internal sealed class FakeDialogs : IUserDialogs
{
    public List<string> Shown { get; } = [];
    public Queue<bool> Confirms { get; } = new();
    public Queue<string?> Prompts { get; } = new();
    /// <summary>Si esta, se llama mientras el dialogo de confirmacion esta abierto.</summary>
    public Action? DuringConfirm { get; set; }

    public Task AlertAsync(string title, string message, string ok)
    {
        Shown.Add($"{title}|{message}");
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        Shown.Add($"{title}|{message}");
        DuringConfirm?.Invoke();
        return Task.FromResult(Confirms.Count > 0 && Confirms.Dequeue());
    }

    public Task<string?> PromptAsync(string title, string message, string accept, string cancel)
    {
        Shown.Add($"{title}|{message}");
        return Task.FromResult(Prompts.Count > 0 ? Prompts.Dequeue() : null);
    }
}

internal sealed class InlineUi : IUiThread
{
    public void Post(Func<Task> action) => action().GetAwaiter().GetResult();
}

internal sealed class FakeMap : IMapBridge
{
    public List<string> Scripts { get; } = [];
    /// <summary>Cuantas veces contesta «no» a «¿estas listo?» antes de decir que si.</summary>
    public int NotReadyAnswers { get; set; }
    public bool Throws { get; set; }
    public bool ThrowsOnReady { get; set; }

    public Task<string?> EvaluateAsync(string script)
    {
        if (script.StartsWith("(typeof centerOnLocation"))
        {
            if (ThrowsOnReady)
                throw new InvalidOperationException("WebView sin montar");
            return Task.FromResult<string?>(NotReadyAnswers-- > 0 ? "\"no\"" : "\"yes\"");
        }
        Scripts.Add(script);
        if (Throws)
            throw new InvalidOperationException("WebView roto");
        return Task.FromResult<string?>(null);
    }
}

internal sealed class FakeHomeView : IHomeView
{
    public string Title = "", Status = "", Icon = "", Html = "", RecordTitle = "", RecordDetail = "";
    public string FollowTitle = "", FollowDetail = "", CompareTitle = "", CompareDetail = "";
    public bool Recording, FollowVisible, OffRoute, CompareVisible, CompareButtons;
    public List<string> CompareHistory { get; } = [];

    public void SetTitle(string text) => Title = text;
    public void SetStatus(string text) => Status = text;
    public void SetStateIcon(string file) => Icon = file;
    public void SetMapHtml(string html) => Html = html;
    public void ShowRecording(bool recording) => Recording = recording;
    public void SetRecordingLabels(string title, string detail) => (RecordTitle, RecordDetail) = (title, detail);
    public void ShowFollowBar(bool visible) => FollowVisible = visible;
    public void SetFollowStatus(string title, string detail, bool offRoute) => (FollowTitle, FollowDetail, OffRoute) = (title, detail, offRoute);
    public void ShowCompareBar(bool visible, bool withButtons) => (CompareVisible, CompareButtons) = (visible, withButtons);

    public void SetCompareTexts(string title, string detail)
    {
        (CompareTitle, CompareDetail) = (title, detail);
        CompareHistory.Add(title);
    }
}

internal sealed class FakeLocationSource : ILocationSource
{
    public event Action<Location>? LocationChanged;
    public bool StartResult { get; set; } = true;
    public Exception? StartThrows { get; set; }
    public Exception? CurrentThrows { get; set; }
    public Location? Current { get; set; }
    public Location? LastKnown { get; set; }
    public int Starts, Stops;

    public Task<bool> ListeningStartAsync()
    {
        Starts++;
        if (StartThrows is not null)
            throw StartThrows;
        return Task.FromResult(StartResult);
    }

    public Task ListeningStopAsync()
    {
        Stops++;
        return Task.CompletedTask;
    }

    public Task<Location?> GetCurrentLocationAsync() =>
        CurrentThrows is not null ? Task.FromException<Location?>(CurrentThrows) : Task.FromResult(Current);

    public Task<Location?> GetLastKnownLocationAsync() => Task.FromResult(LastKnown);

    public void Raise(Location location) => LocationChanged?.Invoke(location);
    public int Subscribers => LocationChanged?.GetInvocationList().Length ?? 0;
}

internal sealed class FakeCompass : ICompass
{
    public bool IsSupported { get; set; } = true;
    public bool IsMonitoring { get; set; }
    public bool ThrowOnStart { get; set; }
    public bool ThrowOnStop { get; set; }
    public event EventHandler<CompassChangedEventArgs>? ReadingChanged;

    public void Start(SensorSpeed sensorSpeed)
    {
        if (ThrowOnStart)
            throw new FeatureNotSupportedException();
        IsMonitoring = true;
    }

    public void Start(SensorSpeed sensorSpeed, bool applyLowPassFilter) => Start(sensorSpeed);

    public void Stop()
    {
        if (ThrowOnStop)
            throw new InvalidOperationException("brujula rota");
        IsMonitoring = false;
    }

    public void Raise(double heading) => ReadingChanged?.Invoke(this, new CompassChangedEventArgs(new CompassData(heading)));
    public int Subscribers => ReadingChanged?.GetInvocationList().Length ?? 0;
}

internal sealed class FakeTracking : ITrackingService
{
    public List<string> Calls { get; } = [];
    public void Start(string title, string text) => Calls.Add($"start {title} {text}");
    public void Stop() => Calls.Add("stop");
}

internal sealed class FakeBattery : IBatterySettings
{
    public bool Ignored { get; set; }
    public bool Throws { get; set; }
    public List<string> Opened { get; } = [];

    public bool IsOptimizationIgnored() => Throws ? throw new InvalidOperationException("sin PowerManager") : Ignored;
    public void OpenOptimizationSettings() => Opened.Add("optimization");
    public void OpenBatterySaverSettings() => Opened.Add("saver");
}

internal sealed class FakePreferences : Microsoft.Maui.Storage.IPreferences
{
    public Dictionary<string, object?> Values { get; } = [];
    public bool ContainsKey(string key, string? sharedName = null) => Values.ContainsKey(key);
    public void Remove(string key, string? sharedName = null) => Values.Remove(key);
    public void Clear(string? sharedName = null) => Values.Clear();
    public void Set<T>(string key, T value, string? sharedName = null) => Values[key] = value;
    public T Get<T>(string key, T defaultValue, string? sharedName = null) =>
        Values.TryGetValue(key, out var v) && v is T t ? t : defaultValue;
}

internal sealed class FakeNavigator : INavigator
{
    public List<string> Routes { get; } = [];
    public bool Throws { get; set; }

    public Task GoToAsync(string route)
    {
        if (Throws)
            throw new InvalidOperationException("sin Shell");
        Routes.Add(route);
        return Task.CompletedTask;
    }

    public Task ShowRouteInfoAsync(string routeName)
    {
        Routes.Add("info:" + routeName);
        return Task.CompletedTask;
    }
}

internal sealed class FakePicker : IGpxPicker
{
    public (string FileName, string Content)? File { get; set; }
    public Task<(string FileName, string Content)?> PickAsync(string title) => Task.FromResult(File);
}

internal sealed class FakeLinks : ILinkOpener
{
    public List<string> Opened { get; } = [];
    public Task OpenAsync(string uri)
    {
        Opened.Add(uri);
        return Task.CompletedTask;
    }
}

internal sealed class FakeGeolocation : IGeolocation
{
    public bool IsListeningForeground { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool StartResult { get; set; } = true;
    public Exception? StartThrows { get; set; }
    public Location? Fix { get; set; }
    public Location? LastKnown { get; set; }
    public Exception? FixThrows { get; set; }
    public Exception? LastKnownThrows { get; set; }
    public GeolocationListeningRequest? Request { get; private set; }
    public int Stops;

    public event EventHandler<GeolocationLocationChangedEventArgs>? LocationChanged;
    public event EventHandler<GeolocationListeningFailedEventArgs>? ListeningFailed { add { } remove { } }

    public Task<Location?> GetLastKnownLocationAsync() =>
        LastKnownThrows is not null ? Task.FromException<Location?>(LastKnownThrows) : Task.FromResult(LastKnown);

    public Task<Location?> GetLocationAsync(GeolocationRequest request, CancellationToken cancelToken) =>
        FixThrows is not null ? Task.FromException<Location?>(FixThrows) : Task.FromResult(Fix);

    public Task<bool> StartListeningForegroundAsync(GeolocationListeningRequest request)
    {
        Request = request;
        if (StartThrows is not null)
            throw StartThrows;
        IsListeningForeground = StartResult;
        return Task.FromResult(StartResult);
    }

    public void StopListeningForeground()
    {
        Stops++;
        IsListeningForeground = false;
    }

    public void Raise(Location? location) => LocationChanged?.Invoke(this, new GeolocationLocationChangedEventArgs(location!));
    public int Subscribers => LocationChanged?.GetInvocationList().Length ?? 0;
}

internal sealed class FakePermission : ILocationPermission
{
    public PermissionStatus Check { get; set; } = PermissionStatus.Granted;
    public PermissionStatus Request { get; set; } = PermissionStatus.Granted;
    public int Requests;

    public Task<PermissionStatus> CheckAsync() => Task.FromResult(Check);

    public Task<PermissionStatus> RequestAsync()
    {
        Requests++;
        return Task.FromResult(Request);
    }
}

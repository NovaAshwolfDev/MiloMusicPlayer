using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using MiloMusicPlayer.Models;
using MiloMusicPlayer.Services;

namespace MiloMusicPlayer.ViewModels;

public class FriendsViewModel : INotifyPropertyChanged
{
    private readonly FriendsService _api;
    private SessionWebSocket _sessionWs;
    private string? _currentSessionId;

    public ObservableCollection<Friend> Friends { get; } = new();
    public ObservableCollection<ActivityEntry> Activities { get; } = new();
    public ObservableCollection<ChatMessage> ChatMessages { get; } = new();
    public ObservableCollection<string> SessionParticipants { get; } = new();
    public ObservableCollection<UserSearchResult> SearchResults { get; } = new();

    private bool _isFriendsVisible = true;
    public bool IsFriendsVisible { get => _isFriendsVisible; set { _isFriendsVisible = value; OnPropertyChanged(nameof(IsFriendsVisible)); } }

    private bool _isFeedVisible;
    public bool IsFeedVisible { get => _isFeedVisible; set { _isFeedVisible = value; OnPropertyChanged(nameof(IsFeedVisible)); } }

    private bool _creatingSession = false;
    private bool _isSessionVisible;
    public bool IsSessionVisible { get => _isSessionVisible; set { _isSessionVisible = value; OnPropertyChanged(nameof(IsSessionVisible)); } }

    private bool _isInSession;
    public bool IsInSession { get => _isInSession; set { _isInSession = value; OnPropertyChanged(nameof(IsInSession)); } }

    private string _newChatMessage;
    public string NewChatMessage { get => _newChatMessage; set { _newChatMessage = value; OnPropertyChanged(nameof(NewChatMessage)); } }
    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set { _searchQuery = value; OnPropertyChanged(nameof(SearchQuery)); _ = SearchUsers(value); }
    }

    public ICommand ShowFriendsCommand { get; }
    public ICommand ShowFeedCommand { get; }
    public ICommand ShowSessionCommand { get; }
    public ICommand SendChatCommand { get; }
    public ICommand CreateSessionCommand { get; }
    public ICommand LeaveSessionCommand { get; }
    public ICommand JoinSessionCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AddFriendCommand { get; }

    public Action<SessionData> OnSyncPlayback { get; set; }
    private bool _isLoggedIn;
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set { _isLoggedIn = value; OnPropertyChanged(nameof(IsLoggedIn)); }
    }    
    public FriendsViewModel()
    {
        string token = SettingsManager.Current.AuthToken ?? "";
        _api = new FriendsService(token);
        IsLoggedIn = !string.IsNullOrWhiteSpace(token);

        ShowFriendsCommand = new RelayCommand(() => { IsFriendsVisible = true; IsFeedVisible = false; IsSessionVisible = false; });
        ShowFeedCommand = new RelayCommand(() => { IsFriendsVisible = false; IsFeedVisible = true; IsSessionVisible = false; });
        ShowSessionCommand = new RelayCommand(() => { IsFriendsVisible = false; IsFeedVisible = false; IsSessionVisible = true; });
        SendChatCommand = new RelayCommand(async () => await SendChat());
        CreateSessionCommand = new RelayCommand(async () => await CreateSession());
        LeaveSessionCommand = new RelayCommand(async () => await LeaveSession());
        JoinSessionCommand = new RelayCommand<string>(async (id) => await JoinSession(id));
        RefreshCommand = new RelayCommand(async () => await Refresh());
        AddFriendCommand = new RelayCommand<string>(async (id) => await AddFriend(id));

        if (!string.IsNullOrWhiteSpace(token))
        {
            Task.Run(async () =>
            {
                await LoadFriends();
                await LoadActivityFeed();
                StartHeartbeat();
            });
        }
    }
    public async Task Refresh()
    {
        await LoadFriends();
        await LoadActivityFeed();
    }
    private async Task LoadFriends()
    {
        try
        {
            var friends = await _api.GetFriendsAsync();
            Dispatcher.UIThread.Post(() =>
            {
                Friends.Clear();
                foreach (var f in friends) Friends.Add(f);
            });
        }
        catch { }
    }

    private async Task LoadActivityFeed()
    {
        try
        {
            var feed = await _api.GetActivityFeedAsync();
            var latestPerUser = feed
                .GroupBy(a => a.UserId)
                .Select(g => g.OrderByDescending(a => a.Timestamp).First())
                .ToList();

            Dispatcher.UIThread.Post(() =>
            {
                Activities.Clear();
                foreach (var a in latestPerUser) Activities.Add(a);
            });
        }
        catch { /* log */ }
    }    
    private async void StartHeartbeat()
    {
        int ticks = 0;
        while (true)
        {
            await _api.SendHeartbeatAsync(IsInSession ? _currentSessionId : null);
            ticks++;
            if (ticks % 4 == 0)
            {
                await LoadFriends();
                await LoadActivityFeed();
            }
            await Task.Delay(30_000);
        }
    }
    public async Task CreateSession()
    {
        if (_creatingSession) return;
        _creatingSession = true;
        try
        {
            var sessionId = await _api.CreateSessionAsync();
            await JoinSession(sessionId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CreateSession] {ex.Message}");
        }
        finally { _creatingSession = false; }
    }
    public async Task LeaveSession()
    {
        if (_sessionWs != null)
        {
            _sessionWs.StopAudioStream();
            await _sessionWs.CloseAsync();
            _sessionWs.Dispose();
            _sessionWs = null;
        }
        _currentSessionId = null;
        IsInSession = false;
        Dispatcher.UIThread.Post(() =>
        {
            ChatMessages.Clear();
            SessionParticipants.Clear();
            IsFriendsVisible = true;
            IsFeedVisible = false;
            IsSessionVisible = false;
        });
    }
    public async Task AddFriend(string friendUserId)
    {
        try
        {
            await _api.AddFriendAsync(friendUserId);
            await LoadFriends();
        }
        catch { }
    }
    private async Task SearchUsers(string query)
    {
        if (query.Length < 2) { SearchResults.Clear(); return; }
        try
        {
            var results = await _api.SearchUsersAsync(query);
            Dispatcher.UIThread.Post(() =>
            {
                SearchResults.Clear();
                foreach (var r in results) SearchResults.Add(r);
            });
        }
        catch { }
    }

    public async Task JoinSession(string sessionId)
    {
        _currentSessionId = sessionId;
        if (_sessionWs != null)
        {
            await _sessionWs.CloseAsync();
            _sessionWs.Dispose();
        }

        string wsUrl = $"wss://api.miloashwolf.gay/session/{sessionId}" +
            $"?userId={Uri.EscapeDataString(SettingsManager.Current.UserId ?? "")}" +
            $"&displayName={Uri.EscapeDataString(SettingsManager.Current.DisplayName ?? "")}";

        _sessionWs = new SessionWebSocket(wsUrl);

        _sessionWs.OnSessionUpdated += s =>
            Dispatcher.UIThread.Post(() => OnSyncPlayback?.Invoke(s));
        _sessionWs.OnChatReceived += m =>
            Dispatcher.UIThread.Post(() => ChatMessages.Add(m));
        _sessionWs.OnUserJoined += (uid, name) =>
            Dispatcher.UIThread.Post(() => SessionParticipants.Add(name));
        _sessionWs.OnUserLeft += uid =>
            Dispatcher.UIThread.Post(() => SessionParticipants.Remove(uid));
        _sessionWs.OnHostResolved += hostId =>
        {
            Console.WriteLine($"[Session] Host is {hostId}, I am {SettingsManager.Current.UserId}");
            if (hostId == SettingsManager.Current.UserId)
            {
                Console.WriteLine("[Session] I am host, starting audio stream");
                _sessionWs.StartAudioStream();
            }
            else
            {
                Console.WriteLine("[Session] I am peer, not starting audio stream");
            }
        };
        await _sessionWs.ConnectAsync();
        IsInSession = true;
        ShowSessionCommand.Execute(null);
    }

    private async Task SendChat()
    {
        if (string.IsNullOrWhiteSpace(NewChatMessage)) return;
        await _sessionWs?.SendChat(NewChatMessage);
        NewChatMessage = "";
    }

    public async Task SendPlaybackUpdate(double pos, bool playing, string trackPath)
    {
        if (_sessionWs != null)
            await _sessionWs.SendPlaybackUpdate(pos, playing, trackPath);
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public async Task PostActivity(string type, string trackTitle, string trackArtist)
    {
        try { await _api.PostActivityAsync(type, trackTitle, trackArtist); }
        catch { }
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;
    public RelayCommand(Action execute, Func<bool> canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object parameter) => _execute();
    public event EventHandler CanExecuteChanged;
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool> _canExecute;
    public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;
    public void Execute(object parameter) => _execute((T)parameter);
    public event EventHandler CanExecuteChanged;
}
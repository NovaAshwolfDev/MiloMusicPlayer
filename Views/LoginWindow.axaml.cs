using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MiloMusicPlayer.Services;

namespace MiloMusicPlayer.Views;

public partial class LoginWindow : Window
{
    public string Token { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    
    public LoginWindow()
    {
        InitializeComponent();
    }
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void Login_Click(object? sender, RoutedEventArgs e)
    {
        var email = EmailTextBox.Text;
        var password = PasswordTextBox.Text;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ErrorText.Text = "Email and password are required.";
            return;
        }

        var auth = new AuthService();
        try
        {
            Token = await auth.LoginAsync(email, password);
            UserId = auth.UserId;
            DisplayName = auth.DisplayName;
            Close();
        }
        catch
        {
            ErrorText.Text = "Login failed. Check your credentials.";
        }
    }
    private async void Register_Click(object? sender, RoutedEventArgs e)
    {
        var email = EmailTextBox.Text;
        var password = PasswordTextBox.Text;
        var displayName = DisplayNameTextBox.Text;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ErrorText.Text = "Email and password are required.";
            return;
        }
        var auth = new AuthService();
        try
        {
            Token = await auth.RegisterAsync(email, password, displayName);
            Close();
        }
        catch
        {
            ErrorText.Text = "Registration failed. Maybe the email is already in use?";
        }
    }
}
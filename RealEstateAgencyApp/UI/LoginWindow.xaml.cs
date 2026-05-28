using System.Windows;

namespace RealEstateAgencyApp.UI;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        TbLogin.Text = "manager";
    }

    private async void BtnLogin_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (App.Repository is null)
            {
                MessageBox.Show("Репозиторий БД не инициализирован.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string login = TbLogin.Text.Trim();
            string password = PbPassword.Password.Trim();

            bool isValid = await App.Repository.AuthenticateAsync(login, password);
            if (!isValid)
            {
                MessageBox.Show("Неверный логин или пароль.", "Вход", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var main = new MainWindow();
            main.Show();
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка авторизации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExit_OnClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}

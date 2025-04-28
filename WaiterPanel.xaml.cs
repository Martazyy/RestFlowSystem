using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RestFlowSystem.PagesAP;

namespace RestFlowSystem
{
    public partial class WaiterPanel : Window
    {
        private bool isLoggingOut = false; // Флаг для отслеживания смены аккаунта

        public WaiterPanel(string login)
        {
            InitializeComponent();
            MainFrame.Navigate(new PageAP_Orders());

            try
            {
                using (var db = new Entities())
                {
                    var userInfo = db.Users
                        .FirstOrDefault(u => u.Username == login);

                    if (userInfo != null && userInfo.Employees != null && userInfo.Employees.Positions != null)
                    {
                        Labl.Text = $"{userInfo.Employees.FirstName} {userInfo.Employees.LastName} ({userInfo.Employees.Positions.PositionName})";
                    }
                    else
                    {
                        Console.WriteLine("Пользователь или связанные данные не найдены");
                        Labl.Text = "Данные пользователя не найдены";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PagesWP.PageWP_Menu());
        }

        private void Orders_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PageAP_Orders());
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            // Устанавливаем флаг, чтобы указать, что это смена аккаунта
            isLoggingOut = true;

            MainWindow loginWindow = new MainWindow();
            loginWindow.WindowState = this.WindowState;
            loginWindow.Show();
            Close(); // Закрываем окно официанта
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Показываем диалог подтверждения только если это НЕ смена аккаунта
            if (!isLoggingOut)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Уверены, что хотите выйти из приложения?",
                    "Подтверждение выхода",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
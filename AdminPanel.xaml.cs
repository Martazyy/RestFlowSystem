using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RestFlowSystem.PagesAP;

namespace RestFlowSystem
{
    public partial class AdminPanel : Window
    {
        private bool isLoggingOut = false; // Флаг для отслеживания смены аккаунта

        public AdminPanel(string login)
        {
            InitializeComponent();
            MainFrame.Navigate(new PageAP_Employees());

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

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            // Устанавливаем флаг, чтобы указать, что это смена аккаунта
            isLoggingOut = true;

            MainWindow loginWindow = new MainWindow();
            loginWindow.WindowState = this.WindowState;
            loginWindow.Show();
            Close(); // Закрываем окно администратора
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PageAP_Employees());
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PageAP_Users());
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PageAP_Menu());
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PageAP_Purchases());
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PageAP_Orders());
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PageAP_Inventory());
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PageAP_Reports());
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
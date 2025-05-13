using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RestFlowSystem.PagesAP;
using RestFlowSystem.PagesWP;

namespace RestFlowSystem
{
    public partial class ChefPanel : Window
    {
        private bool isLoggingOut = false; 

        public ChefPanel(string login)
        {
            InitializeComponent();
            MainFrame.Navigate(new PageWP_Menu());

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
            MainFrame.Navigate(new PageAP_Menu());
        }

        private void Orders_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PageAP_Orders());
        }

        private void Purchases_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PageAP_Purchases());
        }

        private void Inventory_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PageAP_Inventory());
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            isLoggingOut = true;

            MainWindow loginWindow = new MainWindow();
            loginWindow.WindowState = this.WindowState;
            loginWindow.Show();
            Close(); 
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
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
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace RestFlowSystem
{
    /// <summary>
    /// Главное окно приложения, содержащее логику авторизации пользователей
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Флаг успешного входа
        /// </summary>
        private bool _isSuccessfulLogin = false;

        /// <summary>
        /// Инициализирует главное окно и подписывает обработчик на событие закрытия окна
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            this.Closing += Window_Closing;
        }

        /// <summary>
        /// Вычисляет SHA1-хеш от строки пароля
        /// </summary>
        /// <param name="password">Пароль, который нужно захешировать</param>
        /// <returns>Строка-хеш пароля</returns>
        public static string GetHash(string password)
        {
            using (var hash = SHA1.Create())
            {
                return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(password)).Select(x => x.ToString("X2")));
            }
        }

        /// <summary>
        /// Обрабатывает клик по кнопке входа, выполняет проверку введённых данных и авторизацию пользователя
        /// </summary>
        /// <param name="sender">Источник события</param>
        /// <param name="e">Аргументы события</param>
        /// <exception cref="Exception">Происходит в случае ошибки работы с базой данных</exception>
        /// <remarks>В случае успешного входа открывается соответствующая панель в зависимости от роли пользователя</remarks>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            bool isValid = true;
            ClearErrors();

            string login = LoginTextBox.Text;
            string password = PasswordBox.Visibility == Visibility.Visible
                ? PasswordBox.Password
                : PasswordTextBox.Text;

            if (string.IsNullOrWhiteSpace(login))
            {
                LoginErrorText.Text = "Логин не может быть пустым";
                isValid = false;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                PasswordErrorText.Text = "Пароль не может быть пустым";
                isValid = false;
            }

            if (!isValid)
            {
                MessageBox.Show("Пожалуйста, проверьте правильность заполнения полей.",
                               "Ошибка входа",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var db = new Entities())
                {
                    string passwordHash = GetHash(password);
                    var user = db.Users.AsNoTracking()
                        .FirstOrDefault(u => u.Username == login && u.PasswordHash == passwordHash);

                    if (user == null)
                    {
                        LoginErrorText.Text = "Неверный логин или пароль";
                        PasswordErrorText.Text = "Неверный логин или пароль";
                        MessageBox.Show("Пользователь с такими данными не найден!",
                                       "Ошибка",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);
                        return;
                    }

                    if (!user.IsActive)
                    {
                        MessageBox.Show("Доступ заблокирован! Обратитесь к администратору системы.",
                                       "Доступ запрещен",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Warning);
                        return;
                    }

                    _isSuccessfulLogin = true;
                    if (user.RoleID == 1) 
                    {
                        AdminPanel adminPanel = new AdminPanel(login);
                        adminPanel.WindowState = this.WindowState;
                        adminPanel.Show();
                        this.Close();
                    }
                    else if (user.RoleID == 2) 
                    {
                        WaiterPanel waiterPanel = new WaiterPanel(login);
                        waiterPanel.WindowState = this.WindowState;
                        waiterPanel.Show();
                        this.Close();
                    }
                    else if (user.RoleID == 3) 
                    {
                        ChefPanel chefPanel = new ChefPanel(login);
                        chefPanel.WindowState = this.WindowState;
                        chefPanel.Show();
                        this.Close();
                    }
                }
            }
            
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при входе: {ex.Message}",
                               "Ошибка базы данных",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Очищает текст ошибок, связанных с вводом логина и пароля
        /// </summary>
        private void ClearErrors()
        {
            LoginErrorText.Text = "";
            PasswordErrorText.Text = "";
        }

        /// <summary>
        /// Обрабатывает событие закрытия окна. Подтверждает закрытие, если пользователь не вошел
        /// </summary>
        /// <param name="sender">Источник события</param>
        /// <param name="e">Аргументы события</param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isSuccessfulLogin)
            {
                return;
            }

            MessageBoxResult result = MessageBox.Show("Уверены, что хотите выйти из приложения?",
                                                     "Подтверждение",
                                                     MessageBoxButton.YesNo,
                                                     MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Показывает пароль в виде открытого текста
        /// </summary>
        /// <param name="sender">Источник события</param>
        /// <param name="e">Аргументы события</param>
        private void ShowPasswordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            PasswordTextBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Hidden;
            PasswordTextBox.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Скрывает пароль, показывая его как точки
        /// </summary>
        /// <param name="sender">Источник события</param>
        /// <param name="e">Аргументы события</param>
        private void ShowPasswordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            PasswordBox.Password = PasswordTextBox.Text;
            PasswordTextBox.Visibility = Visibility.Hidden;
            PasswordBox.Visibility = Visibility.Visible;
        }
    }
}

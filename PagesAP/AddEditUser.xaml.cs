using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace RestFlowSystem.PagesAP
{
    public partial class AddEditUser : Page
    {
        private Entities db = Entities.GetContext();
        private Users _user;
        private TextBox _visibleTextBox; // Храним TextBox как поле, чтобы не создавать его заново

        public AddEditUser(Users selectedUser = null, bool isAdding = true)
        {
            InitializeComponent();
            if (selectedUser != null)
            {
                _user = selectedUser;
                Title_Edit.Text = "Редактирование пользователя";
                UsernameTextBox.Text = _user.Username;
            }
            else
            {
                _user = new Users { IsActive = true };
                Title_Edit.Text = "Добавление пользователя";
            }
            DataContext = _user;

            var employees = db.Employees.ToList();
            EmployeeComboBox.ItemsSource = employees;
            if (employees.Count == 0)
            {
                MessageBox.Show("Список сотрудников пуст. Добавьте сотрудников в разделе 'Сотрудники'.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            RoleComboBox.ItemsSource = db.Roles.ToList();

            // Инициализация TextBox для отображения пароля
            InitializePasswordTextBox();
        }

        private void InitializePasswordTextBox()
        {
            // Создаём TextBox один раз при инициализации страницы
            _visibleTextBox = new TextBox
            {
                Style = (Style)FindResource("InputBoxStyle"),
                Text = PasswordBox.Password,
                Margin = PasswordBox.Margin,
                Visibility = Visibility.Collapsed // Скрыт по умолчанию
            };
            _visibleTextBox.TextChanged += (s, ev) => PasswordBox.Password = _visibleTextBox.Text;

            // Добавляем TextBox в Grid один раз
            Grid.SetRow(_visibleTextBox, 0);
            (PasswordBox.Parent as Grid).Children.Add(_visibleTextBox);

            // Сохраняем TextBox в Tag для последующего использования
            PasswordBox.Tag = _visibleTextBox;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            bool isValid = true;
            StringBuilder errors = new StringBuilder();

            if (_user.EmployeeID == 0)
            {
                errors.AppendLine("Выберите сотрудника!");
                isValid = false;
            }

            if (!CheckUsername(_user.Username))
            {
                errors.AppendLine("Логин должен быть не пустым и содержать только буквы, цифры или символы _-!");
                isValid = false;
            }

            string password = PasswordBox.Password;
            if (!string.IsNullOrWhiteSpace(password))
            {
                if (!CheckPassword(password))
                {
                    errors.AppendLine("Пароль должен быть не менее 6 символов и содержать хотя бы одну цифру!");
                    isValid = false;
                }
                else
                {
                    _user.PasswordHash = GetHash(password);
                }
            }
            else if (_user.UserID == 0)
            {
                errors.AppendLine("Введите пароль!");
                isValid = false;
            }

            if (_user.RoleID == 0)
            {
                errors.AppendLine("Выберите роль!");
                isValid = false;
            }

            if (!isValid)
            {
                MessageBox.Show(errors.ToString(), "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existingUser = db.Users.FirstOrDefault(u => u.Username == _user.Username && u.UserID != _user.UserID);
            if (existingUser != null)
            {
                MessageBox.Show("Пользователь с таким логином уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_user.UserID == 0)
            {
                db.Users.Add(_user);
            }

            try
            {
                db.SaveChanges();
                MessageBox.Show("Данные успешно сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigationService?.Navigate(new PageAP_Users());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new PageAP_Users());
        }

        private void ShowPasswordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_visibleTextBox != null)
            {
                _visibleTextBox.Text = PasswordBox.Password;
                _visibleTextBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowPasswordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_visibleTextBox != null)
            {
                PasswordBox.Password = _visibleTextBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                _visibleTextBox.Visibility = Visibility.Collapsed;
            }
        }

        // Методы валидации
        private bool CheckUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            return Regex.IsMatch(username, @"^[a-zA-Z0-9_-]+$");
        }

        private bool CheckPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6) return false;
            return password.Any(char.IsDigit);
        }

        // Метод хэширования пароля
        public static string GetHash(string password)
        {
            using (var hash = SHA1.Create())
            {
                return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(password)).Select(x => x.ToString("X2")));
            }
        }
    }
}
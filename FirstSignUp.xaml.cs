using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace RestFlowSystem
{
    public partial class FirstSignUp : Window
    {
        private bool _isSuccessfulRegistration = false; 

        public FirstSignUp()
        {
            InitializeComponent();
            this.Closing += Window_Closing;

            try
            {
                using (var db = new Entities())
                {
                    var adminUser = db.Users.AsNoTracking().FirstOrDefault(u => u.RoleID == 1);
                    if (adminUser != null)
                    {
                        _isSuccessfulRegistration = true; 
                        MainWindow mw = new MainWindow();
                        mw.WindowState = this.WindowState;
                        mw.Show();
                        this.Close(); 
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке администратора: {ex.Message}",
                               "Ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

        public static string GetHash(string password)
        {
            using (var hash = SHA1.Create())
            {
                return string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(password)).Select(x => x.ToString("X2")));
            }
        }

        private bool isValidPhoneNumber(string phone)
        {
            Regex phoneRegex = new Regex(@"^\+7[0-9]{10}$");
            return phoneRegex.IsMatch(phone);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            bool isValid = true;
            ClearErrors();

            if (!CheckSurname(SurnameTextBox.Text))
            {
                SurnameErrorText.Text = "Только русские/английские буквы, первая заглавная";
                isValid = false;
            }
            if (!CheckName(NameTextBox.Text))
            {
                NameErrorText.Text = "Только русские/английские буквы, первая заглавная";
                isValid = false;
            }
            if (!CheckMiddleName(MiddleNameTextBox.Text))
            {
                MiddleNameErrorText.Text = "Только русские буквы, первая заглавная";
                isValid = false;
            }
            if (!CheckBirthDate(BirthDatePicker.SelectedDate))
            {
                BirthDateErrorText.Text = "Возраст должен быть от 18 до 90 лет";
                isValid = false;
            }
            if (!CheckPosition(PositionTextBox.Text))
            {
                PositionErrorText.Text = "Только русские буквы";
                isValid = false;
            }
            if (!isValidPhoneNumber(PhoneTextBox.Text))
            {
                PhoneErrorText.Text = "Формат: +7XXXXXXXXXX";
                isValid = false;
            }
            if (!CheckLogin(LoginTextBox.Text))
            {
                LoginErrorText.Text = "Логин не может быть пустым";
                isValid = false;
            }
            string password = ShowPasswordCheckBox.IsChecked == true ? PasswordTextBox.Text : PasswordBox.Password;
            if (!CheckPassword(password))
            {
                PasswordErrorText.Text = "Минимум 6 символов, хотя бы одна цифра";
                isValid = false;
            }

            if (!isValid)
            {
                MessageBox.Show("Пожалуйста, проверьте правильность заполнения полей.",
                               "Ошибки регистрации",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var db = new Entities())
                {
                    var user = db.Users.AsNoTracking().FirstOrDefault(u => u.Username == LoginTextBox.Text);
                    if (user != null)
                    {
                        LoginErrorText.Text = "Логин уже занят";
                        MessageBox.Show("Пользователь с таким логином уже существует!",
                                       "Ошибка",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);
                        return;
                    }

                    var phoneUser = db.Users.AsNoTracking().FirstOrDefault(u => u.Employees.Phone == PhoneTextBox.Text);
                    if (phoneUser != null)
                    {
                        PhoneErrorText.Text = "Телефон уже зарегистрирован";
                        MessageBox.Show("Пользователь с таким номером телефона уже существует!",
                                       "Ошибка",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);
                        return;
                    }
                }

                RegisterNewUser();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при проверке данных: {ex.Message}",
                               "Ошибка",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

        private void ClearErrors()
        {
            NameErrorText.Text = "";
            SurnameErrorText.Text = "";
            MiddleNameErrorText.Text = "";
            BirthDateErrorText.Text = "";
            PositionErrorText.Text = "";
            PhoneErrorText.Text = "";
            LoginErrorText.Text = "";
            PasswordErrorText.Text = "";
        }

        private bool CheckSurname(string surname)
        {
            if (string.IsNullOrWhiteSpace(surname)) return false;
            return Regex.IsMatch(surname, @"^[А-Яа-яЁёA-Za-z]+$") && char.IsUpper(surname[0]);
        }

        private bool CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return Regex.IsMatch(name, @"^[А-Яа-яЁёA-Za-z]+$") && char.IsUpper(name[0]);
        }

        private bool CheckMiddleName(string middleName)
        {
            if (string.IsNullOrWhiteSpace(middleName)) return true;
            return Regex.IsMatch(middleName, @"^[А-Яа-яЁё]+$") && char.IsUpper(middleName[0]);
        }

        private bool CheckBirthDate(DateTime? birthDate)
        {
            if (!birthDate.HasValue) return false;

            DateTime today = DateTime.Today;
            int age = today.Year - birthDate.Value.Year;
            if (birthDate.Value.Date > today.AddYears(-age)) age--;
            return birthDate.Value <= today && age >= 18 && age <= 90;
        }

        private bool CheckPosition(string position)
        {
            if (string.IsNullOrWhiteSpace(position)) return false;
            return Regex.IsMatch(position, @"^[А-Яа-яЁё]+$");
        }

        private bool CheckLogin(string login)
        {
            return !string.IsNullOrWhiteSpace(login);
        }

        private bool CheckPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6) return false;
            return password.Any(char.IsDigit);
        }

        private void RegisterNewUser()
        {
            try
            {
                using (var db = new Entities())
                {
                    var adminRole = db.Roles.FirstOrDefault(r => r.RoleID == 0);
                    if (adminRole == null)
                    {
                        adminRole = new Roles { RoleID = 0, RoleName = "Администратор" };
                        db.Roles.Add(adminRole);
                        db.SaveChanges();
                    }

                    var position = db.Positions.FirstOrDefault(p => p.PositionName == PositionTextBox.Text);
                    if (position == null)
                    {
                        position = new Positions { PositionName = PositionTextBox.Text };
                        db.Positions.Add(position);
                        db.SaveChanges();
                    }

                    var employee = new Employees
                    {
                        FirstName = NameTextBox.Text,
                        LastName = SurnameTextBox.Text,
                        MiddleName = MiddleNameTextBox.Text,
                        BirthDate = BirthDatePicker.SelectedDate.Value,
                        Phone = PhoneTextBox.Text,
                        PositionID = position.PositionID,
                        Salary = 0.0m,
                        HireDate = DateTime.Now
                    };

                    db.Employees.Add(employee);
                    db.SaveChanges();

                    var user = new Users
                    {
                        EmployeeID = employee.EmployeeID,
                        Username = LoginTextBox.Text,
                        PasswordHash = GetHash(ShowPasswordCheckBox.IsChecked == true ? PasswordTextBox.Text : PasswordBox.Password),
                        RoleID = adminRole.RoleID
                    };

                    db.Users.Add(user);
                    db.SaveChanges();

                    _isSuccessfulRegistration = true;
                    MainWindow mw = new MainWindow();
                    mw.WindowState = this.WindowState;
                    mw.Show();
                    this.Close(); 
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при регистрации пользователя: {ex.Message}",
                               "Ошибка базы данных",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isSuccessfulRegistration)
            {
                return;
            }

            var result = MessageBox.Show("Вы уверены, что хотите закрыть окно?",
                                         "Закрытие окна",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true; 
            }
        }

        private void ShowPasswordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            PasswordTextBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Hidden;
            PasswordTextBox.Visibility = Visibility.Visible;
        }

        private void ShowPasswordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            PasswordBox.Password = PasswordTextBox.Text;
            PasswordTextBox.Visibility = Visibility.Hidden;
            PasswordBox.Visibility = Visibility.Visible;
        }
    }
}
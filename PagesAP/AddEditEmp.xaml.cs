using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestFlowSystem.PagesAP
{
    public partial class AddEditEmp : Page
    {
        private Entities db = Entities.GetContext();
        private Employees _employee;

        public AddEditEmp(Employees selectedEmployee = null, bool isAdding = true)
        {
            InitializeComponent();
            if (selectedEmployee != null)
            {
                _employee = selectedEmployee;
                Title_Edit.Text = "Редактирование сотрудника";
            }
            else
            {
                _employee = new Employees { HireDate = DateTime.Now };
                Title_Edit.Text = "Добавление сотрудника";
            }
            DataContext = _employee;
            PositionComboBox.ItemsSource = db.Positions.ToList();
        }

        private void PositionComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string newPositionName = PositionComboBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(newPositionName))
                {
                    try
                    {
                        var existingPosition = db.Positions.FirstOrDefault(p => p.PositionName == newPositionName);
                        if (existingPosition == null)
                        {
                            var newPosition = new Positions { PositionName = newPositionName };
                            db.Positions.Add(newPosition);
                            db.SaveChanges();

                            PositionComboBox.ItemsSource = db.Positions.ToList();
                            PositionComboBox.SelectedItem = newPosition;
                            _employee.PositionID = newPosition.PositionID;
                            MessageBox.Show($"Должность '{newPositionName}' добавлена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            PositionComboBox.SelectedItem = existingPosition;
                            _employee.PositionID = existingPosition.PositionID;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при добавлении должности: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            bool isValid = true;
            ClearErrors();

            if (!CheckLastName(_employee.LastName))
            {
                LastNameErrorText.Text = "Только русские/английские буквы, первая заглавная";
                isValid = false;
            }
            if (!CheckFirstName(_employee.FirstName))
            {
                FirstNameErrorText.Text = "Только русские/английские буквы, первая заглавная";
                isValid = false;
            }
            if (!CheckMiddleName(_employee.MiddleName))
            {
                MiddleNameErrorText.Text = "Только русские буквы, первая заглавная (или пусто)";
                isValid = false;
            }
            if (!CheckBirthDate(_employee.BirthDate))
            {
                BirthDateErrorText.Text = "Возраст должен быть от 18 до 90 лет";
                isValid = false;
            }
            if (!CheckPhone(_employee.Phone))
            {
                PhoneErrorText.Text = "Формат: +7XXXXXXXXXX";
                isValid = false;
            }
            if (!CheckPosition(_employee.PositionID))
            {
                PositionErrorText.Text = "Выберите или добавьте должность";
                isValid = false;
            }
            if (!CheckSalary(_employee.Salary))
            {
                SalaryErrorText.Text = "Зарплата должна быть числом больше 0";
                isValid = false;
            }
            if (!CheckHireDate(_employee.HireDate))
            {
                HireDateErrorText.Text = "Дата приема не может быть в будущем";
                isValid = false;
            }

            if (!isValid)
            {
                MessageBox.Show("Пожалуйста, проверьте правильность заполнения полей.", "Ошибки ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var existingEmployee = db.Employees.FirstOrDefault(emp => emp.Phone == _employee.Phone && emp.EmployeeID != _employee.EmployeeID);
                if (existingEmployee != null)
                {
                    PhoneErrorText.Text = "Телефон уже зарегистрирован";
                    MessageBox.Show("Сотрудник с таким номером телефона уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке телефона: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_employee.EmployeeID == 0)
            {
                db.Employees.Add(_employee);
            }

            try
            {
                db.SaveChanges();
                MessageBox.Show("Данные успешно сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigationService.Navigate(new PageAP_Employees());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new PageAP_Employees());
        }

        private void ClearErrors()
        {
            LastNameErrorText.Text = "";
            FirstNameErrorText.Text = "";
            MiddleNameErrorText.Text = "";
            BirthDateErrorText.Text = "";
            PhoneErrorText.Text = "";
            PositionErrorText.Text = "";
            SalaryErrorText.Text = "";
            HireDateErrorText.Text = "";
        }

        private bool CheckLastName(string lastName)
        {
            if (string.IsNullOrWhiteSpace(lastName)) return false;
            return Regex.IsMatch(lastName, @"^[А-Яа-яЁёA-Za-z]+$") && char.IsUpper(lastName[0]);
        }

        private bool CheckFirstName(string firstName)
        {
            if (string.IsNullOrWhiteSpace(firstName)) return false;
            return Regex.IsMatch(firstName, @"^[А-Яа-яЁёA-Za-z]+$") && char.IsUpper(firstName[0]);
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

        private bool CheckPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return false;
            Regex phoneRegex = new Regex(@"^\+7[0-9]{10}$");
            return phoneRegex.IsMatch(phone);
        }

        private bool CheckPosition(int positionId)
        {
            return positionId > 0; 
        }

        private bool CheckSalary(decimal? salary)
        {
            return salary.HasValue && salary.Value > 0;
        }

        private bool CheckHireDate(DateTime? hireDate)
        {
            if (!hireDate.HasValue) return false;
            DateTime today = DateTime.Today;
            return hireDate.Value.Date <= today;
        }
    }
}
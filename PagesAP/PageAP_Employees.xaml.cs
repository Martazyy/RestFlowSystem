using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace RestFlowSystem.PagesAP
{
    public partial class PageAP_Employees : Page
    {
        private Entities db = Entities.GetContext();
        private DispatcherTimer _searchTimer;
        private ObservableCollection<Employees> _employees;
        private CollectionViewSource _employeeViewSource;

        public PageAP_Employees()
        {
            InitializeComponent();

            _employees = new ObservableCollection<Employees>();
            _employeeViewSource = (CollectionViewSource)Resources["EmployeeViewSource"];
            _employeeViewSource.Source = _employees;

            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                RefreshView();
            };

            LoadPositions();
            LoadEmployees();
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                db.ChangeTracker.Entries().ToList().ForEach(x => x.Reload());
                LoadPositions();
                LoadEmployees();
            }
        }

        private void LoadPositions()
        {
            try
            {
                var positions = db.Positions.ToList();
                positions.Insert(0, new Positions { PositionName = "Все должности" });
                SortPosition.ItemsSource = positions;
                SortPosition.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке должностей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadEmployees()
        {
            try
            {
                _employees.Clear();
                var employees = db.Employees.Include("Positions").ToList();
                foreach (var employee in employees)
                {
                    _employees.Add(employee);
                }
                RefreshView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке сотрудников: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                NoDataMessage.Visibility = Visibility.Visible;
            }
        }

        private void RefreshView()
        {
            try
            {
                _employeeViewSource.View.Filter = item =>
                {
                    var employee = item as Employees;
                    if (employee == null) return false;
                    string searchText = SearchEmployeeName.Text?.ToLower() ?? "";
                    bool nameMatch = string.IsNullOrWhiteSpace(searchText) ||
                                     (employee.LastName != null && employee.LastName.ToLower().Contains(searchText)) ||
                                     (employee.FirstName != null && employee.FirstName.ToLower().Contains(searchText)) ||
                                     (employee.MiddleName != null && employee.MiddleName.ToLower().Contains(searchText)) ||
                                     (employee.BirthDate != null && employee.BirthDate.ToString("dd.MM.yyyy").Contains(searchText)) ||
                                     (employee.Phone != null && employee.Phone.ToLower().Contains(searchText)) ||
                                     (employee.Positions?.PositionName != null && employee.Positions.PositionName.ToLower().Contains(searchText)) ||
                                     (employee.Salary.ToString().Contains(searchText)) ||
                                     (employee.HireDate != null && employee.HireDate.ToString("dd.MM.yyyy").Contains(searchText));

                    var selectedPosition = SortPosition.SelectedItem as Positions;
                    bool positionMatch = selectedPosition == null ||
                                         selectedPosition.PositionName == "Все должности" ||
                                         (employee.PositionID == selectedPosition.PositionID);

                    return nameMatch && positionMatch;
                };

                NoDataMessage.Visibility = _employeeViewSource.View.Cast<object>().Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при фильтрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Information);
                NoDataMessage.Visibility = Visibility.Visible;
            }
        }

        private void SearchEmployeeName_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void SortPosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshView();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            SearchEmployeeName.Text = string.Empty;
            SortPosition.SelectedIndex = 0;
            RefreshView();
        }

        private void AddEmployee_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new AddEditEmp(null, isAdding: true));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии страницы добавления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditEmployee_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new AddEditEmp((sender as Button).DataContext as Employees, isAdding: false));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии страницы редактирования: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            var employeesForRemoving = DGridEmployees.SelectedItems.Cast<Employees>().ToList();

            if (employeesForRemoving.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одного сотрудника для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StringBuilder employeesWithOrders = new StringBuilder();
            foreach (var employee in employeesForRemoving)
            {
                try
                {
                    var relatedOrders = db.Orders
                        .Where(o => o.WaiterID == employee.EmployeeID)
                        .ToList();

                    if (relatedOrders.Any())
                    {
                        employeesWithOrders.AppendLine($"Сотрудник {employee.LastName} {employee.FirstName} прикреплён к {relatedOrders.Count} заказам.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при проверке заказов для сотрудника {employee.LastName} {employee.FirstName}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            if (employeesWithOrders.Length > 0)
            {
                var result = MessageBox.Show(
                    $"{employeesWithOrders}\n\nУдаление невозможно, пока сотрудник прикреплён к заказам. Перейти на страницу заказов для управления?",
                    "Предупреждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        NavigationService?.Navigate(new PageAP_Orders());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при переходе на страницу заказов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                return;
            }

            if (MessageBox.Show($"Вы точно хотите удалить выбранные элементы? Количество: {employeesForRemoving.Count}", "Внимание",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    db.Employees.RemoveRange(employeesForRemoving);
                    db.SaveChanges();
                    MessageBox.Show("Данные успешно удалены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadEmployees();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace RestFlowSystem.PagesAP
{
    public partial class PageAP_Users : Page
    {
        private Entities _context = Entities.GetContext();
        private DispatcherTimer _searchTimer;
        private ObservableCollection<Users> _users;
        private CollectionViewSource _userViewSource;

        public PageAP_Users()
        {
            InitializeComponent();

            _users = new ObservableCollection<Users>();
            _userViewSource = (CollectionViewSource)Resources["UserViewSource"];
            _userViewSource.Source = _users;

            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                RefreshView();
            };

            LoadRoles();
            LoadUsers();
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                _context.ChangeTracker.Entries().ToList().ForEach(x => x.Reload());
                LoadRoles();
                LoadUsers();
            }
        }

        private void LoadRoles()
        {
            try
            {
                FilterRole.Items.Clear();
                FilterRole.Items.Add(new ComboBoxItem { Content = "Все" });
                foreach (var role in _context.Roles.ToList())
                {
                    FilterRole.Items.Add(new ComboBoxItem { Content = role.RoleName, Tag = role.RoleID });
                }
                FilterRole.SelectedIndex = 0;

                FilterAccess.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке ролей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadUsers()
        {
            try
            {
                _users.Clear();
                var users = _context.Users
                    .Include(u => u.Employees.Positions)
                    .Include(u => u.Roles)
                    .ToList();
                foreach (var user in users)
                {
                    _users.Add(user);
                }
                RefreshView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке пользователей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                NoDataMessage.Visibility = Visibility.Visible;
            }
        }

        private void RefreshView()
        {
            try
            {
                _userViewSource.View.Filter = item =>
                {
                    var user = item as Users;
                    if (user == null) return false;

                    string searchText = SearchUser.Text?.ToLower() ?? "";
                    bool searchMatch = string.IsNullOrWhiteSpace(searchText) ||
                                       (user.Employees?.LastName?.ToLower().Contains(searchText) ?? false) ||
                                       (user.Employees?.FirstName?.ToLower().Contains(searchText) ?? false) ||
                                       (user.Employees?.MiddleName?.ToLower().Contains(searchText) ?? false) ||
                                       (user.Username?.ToLower().Contains(searchText) ?? false) ||
                                       (user.PasswordHash?.ToLower().Contains(searchText) ?? false) ||
                                       (user.Employees?.Positions?.PositionName?.ToLower().Contains(searchText) ?? false) ||
                                       (user.Roles?.RoleName?.ToLower().Contains(searchText) ?? false);

                    var selectedRole = FilterRole.SelectedItem as ComboBoxItem;
                    bool roleMatch = selectedRole == null ||
                                     selectedRole.Content.ToString() == "Все" ||
                                     (selectedRole.Tag != null && user.RoleID == (int)selectedRole.Tag);

                    var selectedAccess = FilterAccess.SelectedItem as ComboBoxItem;
                    bool accessMatch = selectedAccess == null ||
                                       selectedAccess.Content.ToString() == "Все" ||
                                       (selectedAccess.Content.ToString() == "Есть" && user.IsActive) ||
                                       (selectedAccess.Content.ToString() == "Нет" && !user.IsActive);

                    return searchMatch && roleMatch && accessMatch;
                };

                NoDataMessage.Visibility = _userViewSource.View.Cast<object>().Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при фильтрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Information);
                NoDataMessage.Visibility = Visibility.Visible;
            }
        }

        private void SearchUser_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void FilterRole_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshView();
        }

        private void FilterAccess_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshView();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            SearchUser.Text = string.Empty;
            FilterRole.SelectedIndex = 0;
            FilterAccess.SelectedIndex = 0;
            RefreshView();
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new AddEditUser(null, isAdding: true));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии страницы добавления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new AddEditUser((sender as Button).DataContext as Users, isAdding: false));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии страницы редактирования: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var usersForRemoving = DGridUsers.SelectedItems.Cast<Users>().ToList();

            if (usersForRemoving.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одного пользователя для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Вы точно хотите удалить выбранные элементы? Количество: {usersForRemoving.Count}", "Внимание",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    _context.Users.RemoveRange(usersForRemoving);
                    _context.SaveChanges();
                    MessageBox.Show("Данные успешно удалены!");
                    LoadUsers();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void NoCheckBox_Loaded(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                var user = checkBox.DataContext as Users;
                if (user != null)
                {
                    checkBox.IsChecked = !user.IsActive;
                }
            }
        }
    }
}
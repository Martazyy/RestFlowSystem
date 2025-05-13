using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace RestFlowSystem.PagesAP
{
    public partial class PageAP_Menu : Page
    {
        private Entities _context = Entities.GetContext();
        private DispatcherTimer _searchTimer;
        private ObservableCollection<Menu> _menu;
        private CollectionViewSource _menuViewSource;

        public PageAP_Menu()
        {
            InitializeComponent();

            _menu = new ObservableCollection<Menu>();
            _menuViewSource = (CollectionViewSource)Resources["MenuViewSource"];
            _menuViewSource.Source = _menu;

            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                RefreshView();
            };

            LoadCategories();
            LoadMenuData();
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                _context.ChangeTracker.Entries().ToList().ForEach(x => x.Reload());
                LoadCategories();
                LoadMenuData();
            }
        }

        private void LoadCategories()
        {
            try
            {
                FilterCategory.Items.Clear();
                FilterCategory.Items.Add(new ComboBoxItem { Content = "Все" });
                foreach (var category in _context.MenuCategories.ToList())
                {
                    FilterCategory.Items.Add(new ComboBoxItem { Content = category.CategoryName, Tag = category.CategoryID });
                }
                FilterCategory.SelectedIndex = 0;

                FilterStopList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке категорий: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadMenuData()
        {
            try
            {
                _menu.Clear();
                var menuItems = _context.Menu
                    .Include(m => m.MenuCategories)
                    .ToList();
                foreach (var item in menuItems)
                {
                    _menu.Add(item);
                }
                RefreshView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                NoDataMessage.Visibility = Visibility.Visible;
            }
        }

        private void RefreshView()
        {
            try
            {
                _menuViewSource.View.Filter = item =>
                {
                    var menuItem = item as Menu;
                    if (menuItem == null) return false;

                    string searchText = SearchMenu.Text?.ToLower() ?? "";
                    bool searchMatch = string.IsNullOrWhiteSpace(searchText) ||
                                       (menuItem.Name?.ToLower().Contains(searchText) ?? false) ||
                                       (menuItem.Description?.ToLower().Contains(searchText) ?? false) ||
                                       (menuItem.MenuCategories?.CategoryName?.ToLower().Contains(searchText) ?? false) ||
                                       (menuItem.Price.ToString("F2").Contains(searchText));

                    var selectedCategory = FilterCategory.SelectedItem as ComboBoxItem;
                    bool categoryMatch = selectedCategory == null ||
                                         selectedCategory.Content.ToString() == "Все" ||
                                         (selectedCategory.Tag != null && menuItem.CategoryID == (int)selectedCategory.Tag);

                    var selectedStopList = FilterStopList.SelectedItem as ComboBoxItem;
                    bool stopListMatch = selectedStopList == null ||
                                         selectedStopList.Content.ToString() == "Все" ||
                                         (selectedStopList.Content.ToString() == "В стоп-листе" && menuItem.StopList) ||
                                         (selectedStopList.Content.ToString() == "Не в стоп-листе" && !menuItem.StopList);

                    return searchMatch && categoryMatch && stopListMatch;
                };

                NoDataMessage.Visibility = _menuViewSource.View.Cast<object>().Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при фильтрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Information);
                NoDataMessage.Visibility = Visibility.Visible;
            }
        }

        private void SearchMenu_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void FilterCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshView();
        }

        private void FilterStopList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshView();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            SearchMenu.Text = string.Empty;
            FilterCategory.SelectedIndex = 0;
            FilterStopList.SelectedIndex = 0;
            RefreshView();
        }

        private void AddMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new AddEditMenu(null, isAdding: true));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии страницы добавления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteMenu_Click(object sender, RoutedEventArgs e)
        {
            var menuForRemoving = DGridMenu.SelectedItems.Cast<Menu>().ToList();

            if (menuForRemoving.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одно блюдо для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var menuWithOrders = menuForRemoving
                .Select(menu => new
                {
                    MenuItem = menu,
                    OrdersCount = _context.OrderItems.Count(o => o.MenuID == menu.MenuID)
                })
                .Where(x => x.OrdersCount > 0)
                .ToList();

            if (menuWithOrders.Any())
            {
                string warningMessage = "Некоторые блюда не могут быть удалены, так как они участвуют в заказах:\n\n";
                foreach (var item in menuWithOrders)
                {
                    warningMessage += $"- \"{item.MenuItem.Name}\" участвует в {item.OrdersCount} заказ(ах)\n";
                }

                MessageBox.Show(warningMessage, "Удаление невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Вы точно хотите удалить выбранные элементы? Количество: {menuForRemoving.Count}", "Внимание",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    _context.Menu.RemoveRange(menuForRemoving);
                    _context.SaveChanges();
                    MessageBox.Show("Данные успешно удалены!");
                    LoadMenuData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void EditMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new AddEditMenu((sender as Button).DataContext as Menu, isAdding: false));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии страницы редактирования: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image image && image.DataContext is Menu menuItem)
            {
                PopupImage.Source = image.Source;

                ImagePopup.IsOpen = true;

                ImagePopup.Focus();
            }
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Border)
            {
                ImagePopup.IsOpen = false;
            }
        }
    }
}
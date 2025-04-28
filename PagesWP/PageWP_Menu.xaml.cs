using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace RestFlowSystem.PagesWP
{
    public partial class PageWP_Menu : Page
    {
        private Entities _context = Entities.GetContext();
        private DispatcherTimer _searchTimer;
        private ObservableCollection<Menu> _menu;
        private CollectionViewSource _menuViewSource;

        public PageWP_Menu()
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

                    // Поиск по всем полям
                    string searchText = SearchMenu.Text?.ToLower() ?? "";
                    bool searchMatch = string.IsNullOrWhiteSpace(searchText) ||
                                       (menuItem.Name?.ToLower().Contains(searchText) ?? false) ||
                                       (menuItem.Description?.ToLower().Contains(searchText) ?? false) ||
                                       (menuItem.MenuCategories?.CategoryName?.ToLower().Contains(searchText) ?? false) ||
                                       (menuItem.Price.ToString("F2").Contains(searchText));

                    // Фильтрация по категории
                    var selectedCategory = FilterCategory.SelectedItem as ComboBoxItem;
                    bool categoryMatch = selectedCategory == null ||
                                         selectedCategory.Content.ToString() == "Все" ||
                                         (selectedCategory.Tag != null && menuItem.CategoryID == (int)selectedCategory.Tag);

                    // Фильтрация по стоп-листу
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

        private void ShowDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new ShowMenu((sender as Button).DataContext as Menu));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии страницы просмотра: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
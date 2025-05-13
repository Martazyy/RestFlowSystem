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
    public partial class PageAP_Orders : Page
    {
        private Entities db = Entities.GetContext();
        private DispatcherTimer _searchTimer;
        private ObservableCollection<dynamic> _orders;
        private CollectionViewSource _orderViewSource;

        public PageAP_Orders()
        {
            InitializeComponent();

            _orders = new ObservableCollection<dynamic>();
            _orderViewSource = (CollectionViewSource)Resources["OrderViewSource"];
            _orderViewSource.Source = _orders;

            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                RefreshView();
            };

            LoadStatusesAndPaymentMethods();
            LoadOrders();
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                db.ChangeTracker.Entries().ToList().ForEach(x => x.Reload());
                LoadStatusesAndPaymentMethods();
                LoadOrders();
            }
        }

        private void LoadStatusesAndPaymentMethods()
        {
            try
            {
                FilterStatus.Items.Clear();
                FilterStatus.Items.Add(new ComboBoxItem { Content = "Все" });
                foreach (var status in db.OrderStatuses.ToList())
                {
                    FilterStatus.Items.Add(new ComboBoxItem { Content = status.StatusName, Tag = status.StatusID });
                }
                FilterStatus.SelectedIndex = 0;

                FilterPaymentMethod.Items.Clear();
                FilterPaymentMethod.Items.Add(new ComboBoxItem { Content = "Все" });
                foreach (var paymentMethod in db.PaymentMethods.ToList())
                {
                    FilterPaymentMethod.Items.Add(new ComboBoxItem { Content = paymentMethod.MethodName, Tag = paymentMethod.PaymentMethodID });
                }
                FilterPaymentMethod.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке статусов или типов оплаты: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadOrders()
        {
            try
            {
                _orders.Clear();
                var orders = db.Orders
                    .Include(o => o.Employees)
                    .Include(o => o.OrderStatuses)
                    .Include(o => o.PaymentMethods)
                    .ToList()
                    .Select(o => new
                    {
                        o.OrderID,
                        o.TableNum,
                        WaiterName = o.Employees != null ? $"{o.Employees.FirstName} {o.Employees.LastName}" : "Не указан",
                        o.OrderDate,
                        StatusName = o.OrderStatuses?.StatusName,
                        PaymentMethodName = o.PaymentMethods?.MethodName,
                        o.TotalAmount,
                        o.StatusID,
                        o.PaymentMethodID
                    })
                    .ToList();
                foreach (var order in orders)
                {
                    _orders.Add(order);
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
                _orderViewSource.View.Filter = item =>
                {
                    var order = item as dynamic;
                    if (order == null) return false;

                    string searchText = SearchOrder.Text?.ToLower() ?? "";
                    bool searchMatch = string.IsNullOrWhiteSpace(searchText) ||
                                       (order.OrderID.ToString().Contains(searchText)) ||
                                       (order.TableNum.ToString().Contains(searchText)) ||
                                       (order.WaiterName?.ToLower().Contains(searchText) ?? false) ||
                                       (order.OrderDate.ToString("dd.MM.yyyy HH:mm").Contains(searchText)) ||
                                       (order.StatusName?.ToLower().Contains(searchText) ?? false) ||
                                       (order.PaymentMethodName?.ToLower().Contains(searchText) ?? false) ||
                                       (order.TotalAmount.ToString("F2").Contains(searchText));

                    var selectedStatus = FilterStatus.SelectedItem as ComboBoxItem;
                    bool statusMatch = selectedStatus == null ||
                                       selectedStatus.Content.ToString() == "Все" ||
                                       (selectedStatus.Tag != null && order.StatusID == (int)selectedStatus.Tag);

                    var selectedPaymentMethod = FilterPaymentMethod.SelectedItem as ComboBoxItem;
                    bool paymentMethodMatch = selectedPaymentMethod == null ||
                                              selectedPaymentMethod.Content.ToString() == "Все" ||
                                              (selectedPaymentMethod.Tag != null && order.PaymentMethodID == (int)selectedPaymentMethod.Tag);

                    return searchMatch && statusMatch && paymentMethodMatch;
                };

                NoDataMessage.Visibility = _orderViewSource.View.Cast<object>().Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при фильтрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Information);
                NoDataMessage.Visibility = Visibility.Visible;
            }
        }

        private void SearchOrder_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void FilterStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshView();
        }

        private void FilterPaymentMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshView();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            SearchOrder.Text = string.Empty;
            FilterStatus.SelectedIndex = 0;
            FilterPaymentMethod.SelectedIndex = 0;
            RefreshView();
        }

        private void AddOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new AddEditOrder(null));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии страницы добавления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = DGridOrders.SelectedItems.Cast<object>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Выберите хотя бы один заказ для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ordersForRemoving = selectedItems
                .Select(item => (int)item.GetType().GetProperty("OrderID").GetValue(item))
                .ToList();

            var ordersToRemove = db.Orders
                .Where(o => ordersForRemoving.Contains(o.OrderID))
                .ToList();

            if (ordersToRemove.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы один заказ для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Вы точно хотите удалить выбранные заказы? Количество: {ordersToRemove.Count}", "Внимание",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    foreach (var order in ordersToRemove)
                    {
                        var orderItems = db.OrderItems
                            .Where(oi => oi.OrderID == order.OrderID)
                            .Include(oi => oi.Menu.DishIngredients)
                            .ToList();

                        foreach (var item in orderItems)
                        {
                            foreach (var dishIngredient in item.Menu.DishIngredients)
                            {
                                var inventory = db.Inventory
                                    .FirstOrDefault(i => i.IngredientID == dishIngredient.IngredientID);
                                if (inventory != null)
                                {
                                    inventory.Quantity += dishIngredient.Quantity * item.Quantity;
                                }
                            }
                        }
                    }

                    db.Orders.RemoveRange(ordersToRemove);
                    db.SaveChanges();
                    MessageBox.Show("Заказы успешно удалены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadOrders();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EditOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var orderId = (int)(sender as Button).Tag;
                var order = db.Orders.FirstOrDefault(o => o.OrderID == orderId);
                NavigationService.Navigate(new AddEditOrder(order));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии страницы редактирования: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
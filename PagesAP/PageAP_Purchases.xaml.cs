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
    public partial class PageAP_Purchases : Page
    {
        private Entities db = Entities.GetContext();
        private DispatcherTimer _searchTimer;
        private ObservableCollection<Purchases> _purchases;
        private CollectionViewSource _purchaseViewSource;
        private DateTime? _minDate;
        private DateTime? _maxDate;

        public PageAP_Purchases()
        {
            InitializeComponent();

            _purchases = new ObservableCollection<Purchases>();
            _purchaseViewSource = (CollectionViewSource)Resources["PurchaseViewSource"];
            _purchaseViewSource.Source = _purchases;

            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                RefreshView();
            };
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPurchases();
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible)
            {
                LoadPurchases();
            }
        }

        private void LoadPurchases()
        {
            try
            {
                _purchases.Clear();
                var purchases = db.Purchases
                    .Include(p => p.Suppliers)
                    .ToList();
                foreach (var purchase in purchases)
                {
                    _purchases.Add(purchase);
                }

                _minDate = _purchases.Any() ? _purchases.Min(p => p.PurchaseDate) : (DateTime?)null;
                _maxDate = _purchases.Any() ? _purchases.Max(p => p.PurchaseDate) : (DateTime?)null;

                DateFrom.SelectedDate = _minDate;
                DateTo.SelectedDate = _maxDate;

                RefreshView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке закупок: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                NoDataMessage.Visibility = Visibility.Visible;
            }
        }

        private void RefreshView()
        {
            try
            {
                _purchaseViewSource.View.Filter = item =>
                {
                    var purchase = item as Purchases;
                    if (purchase == null) return false;

                    string searchText = SearchPurchase.Text?.ToLower() ?? "";
                    bool searchMatch = string.IsNullOrWhiteSpace(searchText) ||
                                       (purchase.Suppliers?.SupplierName?.ToLower().Contains(searchText) ?? false) ||
                                       (purchase.PurchaseDate.ToString("dd.MM.yyyy").Contains(searchText)) ||
                                       (purchase.TotalAmount.ToString().Contains(searchText));

                    bool dateMatch = true;
                    if (DateFrom.SelectedDate.HasValue)
                    {
                        dateMatch = purchase.PurchaseDate >= DateFrom.SelectedDate.Value.Date;
                    }
                    if (DateTo.SelectedDate.HasValue)
                    {
                        dateMatch = dateMatch && purchase.PurchaseDate <= DateTo.SelectedDate.Value.Date.AddDays(1).AddTicks(-1);
                    }

                    return searchMatch && dateMatch;
                };

                NoDataMessage.Visibility = _purchaseViewSource.View.Cast<object>().Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при фильтрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Information);
                NoDataMessage.Visibility = Visibility.Visible;
            }
        }

        private void SearchPurchase_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void DateFrom_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshView();
        }

        private void DateTo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshView();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            SearchPurchase.Text = string.Empty;
            DateFrom.SelectedDate = _minDate;
            DateTo.SelectedDate = _maxDate;
            RefreshView();
        }

        private void AddPurchase_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new AddEditPurchase());
        }

        private void EditPurchase_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var purchaseId = (int)button.Tag;
            var purchase = db.Purchases.FirstOrDefault(p => p.PurchaseID == purchaseId);
            if (purchase != null)
            {
                NavigationService?.Navigate(new AddEditPurchase(purchase));
            }
        }

        private void DeletePurchase_Click(object sender, RoutedEventArgs e)
        {
            var selectedPurchase = DGridPurchases.SelectedItem as Purchases;

            if (selectedPurchase == null)
            {
                MessageBox.Show("Выберите закупку для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Вы уверены, что хотите удалить эту закупку? Это действие нельзя отменить.",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                var purchaseDetails = db.PurchaseDetails
                    .Where(pd => pd.PurchaseID == selectedPurchase.PurchaseID)
                    .ToList();

                foreach (var detail in purchaseDetails)
                {
                    var inventory = db.Inventory.FirstOrDefault(i => i.IngredientID == detail.IngredientID);
                    if (inventory != null)
                    {
                        inventory.Quantity -= detail.Quantity;
                        if (inventory.Quantity < 0)
                        {
                            inventory.Quantity = 0;
                        }
                    }
                }

                db.PurchaseDetails.RemoveRange(purchaseDetails);

                var purchaseToDelete = db.Purchases.FirstOrDefault(p => p.PurchaseID == selectedPurchase.PurchaseID);
                if (purchaseToDelete != null)
                {
                    db.Purchases.Remove(purchaseToDelete);
                }

                db.SaveChanges();

                MessageBox.Show("Закупка успешно удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadPurchases(); 
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении закупки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GoToInventory_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new PageAP_Inventory());
        }
    }
}
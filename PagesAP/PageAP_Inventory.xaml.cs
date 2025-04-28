using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using System.Collections.Generic;

namespace RestFlowSystem.PagesAP
{
    public partial class PageAP_Inventory : Page
    {
        private bool _isFirstLoad = true;
        private DispatcherTimer _searchTimer;
        private ObservableCollection<Inventory> _inventory;
        private CollectionViewSource _inventoryViewSource;
        private DateTime? _minDate;
        private DateTime? _maxDate;

        public PageAP_Inventory()
        {
            InitializeComponent();

            _inventory = new ObservableCollection<Inventory>();
            _inventoryViewSource = (CollectionViewSource)Resources["InventoryViewSource"];
            _inventoryViewSource.Source = _inventory;

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
            if (_isFirstLoad)
            {
                _isFirstLoad = false;
                LoadInventory();
            }
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible && !_isFirstLoad)
            {
                Entities.GetContext().ChangeTracker.Entries().ToList().ForEach(x => x.Reload());
                LoadInventory();
            }
        }

        private void LoadInventory()
        {
            try
            {
                if (!IsLoaded || !DGridInventory.IsLoaded || !DGridInventory.IsVisible)
                {
                    return;
                }

                _inventory.Clear();
                var inventoryItems = Entities.GetContext().Inventory
                    .Include(i => i.Ingredients)
                    .ToList();
                foreach (var item in inventoryItems)
                {
                    _inventory.Add(item);
                }

                // Установка минимальной и максимальной даты для DatePicker
                // Считаем LastDeliveryDate == DateTime.MinValue как отсутствие даты
                var validDates = _inventory
                    .Where(i => i.LastDeliveryDate != DateTime.MinValue)
                    .Select(i => i.LastDeliveryDate)
                    .ToList();

                _minDate = validDates.Any() ? validDates.Min() : (DateTime?)null;
                _maxDate = validDates.Any() ? validDates.Max() : (DateTime?)null;

                DateFrom.SelectedDate = _minDate;
                DateTo.SelectedDate = _maxDate;

                RefreshView();
            }
            catch (Exception ex)
            {
                _inventory.Clear();
                MessageBox.Show($"Ошибка при загрузке данных склада: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                NoDataMessage.Visibility = Visibility.Visible;
            }
        }

        private void RefreshView()
        {
            try
            {
                _inventoryViewSource.View.Filter = item =>
                {
                    var inventoryItem = item as Inventory;
                    if (inventoryItem == null) return false;

                    // Поиск по всем полям
                    string searchText = SearchInventory.Text?.ToLower() ?? "";
                    bool searchMatch = string.IsNullOrWhiteSpace(searchText) ||
                                       (inventoryItem.Ingredients?.Name?.ToLower().Contains(searchText) ?? false) ||
                                       (inventoryItem.Quantity.ToString().Contains(searchText)) ||
                                       (inventoryItem.Ingredients?.Unit?.ToLower().Contains(searchText) ?? false) ||
                                       (inventoryItem.LastDeliveryDate != DateTime.MinValue
                                           ? inventoryItem.LastDeliveryDate.ToString("dd.MM.yyyy").Contains(searchText)
                                           : false);

                    // Фильтрация по периоду
                    bool dateMatch = true;
                    bool hasDate = inventoryItem.LastDeliveryDate != DateTime.MinValue;

                    if (DateFrom.SelectedDate.HasValue)
                    {
                        if (hasDate)
                        {
                            dateMatch = inventoryItem.LastDeliveryDate >= DateFrom.SelectedDate.Value.Date;
                        }
                        else
                        {
                            dateMatch = false; // Если дата не указана, исключаем запись
                        }
                    }

                    if (DateTo.SelectedDate.HasValue)
                    {
                        if (hasDate)
                        {
                            dateMatch = dateMatch && inventoryItem.LastDeliveryDate <= DateTo.SelectedDate.Value.Date.AddDays(1).AddTicks(-1);
                        }
                        else
                        {
                            dateMatch = false; // Если дата не указана, исключаем запись
                        }
                    }

                    // Если обе даты фильтра не выбраны, показываем записи даже без даты
                    if (!DateFrom.SelectedDate.HasValue && !DateTo.SelectedDate.HasValue)
                    {
                        dateMatch = true;
                    }

                    return searchMatch && dateMatch;
                };

                NoDataMessage.Visibility = _inventoryViewSource.View.Cast<object>().Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при фильтрации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Information);
                NoDataMessage.Visibility = Visibility.Visible;
            }
        }

        private void SearchInventory_TextChanged(object sender, TextChangedEventArgs e)
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
            SearchInventory.Text = string.Empty;
            DateFrom.SelectedDate = _minDate;
            DateTo.SelectedDate = _maxDate;
            RefreshView();
        }

        private void UpdateMenuStopList(int ingredientId, decimal currentQuantity)
        {
            try
            {
                var context = Entities.GetContext();

                // Находим все блюда, которые используют этот ингредиент
                var affectedDishes = context.DishIngredients
                    .Where(di => di.IngredientID == ingredientId)
                    .Select(di => di.MenuID)
                    .Distinct()
                    .ToList();

                // Список для сообщений о стоп-листе
                List<string> stopListMessages = new List<string>();

                foreach (var menuId in affectedDishes)
                {
                    var dish = context.Menu.FirstOrDefault(m => m.MenuID == menuId);
                    if (dish == null) continue;

                    // Проверяем, достаточно ли ингредиента для этого блюда
                    var dishIngredient = context.DishIngredients
                        .Include(di => di.Ingredients)
                        .FirstOrDefault(di => di.MenuID == menuId && di.IngredientID == ingredientId);

                    if (dishIngredient == null) continue;

                    // Проверяем, достаточно ли ингредиента на складе
                    bool hasEnoughIngredient = currentQuantity >= dishIngredient.Quantity;

                    // Формируем сообщение о нехватке ингредиента
                    string missingIngredientMessage = $"- {dishIngredient.Ingredients.Name} (нужно: {dishIngredient.Quantity}, на складе: {currentQuantity})";

                    // Обновляем StopList
                    if (!hasEnoughIngredient)
                    {
                        // Если ингредиента недостаточно и блюдо не в стоп-листе
                        if (!dish.StopList)
                        {
                            dish.StopList = true;
                            stopListMessages.Add($"Блюдо '{dish.Name}' добавлено в стоп-лист из-за нехватки ингредиента:\n{missingIngredientMessage}");
                            System.Diagnostics.Debug.WriteLine($"Блюдо '{dish.Name}' добавлено в стоп-лист: {missingIngredientMessage}");
                        }
                    }
                    else
                    {
                        // Если ингредиента достаточно, проверяем, можно ли снять блюдо из стоп-листа
                        if (dish.StopList)
                        {
                            // Проверяем остальные ингредиенты блюда
                            var otherIngredients = context.DishIngredients
                                .Include(di => di.Ingredients)
                                .Where(di => di.MenuID == menuId && di.IngredientID != ingredientId)
                                .ToList();

                            bool hasEnoughOtherIngredients = true;
                            string otherMissingIngredients = "";
                            foreach (var otherIngredient in otherIngredients)
                            {
                                var inventory = context.Inventory.FirstOrDefault(i => i.IngredientID == otherIngredient.IngredientID);
                                if (inventory == null || inventory.Quantity < otherIngredient.Quantity)
                                {
                                    hasEnoughOtherIngredients = false;
                                    otherMissingIngredients += $"\n- {otherIngredient.Ingredients.Name} (нужно: {otherIngredient.Quantity}, на складе: {(inventory != null ? inventory.Quantity : 0)})";
                                }
                            }

                            if (hasEnoughOtherIngredients)
                            {
                                dish.StopList = false;
                                stopListMessages.Add($"Блюдо '{dish.Name}' снято из стоп-листа: ингредиентов достаточно.");
                                System.Diagnostics.Debug.WriteLine($"Блюдо '{dish.Name}' снято из стоп-листа, ингредиентов достаточно.");
                            }
                            else
                            {
                                // Если других ингредиентов недостаточно, блюдо остаётся в стоп-листе
                                stopListMessages.Add($"Блюдо '{dish.Name}' остаётся в стоп-листе из-за нехватки других ингредиентов:{otherMissingIngredients}");
                                System.Diagnostics.Debug.WriteLine($"Блюдо '{dish.Name}' остаётся в стоп-листе: {otherMissingIngredients}");
                            }
                        }
                    }
                }

                context.SaveChanges();

                // Показываем сообщения о стоп-листе
                if (stopListMessages.Any())
                {
                    MessageBox.Show(string.Join("\n\n", stopListMessages), "Обновление стоп-листа", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении стоп-листа блюд: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateInventory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button == null) return;

                var inventoryItem = button.DataContext as Inventory;
                if (inventoryItem == null) return;

                // Создаём окно для ввода нового количества
                var window = new Window
                {
                    Title = "Обновление количества",
                    Width = 400,
                    Height = 130,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                var stackPanel = new StackPanel { Margin = new Thickness(10) };
                var textBlock = new TextBlock
                {
                    Text = $"Введите новое количество для '{inventoryItem.Ingredients.Name}' (текущее: {inventoryItem.Quantity}):"
                };
                var textBox = new TextBox { Text = inventoryItem.Quantity.ToString(), Margin = new Thickness(0, 5, 0, 5) };
                var confirmButton = new Button { Content = "Подтвердить", Width = 100 };
                confirmButton.Click += (s, args) =>
                {
                    if (decimal.TryParse(textBox.Text, out decimal newQuantity) && newQuantity >= 0)
                    {
                        inventoryItem.Quantity = newQuantity;
                        inventoryItem.LastDeliveryDate = DateTime.Today;
                        Entities.GetContext().SaveChanges();

                        // Обновляем StopList для всех блюд, использующих этот ингредиент
                        UpdateMenuStopList(inventoryItem.IngredientID, newQuantity);

                        MessageBox.Show("Количество успешно обновлено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadInventory();
                        window.Close();
                    }
                    else
                    {
                        MessageBox.Show("Количество должно быть числом больше или равно 0.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };
                stackPanel.Children.Add(textBlock);
                stackPanel.Children.Add(textBox);
                stackPanel.Children.Add(confirmButton);
                window.Content = stackPanel;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении количества: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteInventory_Click(object sender, RoutedEventArgs e)
        {
            var selectedInventory = DGridInventory.SelectedItem as Inventory;

            if (selectedInventory == null)
            {
                MessageBox.Show("Выберите ингредиент для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Вы уверены, что хотите удалить ингредиент '{selectedInventory.Ingredients.Name}'? Это действие нельзя отменить.",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                var context = Entities.GetContext();
                var ingredientId = selectedInventory.IngredientID;

                // Находим все блюда, которые используют этот ингредиент, ДО удаления
                var affectedDishes = context.DishIngredients
                    .Where(di => di.IngredientID == ingredientId)
                    .Select(di => di.MenuID)
                    .Distinct()
                    .ToList();

                // Удаляем связанные записи из DishIngredients
                var dishIngredientsToDelete = context.DishIngredients
                    .Where(di => di.IngredientID == ingredientId)
                    .ToList();
                context.DishIngredients.RemoveRange(dishIngredientsToDelete);

                // Удаляем запись из Inventory
                var inventoryToDelete = context.Inventory.FirstOrDefault(i => i.InventoryID == selectedInventory.InventoryID);
                if (inventoryToDelete != null)
                {
                    context.Inventory.Remove(inventoryToDelete);
                }

                // Проверяем, используется ли ингредиент в других таблицах
                var isIngredientUsedInPurchases = context.PurchaseDetails.Any(pd => pd.IngredientID == ingredientId);
                var isIngredientUsedInInventory = context.Inventory.Any(i => i.IngredientID == ingredientId && i.InventoryID != selectedInventory.InventoryID);

                // Если ингредиент больше не используется, удаляем его из Ingredients
                if (!isIngredientUsedInPurchases && !isIngredientUsedInInventory)
                {
                    var ingredientToDelete = context.Ingredients.FirstOrDefault(i => i.IngredientID == ingredientId);
                    if (ingredientToDelete != null)
                    {
                        context.Ingredients.Remove(ingredientToDelete);
                    }
                }

                // Сохраняем изменения
                context.SaveChanges();

                // Обновляем StopList для затронутых блюд
                List<string> stopListMessages = new List<string>();
                foreach (var menuId in affectedDishes)
                {
                    var dish = context.Menu.FirstOrDefault(m => m.MenuID == menuId);
                    if (dish == null) continue;

                    // Проверяем, остались ли у блюда ингредиенты
                    var remainingIngredients = context.DishIngredients
                        .Where(di => di.MenuID == menuId)
                        .ToList();

                    if (!remainingIngredients.Any())
                    {
                        // Если ингредиентов больше нет, добавляем блюдо в StopList
                        if (!dish.StopList)
                        {
                            dish.StopList = true;
                            stopListMessages.Add($"Блюдо '{dish.Name}' добавлено в стоп-лист: у блюда больше нет ингредиентов.");
                        }
                    }
                    else
                    {
                        // Если ингредиенты есть, проверяем их количество на складе
                        bool hasEnoughIngredients = true;
                        string missingIngredients = "";
                        foreach (var dishIngredient in remainingIngredients)
                        {
                            var inventory = context.Inventory.FirstOrDefault(i => i.IngredientID == dishIngredient.IngredientID);
                            if (inventory == null || inventory.Quantity < dishIngredient.Quantity)
                            {
                                hasEnoughIngredients = false;
                                missingIngredients += $"\n- {dishIngredient.Ingredients.Name} (нужно: {dishIngredient.Quantity}, на складе: {(inventory != null ? inventory.Quantity : 0)})";
                            }
                        }

                        if (!hasEnoughIngredients && !dish.StopList)
                        {
                            dish.StopList = true;
                            stopListMessages.Add($"Блюдо '{dish.Name}' добавлено в стоп-лист из-за нехватки ингредиентов:{missingIngredients}");
                        }
                        else if (hasEnoughIngredients && dish.StopList)
                        {
                            dish.StopList = false;
                            stopListMessages.Add($"Блюдо '{dish.Name}' снято из стоп-листа: ингредиентов достаточно.");
                        }
                    }
                }

                context.SaveChanges();

                if (stopListMessages.Any())
                {
                    MessageBox.Show(string.Join("\n\n", stopListMessages), "Обновление стоп-листа", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                MessageBox.Show("Ингредиент успешно удалён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadInventory(); // Обновляем таблицу
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении ингредиента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GoToPurchases_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService?.Navigate(new PageAP_Purchases());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при переходе на страницу поставок: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
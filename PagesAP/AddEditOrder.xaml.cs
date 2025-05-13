using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestFlowSystem.PagesAP
{
    public partial class AddEditOrder : Page
    {
        private Entities db = Entities.GetContext();
        private Orders _order;
        private ObservableCollection<OrderItems> _tempOrderItems;

        public AddEditOrder(Orders selectedOrder = null)
        {
            InitializeComponent();
            _tempOrderItems = new ObservableCollection<OrderItems>();

            if (selectedOrder != null)
            {
                _order = selectedOrder;
                Title_Edit.Text = "Редактирование заказа";
            }
            else
            {
                _order = new Orders { OrderDate = DateTime.Now, TableNum = 1 };
                Title_Edit.Text = "Добавление заказа";
            }
            DataContext = _order;

            LoadComboBoxes();
            LoadOrderItems();
            UpdateTotalAmount();
        }

        private void LoadComboBoxes()
        {
            WaiterComboBox.ItemsSource = db.Employees
                .Where(e => e.Positions.PositionName == "Официант")
                .Select(e => new { EmployeeID = e.EmployeeID, FullName = e.FirstName + " " + e.LastName })
                .ToList();
            StatusComboBox.ItemsSource = db.OrderStatuses.ToList();
            PaymentMethodComboBox.ItemsSource = db.PaymentMethods.ToList();
            NewMenuComboBox.ItemsSource = db.Menu.Where(m => !m.StopList).ToList();
        }

        private void LoadOrderItems()
        {
            if (_order.OrderID > 0)
            {
                DGridOrderItems.ItemsSource = db.OrderItems
                    .Include(oi => oi.Menu)
                    .Where(oi => oi.OrderID == _order.OrderID)
                    .ToList();
            }
            else
            {
                DGridOrderItems.ItemsSource = _tempOrderItems;
            }
        }

        private void UpdateTotalAmount()
        {
            var items = DGridOrderItems.ItemsSource as IEnumerable<OrderItems>;
            if (items != null)
            {
                _order.TotalAmount = items.Sum(i => i.ItemTotal);
            }
            else
            {
                _order.TotalAmount = 0;
            }
            TotalAmountTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        }

        private void UpdateMenuStopListForIngredients(List<int> ingredientIds)
        {
            try
            {
                var affectedDishes = db.DishIngredients
                    .Where(di => ingredientIds.Contains(di.IngredientID))
                    .Select(di => di.MenuID)
                    .Distinct()
                    .ToList();

                List<string> stopListMessages = new List<string>();

                foreach (var menuId in affectedDishes)
                {
                    var dish = db.Menu.FirstOrDefault(m => m.MenuID == menuId);
                    if (dish == null) continue;

                    var dishIngredients = db.DishIngredients
                        .Include(di => di.Ingredients)
                        .Where(di => di.MenuID == menuId)
                        .ToList();

                    bool hasEnoughIngredients = true;
                    string missingIngredients = "";
                    foreach (var dishIngredient in dishIngredients)
                    {
                        var inventory = db.Inventory.FirstOrDefault(i => i.IngredientID == dishIngredient.IngredientID);
                        if (inventory == null || inventory.Quantity < dishIngredient.Quantity)
                        {
                            hasEnoughIngredients = false;
                            missingIngredients += $"\n- {dishIngredient.Ingredients.Name} (нужно: {dishIngredient.Quantity}, на складе: {(inventory != null ? inventory.Quantity : 0)})";
                        }
                    }

                    if (!hasEnoughIngredients)
                    {
                        if (!dish.StopList)
                        {
                            dish.StopList = true;
                            stopListMessages.Add($"Блюдо '{dish.Name}' добавлено в стоп-лист из-за нехватки ингредиентов:{missingIngredients}");
                            System.Diagnostics.Debug.WriteLine($"Блюдо '{dish.Name}' добавлено в стоп-лист: {missingIngredients}");
                        }
                    }
                    else
                    {
                        if (dish.StopList)
                        {
                            dish.StopList = false;
                            stopListMessages.Add($"Блюдо '{dish.Name}' снято из стоп-листа: ингредиентов достаточно.");
                            System.Diagnostics.Debug.WriteLine($"Блюдо '{dish.Name}' снято из стоп-листа, ингредиентов достаточно.");
                        }
                    }
                }

                db.SaveChanges();

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

        private void AddOrderItem_Click(object sender, RoutedEventArgs e)
        {
            if (NewMenuComboBox.SelectedItem == null)
            {
                QuantityError.Text = "Выберите блюдо!";
                QuantityError.Visibility = Visibility.Visible;
                return;
            }

            if (!int.TryParse(NewQuantityTextBox.Text, out int quantity) || quantity <= 0)
            {
                QuantityError.Text = "Введите корректное количество (положительное целое число)!";
                QuantityError.Visibility = Visibility.Visible;
                return;
            }

            var selectedMenu = NewMenuComboBox.SelectedItem as Menu;
            var dishIngredients = db.DishIngredients
                .Where(di => di.MenuID == selectedMenu.MenuID)
                .Include(di => di.Ingredients)
                .ToList();

            foreach (var dishIngredient in dishIngredients)
            {
                var inventory = db.Inventory.FirstOrDefault(i => i.IngredientID == dishIngredient.IngredientID);
                if (inventory == null || inventory.Quantity < dishIngredient.Quantity * quantity)
                {
                    QuantityError.Text = $"Недостаточно ингредиента {dishIngredient.Ingredients.Name} на складе!";
                    QuantityError.Visibility = Visibility.Visible;
                    return;
                }
            }

            var newOrderItem = new OrderItems
            {
                MenuID = selectedMenu.MenuID,
                Quantity = quantity,
                ItemTotal = quantity * selectedMenu.Price
            };

            if (_order.OrderID > 0)
            {
                newOrderItem.OrderID = _order.OrderID;
                db.OrderItems.Add(newOrderItem);
                db.SaveChanges();
            }
            else
            {
                _tempOrderItems.Add(newOrderItem);
            }

            var affectedIngredientIds = new List<int>();
            foreach (var dishIngredient in dishIngredients)
            {
                var inventory = db.Inventory.FirstOrDefault(i => i.IngredientID == dishIngredient.IngredientID);
                inventory.Quantity -= dishIngredient.Quantity * quantity;
                affectedIngredientIds.Add(dishIngredient.IngredientID);
            }
            db.SaveChanges();

            UpdateMenuStopListForIngredients(affectedIngredientIds);

            LoadOrderItems();
            UpdateTotalAmount();
            NewMenuComboBox.SelectedIndex = -1;
            NewQuantityTextBox.Text = "1";
            QuantityError.Visibility = Visibility.Collapsed;

            NewMenuComboBox.ItemsSource = db.Menu.Where(m => !m.StopList).ToList();
        }

        private void DeleteOrderItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var orderItem = button.Tag as OrderItems;

            if (orderItem != null)
            {
                var dishIngredients = db.DishIngredients
                    .Where(di => di.MenuID == orderItem.MenuID)
                    .Include(di => di.Ingredients)
                    .ToList();

                var affectedIngredientIds = new List<int>();
                foreach (var dishIngredient in dishIngredients)
                {
                    var inventory = db.Inventory.FirstOrDefault(i => i.IngredientID == dishIngredient.IngredientID);
                    if (inventory != null)
                    {
                        inventory.Quantity += dishIngredient.Quantity * orderItem.Quantity;
                        affectedIngredientIds.Add(dishIngredient.IngredientID);
                    }
                }

                if (_order.OrderID > 0 && orderItem.OrderItemID > 0)
                {
                    var dbOrderItem = db.OrderItems.FirstOrDefault(oi => oi.OrderItemID == orderItem.OrderItemID);
                    if (dbOrderItem != null)
                    {
                        db.OrderItems.Remove(dbOrderItem);
                        db.SaveChanges();
                    }
                }
                else
                {
                    _tempOrderItems.Remove(orderItem);
                }

                db.SaveChanges();

                UpdateMenuStopListForIngredients(affectedIngredientIds);
            }

            LoadOrderItems();
            UpdateTotalAmount();

            NewMenuComboBox.ItemsSource = db.Menu.Where(m => !m.StopList).ToList();
        }

        private void TableNumTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]+$");
        }

        private void TableNumTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(TableNumTextBox.Text, out int value))
            {
                if (value < 1)
                {
                    _order.TableNum = 1;
                    TableNumTextBox.Text = "1";
                }
                else if (value > 100)
                {
                    _order.TableNum = 100;
                    TableNumTextBox.Text = "100";
                }
                TableNumError.Visibility = Visibility.Collapsed;
            }
            else if (string.IsNullOrEmpty(TableNumTextBox.Text))
            {
                _order.TableNum = 1;
                TableNumTextBox.Text = "1";
            }
        }

        private void TableNumUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_order.TableNum < 100)
            {
                _order.TableNum++;
                TableNumTextBox.Text = _order.TableNum.ToString();
                TableNumError.Visibility = Visibility.Collapsed;
            }
        }

        private void TableNumDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_order.TableNum > 1)
            {
                _order.TableNum--;
                TableNumTextBox.Text = _order.TableNum.ToString();
                TableNumError.Visibility = Visibility.Collapsed;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TableNumTextBox.Text, out int tableNum) || tableNum < 1 || tableNum > 100)
            {
                TableNumError.Visibility = Visibility.Visible;
                TableNumError.Text = "Номер стола должен быть от 1 до 100!";
                return;
            }
            _order.TableNum = tableNum;
            TableNumError.Visibility = Visibility.Collapsed;

            if (WaiterComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите официанта!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StatusComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите статус!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PaymentMethodComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите способ оплаты!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_order.OrderID == 0)
            {
                db.Orders.Add(_order);
                db.SaveChanges();

                foreach (var item in _tempOrderItems)
                {
                    item.OrderID = _order.OrderID;
                    db.OrderItems.Add(item);
                }
            }

            try
            {
                db.SaveChanges();
                MessageBox.Show("Заказ успешно сохранён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigationService?.Navigate(new PageAP_Orders());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new PageAP_Orders());
        }
    }
}
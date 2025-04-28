using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RestFlowSystem.PagesAP
{
    public partial class AddEditPurchase : Page
    {
        private Entities db = Entities.GetContext();
        private Purchases _purchase;
        private ObservableCollection<PurchaseDetails> _tempPurchaseDetails;

        public AddEditPurchase(Purchases selectedPurchase = null)
        {
            InitializeComponent();
            _tempPurchaseDetails = new ObservableCollection<PurchaseDetails>();

            if (selectedPurchase != null)
            {
                _purchase = selectedPurchase;
                Title_Edit.Text = "Редактирование закупки";
                var details = db.PurchaseDetails
                    .Include(pd => pd.Ingredients)
                    .Where(pd => pd.PurchaseID == _purchase.PurchaseID)
                    .ToList();
                foreach (var detail in details)
                {
                    _tempPurchaseDetails.Add(detail);
                }
            }
            else
            {
                _purchase = new Purchases { PurchaseDate = DateTime.Now };
                Title_Edit.Text = "Добавление закупки";
            }
            DataContext = _purchase;

            LoadComboBoxes();
            LoadPurchaseDetails();
            UpdateTotalAmount();
        }

        private void LoadComboBoxes()
        {
            SupplierComboBox.ItemsSource = db.Suppliers.ToList();
            NewIngredientComboBox.ItemsSource = db.Ingredients.ToList();
        }

        private void LoadPurchaseDetails()
        {
            DGridPurchaseDetails.ItemsSource = _tempPurchaseDetails;
        }

        private void UpdateTotalAmount()
        {
            if (_tempPurchaseDetails.Any())
            {
                _purchase.TotalAmount = _tempPurchaseDetails.Sum(i => i.ItemTotal);
                System.Diagnostics.Debug.WriteLine($"TotalAmount updated: {_purchase.TotalAmount}");
            }
            else
            {
                _purchase.TotalAmount = 0;
                System.Diagnostics.Debug.WriteLine("TotalAmount set to 0: No items");
            }
            TotalAmountTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        }

        private void SupplierComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var comboBox = sender as ComboBox;
                var supplierName = comboBox.Text.Trim();

                // Проверяем, существует ли уже поставщик с таким именем
                var existingSupplier = db.Suppliers.FirstOrDefault(s => s.SupplierName == supplierName);
                if (existingSupplier != null)
                {
                    SupplierComboBox.SelectedItem = existingSupplier;
                    return;
                }

                // Если поставщик не существует, добавляем нового
                if (string.IsNullOrWhiteSpace(supplierName))
                {
                    MessageBox.Show("Введите название поставщика!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var newSupplier = new Suppliers
                    {
                        SupplierName = supplierName
                    };
                    db.Suppliers.Add(newSupplier);
                    db.SaveChanges();

                    // Обновляем список поставщиков
                    LoadComboBoxes();
                    SupplierComboBox.SelectedItem = newSupplier;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при добавлении поставщика: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddNewIngredient()
        {
            var ingredientName = NewIngredientComboBox.Text.Trim();
            var unit = UnitComboBox.Text.Trim();

            // Проверяем, существует ли уже ингредиент с таким именем
            var existingIngredient = db.Ingredients.FirstOrDefault(i => i.Name == ingredientName);
            if (existingIngredient != null)
            {
                NewIngredientComboBox.SelectedItem = existingIngredient;
                UnitComboBox.Text = existingIngredient.Unit ?? string.Empty;
                return;
            }

            // Если ингредиент не существует, добавляем новый
            if (string.IsNullOrWhiteSpace(ingredientName))
            {
                MessageBox.Show("Введите название ингредиента!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var newIngredient = new Ingredients
                {
                    Name = ingredientName,
                    Unit = string.IsNullOrWhiteSpace(unit) ? null : unit
                };
                db.Ingredients.Add(newIngredient);
                db.SaveChanges();

                // Обновляем список ингредиентов
                LoadComboBoxes();
                NewIngredientComboBox.SelectedItem = newIngredient;
                UnitComboBox.Text = newIngredient.Unit ?? string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении ингредиента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NewIngredientComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddNewIngredient();
            }
        }

        private void UnitComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddNewIngredient();
            }
        }

        private void NewIngredientComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NewIngredientComboBox.SelectedItem is Ingredients selectedIngredient)
            {
                UnitComboBox.Text = selectedIngredient.Unit ?? string.Empty;
            }
            else
            {
                UnitComboBox.Text = string.Empty;
            }
        }

        private void AddPurchaseDetail_Click(object sender, RoutedEventArgs e)
        {
            if (NewIngredientComboBox.SelectedItem == null)
            {
                QuantityError.Text = "Выберите ингредиент!";
                QuantityError.Visibility = Visibility.Visible;
                return;
            }

            if (!decimal.TryParse(NewQuantityTextBox.Text, out decimal quantity) || quantity <= 0)
            {
                QuantityError.Text = "Введите корректное количество (положительное число)!";
                QuantityError.Visibility = Visibility.Visible;
                return;
            }

            if (!decimal.TryParse(NewUnitPriceTextBox.Text, out decimal unitPrice) || unitPrice <= 0)
            {
                QuantityError.Text = "Введите корректную цену за единицу (положительное число)!";
                QuantityError.Visibility = Visibility.Visible;
                return;
            }

            var selectedIngredient = NewIngredientComboBox.SelectedItem as Ingredients;
            var newPurchaseDetail = new PurchaseDetails
            {
                IngredientID = selectedIngredient.IngredientID,
                Quantity = quantity,
                UnitPrice = unitPrice,
                ItemTotal = quantity * unitPrice,
                Ingredients = selectedIngredient
            };

            _tempPurchaseDetails.Add(newPurchaseDetail);

            // Обновляем склад
            var inventory = db.Inventory.FirstOrDefault(i => i.IngredientID == selectedIngredient.IngredientID);
            if (inventory == null)
            {
                inventory = new Inventory
                {
                    IngredientID = selectedIngredient.IngredientID,
                    Quantity = quantity,
                    LastDeliveryDate = _purchase.PurchaseDate
                };
                db.Inventory.Add(inventory);
            }
            else
            {
                inventory.Quantity += quantity;
                inventory.LastDeliveryDate = _purchase.PurchaseDate;
            }
            db.SaveChanges();

            LoadPurchaseDetails();
            UpdateTotalAmount();
            NewIngredientComboBox.SelectedIndex = -1;
            UnitComboBox.SelectedIndex = -1; // Сбрасываем выбор
            UnitComboBox.Text = string.Empty; // Очищаем текстовое поле
            NewQuantityTextBox.Text = "1";
            NewUnitPriceTextBox.Text = "0";
            QuantityError.Visibility = Visibility.Collapsed;
        }

        private void DeletePurchaseDetail_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var purchaseDetail = button.Tag as PurchaseDetails;

            if (purchaseDetail != null)
            {
                // Возвращаем количество на склад
                var inventory = db.Inventory.FirstOrDefault(i => i.IngredientID == purchaseDetail.IngredientID);
                if (inventory != null)
                {
                    inventory.Quantity -= purchaseDetail.Quantity;
                    if (inventory.Quantity < 0)
                    {
                        inventory.Quantity = 0;
                    }
                }

                if (_purchase.PurchaseID > 0 && purchaseDetail.PurchaseDetailID > 0)
                {
                    var dbPurchaseDetail = db.PurchaseDetails.FirstOrDefault(pd => pd.PurchaseDetailID == purchaseDetail.PurchaseDetailID);
                    if (dbPurchaseDetail != null)
                    {
                        db.PurchaseDetails.Remove(dbPurchaseDetail);
                        db.SaveChanges();
                    }
                }

                _tempPurchaseDetails.Remove(purchaseDetail);
            }

            db.SaveChanges();
            LoadPurchaseDetails();
            UpdateTotalAmount();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (SupplierComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите поставщика!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_tempPurchaseDetails.Any())
            {
                MessageBox.Show("Добавьте хотя бы один ингредиент!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_purchase.PurchaseID == 0)
            {
                db.Purchases.Add(_purchase);
                db.SaveChanges();

                foreach (var item in _tempPurchaseDetails)
                {
                    item.PurchaseID = _purchase.PurchaseID;
                    db.PurchaseDetails.Add(item);
                }
            }
            else
            {
                // Удаляем старые детали, которые больше не нужны
                var existingDetails = db.PurchaseDetails.Where(pd => pd.PurchaseID == _purchase.PurchaseID).ToList();
                foreach (var existingDetail in existingDetails)
                {
                    if (!_tempPurchaseDetails.Any(pd => pd.PurchaseDetailID == existingDetail.PurchaseDetailID))
                    {
                        db.PurchaseDetails.Remove(existingDetail);
                    }
                }
            }

            try
            {
                db.SaveChanges();
                MessageBox.Show("Закупка успешно сохранена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    NavigationService?.Navigate(new PageAP_Purchases());
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                NavigationService?.Navigate(new PageAP_Purchases());
            }));
        }
    }
}
using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace RestFlowSystem.PagesAP
{
    public partial class AddEditMenu : Page
    {
        private Entities db = Entities.GetContext();
        private Menu _menu;
        private ObservableCollection<DishIngredients> _tempIngredients;
        private CollectionViewSource _ingredientsViewSource; // Для фильтрации ингредиентов

        public AddEditMenu(Menu selectedMenu = null, bool isAdding = true)
        {
            InitializeComponent();
            _tempIngredients = new ObservableCollection<DishIngredients>();

            // Инициализация CollectionViewSource
            _ingredientsViewSource = (CollectionViewSource)Resources["IngredientsViewSource"];

            if (selectedMenu != null)
            {
                _menu = selectedMenu;
                Title_Edit.Text = "Редактирование блюда";
            }
            else
            {
                _menu = new Menu { StopList = false };
                Title_Edit.Text = "Добавление блюда";
            }
            DataContext = _menu;

            CategoryComboBox.ItemsSource = db.MenuCategories.ToList();
            RefreshIngredientsComboBox();
            LoadIngredients();
            LoadImagePreview();
            ClearErrors();

            // Сбрасываем выбор и текст в ComboBox при инициализации
            NewIngredientComboBox.SelectedIndex = -1;
            NewIngredientComboBox.Text = string.Empty;
            _ingredientsViewSource.View.Filter = null;
            _ingredientsViewSource.View.Refresh();
        }

        private void LoadIngredients()
        {
            if (_menu.MenuID > 0)
            {
                DGridDishIngredients.ItemsSource = db.DishIngredients
                    .Include(di => di.Ingredients)
                    .Where(di => di.MenuID == _menu.MenuID)
                    .ToList();
            }
            else
            {
                DGridDishIngredients.ItemsSource = _tempIngredients;
            }
        }

        private void LoadImagePreview()
        {
            if (_menu.Image != null && _menu.Image.Length > 0)
            {
                using (var ms = new MemoryStream(_menu.Image))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    PreviewImage.Source = bitmap;
                }
            }
            else
            {
                PreviewImage.Source = null;
            }
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.png)|*.jpg;*.png|All files (*.*)|*.*",
                Title = "Выберите изображение блюда"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _menu.Image = File.ReadAllBytes(openFileDialog.FileName);
                    LoadImagePreview();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearErrors()
        {
            NameError.Visibility = Visibility.Collapsed;
            DescriptionError.Visibility = Visibility.Collapsed;
            PriceError.Visibility = Visibility.Collapsed;
            QuantityError.Visibility = Visibility.Collapsed;
        }

        private void RefreshIngredientsComboBox()
        {
            var ingredients = db.Ingredients.ToList();

            // Привязываем список ингредиентов к CollectionViewSource
            _ingredientsViewSource.Source = ingredients;

            if (!ingredients.Any())
            {
                QuantityError.Text = "Нет доступных ингредиентов. Добавьте ингредиенты на странице склада.";
                QuantityError.Visibility = Visibility.Visible;
                NewIngredientComboBox.IsEnabled = false;
            }
            else
            {
                NewIngredientComboBox.IsEnabled = true;
            }
        }

        private void NewIngredientComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedIngredient = NewIngredientComboBox.SelectedItem as Ingredients;
            if (selectedIngredient != null)
            {
                // Устанавливаем UnitComboBox в соответствии с Unit ингредиента
                string unit = selectedIngredient.Unit ?? "г";
                UnitComboBox.SelectedItem = UnitComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(item => item.Content.ToString() == unit) ?? UnitComboBox.Items[0];
                UnitComboBox.IsEnabled = false; // Запрещаем изменение единицы измерения
            }
            else
            {
                UnitComboBox.SelectedIndex = 0; // Сбрасываем на "г"
                UnitComboBox.IsEnabled = true;
            }
        }

        private void NewIngredientComboBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ingredientsViewSource == null) return;

            string filterText = NewIngredientComboBox.Text?.ToLower() ?? "";

            _ingredientsViewSource.View.Filter = item =>
            {
                if (string.IsNullOrWhiteSpace(filterText)) return true;

                var ingredient = item as Ingredients;
                return ingredient != null && (ingredient.Name?.ToLower().Contains(filterText) ?? false);
            };

            // Открываем выпадающий список только если есть текст
            if (!string.IsNullOrWhiteSpace(filterText) && !NewIngredientComboBox.IsDropDownOpen)
            {
                NewIngredientComboBox.IsDropDownOpen = true;
            }
        }

        private void AddIngredient_Click(object sender, RoutedEventArgs e)
        {
            ClearErrors();

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

            int selectedIngredientId = (int)NewIngredientComboBox.SelectedValue;

            // Проверка на дублирование ингредиента
            bool ingredientExists = false;
            if (_menu.MenuID > 0)
            {
                ingredientExists = db.DishIngredients
                    .Any(di => di.MenuID == _menu.MenuID && di.IngredientID == selectedIngredientId);
            }
            else
            {
                ingredientExists = _tempIngredients
                    .Any(di => di.IngredientID == selectedIngredientId);
            }

            if (ingredientExists)
            {
                QuantityError.Text = "Этот ингредиент уже добавлен в блюдо!";
                QuantityError.Visibility = Visibility.Visible;
                return;
            }

            var newDishIngredient = new DishIngredients
            {
                IngredientID = selectedIngredientId,
                Quantity = quantity
            };

            if (_menu.MenuID > 0)
            {
                newDishIngredient.MenuID = _menu.MenuID;
                db.DishIngredients.Add(newDishIngredient);
                db.SaveChanges();
            }
            else
            {
                _tempIngredients.Add(newDishIngredient);
            }

            LoadIngredients();

            // Сбрасываем выбор и текст в ComboBox
            NewIngredientComboBox.SelectedIndex = -1;
            NewIngredientComboBox.Text = string.Empty;
            NewQuantityTextBox.Text = "0";
            UnitComboBox.SelectedIndex = 0; // Сбрасываем на "г"

            // Сбрасываем фильтр
            _ingredientsViewSource.View.Filter = null;
            _ingredientsViewSource.View.Refresh();
        }

        private void DeleteDishIngredient_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var dishIngredient = button.Tag as DishIngredients;

            if (dishIngredient != null)
            {
                if (_menu.MenuID > 0 && dishIngredient.DishIngredientID > 0)
                {
                    var dbIngredient = db.DishIngredients.FirstOrDefault(di => di.DishIngredientID == dishIngredient.DishIngredientID);
                    if (dbIngredient != null)
                    {
                        db.DishIngredients.Remove(dbIngredient);
                        db.SaveChanges();
                    }
                }
                else
                {
                    _tempIngredients.Remove(dishIngredient);
                }

                db.SaveChanges();
            }

            LoadIngredients();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            bool isValid = true;
            ClearErrors();

            // Проверка на отсутствие ингредиентов
            bool hasIngredients = false;
            if (_menu.MenuID > 0)
            {
                hasIngredients = db.DishIngredients.Any(di => di.MenuID == _menu.MenuID);
            }
            else
            {
                hasIngredients = _tempIngredients.Any();
            }

            if (!hasIngredients)
            {
                QuantityError.Text = "Блюдо не может быть сохранено без ингредиентов! Добавьте хотя бы один ингредиент.";
                QuantityError.Visibility = Visibility.Visible;
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(_menu.Name))
            {
                NameError.Text = "Введите название блюда!";
                NameError.Visibility = Visibility.Visible;
                isValid = false;
            }

            if (_menu.CategoryID == 0)
            {
                MessageBox.Show("Выберите категорию!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                isValid = false;
            }

            decimal price = _menu.Price;
            bool isPriceChanged = false;

            if (!string.IsNullOrWhiteSpace(PriceTextBox.Text) && decimal.TryParse(PriceTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal newPrice))
            {
                if (_menu.MenuID == 0 || newPrice != _menu.Price)
                {
                    price = newPrice;
                    isPriceChanged = true;
                }
            }

            if (isPriceChanged || _menu.MenuID == 0)
            {
                string priceText = PriceTextBox.Text.Trim();
                Console.WriteLine($"PriceTextBox.Text after Trim: '{priceText}'");

                if (string.IsNullOrWhiteSpace(priceText))
                {
                    PriceError.Text = "Цена не может быть пустой!";
                    PriceError.Visibility = Visibility.Visible;
                    isValid = false;
                }
                else if (!decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
                {
                    PriceError.Text = "Введите корректное число для цены (используйте точку или запятую)!";
                    PriceError.Visibility = Visibility.Visible;
                    isValid = false;
                }
                else if (price <= 0)
                {
                    PriceError.Text = "Цена должна быть положительным числом!";
                    PriceError.Visibility = Visibility.Visible;
                    isValid = false;
                }
                else
                {
                    _menu.Price = price;
                }
            }

            if (!isValid)
            {
                return;
            }

            var existingMenu = db.Menu.FirstOrDefault(m => m.Name == _menu.Name && m.MenuID != _menu.MenuID);
            if (existingMenu != null)
            {
                NameError.Text = "Блюдо с таким названием уже существует!";
                NameError.Visibility = Visibility.Visible;
                return;
            }

            if (_menu.MenuID == 0)
            {
                db.Menu.Add(_menu);
                db.SaveChanges();

                foreach (var ingredient in _tempIngredients)
                {
                    ingredient.MenuID = _menu.MenuID;
                    db.DishIngredients.Add(ingredient);
                }
            }

            try
            {
                db.SaveChanges();
                MessageBox.Show("Данные успешно сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigationService?.Navigate(new PageAP_Menu());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new PageAP_Menu());
        }
    }
}
using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Globalization;
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
        private CollectionViewSource _ingredientsViewSource;

        public AddEditMenu(Menu selectedMenu = null, bool isAdding = true)
        {
            InitializeComponent();
            _tempIngredients = new ObservableCollection<DishIngredients>();

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
            if (string.IsNullOrWhiteSpace(_menu.Image))
            {
                PreviewImage.Source = new BitmapImage(new Uri("/Images/placeholder.jpg", UriKind.Relative));
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_menu.Image, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                PreviewImage.Source = bitmap;
            }
            catch (Exception)
            {
                PreviewImage.Source = new BitmapImage(new Uri("/Images/placeholder.jpg", UriKind.Relative));
            }
        }

        private void ImageUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadImagePreview(); 
        }

        private void ClearErrors()
        {
            NameError.Text = "";
            DescriptionError.Text = "";
            CategoryError.Text = "";
            PriceError.Text = "";
            QuantityError.Text = "";
        }

        private void RefreshIngredientsComboBox()
        {
            var ingredients = db.Ingredients.ToList();

            _ingredientsViewSource.Source = ingredients;

            if (!ingredients.Any())
            {
                QuantityError.Text = "Нет доступных ингредиентов. Добавьте ингредиенты на странице склада.";
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
                string unit = selectedIngredient.Unit ?? "г";
                UnitComboBox.SelectedItem = UnitComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(item => item.Content.ToString() == unit) ?? UnitComboBox.Items[0];
                UnitComboBox.IsEnabled = false;
            }
            else
            {
                UnitComboBox.SelectedIndex = 0;
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
                return;
            }

            if (!decimal.TryParse(NewQuantityTextBox.Text, out decimal quantity) || quantity <= 0)
            {
                QuantityError.Text = "Введите корректное количество (положительное число)!";
                return;
            }

            int selectedIngredientId = (int)NewIngredientComboBox.SelectedValue;

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

            NewIngredientComboBox.SelectedIndex = -1;
            NewIngredientComboBox.Text = string.Empty;
            NewQuantityTextBox.Text = "0";
            UnitComboBox.SelectedIndex = 0;

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
                QuantityError.Text = "Добавьте хотя бы один ингредиент";
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(_menu.Name))
            {
                NameError.Text = "Введите название блюда";
                isValid = false;
            }

            if (_menu.CategoryID == 0)
            {
                CategoryError.Text = "Выберите категорию";
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

                if (string.IsNullOrWhiteSpace(priceText))
                {
                    PriceError.Text = "Введите цену";
                    isValid = false;
                }
                else if (!decimal.TryParse(priceText, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
                {
                    PriceError.Text = "Введите корректное число для цены";
                    isValid = false;
                }
                else if (price <= 0)
                {
                    PriceError.Text = "Цена должна быть больше 0";
                    isValid = false;
                }
                else
                {
                    _menu.Price = price;
                }
            }

            if (!string.IsNullOrWhiteSpace(_menu.Image))
            {
                try
                {
                    new Uri(_menu.Image, UriKind.Absolute);
                }
                catch
                {
                    NameError.Text = "Введите корректный URL для изображения";
                    isValid = false;
                }
            }

            if (!isValid)
            {
                MessageBox.Show("Пожалуйста, проверьте правильность заполнения полей.", "Ошибки ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existingMenu = db.Menu.FirstOrDefault(m => m.Name == _menu.Name && m.MenuID != _menu.MenuID);
            if (existingMenu != null)
            {
                NameError.Text = "Блюдо с таким названием уже существует";
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
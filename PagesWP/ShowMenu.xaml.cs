using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;

namespace RestFlowSystem.PagesWP
{
    public partial class ShowMenu : Page
    {
        private Entities db = Entities.GetContext();
        private Menu _menu;

        public ShowMenu(Menu selectedMenu)
        {
            InitializeComponent();
            _menu = selectedMenu;
            LoadMenuDetails();
        }

        private void LoadMenuDetails()
        {
            if (_menu == null)
            {
                MessageBox.Show("Блюдо не найдено.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                NavigationService?.GoBack();
                return;
            }

            // Загрузка изображения
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

            // Заполнение текстовых полей
            NameTextBlock.Text = _menu.Name;
            DescriptionTextBlock.Text = _menu.Description ?? "Описание отсутствует";
            CategoryTextBlock.Text = db.MenuCategories
                .FirstOrDefault(c => c.CategoryID == _menu.CategoryID)?.CategoryName ?? "Не указана";
            PriceTextBlock.Text = _menu.Price.ToString("F2");
            StopListCheckBox.IsChecked = _menu.StopList;

            // Загрузка ингредиентов
            var ingredients = db.DishIngredients
                .Include(di => di.Ingredients)
                .Where(di => di.MenuID == _menu.MenuID)
                .ToList();
            DGridDishIngredients.ItemsSource = ingredients;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.GoBack();
        }
    }
}
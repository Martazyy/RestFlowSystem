using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using Word = Microsoft.Office.Interop.Word;
using Excel = Microsoft.Office.Interop.Excel;

namespace RestFlowSystem.PagesAP
{
    public partial class PageAP_Reports : Page
    {
        private Entities _context;
        private Chart chart;
        private bool isChartInitialized = false;

        public PageAP_Reports()
        {
            InitializeComponent();

            chart = new Chart();
            chart.ChartAreas.Add(new ChartArea("Main"));

            chart.Series.Add(new Series("Доходы")
            {
                IsValueShownAsLabel = true,
                Color = System.Drawing.Color.Green
            });

            chart.Series.Add(new Series("Расходы")
            {
                IsValueShownAsLabel = true,
                Color = System.Drawing.Color.Red
            });

            chart.Series.Add(new Series("Прибыль")
            {
                IsValueShownAsLabel = true,
                Color = System.Drawing.Color.Blue
            });

            chart.Legends.Add(new Legend());
            ChartHost.Child = chart;

            isChartInitialized = true;
            System.Diagnostics.Debug.WriteLine("Chart initialized in constructor");

            CmbDiagram.ItemsSource = Enum.GetValues(typeof(SeriesChartType));

            EndDatePicker.SelectedDate = DateTime.Today;
            StartDatePicker.SelectedDate = DateTime.Today.AddMonths(-11).AddDays(1 - DateTime.Today.Day);

            Loaded += PageAP_Reports_Loaded;
        }

        private void PageAP_Reports_Loaded(object sender, RoutedEventArgs e)
        {
            _context = Entities.GetContext();
            System.Diagnostics.Debug.WriteLine($"Context initialized in Loaded: {_context != null}");
            UpdateChart(null, null);
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateChart(sender, e);
        }

        private void UpdateChart(object sender, SelectionChangedEventArgs e)
        {
            if (!isChartInitialized || chart == null)
            {
                System.Diagnostics.Debug.WriteLine("UpdateChart skipped: Chart not initialized yet");
                return;
            }

            if (chart.Series["Доходы"] == null || chart.Series["Расходы"] == null || chart.Series["Прибыль"] == null)
            {
                MessageBox.Show("Ошибка: одна из серий графика не инициализирована.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_context == null)
            {
                _context = Entities.GetContext();
                System.Diagnostics.Debug.WriteLine($"Context re-initialized in UpdateChart: {_context != null}");
                if (_context == null)
                {
                    MessageBox.Show("Ошибка: не удалось инициализировать контекст базы данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                DateTime startDate = StartDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-11).AddDays(1 - DateTime.Today.Day);
                DateTime endDate = EndDatePicker.SelectedDate ?? DateTime.Today;

                // Проверка: начальная дата не должна быть раньше 2000 года
                DateTime minAllowedDate = new DateTime(2000, 1, 1);
                if (startDate < minAllowedDate)
                {
                    MessageBox.Show("Начальная дата не может быть раньше 1 января 2000 года.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    startDate = minAllowedDate;
                    StartDatePicker.SelectedDate = startDate; // Обновляем значение в DatePicker
                }

                if (startDate > endDate)
                {
                    MessageBox.Show("Начальная дата не может быть позже конечной.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StartDatePicker.SelectedDate = DateTime.Today.AddMonths(-11).AddDays(1 - DateTime.Today.Day);
                    EndDatePicker.SelectedDate = DateTime.Today;
                    startDate = StartDatePicker.SelectedDate.Value;
                    endDate = EndDatePicker.SelectedDate.Value;
                }

                if (endDate > DateTime.Today)
                {
                    endDate = DateTime.Today;
                    EndDatePicker.SelectedDate = endDate;
                }

                // Определяем режим группировки
                string groupBy = (CmbGroupBy.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "По месяцам";
                bool groupByDays = groupBy == "По дням";

                // Предварительно загружаем данные за весь период
                var ordersInRange = _context.Orders
                    .Where(o => o.OrderDate != null &&
                                o.OrderDate.Value >= startDate &&
                                o.OrderDate.Value <= endDate)
                    .ToList();

                var purchaseDetailsInRange = _context.PurchaseDetails
                    .Where(pd => pd.Purchases != null &&
                                 pd.Purchases.PurchaseDate != null &&
                                 pd.Purchases.PurchaseDate >= startDate &&
                                 pd.Purchases.PurchaseDate <= endDate)
                    .ToList();

                // Вычисляем данные в зависимости от режима
                var dataPoints = groupByDays
                    ? Enumerable.Range(0, (endDate - startDate).Days + 1)
                        .Select(i => startDate.AddDays(i))
                        .Select(date =>
                        {
                            System.Diagnostics.Debug.WriteLine($"Processing day: {date:dd.MM.yyyy}");

                            // Вычисляем конец дня заранее
                            DateTime endOfDay = date.AddDays(1);

                            var orders = ordersInRange
                                .Where(o => o.OrderDate.Value >= date &&
                                            o.OrderDate.Value < endOfDay)
                                .ToList();
                            decimal income = orders.Any() ? orders.Sum(o => o.TotalAmount) : 0m;

                            var purchaseDetails = purchaseDetailsInRange
                                .Where(pd => pd.Purchases.PurchaseDate >= date &&
                                             pd.Purchases.PurchaseDate < endOfDay)
                                .ToList();
                            decimal expense = purchaseDetails.Any() ? purchaseDetails.Sum(pd => pd.Quantity * pd.UnitPrice) : 0m;

                            return new
                            {
                                Date = date,
                                Income = income,
                                Expense = expense
                            };
                        })
                        .ToList()
                    : Enumerable.Range(0, (endDate.Year - startDate.Year) * 12 + endDate.Month - startDate.Month + 1)
                        .Select(i => startDate.AddMonths(i))
                        .Select(date =>
                        {
                            System.Diagnostics.Debug.WriteLine($"Processing month: {date:MMM yyyy}");

                            var orders = ordersInRange
                                .Where(o => o.OrderDate.Value.Year == date.Year &&
                                            o.OrderDate.Value.Month == date.Month)
                                .ToList();
                            decimal income = orders.Any() ? orders.Sum(o => o.TotalAmount) : 0m;

                            var purchaseDetails = purchaseDetailsInRange
                                .Where(pd => pd.Purchases.PurchaseDate.Year == date.Year &&
                                             pd.Purchases.PurchaseDate.Month == date.Month)
                                .ToList();
                            decimal expense = purchaseDetails.Any() ? purchaseDetails.Sum(pd => pd.Quantity * pd.UnitPrice) : 0m;

                            return new
                            {
                                Date = date,
                                Income = income,
                                Expense = expense
                            };
                        })
                        .ToList();

                // Очищаем серии
                foreach (var series in chart.Series)
                {
                    series.Points.Clear();
                    series.Enabled = true; // Включаем все серии по умолчанию
                }

                // Проверяем, есть ли данные
                if (dataPoints == null || !dataPoints.Any() || dataPoints.All(d => d.Income == 0 && d.Expense == 0))
                {
                    MessageBox.Show("Нет данных для отображения за выбранный период.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string selectedDataType = (CmbDataType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Доходы и расходы";
                SeriesChartType selectedChartType = (SeriesChartType)(CmbDiagram.SelectedItem ?? SeriesChartType.Column);

                // Попытка построить график
                try
                {
                    // Проверяем тип графика на известные проблемные типы
                    if (selectedChartType == SeriesChartType.ThreeLineBreak ||
                        selectedChartType == SeriesChartType.Renko ||
                        selectedChartType == SeriesChartType.Kagi ||
                        selectedChartType == SeriesChartType.PointAndFigure)
                    {
                        throw new InvalidOperationException($"Тип диаграммы {selectedChartType} не поддерживается в текущем контексте.");
                    }

                    // Устанавливаем тип графика
                    foreach (var series in chart.Series)
                    {
                        series.ChartType = selectedChartType;
                    }

                    // Устанавливаем видимость в легенде
                    chart.Series["Доходы"].IsVisibleInLegend = selectedDataType == "Доходы" || selectedDataType == "Доходы и расходы";
                    chart.Series["Расходы"].IsVisibleInLegend = selectedDataType == "Расходы" || selectedDataType == "Доходы и расходы";
                    chart.Series["Прибыль"].IsVisibleInLegend = selectedDataType == "Прибыль";

                    // Добавляем точки синхронно, чтобы все серии имели одинаковое количество точек
                    foreach (var data in dataPoints)
                    {
                        if (data == null) continue;

                        string label = groupByDays ? data.Date.ToString("dd.MM.yyyy") : data.Date.ToString("MMM yyyy");
                        decimal income = data.Income;
                        decimal expense = data.Expense;
                        decimal profit = income - expense;

                        System.Diagnostics.Debug.WriteLine($"Adding data for {label}: Income={income}, Expense={expense}, Profit={profit}");

                        // Добавляем точки во все серии для синхронизации
                        switch (selectedDataType)
                        {
                            case "Доходы":
                                chart.Series["Доходы"].Points.AddXY(label, income);
                                chart.Series["Расходы"].Points.AddXY(label, 0); // Пустая точка для выравнивания
                                chart.Series["Прибыль"].Points.AddXY(label, 0); // Пустая точка для выравнивания
                                break;
                            case "Расходы":
                                chart.Series["Доходы"].Points.AddXY(label, 0); // Пустая точка для выравнивания
                                chart.Series["Расходы"].Points.AddXY(label, expense);
                                chart.Series["Прибыль"].Points.AddXY(label, 0); // Пустая точка для выравнивания
                                break;
                            case "Прибыль":
                                chart.Series["Доходы"].Points.AddXY(label, 0); // Пустая точка для выравнивания
                                chart.Series["Расходы"].Points.AddXY(label, 0); // Пустая точка для выравнивания
                                chart.Series["Прибыль"].Points.AddXY(label, profit);
                                break;
                            case "Доходы и расходы":
                            default:
                                chart.Series["Доходы"].Points.AddXY(label, income);
                                chart.Series["Расходы"].Points.AddXY(label, expense);
                                chart.Series["Прибыль"].Points.AddXY(label, 0); // Пустая точка для выравнивания
                                break;
                        }
                    }

                    // Настройка осей для лучшей читаемости
                    chart.ChartAreas[0].AxisX.LabelStyle.Angle = groupByDays ? -45 : 0;
                    chart.ChartAreas[0].AxisX.Interval = groupByDays ? 7 : 1;
                }
                catch (Exception chartEx)
                {
                    // Если возникло исключение (например, InvalidOperationException для ThreeLineBreak), показываем сообщение и сбрасываем на Column
                    MessageBox.Show("Не удалось построить график с текущими параметрами. Будет использован тип по умолчанию (Column).", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    System.Diagnostics.Debug.WriteLine($"Chart error: {chartEx.Message}");

                    // Очищаем серии
                    foreach (var series in chart.Series)
                    {
                        series.Points.Clear();
                        series.ChartType = SeriesChartType.Column;
                        series.Enabled = true;
                    }

                    // Обновляем выбор в ComboBox
                    CmbDiagram.SelectedItem = SeriesChartType.Column;

                    // Устанавливаем видимость в легенде
                    chart.Series["Доходы"].IsVisibleInLegend = selectedDataType == "Доходы" || selectedDataType == "Доходы и расходы";
                    chart.Series["Расходы"].IsVisibleInLegend = selectedDataType == "Расходы" || selectedDataType == "Доходы и расходы";
                    chart.Series["Прибыль"].IsVisibleInLegend = selectedDataType == "Прибыль";

                    // Повторно добавляем точки для Column
                    foreach (var data in dataPoints)
                    {
                        if (data == null) continue;

                        string label = groupByDays ? data.Date.ToString("dd.MM.yyyy") : data.Date.ToString("MMM yyyy");
                        decimal income = data.Income;
                        decimal expense = data.Expense;
                        decimal profit = income - expense;

                        switch (selectedDataType)
                        {
                            case "Доходы":
                                chart.Series["Доходы"].Points.AddXY(label, income);
                                chart.Series["Расходы"].Points.AddXY(label, 0);
                                chart.Series["Прибыль"].Points.AddXY(label, 0);
                                break;
                            case "Расходы":
                                chart.Series["Доходы"].Points.AddXY(label, 0);
                                chart.Series["Расходы"].Points.AddXY(label, expense);
                                chart.Series["Прибыль"].Points.AddXY(label, 0);
                                break;
                            case "Прибыль":
                                chart.Series["Доходы"].Points.AddXY(label, 0);
                                chart.Series["Расходы"].Points.AddXY(label, 0);
                                chart.Series["Прибыль"].Points.AddXY(label, profit);
                                break;
                            case "Доходы и расходы":
                            default:
                                chart.Series["Доходы"].Points.AddXY(label, income);
                                chart.Series["Расходы"].Points.AddXY(label, expense);
                                chart.Series["Прибыль"].Points.AddXY(label, 0);
                                break;
                        }
                    }

                    // Настройка осей для Column
                    chart.ChartAreas[0].AxisX.LabelStyle.Angle = groupByDays ? -45 : 0;
                    chart.ChartAreas[0].AxisX.Interval = groupByDays ? 7 : 1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"Exception: {ex}");
            }
        }

        private void ExportToWordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем, есть ли данные для экспорта
                if (chart.Series.All(s => !s.Points.Any()))
                {
                    MessageBox.Show("Нет данных для экспорта. Постройте диаграмму, выбрав период и тип данных.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Получаем настройки диаграммы
                string selectedDataType = (CmbDataType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Доходы и расходы";
                string groupBy = (CmbGroupBy.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "По месяцам";
                DateTime startDate = StartDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-11).AddDays(1 - DateTime.Today.Day);
                DateTime endDate = EndDatePicker.SelectedDate ?? DateTime.Today;

                // Создаём новый документ Word
                var application = new Word.Application();
                Word.Document document = application.Documents.Add();

                // Добавляем надпись "RestFlow: Отчет от <дата и время>" сверху слева мелким шрифтом
                Word.Paragraph headerParagraph = document.Paragraphs.Add();
                Word.Range headerRange = headerParagraph.Range;
                headerRange.Text = $"RestFlow: Отчет от {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                headerRange.Font.Size = 8; // Мелкий шрифт
                headerRange.Font.Color = Word.WdColor.wdColorGray50; // Серый цвет
                headerRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphLeft;
                headerRange.InsertParagraphAfter();

                // Добавляем заголовок
                Word.Paragraph titleParagraph = document.Paragraphs.Add();
                Word.Range titleRange = titleParagraph.Range;
                titleRange.Text = $"Отчёт: {selectedDataType} ({groupBy})\nПериод: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
                titleParagraph.set_Style("Заголовок 1");
                titleRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                titleRange.InsertParagraphAfter();

                // Определяем количество столбцов в таблице в зависимости от типа данных
                int columnCount = 2; // Минимально: Дата + 1 тип данных
                if (selectedDataType == "Доходы и расходы")
                {
                    columnCount = 3; // Дата, Доходы, Расходы
                }

                // Определяем количество строк (количество точек данных)
                int rowCount = chart.Series.First(s => s.Points.Any()).Points.Count + 1; // +1 для заголовка таблицы

                // Добавляем таблицу
                Word.Paragraph tableParagraph = document.Paragraphs.Add();
                Word.Range tableRange = tableParagraph.Range;
                Word.Table dataTable = document.Tables.Add(tableRange, rowCount, columnCount);
                dataTable.Borders.InsideLineStyle = Word.WdLineStyle.wdLineStyleSingle;
                dataTable.Borders.OutsideLineStyle = Word.WdLineStyle.wdLineStyleSingle;
                dataTable.Range.Cells.VerticalAlignment = Word.WdCellVerticalAlignment.wdCellAlignVerticalCenter;

                // Заполняем заголовки таблицы
                Word.Range cellRange;
                cellRange = dataTable.Cell(1, 1).Range;
                cellRange.Text = "Дата";
                cellRange.Bold = 1;
                cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;

                if (selectedDataType == "Доходы и расходы")
                {
                    cellRange = dataTable.Cell(1, 2).Range;
                    cellRange.Text = "Доходы";
                    cellRange.Bold = 1;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;

                    cellRange = dataTable.Cell(1, 3).Range;
                    cellRange.Text = "Расходы";
                    cellRange.Bold = 1;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                }
                else if (selectedDataType == "Прибыль")
                {
                    cellRange = dataTable.Cell(1, 2).Range;
                    cellRange.Text = "Прибыль";
                    cellRange.Bold = 1;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                }
                else if (selectedDataType == "Доходы")
                {
                    cellRange = dataTable.Cell(1, 2).Range;
                    cellRange.Text = "Доходы";
                    cellRange.Bold = 1;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                }
                else if (selectedDataType == "Расходы")
                {
                    cellRange = dataTable.Cell(1, 2).Range;
                    cellRange.Text = "Расходы";
                    cellRange.Bold = 1;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                }

                // Заполняем данные таблицы
                for (int i = 0; i < rowCount - 1; i++)
                {
                    int rowIndex = i + 2; // Начинаем с 2-й строки (после заголовка)

                    // Дата
                    string dateLabel = chart.Series.First(s => s.Points.Any()).Points[i].AxisLabel;
                    cellRange = dataTable.Cell(rowIndex, 1).Range;
                    cellRange.Text = dateLabel;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;

                    // Значения в зависимости от типа данных
                    if (selectedDataType == "Доходы и расходы")
                    {
                        decimal income = (decimal)chart.Series["Доходы"].Points[i].YValues[0];
                        decimal expense = (decimal)chart.Series["Расходы"].Points[i].YValues[0];

                        cellRange = dataTable.Cell(rowIndex, 2).Range;
                        cellRange.Text = income.ToString("N2");
                        cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;

                        cellRange = dataTable.Cell(rowIndex, 3).Range;
                        cellRange.Text = expense.ToString("N2");
                        cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                    }
                    else if (selectedDataType == "Прибыль")
                    {
                        decimal profit = (decimal)chart.Series["Прибыль"].Points[i].YValues[0];

                        cellRange = dataTable.Cell(rowIndex, 2).Range;
                        cellRange.Text = profit.ToString("N2");
                        cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                    }
                    else if (selectedDataType == "Доходы")
                    {
                        decimal income = (decimal)chart.Series["Доходы"].Points[i].YValues[0];

                        cellRange = dataTable.Cell(rowIndex, 2).Range;
                        cellRange.Text = income.ToString("N2");
                        cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                    }
                    else if (selectedDataType == "Расходы")
                    {
                        decimal expense = (decimal)chart.Series["Расходы"].Points[i].YValues[0];

                        cellRange = dataTable.Cell(rowIndex, 2).Range;
                        cellRange.Text = expense.ToString("N2");
                        cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                    }
                }

                // Делаем документ видимым
                application.Visible = true;

                // Формируем имя файла с текущей датой
                string currentDate = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string docxPath = $@"Z:\Programs\RestFlowSystem\ReportsOut\Отчет_{currentDate}.docx";
                string pdfPath = $@"Z:\Programs\RestFlowSystem\ReportsOut\Отчет_{currentDate}.pdf";

                // Сохраняем документ в формате .docx и .pdf
                document.SaveAs2(docxPath);
                document.SaveAs2(pdfPath, Word.WdExportFormat.wdExportFormatPDF);

                MessageBox.Show($"Экспорт в Word успешно выполнен!\nФайлы сохранены по пути:\n{docxPath}\n{pdfPath}",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в Word: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToExcelButton_Click(object sender, RoutedEventArgs e)
        {
            Excel.Application application = null; // Объявляем вне try
            try
            {
                // Проверяем, есть ли данные для экспорта
                if (chart.Series.All(s => !s.Points.Any()))
                {
                    MessageBox.Show("Нет данных для экспорта. Постройте диаграмму, выбрав период и тип данных.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Получаем настройки диаграммы
                string selectedDataType = (CmbDataType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Доходы и расходы";
                string groupBy = (CmbGroupBy.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "По месяцам";
                DateTime startDate = StartDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-11).AddDays(1 - DateTime.Today.Day);
                DateTime endDate = EndDatePicker.SelectedDate ?? DateTime.Today;

                // Создаём новый документ Excel
                application = new Excel.Application();
                Excel.Workbook workbook = application.Workbooks.Add();
                Excel.Worksheet worksheet = workbook.ActiveSheet;

                // Добавляем заголовок
                worksheet.Cells[1, 1] = $"RestFlow: Отчет от {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                Excel.Range headerRange = worksheet.Range["A1"];
                headerRange.Font.Size = 8;
                headerRange.Font.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.Gray);
                headerRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignLeft;

                // Добавляем основной заголовок
                worksheet.Cells[2, 1] = $"Отчёт: {selectedDataType} ({groupBy})";
                worksheet.Cells[3, 1] = $"Период: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
                Excel.Range titleRange = worksheet.Range["A2:A3"];
                titleRange.Font.Size = 12;
                titleRange.Font.Bold = true;
                titleRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                worksheet.Columns[1].AutoFit(); // Автоматическая подгонка ширины столбца

                // Определяем количество столбцов в таблице в зависимости от типа данных
                int columnCount = 2; // Минимально: Дата + 1 тип данных
                if (selectedDataType == "Доходы и расходы")
                {
                    columnCount = 3; // Дата, Доходы, Расходы
                }

                // Определяем количество строк (количество точек данных + 1 строка для заголовков)
                int rowCount = chart.Series.First(s => s.Points.Any()).Points.Count + 1;

                // Заполняем заголовки таблицы
                worksheet.Cells[5, 1] = "Дата";
                Excel.Range cellRange = worksheet.Range["A5"];
                cellRange.Font.Bold = true;
                cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

                if (selectedDataType == "Доходы и расходы")
                {
                    worksheet.Cells[5, 2] = "Доходы";
                    cellRange = worksheet.Range["B5"];
                    cellRange.Font.Bold = true;
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

                    worksheet.Cells[5, 3] = "Расходы";
                    cellRange = worksheet.Range["C5"];
                    cellRange.Font.Bold = true;
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                }
                else if (selectedDataType == "Прибыль")
                {
                    worksheet.Cells[5, 2] = "Прибыль";
                    cellRange = worksheet.Range["B5"];
                    cellRange.Font.Bold = true;
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                }
                else if (selectedDataType == "Доходы")
                {
                    worksheet.Cells[5, 2] = "Доходы";
                    cellRange = worksheet.Range["B5"];
                    cellRange.Font.Bold = true;
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                }
                else if (selectedDataType == "Расходы")
                {
                    worksheet.Cells[5, 2] = "Расходы";
                    cellRange = worksheet.Range["B5"];
                    cellRange.Font.Bold = true;
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                }

                // Заполняем данные таблицы
                for (int i = 0; i < rowCount - 1; i++)
                {
                    int rowIndex = i + 6; // Начинаем с 6-й строки (после заголовков)

                    // Дата
                    string dateLabel = chart.Series.First(s => s.Points.Any()).Points[i].AxisLabel;
                    worksheet.Cells[rowIndex, 1] = dateLabel;
                    cellRange = worksheet.Range[worksheet.Cells[rowIndex, 1], worksheet.Cells[rowIndex, 1]];
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

                    // Значения в зависимости от типа данных
                    if (selectedDataType == "Доходы и расходы")
                    {
                        decimal income = (decimal)chart.Series["Доходы"].Points[i].YValues[0];
                        decimal expense = (decimal)chart.Series["Расходы"].Points[i].YValues[0];

                        worksheet.Cells[rowIndex, 2] = income;
                        cellRange = worksheet.Range[worksheet.Cells[rowIndex, 2], worksheet.Cells[rowIndex, 2]];
                        cellRange.NumberFormat = "#,##0.00";
                        cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

                        worksheet.Cells[rowIndex, 3] = expense;
                        cellRange = worksheet.Range[worksheet.Cells[rowIndex, 3], worksheet.Cells[rowIndex, 3]];
                        cellRange.NumberFormat = "#,##0.00";
                        cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    }
                    else if (selectedDataType == "Прибыль")
                    {
                        decimal profit = (decimal)chart.Series["Прибыль"].Points[i].YValues[0];

                        worksheet.Cells[rowIndex, 2] = profit;
                        cellRange = worksheet.Range[worksheet.Cells[rowIndex, 2], worksheet.Cells[rowIndex, 2]];
                        cellRange.NumberFormat = "#,##0.00";
                        cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    }
                    else if (selectedDataType == "Доходы")
                    {
                        decimal income = (decimal)chart.Series["Доходы"].Points[i].YValues[0];

                        worksheet.Cells[rowIndex, 2] = income;
                        cellRange = worksheet.Range[worksheet.Cells[rowIndex, 2], worksheet.Cells[rowIndex, 2]];
                        cellRange.NumberFormat = "#,##0.00";
                        cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    }
                    else if (selectedDataType == "Расходы")
                    {
                        decimal expense = (decimal)chart.Series["Расходы"].Points[i].YValues[0];

                        worksheet.Cells[rowIndex, 2] = expense;
                        cellRange = worksheet.Range[worksheet.Cells[rowIndex, 2], worksheet.Cells[rowIndex, 2]];
                        cellRange.NumberFormat = "#,##0.00";
                        cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    }
                }

                // Добавляем границы таблицы
                Excel.Range tableRange = worksheet.Range[worksheet.Cells[5, 1], worksheet.Cells[rowCount + 4, columnCount]];
                tableRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                tableRange.Borders.Weight = Excel.XlBorderWeight.xlThin;

                // Автоматическая подгонка ширины столбцов
                for (int col = 1; col <= columnCount; col++)
                {
                    worksheet.Columns[col].AutoFit();
                }

                // Делаем документ видимым
                application.Visible = true;

                // Формируем имя файла с текущей датой
                string currentDate = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string excelPath = $@"Z:\Programs\RestFlowSystem\ReportsOut\Отчет_{currentDate}.xlsx";

                // Сохраняем документ в формате .xlsx
                workbook.SaveAs(excelPath);
                workbook.Close();

                MessageBox.Show($"Экспорт в Excel успешно выполнен!\nФайл сохранён по пути:\n{excelPath}",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в Excel: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Освобождаем ресурсы Excel, если application была создана
                if (application != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(application);
                }
            }
        }
    }
}
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using Word = Microsoft.Office.Interop.Word;
using Excel = Microsoft.Office.Interop.Excel;
using System.IO;
using System.Drawing.Imaging;

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

            chart.Series.Add(new Series("Выручка")
            {
                IsValueShownAsLabel = true,
                Color = System.Drawing.Color.Green
            });

            chart.Series.Add(new Series("Закупочные расходы")
            {
                IsValueShownAsLabel = true,
                Color = System.Drawing.Color.Red
            });

            chart.Series.Add(new Series("Валовая прибыль")
            {
                IsValueShownAsLabel = true,
                Color = System.Drawing.Color.Blue
            });

            chart.Legends.Add(new Legend());
            ChartHost.Child = chart;

            isChartInitialized = true;
            System.Diagnostics.Debug.WriteLine("Chart initialized in constructor");

            EndDatePicker.SelectedDate = DateTime.Today;
            StartDatePicker.SelectedDate = DateTime.Today.AddMonths(-1);

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

            if (chart.Series["Выручка"] == null || chart.Series["Закупочные расходы"] == null || chart.Series["Валовая прибыль"] == null)
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
                DateTime startDate = StartDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-1);
                DateTime endDate = EndDatePicker.SelectedDate ?? DateTime.Today;

                DateTime minAllowedDate = new DateTime(2000, 1, 1);
                if (startDate < minAllowedDate)
                {
                    MessageBox.Show("Начальная дата не может быть раньше 1 января 2000 года.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    startDate = minAllowedDate;
                    StartDatePicker.SelectedDate = startDate;
                }

                if (startDate > endDate)
                {
                    MessageBox.Show("Начальная дата не может быть позже конечной.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StartDatePicker.SelectedDate = DateTime.Today.AddMonths(-1);
                    EndDatePicker.SelectedDate = DateTime.Today;
                    startDate = StartDatePicker.SelectedDate.Value;
                    endDate = EndDatePicker.SelectedDate.Value;
                }

                if (endDate > DateTime.Today)
                {
                    endDate = DateTime.Today;
                    EndDatePicker.SelectedDate = endDate;
                }

                string groupBy = (CmbGroupBy.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "По месяцам";
                bool groupByDays = groupBy == "По дням";

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

                var dataPoints = groupByDays
                    ? Enumerable.Range(0, (endDate - startDate).Days + 1)
                        .Select(i => startDate.AddDays(i))
                        .Select(date =>
                        {
                            System.Diagnostics.Debug.WriteLine($"Processing day: {date:dd.MM.yyyy}");
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

                foreach (var series in chart.Series)
                {
                    series.Points.Clear();
                    series.Enabled = true;
                }

                if (dataPoints == null || !dataPoints.Any() || dataPoints.All(d => d.Income == 0 && d.Expense == 0))
                {
                    MessageBox.Show("Нет данных для отображения за выбранный период.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string selectedDataType = (CmbDataType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Выручка и закупочные расходы";
                string selectedChartTypeStr = (CmbDiagram.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Column";
                SeriesChartType selectedChartType;
                switch (selectedChartTypeStr)
                {
                    case "Pie":
                        selectedChartType = SeriesChartType.Pie;
                        break;
                    case "Line":
                        selectedChartType = SeriesChartType.Line;
                        break;
                    case "Column":
                    default:
                        selectedChartType = SeriesChartType.Column;
                        break;
                }

                foreach (var series in chart.Series)
                {
                    series.ChartType = selectedChartType;
                }

                chart.Series["Выручка"].IsVisibleInLegend = selectedDataType == "Выручка" || selectedDataType == "Выручка и закупочные расходы";
                chart.Series["Закупочные расходы"].IsVisibleInLegend = selectedDataType == "Закупочные расходы" || selectedDataType == "Выручка и закупочные расходы";
                chart.Series["Валовая прибыль"].IsVisibleInLegend = selectedDataType == "Валовая прибыль";

                foreach (var data in dataPoints)
                {
                    string label = groupByDays ? data.Date.ToString("dd.MM.yyyy") : data.Date.ToString("MMM yyyy");
                    decimal income = data.Income;
                    decimal expense = data.Expense;
                    decimal profit = income - expense;

                    switch (selectedDataType)
                    {
                        case "Выручка":
                            chart.Series["Выручка"].Points.AddXY(label, income);
                            chart.Series["Закупочные расходы"].Points.AddXY(label, 0);
                            chart.Series["Валовая прибыль"].Points.AddXY(label, 0);
                            break;
                        case "Закупочные расходы":
                            chart.Series["Выручка"].Points.AddXY(label, 0);
                            chart.Series["Закупочные расходы"].Points.AddXY(label, expense);
                            chart.Series["Валовая прибыль"].Points.AddXY(label, 0);
                            break;
                        case "Валовая прибыль":
                            chart.Series["Выручка"].Points.AddXY(label, 0);
                            chart.Series["Закупочные расходы"].Points.AddXY(label, 0);
                            chart.Series["Валовая прибыль"].Points.AddXY(label, profit);
                            break;
                        case "Выручка и закупочные расходы":
                        default:
                            chart.Series["Выручка"].Points.AddXY(label, income);
                            chart.Series["Закупочные расходы"].Points.AddXY(label, expense);
                            chart.Series["Валовая прибыль"].Points.AddXY(label, 0);
                            break;
                    }
                }

                chart.ChartAreas[0].AxisX.LabelStyle.Angle = groupByDays ? -45 : 0;
                chart.ChartAreas[0].AxisX.Interval = groupByDays ? 7 : 1;
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
                if (chart.Series.All(s => !s.Points.Any()))
                {
                    MessageBox.Show("Нет данных для экспорта. Постройте диаграмму, выбрав период и тип данных.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string selectedDataType = (CmbDataType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Выручка и закупочные расходы";
                string groupBy = (CmbGroupBy.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "По месяцам";
                DateTime startDate = StartDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-1);
                DateTime endDate = EndDatePicker.SelectedDate ?? DateTime.Today;

                var application = new Word.Application();
                Word.Document document = application.Documents.Add();

                Word.Paragraph headerParagraph = document.Paragraphs.Add();
                Word.Range headerRange = headerParagraph.Range;
                headerRange.Text = $"RestFlow: Отчет от {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                headerRange.Font.Size = 8;
                headerRange.Font.Color = Word.WdColor.wdColorGray50;
                headerRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphLeft;
                headerRange.InsertParagraphAfter();

                Word.Paragraph titleParagraph = document.Paragraphs.Add();
                Word.Range titleRange = titleParagraph.Range;
                titleRange.Text = $"Отчёт: {selectedDataType} ({groupBy})\nПериод: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
                titleParagraph.set_Style("Заголовок 1");
                titleRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                titleRange.InsertParagraphAfter();

                int columnCount = selectedDataType == "Выручка и закупочные расходы" ? 3 : 2;
                int rowCount = chart.Series.First(s => s.Points.Any()).Points.Count + 1;

                Word.Paragraph tableParagraph = document.Paragraphs.Add();
                Word.Range tableRange = tableParagraph.Range;
                Word.Table dataTable = document.Tables.Add(tableRange, rowCount, columnCount);
                dataTable.Borders.InsideLineStyle = Word.WdLineStyle.wdLineStyleSingle;
                dataTable.Borders.OutsideLineStyle = Word.WdLineStyle.wdLineStyleSingle;
                dataTable.Range.Cells.VerticalAlignment = Word.WdCellVerticalAlignment.wdCellAlignVerticalCenter;

                Word.Range cellRange;
                cellRange = dataTable.Cell(1, 1).Range;
                cellRange.Text = "Дата";
                cellRange.Bold = 1;
                cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;

                if (selectedDataType == "Выручка и закупочные расходы")
                {
                    cellRange = dataTable.Cell(1, 2).Range;
                    cellRange.Text = "Выручка";
                    cellRange.Bold = 1;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;

                    cellRange = dataTable.Cell(1, 3).Range;
                    cellRange.Text = "Закупочные расходы";
                    cellRange.Bold = 1;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                }
                else if (selectedDataType == "Валовая прибыль")
                {
                    cellRange = dataTable.Cell(1, 2).Range;
                    cellRange.Text = "Валовая прибыль";
                    cellRange.Bold = 1;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                }
                else if (selectedDataType == "Выручка")
                {
                    cellRange = dataTable.Cell(1, 2).Range;
                    cellRange.Text = "Выручка";
                    cellRange.Bold = 1;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                }
                else if (selectedDataType == "Закупочные расходы")
                {
                    cellRange = dataTable.Cell(1, 2).Range;
                    cellRange.Text = "Закупочные расходы";
                    cellRange.Bold = 1;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                }

                for (int i = 0; i < rowCount - 1; i++)
                {
                    int rowIndex = i + 2;
                    string dateLabel = chart.Series.First(s => s.Points.Any()).Points[i].AxisLabel;
                    cellRange = dataTable.Cell(rowIndex, 1).Range;
                    cellRange.Text = dateLabel;
                    cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;

                    if (selectedDataType == "Выручка и закупочные расходы")
                    {
                        decimal income = (decimal)chart.Series["Выручка"].Points[i].YValues[0];
                        decimal expense = (decimal)chart.Series["Закупочные расходы"].Points[i].YValues[0];
                        cellRange = dataTable.Cell(rowIndex, 2).Range;
                        cellRange.Text = income.ToString("N2");
                        cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                        cellRange = dataTable.Cell(rowIndex, 3).Range;
                        cellRange.Text = expense.ToString("N2");
                        cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                    }
                    else if (selectedDataType == "Валовая прибыль")
                    {
                        decimal profit = (decimal)chart.Series["Валовая прибыль"].Points[i].YValues[0];
                        cellRange = dataTable.Cell(rowIndex, 2).Range;
                        cellRange.Text = profit.ToString("N2");
                        cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                    }
                    else if (selectedDataType == "Выручка")
                    {
                        decimal income = (decimal)chart.Series["Выручка"].Points[i].YValues[0];
                        cellRange = dataTable.Cell(rowIndex, 2).Range;
                        cellRange.Text = income.ToString("N2");
                        cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                    }
                    else if (selectedDataType == "Закупочные расходы")
                    {
                        decimal expense = (decimal)chart.Series["Закупочные расходы"].Points[i].YValues[0];
                        cellRange = dataTable.Cell(rowIndex, 2).Range;
                        cellRange.Text = expense.ToString("N2");
                        cellRange.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                    }
                }

                application.Visible = true;
                string currentDate = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string projectPath = Directory.GetParent(basePath).Parent.Parent.FullName; 
                string reportsPath = Path.Combine(projectPath, "ReportsInfo");

                if (!Directory.Exists(reportsPath))
                {
                    Directory.CreateDirectory(reportsPath);
                }

                string docxPath = Path.Combine(reportsPath, $"Отчет_{currentDate}.docx");
                string pdfPath = Path.Combine(reportsPath, $"Отчет_{currentDate}.pdf");
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
            Excel.Application application = null;
            string tempChartImagePath = null;
            try
            {
                if (chart.Series.All(s => !s.Points.Any()))
                {
                    MessageBox.Show("Нет данных для экспорта. Постройте диаграмму, выбрав период и тип данных.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string selectedDataType = (CmbDataType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Выручка и закупочные расходы";
                string groupBy = (CmbGroupBy.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "По месяцам";
                DateTime startDate = StartDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-1);
                DateTime endDate = EndDatePicker.SelectedDate ?? DateTime.Today;

                application = new Excel.Application();
                Excel.Workbook workbook = application.Workbooks.Add();
                Excel.Worksheet worksheet = workbook.ActiveSheet;

                worksheet.Cells[1, 1] = $"RestFlow: Отчет от {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                Excel.Range headerRange = worksheet.Range["A1"];
                headerRange.Font.Size = 8;
                headerRange.Font.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.Gray);
                headerRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignLeft;

                worksheet.Cells[2, 1] = $"Отчёт: {selectedDataType} ({groupBy})";
                worksheet.Cells[3, 1] = $"Период: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
                Excel.Range titleRange = worksheet.Range["A2:A3"];
                titleRange.Font.Size = 12;
                titleRange.Font.Bold = true;
                titleRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                worksheet.Columns[1].AutoFit();

                int columnCount = selectedDataType == "Выручка и закупочные расходы" ? 3 : 2;
                int rowCount = chart.Series.First(s => s.Points.Any()).Points.Count + 1;

                worksheet.Cells[5, 1] = "Дата";
                Excel.Range cellRange = worksheet.Range["A5"];
                cellRange.Font.Bold = true;
                cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

                if (selectedDataType == "Выручка и закупочные расходы")
                {
                    worksheet.Cells[5, 2] = "Выручка";
                    cellRange = worksheet.Range["B5"];
                    cellRange.Font.Bold = true;
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    worksheet.Cells[5, 3] = "Закупочные расходы";
                    cellRange = worksheet.Range["C5"];
                    cellRange.Font.Bold = true;
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                }
                else if (selectedDataType == "Валовая прибыль")
                {
                    worksheet.Cells[5, 2] = "Валовая прибыль";
                    cellRange = worksheet.Range["B5"];
                    cellRange.Font.Bold = true;
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                }
                else if (selectedDataType == "Выручка")
                {
                    worksheet.Cells[5, 2] = "Выручка";
                    cellRange = worksheet.Range["B5"];
                    cellRange.Font.Bold = true;
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                }
                else if (selectedDataType == "Закупочные расходы")
                {
                    worksheet.Cells[5, 2] = "Закупочные расходы";
                    cellRange = worksheet.Range["B5"];
                    cellRange.Font.Bold = true;
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                }

                for (int i = 0; i < rowCount - 1; i++)
                {
                    int rowIndex = i + 6;
                    string dateLabel = chart.Series.First(s => s.Points.Any()).Points[i].AxisLabel;
                    worksheet.Cells[rowIndex, 1] = dateLabel;
                    cellRange = worksheet.Range[worksheet.Cells[rowIndex, 1], worksheet.Cells[rowIndex, 1]];
                    cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

                    if (selectedDataType == "Выручка и закупочные расходы")
                    {
                    decimalව: decimal income = (decimal)chart.Series["Выручка"].Points[i].YValues[0];
                        decimal expense = (decimal)chart.Series["Закупочные расходы"].Points[i].YValues[0];
                        worksheet.Cells[rowIndex, 2] = income;
                        cellRange = worksheet.Range[worksheet.Cells[rowIndex, 2], worksheet.Cells[rowIndex, 2]];
                        cellRange.NumberFormat = "#,##0.00";
                        cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                        worksheet.Cells[rowIndex, 3] = expense;
                        cellRange = worksheet.Range[worksheet.Cells[rowIndex, 3], worksheet.Cells[rowIndex, 3]];
                        cellRange.NumberFormat = "#,##0.00";
                        cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    }
                    else if (selectedDataType == "Валовая прибыль")
                    {
                        decimal profit = (decimal)chart.Series["Валовая прибыль"].Points[i].YValues[0];
                        worksheet.Cells[rowIndex, 2] = profit;
                        cellRange = worksheet.Range[worksheet.Cells[rowIndex, 2], worksheet.Cells[rowIndex, 2]];
                        cellRange.NumberFormat = "#,##0.00";
                        cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    }
                    else if (selectedDataType == "Выручка")
                    {
                        decimal income = (decimal)chart.Series["Выручка"].Points[i].YValues[0];
                        worksheet.Cells[rowIndex, 2] = income;
                        cellRange = worksheet.Range[worksheet.Cells[rowIndex, 2], worksheet.Cells[rowIndex, 2]];
                        cellRange.NumberFormat = "#,##0.00";
                        cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    }
                    else if (selectedDataType == "Закупочные расходы")
                    {
                        decimal expense = (decimal)chart.Series["Закупочные расходы"].Points[i].YValues[0];
                        worksheet.Cells[rowIndex, 2] = expense;
                        cellRange = worksheet.Range[worksheet.Cells[rowIndex, 2], worksheet.Cells[rowIndex, 2]];
                        cellRange.NumberFormat = "#,##0.00";
                        cellRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    }
                }

                Excel.Range tableRange = worksheet.Range[worksheet.Cells[5, 1], worksheet.Cells[rowCount + 4, columnCount]];
                tableRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous;
                tableRange.Borders.Weight = Excel.XlBorderWeight.xlThin;

                for (int col = 1; col <= columnCount; col++)
                {
                    worksheet.Columns[col].AutoFit();
                }

                tempChartImagePath = Path.Combine(Path.GetTempPath(), $"ChartExport_{Guid.NewGuid()}.png");
                chart.SaveImage(tempChartImagePath, ChartImageFormat.Png);

                int chartRowStart = rowCount + 6;
                Excel.Range chartRange = worksheet.Cells[chartRowStart, 1];
                float chartWidth = 400; 
                float chartHeight = 300;

                worksheet.Shapes.AddPicture(
                    tempChartImagePath,
                    Microsoft.Office.Core.MsoTriState.msoFalse,
                    Microsoft.Office.Core.MsoTriState.msoTrue,
                    Convert.ToSingle(chartRange.Left),
                    Convert.ToSingle(chartRange.Top),
                    chartWidth,
                    chartHeight);

                application.Visible = true;
                string currentDate = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string projectPath = Directory.GetParent(basePath).Parent.Parent.FullName; 
                string reportsPath = Path.Combine(projectPath, "ReportsInfo");

                if (!Directory.Exists(reportsPath))
                {
                    Directory.CreateDirectory(reportsPath);
                }

                string excelPath = Path.Combine(reportsPath, $"Отчет_{currentDate}.xlsx");
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
                if (!string.IsNullOrEmpty(tempChartImagePath) && File.Exists(tempChartImagePath))
                {
                    try
                    {
                        File.Delete(tempChartImagePath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Не удалось удалить временный файл: {ex.Message}");
                    }
                }

                if (application != null)
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(application);
                }
            }
        }
    }
}
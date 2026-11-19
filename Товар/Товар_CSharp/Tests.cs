using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TovarSales
{
    internal static class Tests
    {
        private static StreamWriter log;
        private static int passed;

        private static string Prepare(string path)
        {
            string root = Path.GetFullPath(path ?? Path.Combine(Path.GetTempPath(), "TovarSalesTest_" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            if (Directory.GetFileSystemEntries(root).Length != 0)
                throw new Exception("Для проверки нужна новая пустая папка: " + root);
            return root;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); }
            catch (Exception) { rejected = true; }
            Check(rejected, message);
        }

        private static void Pass(string text)
        {
            passed++;
            log.WriteLine("PASS " + text);
            log.Flush();
        }

        public static int Run(string path)
        {
            string root;
            try { root = Prepare(path); }
            catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
            using (log = new StreamWriter(Path.Combine(root, "results.txt"), false, Encoding.UTF8))
            {
                passed = 0;
                try
                {
                    string file = Path.Combine(root, "store.xml");
                    Store store = new Store(file);
                    Check(store.Data.Products.Count == 5 && store.FindProduct(1).Stock == 40, "Нет начальных товаров.");
                    Pass("Начальные пять товаров из рисунка 8");

                    Product product = store.PutProduct(0, "  Тест  ", "  Проверка  ", 10.50m, 5);
                    int id = product.Id;
                    Check(product.Name == "Тест" && product.Category == "Проверка" && id == 6, "Ошибка карточки.");
                    Pass("T01 Добавление товара 10,50 руб., запас 5");

                    List<SaleItem> cart = new List<SaleItem>();
                    store.AddToCart(cart, id, 2);
                    Check(store.FindProduct(id).Stock == 5, "Корзина изменила запас.");
                    Sale sale = store.Checkout(cart);
                    Check(sale.Total == 21m && store.FindProduct(id).Stock == 3 && sale.Id == 1, "Неправильный расчет.");
                    Pass("T02 Две единицы: 21 руб., запас 3");

                    store = new Store(file);
                    Check(store.Data.Sales.Count == 1 && store.Data.Sales[0].Total == 21m, "История потеряна.");
                    Pass("T03 Повторное чтение сохраняет продажу");

                    Reject(delegate { store.Checkout(new List<SaleItem>()); }, "Пустая корзина принята.");
                    Check(store.Data.Sales.Count == 1, "Отказ изменил историю.");
                    Pass("T04 Пустая корзина отклоняется");

                    cart = new List<SaleItem> { new SaleItem { ProductId = id, Name = "Тест", Price = 10.50m, Quantity = 4 } };
                    string original = File.ReadAllText(file);
                    Reject(delegate { store.Checkout(cart); }, "Превышение запаса принято.");
                    Check(store.FindProduct(id).Stock == 3 && File.ReadAllText(file) == original, "Изменение после отказа.");
                    Pass("T05 Четыре единицы при запасе 3 отклонены");

                    Reject(delegate { store.PutProduct(0, "   ", "Проверка", 10m, 1); }, "Пустое название принято.");
                    Pass("T06 Название из пробелов отклоняется");

                    Reject(delegate { store.PutProduct(0, "Тест", "Проверка", 0m, 1); }, "Нулевая цена принята.");
                    Pass("T07 Нулевая цена отклоняется");

                    cart = new List<SaleItem>();
                    store.AddToCart(cart, id, 1);
                    store.PutProduct(id, "Тест", "Проверка", 11m, 3);
                    Reject(delegate { store.Checkout(cart); }, "Старая цена принята.");
                    Check(cart.Count == 1 && store.FindProduct(id).Stock == 3, "Корзина или запас изменились.");
                    Pass("T08 Измененная цена блокирует старую корзину");

                    cart.Clear();
                    store.AddToCart(cart, id, 1);
                    store.Archive(id);
                    Reject(delegate { store.Checkout(cart); }, "Архивный товар продан.");
                    Check(store.Search("Тест").Count == 0 && store.Data.Sales.Count == 1, "Архив не скрыт.");
                    Pass("T09 Архивный товар не оформляется");

                    Check(File.Exists(file + ".bak"), "Нет резервной копии.");
                    Store backup = new Store(file + ".bak");
                    Check(backup.FindProduct(id).Active && !store.FindProduct(id).Active, "Резервная копия не предыдущая.");
                    Pass("T10 Резервная копия существует и содержит предыдущую версию");

                    cart.Clear();
                    store.AddToCart(cart, 1, 2);
                    original = File.ReadAllText(file);
                    string oldBackup = File.ReadAllText(file + ".bak");
                    int count = store.Data.Products.Count;
                    Directory.CreateDirectory(file + ".tmp");
                    Reject(delegate { store.Checkout(cart); }, "Отказ записи не воспроизведен.");
                    Reject(delegate { store.PutProduct(0, "Новый", "Тест", 1m, 1); }, "Запись товара не отклонена.");
                    Check(store.Data.Products.Count == count && store.FindProduct(1).Stock == 40 &&
                        store.Data.Sales.Count == 1 && cart.Count == 1 && cart[0].Quantity == 2 &&
                        File.ReadAllText(file) == original && File.ReadAllText(file + ".bak") == oldBackup,
                        "Отказ записи изменил данные.");
                    Directory.Delete(file + ".tmp");
                    Pass("T11 Ошибка записи сохраняет файл, резервную копию, данные и корзину");

                    sale = store.Checkout(cart);
                    Check(sale.Total == 130m && store.FindProduct(1).Stock == 38, "Ошибка примера из курсовой.");
                    Pass("Две тетради по 65 руб.: итог 130 руб., запас 40 -> 38");

                    store.PutProduct(1, "Новое название", "Канцтовары", 99m, 38);
                    Store reopened = new Store(file);
                    Sale savedSale = reopened.Data.Sales[1];
                    Check(savedSale.Total == 130m && savedSale.Items[0].Name == "Тетрадь 48 листов" &&
                        Store.Receipt(savedSale).Contains("130,00"), "История зависит от каталога.");
                    Pass("История и окно продажи хранят исходные название и цену");

                    Check(store.Search("КАНЦ").Count == 4 && store.Search("КРУЖКА").Count == 1, "Поиск учитывает регистр.");
                    Pass("Поиск по названию и категории без учета регистра");

                    cart.Clear();
                    store.AddToCart(cart, 2, 2);
                    store.AddToCart(cart, 2, 3);
                    Check(cart.Count == 1 && cart[0].Quantity == 5 && store.FindProduct(2).Stock == 60, "Нет объединения строк.");
                    Reject(delegate { store.AddToCart(cart, 2, 56); }, "В корзине превышен запас.");
                    Check(cart[0].Quantity == 5, "Отказ изменил корзину.");
                    Pass("Повторный товар объединяется, суммарный запас контролируется");

                    StoreData next = store.Copy();
                    next.Products[0].Stock = 0;
                    Check(store.FindProduct(1).Stock == 38, "Копия связана с оригиналом.");
                    Pass("Copy создает независимый набор");

                    next = store.Copy();
                    next.Sales[0].Date = new DateTime(2026, 11, 1, 23, 59, 59);
                    next.Sales[1].Date = new DateTime(2026, 11, 19, 0, 0, 1);
                    store.Save(next);
                    List<Sale> report = store.Report(new DateTime(2026, 11, 1), new DateTime(2026, 11, 19));
                    Check(report.Count == 2 && Store.Revenue(report) == 151m, "Границы периода исключены.");
                    Check(store.Report(new DateTime(2026, 11, 1), new DateTime(2026, 11, 1)).Count == 1, "Не включен конец дня.");
                    Check(store.Report(new DateTime(2026, 11, 20), new DateTime(2026, 11, 21)).Count == 0, "Непустой период.");
                    Reject(delegate { store.Report(DateTime.Today.AddDays(1), DateTime.Today); }, "Неверный период принят.");
                    Pass("Даты отчета включают обе границы; пустой и обратный периоды");

                    string csv = Path.Combine(root, "report.csv");
                    store.ExportCsv(csv, new DateTime(2026, 11, 1), new DateTime(2026, 11, 19));
                    byte[] bytes = File.ReadAllBytes(csv);
                    string csvText = File.ReadAllText(csv, Encoding.UTF8);
                    Check(bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 &&
                        csvText.Contains("1;01.11.2026 23:59;21,00") &&
                        csvText.Contains("2;19.11.2026 00:00;130,00"), "CSV не соответствует формату.");
                    Reject(delegate { store.ExportCsv(file, DateTime.MinValue, DateTime.MaxValue); }, "CSV перезаписал данные.");
                    Pass("CSV: UTF-8 BOM, точка с запятой, номер, дата, сумма");

                    Reject(delegate { store.PutProduct(0, "X", " ", 1m, 1); }, "Пустая категория принята.");
                    Reject(delegate { store.PutProduct(0, new string('X', 101), "X", 1m, 1); }, "Длинное название принято.");
                    Reject(delegate { store.PutProduct(0, "X", new string('X', 51), 1m, 1); }, "Длинная категория принята.");
                    Reject(delegate { store.PutProduct(0, "X", "X", 1.001m, 1); }, "Три знака цены приняты.");
                    Reject(delegate { store.PutProduct(0, "X", "X", 1000001m, 1); }, "Слишком большая цена принята.");
                    Reject(delegate { store.PutProduct(0, "X", "X", 1m, -1); }, "Отрицательный запас принят.");
                    Reject(delegate { store.PutProduct(0, "X", "X", 1m, 1000001); }, "Слишком большой запас принят.");
                    Pass("Ограничения полей карточки");

                    string bad = Path.Combine(root, "broken.xml");
                    File.WriteAllText(bad, "<broken>");
                    Reject(delegate { new Store(bad); }, "Поврежденный XML заменен демонстрационным.");
                    Check(File.ReadAllText(bad) == "<broken>", "Поврежденный файл изменен.");
                    string badData = Path.Combine(root, "invalid.xml");
                    File.WriteAllText(badData, File.ReadAllText(file).Replace("<Stock>38</Stock>", "<Stock>-1</Stock>"));
                    Reject(delegate { new Store(badData); }, "Некорректные данные XML приняты.");
                    Pass("Поврежденный и некорректный XML не перезаписывается");

                    cart = new List<SaleItem>();
                    store.AddToCart(cart, 2, 2);
                    cart.Add(new SaleItem { ProductId = 3, Name = "Блокнот A5", Price = 180m, Quantity = 100 });
                    original = File.ReadAllText(file);
                    Reject(delegate { store.Checkout(cart); }, "Частичная продажа принята.");
                    Check(store.FindProduct(2).Stock == 60 && File.ReadAllText(file) == original, "Частичное списание.");
                    Pass("Ошибка второй строки отменяет всю продажу");

                    log.WriteLine("RESULT: " + passed + " passed; 0 failed.");
                    return 0;
                }
                catch (Exception error)
                {
                    log.WriteLine("FAIL " + error);
                    return 1;
                }
            }
        }

        private static void Capture(Form form, string path)
        {
            form.Refresh();
            Application.DoEvents();
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        public static int RunUi(string path)
        {
            string root;
            try { root = Prepare(path); }
            catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
            using (log = new StreamWriter(Path.Combine(root, "ui-results.txt"), false, Encoding.UTF8))
            {
                passed = 0;
                try
                {
                    Store store = new Store(Path.Combine(root, "store.xml"));
                    using (MainForm form = new MainForm(store))
                    {
                        form.TestMode = true;
                        form.ShowInTaskbar = false;
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = new Point(-20000, -20000);
                        form.Show();
                        Application.DoEvents();
                        foreach (TabPage page in form.tabs.TabPages)
                        {
                            foreach (Control control in page.Controls)
                                Check(control.Left >= 0 && control.Top >= 0 && control.Right <= page.ClientSize.Width &&
                                    control.Bottom <= page.ClientSize.Height, "Элемент за пределами вкладки: " + control.Text);
                        }
                        foreach (DataGridView grid in new DataGridView[] { form.catalogGrid, form.cartGrid, form.salesGrid })
                        {
                            int width = 0;
                            foreach (DataGridViewColumn column in grid.Columns) width += column.Width;
                            Check(width <= grid.ClientSize.Width, "Столбцы не помещаются в таблице.");
                        }
                        Check(form.catalogGrid.Rows.Count == 5, "Нет пяти строк в окне.");
                        Capture(form, Path.Combine(root, "01-catalog.png"));
                        form.searchBox.Text = "ПОСУДА";
                        Check(form.catalogGrid.Rows.Count == 1, "Поиск в окне не сработал.");
                        form.searchBox.Text = "";
                        form.catalogGrid.CurrentCell = form.catalogGrid.Rows[0].Cells[0];
                        form.quantityBox.Value = 2;
                        form.addButton.PerformClick();
                        Check(form.tabs.SelectedIndex == 1 && form.cart.Count == 1 &&
                            Convert.ToDecimal(form.cartGrid.Rows[0].Cells[4].Value) == 130m, "Добавление в окне не сработало.");
                        Capture(form, Path.Combine(root, "03-cart.png"));
                        Pass("Каталог, поиск, добавление двух тетрадей, переход к корзине");

                        form.removeButton.PerformClick();
                        Check(form.cart.Count == 0, "Удаление строки не сработало.");
                        form.tabs.SelectedIndex = 0;
                        form.addButton.PerformClick();
                        form.clearButton.PerformClick();
                        Check(form.cart.Count == 0 && store.FindProduct(1).Stock == 40, "Очистка изменила запас.");
                        Pass("Удаление и очистка корзины через кнопки");

                        using (ProductForm product = new ProductForm(store, store.FindProduct(1)))
                        {
                            product.ShowInTaskbar = false;
                            product.StartPosition = FormStartPosition.Manual;
                            product.Location = form.Location;
                            product.Show();
                            Capture(product, Path.Combine(root, "02-product.png"));
                            product.nameBox.Text = "Тетрадь 48 листов";
                            product.saveButton.PerformClick();
                            Check(product.DialogResult == DialogResult.OK, "Карточка не сохранена.");
                        }
                        Pass("Карточка товара и кнопка сохранения");

                        form.tabs.SelectedIndex = 0;
                        form.addButton.PerformClick();
                        Directory.CreateDirectory(store.PathName + ".tmp");
                        form.checkoutButton.PerformClick();
                        Check(form.LastError != null && form.cart.Count == 1 &&
                            store.FindProduct(1).Stock == 40 && store.Data.Sales.Count == 0, "Ошибка записи потеряла корзину.");
                        Directory.Delete(store.PathName + ".tmp");
                        form.checkoutButton.PerformClick();
                        Check(form.LastError == null && form.cart.Count == 0 && form.LastSale.Total == 130m &&
                            store.FindProduct(1).Stock == 38 && store.Data.Sales.Count == 1, "Продажа из окна не выполнена.");
                        Pass("Ошибка оформления сохраняет корзину; успешное оформление очищает ее");

                        using (ReceiptForm receipt = new ReceiptForm(form.LastSale))
                        {
                            receipt.ShowInTaskbar = false;
                            receipt.StartPosition = FormStartPosition.Manual;
                            receipt.Location = form.Location;
                            receipt.Show();
                            Capture(receipt, Path.Combine(root, "04-receipt.png"));
                            receipt.Close();
                        }
                        form.tabs.SelectedIndex = 2;
                        form.showButton.PerformClick();
                        Check(form.salesGrid.Rows.Count == 1, "Нет продажи в журнале.");
                        Capture(form, Path.Combine(root, "05-report.png"));
                        form.startDate.Value = DateTime.Today.AddDays(1);
                        form.showButton.PerformClick();
                        Check(form.LastError != null, "Неверный период принят окном.");
                        form.startDate.Value = DateTime.Today.AddDays(-30);
                        form.showButton.PerformClick();
                        Check(form.LastError == null && form.salesGrid.Rows.Count == 1, "Отчет не восстановился.");
                        Pass("Окно продажи, журнал, кнопка отчета и неправильный период");
                        form.Close();
                    }
                    Store reopened = new Store(store.PathName);
                    Check(reopened.Data.Sales.Count == 1 && reopened.FindProduct(1).Stock == 38, "Данные окна не сохранились.");
                    Pass("Чтение после закрытия окна");
                    log.WriteLine("RESULT: " + passed + " passed; 0 failed.");
                    return 0;
                }
                catch (Exception error)
                {
                    log.WriteLine("FAIL " + error);
                    return 1;
                }
            }
        }
    }
}



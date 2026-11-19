using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace TovarSales
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool Active { get; set; }
        public Product() { Active = true; }
    }

    public class SaleItem
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Amount { get { return Price * Quantity; } }
    }

    public class Sale
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public List<SaleItem> Items { get; set; }
        public decimal Total
        {
            get
            {
                decimal sum = 0;
                foreach (SaleItem item in Items) sum += item.Amount;
                return sum;
            }
        }
        public Sale() { Items = new List<SaleItem>(); }
    }

    public class StoreData
    {
        public List<Product> Products { get; set; }
        public List<Sale> Sales { get; set; }
        public StoreData()
        {
            Products = new List<Product>();
            Sales = new List<Sale>();
        }
    }

    public class Store
    {
        public StoreData Data { get; private set; }
        public string PathName { get; private set; }
        public static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

        public Store(string path)
        {
            PathName = Path.GetFullPath(path);
            if (File.Exists(PathName))
            {
                try
                {
                    XmlReaderSettings settings = new XmlReaderSettings();
                    settings.DtdProcessing = DtdProcessing.Prohibit;
                    settings.XmlResolver = null;
                    using (XmlReader reader = XmlReader.Create(PathName, settings))
                        Data = (StoreData)new XmlSerializer(typeof(StoreData)).Deserialize(reader);
                    ValidateData(Data);
                }
                catch (Exception error)
                {
                    throw new Exception("Не удалось открыть файл данных. Он оставлен без изменений. " +
                        "Восстановите резервную копию при закрытой программе.\n" + error.Message, error);
                }
            }
            else
            {
                StoreData initial = new StoreData();
                initial.Products.Add(new Product { Id = 1, Name = "Тетрадь 48 листов", Category = "Канцтовары", Price = 65m, Stock = 40 });
                initial.Products.Add(new Product { Id = 2, Name = "Ручка шариковая", Category = "Канцтовары", Price = 30m, Stock = 60 });
                initial.Products.Add(new Product { Id = 3, Name = "Блокнот A5", Category = "Канцтовары", Price = 180m, Stock = 15 });
                initial.Products.Add(new Product { Id = 4, Name = "Кружка керамическая", Category = "Посуда", Price = 350m, Stock = 12 });
                initial.Products.Add(new Product { Id = 5, Name = "Папка для документов", Category = "Канцтовары", Price = 120m, Stock = 20 });
                Save(initial);
            }
        }

        public static void ValidateProduct(string name, string category, decimal price, int stock)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
                throw new Exception("Название должно содержать от 1 до 100 символов.");
            if (String.IsNullOrWhiteSpace(category) || category.Trim().Length > 50)
                throw new Exception("Категория должна содержать от 1 до 50 символов.");
            if (price < 0.01m || price > 1000000m || Decimal.Round(price, 2) != price)
                throw new Exception("Цена должна быть от 0,01 до 1 000 000 рублей, не более двух знаков после запятой.");
            if (stock < 0 || stock > 1000000)
                throw new Exception("Остаток должен быть целым числом от 0 до 1 000 000.");
        }

        private static void ValidateData(StoreData data)
        {
            if (data == null || data.Products == null || data.Sales == null)
                throw new Exception("Неправильная структура XML.");
            List<int> productIds = new List<int>();
            foreach (Product product in data.Products)
            {
                if (product == null || product.Id <= 0 || productIds.Contains(product.Id))
                    throw new Exception("Неправильный или повторяющийся код товара.");
                productIds.Add(product.Id);
                ValidateProduct(product.Name, product.Category, product.Price, product.Stock);
            }
            List<int> saleIds = new List<int>();
            foreach (Sale sale in data.Sales)
            {
                if (sale == null || sale.Id <= 0 || saleIds.Contains(sale.Id) ||
                    sale.Date == DateTime.MinValue || sale.Items == null || sale.Items.Count == 0)
                    throw new Exception("Неправильная запись продажи.");
                saleIds.Add(sale.Id);
                foreach (SaleItem item in sale.Items)
                    if (item == null || !productIds.Contains(item.ProductId) || String.IsNullOrWhiteSpace(item.Name) ||
                        item.Name.Length > 100 || item.Price < 0.01m || item.Price > 1000000m ||
                        Decimal.Round(item.Price, 2) != item.Price || item.Quantity <= 0 || item.Quantity > 1000000)
                        throw new Exception("Неправильная строка продажи.");
            }
        }

        public StoreData Copy()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(StoreData));
            using (MemoryStream memory = new MemoryStream())
            {
                serializer.Serialize(memory, Data);
                memory.Position = 0;
                return (StoreData)serializer.Deserialize(memory);
            }
        }

        public void Save(StoreData next)
        {
            ValidateData(next);
            Directory.CreateDirectory(Path.GetDirectoryName(PathName));
            string temp = PathName + ".tmp";
            using (FileStream file = new FileStream(temp, FileMode.Create, FileAccess.Write))
            {
                new XmlSerializer(typeof(StoreData)).Serialize(file, next);
                file.Flush(true);
            }
            if (File.Exists(PathName)) File.Replace(temp, PathName, PathName + ".bak");
            else File.Move(temp, PathName);
            Data = next;
        }

        public Product FindProduct(int id)
        {
            foreach (Product product in Data.Products)
                if (product.Id == id) return product;
            throw new Exception("Товар не найден.");
        }

        public Product PutProduct(int id, string name, string category, decimal price, int stock)
        {
            ValidateProduct(name, category, price, stock);
            StoreData next = Copy();
            Product product = null;
            int max = 0;
            foreach (Product current in next.Products)
            {
                if (current.Id > max) max = current.Id;
                if (current.Id == id) product = current;
            }
            if (id == 0)
            {
                product = new Product { Id = checked(max + 1) };
                next.Products.Add(product);
            }
            else if (product == null || !product.Active) throw new Exception("Активный товар не найден.");
            product.Name = name.Trim();
            product.Category = category.Trim();
            product.Price = price;
            product.Stock = stock;
            Save(next);
            return product;
        }

        public void Archive(int id)
        {
            StoreData next = Copy();
            Product product = null;
            foreach (Product current in next.Products)
                if (current.Id == id) product = current;
            if (product == null || !product.Active) throw new Exception("Активный товар не найден.");
            product.Active = false;
            Save(next);
        }

        public List<Product> Search(string query)
        {
            List<Product> result = new List<Product>();
            query = (query ?? "").Trim();
            foreach (Product product in Data.Products)
                if (product.Active && (product.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    product.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                    result.Add(product);
            return result;
        }

        public void AddToCart(List<SaleItem> cart, int productId, int quantity)
        {
            Product product = FindProduct(productId);
            if (!product.Active) throw new Exception("Товар находится в архиве.");
            if (quantity <= 0 || quantity > 1000000) throw new Exception("Укажите положительное целое количество до 1 000 000.");
            SaleItem existing = null;
            int total = quantity;
            foreach (SaleItem item in cart)
            {
                if (item.ProductId != productId) continue;
                if (item.Price != product.Price || item.Name != product.Name)
                    throw new Exception("Карточка изменилась. Удалите строку из корзины и добавьте товар заново.");
                total = checked(total + item.Quantity);
                existing = item;
            }
            if (total > product.Stock) throw new Exception("Недостаточный остаток: " + product.Name);
            if (existing == null)
                cart.Add(new SaleItem { ProductId = product.Id, Name = product.Name, Price = product.Price, Quantity = quantity });
            else existing.Quantity += quantity;
        }

        public Sale Checkout(List<SaleItem> cart)
        {
            if (cart.Count == 0) throw new Exception("Корзина пуста.");
            StoreData next = Copy();
            Sale sale = new Sale();
            int max = 0;
            foreach (Sale old in next.Sales) if (old.Id > max) max = old.Id;
            sale.Id = checked(max + 1);
            sale.Date = DateTime.Now;
            foreach (SaleItem line in cart)
            {
                Product product = null;
                foreach (Product current in next.Products)
                    if (current.Id == line.ProductId) product = current;
                if (product == null || !product.Active || line.Quantity <= 0 || product.Stock < line.Quantity)
                    throw new Exception("Недостаточный остаток: " + line.Name);
                if (product.Price != line.Price)
                    throw new Exception("Цена изменилась. Пересоздайте корзину.");
                if (product.Name != line.Name)
                    throw new Exception("Название изменилось. Пересоздайте корзину.");
                product.Stock -= line.Quantity;
                sale.Items.Add(new SaleItem { ProductId = product.Id, Name = product.Name, Price = product.Price, Quantity = line.Quantity });
            }
            next.Sales.Add(sale);
            Save(next);
            return sale;
        }

        public List<Sale> Report(DateTime start, DateTime end)
        {
            if (start.Date > end.Date) throw new Exception("Начало периода позже окончания.");
            List<Sale> result = new List<Sale>();
            foreach (Sale sale in Data.Sales)
                if (sale.Date.Date >= start.Date && sale.Date.Date <= end.Date) result.Add(sale);
            return result;
        }

        public static decimal Revenue(List<Sale> sales)
        {
            decimal total = 0;
            foreach (Sale sale in sales) total += sale.Total;
            return total;
        }

        public void ExportCsv(string path, DateTime start, DateTime end)
        {
            List<Sale> sales = Report(start, end);
            string destination = Path.GetFullPath(path);
            foreach (string reserved in new string[] { PathName, PathName + ".bak", PathName + ".tmp" })
                if (String.Equals(destination, reserved, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Выберите отдельный файл для отчета.");
            using (StreamWriter writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("Номер;Дата;Сумма");
                foreach (Sale sale in sales)
                    writer.WriteLine(sale.Id + ";" + sale.Date.ToString("dd.MM.yyyy HH:mm", Russian) + ";" + sale.Total.ToString("F2", Russian));
            }
        }

        public static string Receipt(Sale sale)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("ТОВАР • Учебная продажа");
            text.AppendLine("Не является фискальным чеком");
            text.AppendLine("Продажа № " + sale.Id + "   " + sale.Date.ToString("dd.MM.yyyy HH:mm", Russian));
            text.AppendLine(new string('-', 46));
            foreach (SaleItem item in sale.Items)
            {
                text.AppendLine(item.Name);
                text.AppendLine(item.Quantity + " × " + item.Price.ToString("F2", Russian) + " = " + item.Amount.ToString("F2", Russian) + " руб.");
            }
            text.AppendLine(new string('-', 46));
            text.AppendLine("ИТОГО: " + sale.Total.ToString("F2", Russian) + " руб.");
            return text.ToString();
        }
    }


    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = Store.Russian;
            Thread.CurrentThread.CurrentUICulture = Store.Russian;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--test")
                return Tests.Run(args.Length > 1 ? args[1] : null);
            if (args.Length > 0 && args[0] == "--ui-test")
                return Tests.RunUi(args.Length > 1 ? args[1] : null);
            if (args.Length != 0)
            {
                MessageBox.Show("Для обычного запуска откройте TovarSales.exe без параметров.");
                return 1;
            }
            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\TovarSales_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("Приложение «Товар» уже запущено.");
                    return 1;
                }
                try
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TovarSales", "store.xml");
                    Application.Run(new MainForm(new Store(path)));
                    return 0;
                }
                catch (Exception error)
                {
                    MessageBox.Show(error.Message, "Не удалось запустить «Товар»", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}

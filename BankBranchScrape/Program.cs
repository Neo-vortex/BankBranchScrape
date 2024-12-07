using HtmlAgilityPack;
using System.Data.SQLite;
using System.Web;

public static class Program
{
    public static async Task Main(string[] args)
    {

        var bankNames = new List<string>()
        {
            /*"بانک تجارت",*/
            "بانک شهر",

        }; 
        var provinces = new List<string>
        {
            "آذربایجان شرقی",
            "آذربایجان غربی",
            "اردبیل",
            "اصفهان",
            "البرز",
            "ایلام",
            "بوشهر",
            "تهران",
            "چهارمحال و بختیاری",
            "خراسان جنوبی",
            "خراسان رضوی",
            "خراسان شمالی",
            "خوزستان",
            "زنجان",
            "سمنان",
            "سیستان و بلوچستان",
            "فارس",
            "قزوین",
            "قم",
            "کردستان",
            "کرمان",
            "کرمانشاه",
            "کهگیلویه و بویراحمد",
            "گلستان",
            "گیلان",
            "لرستان",
            "مازندران",
            "مرکزی",
            "هرمزگان",
            "همدان",
            "یزد"
            
        };

        const string baseUrl = "https://cardinfo.ir/بانک/"; 

        var allBranches = new List<BranchInfo>();

        foreach (var bankName in bankNames)
        {
            foreach (var province in provinces)
            {
                var encodedBankName = Uri.EscapeDataString(bankName);
                var url = $"{baseUrl}{encodedBankName}/شعبه/{Uri.EscapeDataString(province)}";
                Console.WriteLine($"Fetching data for province: {province} , bank : {bankName}");
                var branches = await ScrapeProvinceData(url, bankName);

                allBranches.AddRange(branches);
            }
        }

        SaveBranchesToDatabase(allBranches);
    }

    static async Task<List<BranchInfo>> ScrapeProvinceData(string url, string bankName)
    {
        List<BranchInfo> branches = [];

        using var client = new HttpClient();
        using var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to fetch data from: {url}");
            return branches;
        }

        var htmlContent = await response.Content.ReadAsStringAsync();

        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlContent);

        var rows = htmlDoc.DocumentNode.SelectNodes("//tbody/tr");
        if (rows == null)
        {
            Console.WriteLine($"No data found for URL: {url}");
            return branches;
        }

        for (var i = 0; i < rows.Count; i += 2)
        {
            var branchRow = rows[i];
            var addressRow = rows[i + 1];

            var branchName = branchRow.SelectSingleNode("./td[1]/a")?.InnerText.Trim() ?? "N/A";
            var branchCode = branchRow.SelectSingleNode("./td[2]")?.InnerText.Trim() ?? "N/A";
            var city = branchRow.SelectSingleNode("./td[3]")?.InnerText.Trim() ?? "N/A";
            var phone = branchRow.SelectSingleNode("./td[4]")?.InnerText.Trim() ?? "N/A";

            var rawAddress = addressRow.SelectSingleNode("./td")?.InnerHtml.Trim() ?? "N/A";

            var addressWithoutLinks = HtmlEntity.DeEntitize(
                HtmlAgilityPack.HtmlNode.CreateNode($"<td>{rawAddress}</td>")
                    .InnerText
            );

            var address = addressWithoutLinks.Replace("&nbsp;", "").Trim();

            branches.Add(new BranchInfo
            {
                BankName = bankName,
                Province = HttpUtility.UrlDecode(url.Split('/').Last()),
                BranchName = branchName,
                BranchCode = branchCode,
                City = city,
                Phone = phone,
                Address = address
            });
        }

        return branches;
    }

    private static void SaveBranchesToDatabase(List<BranchInfo> branches)
    {
        const string insertQuery = """
                                   
                                                       INSERT INTO Branches (BankName, Province, BranchName, BranchCode, City, Phone, Address)
                                                       VALUES (@BankName, @Province, @BranchName, @BranchCode, @City, @Phone, @Address)
                                   """;
        using (var connection = new SQLiteConnection("Data Source=branches.db"))
        {
            connection.Open();

            const string createTableQuery = """
                                            
                                                            CREATE TABLE IF NOT EXISTS Branches (
                                                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                                BankName TEXT,
                                                                Province TEXT,
                                                                BranchName TEXT,
                                                                BranchCode TEXT,
                                                                City TEXT,
                                                                Phone TEXT,
                                                                Address TEXT
                                                            )
                                            """;
            using (var cmd = new SQLiteCommand(createTableQuery, connection))
            {
                cmd.ExecuteNonQuery();
            }

            foreach (var branch in branches)
            {
                using var cmd = new SQLiteCommand(insertQuery, connection);
                cmd.Parameters.AddWithValue("@BankName", branch.BankName);
                cmd.Parameters.AddWithValue("@Province", branch.Province);
                cmd.Parameters.AddWithValue("@BranchName", branch.BranchName);
                cmd.Parameters.AddWithValue("@BranchCode", branch.BranchCode);
                cmd.Parameters.AddWithValue("@City", branch.City);
                cmd.Parameters.AddWithValue("@Phone", branch.Phone);
                cmd.Parameters.AddWithValue("@Address", branch.Address);

                cmd.ExecuteNonQuery();
            }

            connection.Close();
        }

        Console.WriteLine("Data saved to database successfully!");
    }
}

internal class BranchInfo
{
    public string BankName { get; set; }
    public string Province { get; set; }
    public string BranchName { get; set; }
    public string BranchCode { get; set; }
    public string City { get; set; }
    public string Phone { get; set; }
    public string Address { get; set; }
}

using System.Globalization;
using PdfSharp.Fonts;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes.Charts;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Npgsql;
using Newtonsoft.Json.Linq;
using PdfSharp.Pdf;
using PdfSharp.Quality;

GlobalFontSettings.UseWindowsFontsUnderWindows = true;

HttpClient client = new HttpClient();
string api_key = System.Environment.GetEnvironmentVariable("COINGECKO_API_KEY");

var connection_string = "Host=localhost;Port=5447;Username=postgres;Password=postgres;Database=prac_db";
var db = NpgsqlDataSource.Create(connection_string);

void pdf_current_data(List<Coin> coins)
{
    string ids = "";
    foreach (var coin in coins)
    {
        ids += coin.id;
        ids += ",";
    }
    
    string request = $"https://api.coingecko.com/api/v3/simple/price?vs_currencies=usd&ids={ids}" +
                     $"&include_market_cap=true&include_24hr_vol=true&include_24hr_change=true&precision=4";
    
    var httpRequestMessage = new HttpRequestMessage
    {
        Method = HttpMethod.Get,
        RequestUri = new Uri(request),
        Headers = { 
            { "x-cg-demo-api-key", api_key }
        }
    };

    HttpResponseMessage response = client.SendAsync(httpRequestMessage).Result;
    if (response.IsSuccessStatusCode)
    {
        string res = response.Content.ReadAsStringAsync().Result;
        JObject obj = JObject.Parse(res);
        var document = new Document();
        foreach (var coin in coins)
        {
            var data = obj[coin.id];
            var paragraph = new Paragraph();
            paragraph.Format.Font.Color = Colors.Black;
            document.LastSection.AddParagraph($"Coin: {coin.name}");
            paragraph.AddText($"Price: ${data["usd"]:0.00}\n");
            paragraph.AddText($"Market Cap: ${(Decimal.Parse(data["usd_market_cap"].ToString()) / 1_000_000_000):0.00} billion\n");
            paragraph.AddText($"24h Traded Volume: ${(Decimal.Parse(data["usd_24h_vol"].ToString()) / 1_000_000_000):0.00} billion\n");
            paragraph.AddText($"Return: {data["usd_24h_change"]:0.00}%\n");
            paragraph.AddText($"\n");
            document.LastSection.Add(paragraph);
        }
        
        MigraDoc.DocumentObjectModel.IO.DdlWriter.WriteToFile(document, "MigraDoc.mdddl");
        
        var pdfRenderer = new PdfDocumentRenderer
        {
            Document = document,
            PdfDocument =
            {
                PageLayout = PdfPageLayout.SinglePage,
                ViewerPreferences =
                {
                    FitWindow = true
                }
            }
        };
        pdfRenderer.PdfDocument.Options.CompressContentStreams = true;
        pdfRenderer.RenderDocument();
        var filename = PdfFileUtility.GetTempPdfFullFileName("samples-MigraDoc/HelloMigraDoc.pdf");
        pdfRenderer.PdfDocument.Save(filename);
        PdfFileUtility.ShowDocument(filename);
    }
    else
    {
        Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
        throw new Exception("API call failed");
    }
}

void print_current_data(List<Coin> coins)
{
    string ids = "";
    foreach (var coin in coins)
    {
        ids += coin.id;
        ids += ",";
    }
    string request = $"https://api.coingecko.com/api/v3/simple/price?vs_currencies=usd&ids={ids}&include_market_cap=true&include_24hr_vol=true&include_24hr_change=true&precision=4";
    
    var httpRequestMessage = new HttpRequestMessage
    {
        Method = HttpMethod.Get,
        RequestUri = new Uri(request),
        Headers = { 
            { "x-cg-demo-api-key", api_key }
        }
    };

    HttpResponseMessage response = client.SendAsync(httpRequestMessage).Result;
    if (response.IsSuccessStatusCode)
    {
        string res = response.Content.ReadAsStringAsync().Result;
        JObject obj = JObject.Parse(res);
        foreach (var coin in coins)
        {
            var data = obj[coin.id];
            Console.WriteLine($"Coin: {coin.name}");
            Console.WriteLine($"Price: ${data["usd"]:0.00}");
            Console.WriteLine($"Market Cap: ${data["usd_market_cap"]:0.00}");
            Console.WriteLine($"24h Traded Volume: ${data["usd_24h_vol"]:0.00}");
            Console.WriteLine($"Return: {data["usd_24h_change"]:0.00}%");
            Console.WriteLine();
        }
    }
    else
    {
        Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
        throw new Exception("API call failed");
    }
}

void add_historical_coin_data_coingecko(Coin c)
{
    string coin = c.id;
    int coin_id = c.coin_id;
    DateOnly to = DateOnly.FromDateTime(DateTime.UtcNow);
    to = to.AddDays(-1);
    if (DateTime.UtcNow.TimeOfDay < TimeSpan.FromMinutes(40))
    {
        to = to.AddDays(-1);
    }
        
    Console.WriteLine($"Coin: {coin_id}");
    var from = to.AddDays(-364);
    
    using (var cmd = db.CreateCommand($"select dateid from dailydata where coinid = {coin_id} order by dateid desc limit 1"))
    {
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var d = reader.GetInt32(0).ToString();
            from = DateOnly.ParseExact(d, "yyyyMMdd").AddDays(1);
        }
    }

    if (from <= to)
    {
        string formatted_from = from.ToString("yyyy-MM-dd");
        string formatted_to = to.ToString("yyyy-MM-dd");
        string request = $"https://api.coingecko.com/api/v3/coins/{coin}/market_chart/range" +
                         $"?vs_currency=usd&from={formatted_from}&to={formatted_to}&interval=daily&precision=2";
        Console.WriteLine($"From: {formatted_from}; To: {formatted_to}");
        Console.WriteLine($"Request: {request}");
        var httpRequestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(request),
            Headers = { 
                { "x-cg-demo-api-key", api_key }
            }
        };
        HttpResponseMessage response = client.SendAsync(httpRequestMessage).Result;
        if (response.IsSuccessStatusCode)
        {
            string res = response.Content.ReadAsStringAsync().Result;
            JObject obj = JObject.Parse(res);
            var prices_midnight = obj["prices"];
            var market_caps = obj["market_caps"];
            var day_volumes = obj["total_volumes"];

            var date = from;
            while (date.DayNumber <= to.DayNumber)
            {
                int responce_array_index = date.DayNumber - from.DayNumber;
                int date_id = Int32.Parse(date.ToString("yyyyMMdd"));

                using (var cmd = db.CreateCommand("INSERT INTO flatdate " +
                                                        "(DATEID, DAY, MONTH, YEAR, WEEKDAY, MONTHNAME, WEEKDAYNAME) " +
                                                        "VALUES ($1, $2, $3, $4, $5, $6, $7) " +
                                                        "ON CONFLICT (dateid) DO NOTHING"))
                {
                    cmd.Parameters.AddWithValue(date_id);
                    cmd.Parameters.AddWithValue(date.Day);
                    cmd.Parameters.AddWithValue(date.Month);
                    cmd.Parameters.AddWithValue(date.Year);
                    cmd.Parameters.AddWithValue(((int)date.DayOfWeek + 6) % 7 + 1);
                    cmd.Parameters.AddWithValue(date.ToString("MMMM", new CultureInfo("en-US")));
                    cmd.Parameters.AddWithValue(date.DayOfWeek.ToString());
                    cmd.ExecuteNonQuery();
                }
            
                using (var cmd = db.CreateCommand("INSERT INTO dailydata " +
                                                        "(sourceid, coinid, dateid, marketcapusd, priceusd, tradedvolumeusd) " +
                                                        "VALUES ($1, $2, $3, $4, $5, $6)"))
                {
                    Decimal market_cap = Decimal.Parse(market_caps[responce_array_index][1].ToString());
                    Decimal price_midnight = Decimal.Parse(prices_midnight[responce_array_index][1].ToString());
                    Decimal day_volume = Decimal.Parse(day_volumes[responce_array_index][1].ToString());
                    cmd.Parameters.AddWithValue(1);
                    cmd.Parameters.AddWithValue(coin_id);
                    cmd.Parameters.AddWithValue(date_id);
                    cmd.Parameters.AddWithValue(market_cap);
                    cmd.Parameters.AddWithValue(price_midnight);
                    cmd.Parameters.AddWithValue(day_volume);
                    cmd.ExecuteNonQuery();
                }
                
                date = date.AddDays(1);
            }
        }
        else
        {
            Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
            throw new Exception("API call failed");
        }
    }
    
    var cur = to.AddDays(-363);
    using (var cmd = db.CreateCommand($"select dateid from dailydiff where coinid = {coin_id} order by dateid desc limit 1"))
    {
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var d = reader.GetInt32(0).ToString();
            cur = DateOnly.ParseExact(d, "yyyyMMdd").AddDays(1);
        }
    }

    Decimal prev_price, prev_market_cap, prev_traded_volume;
    using (var cmd = db.CreateCommand($"select priceusd, marketcapusd, tradedvolumeusd from dailydata " +
                                      $"where coinid = {coin_id} and dateid = {Int32.Parse(cur.AddDays(-1).ToString("yyyyMMdd"))}"))
    {
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            prev_price = reader.GetDecimal(0);
            prev_market_cap = reader.GetDecimal(1);
            prev_traded_volume = reader.GetDecimal(2);
        }
        else
        {
            throw new Exception($"data of {coin} at {cur.AddDays(-1)} not found");
        }
    }
    
    while (cur.DayNumber <= to.DayNumber)
    {
        int date_id = Int32.Parse(cur.ToString("yyyyMMdd"));
        using (var cmd = db.CreateCommand($"select priceusd, marketcapusd, tradedvolumeusd from dailydata " +
                                          $"where coinid = {coin_id} and dateid = {Int32.Parse(cur.ToString("yyyyMMdd"))}"))
        {
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                Decimal price = reader.GetDecimal(0);
                Decimal market_cap = reader.GetDecimal(1);
                Decimal traded_volume = reader.GetDecimal(2);
                
                Decimal price_diff = price - prev_price;
                Decimal market_cap_diff = market_cap - prev_market_cap;
                Decimal traded_volume_diff = traded_volume - prev_traded_volume;
                Decimal return_percent = price_diff / prev_price * 100;
                Decimal log_return = (Decimal)Math.Log((double)(price / prev_price));
                
                using (var nested_cmd = db.CreateCommand("INSERT INTO dailydiff " +
                                                  "(sourceid, coinid, dateid, marketcapdiff, return, tradedvolumediff, logreturn, returnpercent) " +
                                                  "VALUES ($1, $2, $3, $4, $5, $6, $7, $8)"))
                {
                    nested_cmd.Parameters.AddWithValue(1);
                    nested_cmd.Parameters.AddWithValue(coin_id);
                    nested_cmd.Parameters.AddWithValue(date_id);
                    nested_cmd.Parameters.AddWithValue(market_cap_diff);
                    nested_cmd.Parameters.AddWithValue(price_diff);
                    nested_cmd.Parameters.AddWithValue(traded_volume_diff);
                    nested_cmd.Parameters.AddWithValue(log_return);
                    nested_cmd.Parameters.AddWithValue(return_percent);
                    nested_cmd.ExecuteNonQuery();
                }
                
                prev_price = price;
                prev_market_cap = market_cap;
                prev_traded_volume = traded_volume;
            }
            else
            {
                throw new Exception($"data of {coin} at {cur} not found");
            }
        }
        
        cur = cur.AddDays(1);
    }
}

double calculate_volatility(Coin c)
{
    int coin_id = c.coin_id;
    DateOnly to = DateOnly.FromDateTime(DateTime.UtcNow);
    to = to.AddDays(-1);
    if (DateTime.UtcNow.TimeOfDay < TimeSpan.FromMinutes(40))
    {
        to = to.AddDays(-1);
    }
    using (var cmd = db.CreateCommand($"select dailystdev from volatilityinfo " +
                                      $"where coinid = {c.coin_id} and enddateid = {to.ToString("yyyyMMdd")}"))
    {
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            Double sd = reader.GetDouble(0);
            return sd;
        }
    }
    
    add_historical_coin_data_coingecko(c);
    int count = 0;
    List<Decimal> log_rets = new List<Decimal>();
    Decimal mean = 0;
    using (var cmd = db.CreateCommand($"select logreturn from dailydiff where coinid = {c.coin_id}"))
    {
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Decimal log_return = reader.GetDecimal(0);
            mean += log_return;
            log_rets.Add(log_return);
            count++;
        }
    }

    DateOnly from = to;
    using (var cmd = db.CreateCommand($"select dateid from dailydiff where coinid = {c.coin_id} order by dateid limit 1"))
    {
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            from = DateOnly.ParseExact(reader.GetInt32(0).ToString(), "yyyyMMdd");
        }
    }

    Decimal variance = 0;
    if (count == 0)
    {
        throw new Exception($"no return info on coin {c.name}");
    }

    mean /= count;
    for (int i = 0; i < count; i++)
    {
        variance += (mean - log_rets[i]) * (mean - log_rets[i]);
    }
    
    double stdev = Math.Sqrt((double)(variance / count));
    using (var nested_cmd = db.CreateCommand("INSERT INTO volatilityinfo " +
                                             "(sourceid, coinid, begindateid, enddateid, dailystdev) " +
                                             "VALUES ($1, $2, $3, $4, $5)"))
    {
        nested_cmd.Parameters.AddWithValue(1);
        nested_cmd.Parameters.AddWithValue(coin_id);
        nested_cmd.Parameters.AddWithValue(Int32.Parse(from.ToString("yyyyMMdd")));
        nested_cmd.Parameters.AddWithValue(Int32.Parse(to.ToString("yyyyMMdd")));
        nested_cmd.Parameters.AddWithValue(stdev);
        nested_cmd.ExecuteNonQuery();
    }
    return stdev;
}

void basic_analysis(Coin c)
{
    int coin_id = c.coin_id;
    DateOnly from, to;
    using (var cmd = db.CreateCommand($"select dateid from dailydata where coinid = {coin_id} order by dateid desc limit 1"))
    {
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var d = reader.GetInt32(0).ToString();
            to = DateOnly.ParseExact(d, "yyyyMMdd");
        }
        else
        {
            throw new Exception($"data of {coin_id} not found");
        }
    }
    using (var cmd = db.CreateCommand($"select dateid from dailydata where coinid = {coin_id} order by dateid limit 1"))
    {
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var d = reader.GetInt32(0).ToString();
            from = DateOnly.ParseExact(d, "yyyyMMdd");
        }
        else
        {
            throw new Exception($"data of {coin_id} not found");
        }
    }
    List<Decimal> prices = new List<Decimal>();
    using (var cmd = db.CreateCommand($"select priceusd from dailydata where coinid = {coin_id} order by dateid"))
    {
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Decimal d = reader.GetDecimal(0);
            prices.Add(d);
        }
    }
    Console.WriteLine($"Coin: {c.name}");
    Console.WriteLine($"Period: {from} - {to}");
    Decimal min = prices.Min();
    Decimal max = prices.Max();
    Decimal mean = prices.Average();
    Decimal median;
    Decimal variance = 0;
    int count = prices.Count;
    if (count % 2 == 0)
    {
        median = (prices[count / 2] + prices[count / 2 - 1]) / 2;
    }
    else
    {
        median = prices[count / 2];
    }

    for (int i = 0; i < count; i++)
    {
        variance += (prices[i] - median) * (prices[i] - median);
    }
    variance /= count;
    double stdev = Math.Sqrt((double)(variance));
    Console.WriteLine($"Min Price: {min}");
    Console.WriteLine($"Max Price: {max}");
    Console.WriteLine($"Mean Price: {mean}");
    Console.WriteLine($"St. dev. of Price: {stdev}");
    Console.WriteLine($"Median Price: {median}");
}

double calculate_correlation(Coin c1, Coin c2)
{
    DateOnly to = DateOnly.FromDateTime(DateTime.UtcNow);
    to = to.AddDays(-1);
    if (DateTime.UtcNow.TimeOfDay < TimeSpan.FromMinutes(40))
    {
        to = to.AddDays(-1);
    }
    using (var cmd = db.CreateCommand($"select pearsoncorrelationvalue from correlationinfo " +
                                      $"where secondcoinid = {c2.coin_id} and firstcoinid = {c1.coin_id} " +
                                      $"and enddateid = {to.ToString("yyyyMMdd")}"))
    {
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            Double pcv = reader.GetDouble(0);
            return pcv;
        }
    }
    
    List<Tuple<DateOnly, Decimal>> prices1 = new List<Tuple<DateOnly, Decimal>>();
    List<Tuple<DateOnly, Decimal>> prices2 = new List<Tuple<DateOnly, Decimal>>();
    using (var cmd = db.CreateCommand($"select priceusd, dateid from dailydata where coinid = {c1.coin_id} order by dateid"))
    {
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Decimal d = reader.GetDecimal(0);
            DateOnly date = DateOnly.ParseExact(reader.GetInt32(1).ToString(), "yyyyMMdd");
            prices1.Add(new Tuple<DateOnly, Decimal>(date, d));
        }
    }
    using (var cmd = db.CreateCommand($"select priceusd, dateid from dailydata where coinid = {c2.coin_id} order by dateid"))
    {
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Decimal d = reader.GetDecimal(0);
            DateOnly date = DateOnly.ParseExact(reader.GetInt32(1).ToString(), "yyyyMMdd");
            prices2.Add(new Tuple<DateOnly, Decimal>(date, d));
        }
    }
    int i = 0;
    int j = 0;
    Decimal expected_product = 0;
    Decimal mean1 = 0;
    Decimal mean2 = 0;
    Decimal variance1 = 0;
    Decimal variance2 = 0;
    int count = 0;
    while (i < prices1.Count && j < prices2.Count)
    {
        if (prices1[i].Item1 == prices2[j].Item1)
        {
            mean1 += prices1[i].Item2;
            mean2 += prices2[j].Item2;
            count++;
            i++;
            j++;
        }
        else if (prices1[i].Item1 < prices2[j].Item1)
        {
            i++;
            Console.WriteLine($"data for {c2.name} at {prices1[i].Item1} not found, skipped");
        }
        else if (prices1[i].Item1 > prices2[j].Item1)
        {
            j++;
            Console.WriteLine($"data for {c1.name} at {prices2[j].Item1} not found, skipped");
        }
    }
    if (count == 0)
    {
        throw new Exception("data not found");
    }
    mean1 /= count;
    mean2 /= count;
    i = 0;
    j = 0;
    while (i < prices1.Count && j < prices2.Count)
    {
        if (prices1[i].Item1 == prices2[j].Item1)
        {
            expected_product += (prices1[i].Item2 - mean1) * (prices2[j].Item2 - mean2);
            variance1 += (prices1[i].Item2 - mean1) * (prices1[i].Item2 - mean1);
            variance2 += (prices2[j].Item2 - mean2) * (prices2[j].Item2 - mean2);
            i++;
            j++;
        }
        else if (prices1[i].Item1 < prices2[j].Item1)
        {
            i++;
        }
        else if (prices1[i].Item1 > prices2[j].Item1)
        {
            j++;
        }
    }
    expected_product /= count;
    variance1 /= count;
    variance2 /= count;
    double stdev1 = Math.Sqrt((double)(variance1));
    double stdev2 = Math.Sqrt((double)(variance2));
    double coeff = 0;
    if (stdev1 < 0.001)
    {
        Console.WriteLine("st. dev. of the first coin is very close to 0");
        Console.WriteLine("that means that the coin is (probably) bound to some fixed value");
        Console.WriteLine("(for example USDT (Tether) and USDC are bound to the US Dollar)");
        Console.WriteLine($"Pearson's correlation coefficient: 0.0000");
    }
    else if (stdev2 < 0.001)
    {
        Console.WriteLine("st. dev. of the second coin is very close to 0");
        Console.WriteLine("that means that the coin is bound to some fixed value");
        Console.WriteLine("(for example USDT (Tether) and USDC are bound to the US Dollar)");
        Console.WriteLine($"Pearson's correlation coefficient: 0.0000");
    }
    else
    {
        coeff = (double)expected_product / (stdev1 * stdev2);
        /*Console.WriteLine($"Count: {count}");
        Console.WriteLine($"Mean1: {mean1:0.00} USD");
        Console.WriteLine($"Mean2: {mean2:0.00} USD");
        Console.WriteLine($"stdev1: {stdev1:0.00} USD");
        Console.WriteLine($"stdev2: {stdev2:0.00} USD");*/
    }
    DateOnly from = DateOnly.FromDayNumber(Int32.Min(prices1[0].Item1.DayNumber, prices2[0].Item1.DayNumber));
    using (var nested_cmd = db.CreateCommand("INSERT INTO correlationinfo " +
                                             "(sourceid, firstcoinid, secondcoinid, begindateid, enddateid, pearsoncorrelationvalue) " +
                                             "VALUES ($1, $2, $3, $4, $5, $6)"))
    {
        nested_cmd.Parameters.AddWithValue(1);
        nested_cmd.Parameters.AddWithValue(c1.coin_id);
        nested_cmd.Parameters.AddWithValue(c2.coin_id);
        nested_cmd.Parameters.AddWithValue(Int32.Parse(from.ToString("yyyyMMdd")));
        nested_cmd.Parameters.AddWithValue(Int32.Parse(to.ToString("yyyyMMdd")));
        nested_cmd.Parameters.AddWithValue(coeff);
        nested_cmd.ExecuteNonQuery();
    }

    return coeff;
} 

List<Coin> coins = new List<Coin>();
using (var cmd = db.CreateCommand("SELECT * FROM coin"))
{
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        int coin_id = reader.GetInt32(0);
        string id = reader.GetString(1);
        string symbol = reader.GetString(2);
        string name = reader.GetString(3);
        coins.Add(new Coin(coin_id, id, symbol, name));
    }
}

Coin? find_coin(string s)
{
    for (int i = 0; i < coins.Count; i++)
    {
        if (coins[i].id == s || 
            coins[i].symbol == s ||
            coins[i].name == s ||
            coins[i].coin_id.ToString() == s) 
            return coins[i];
    }
    return null;
}

if (args.Length > 0 && args[0] == "analyze")
{
    if (args.Length > 1 && args[1] == "basic")
    {
        if (args.Length == 3)
        {
            Coin? c = find_coin(args[2]);
            if (c != null)
            {
                basic_analysis(c.Value);
            }
            else
            {
                Console.WriteLine("coin not found");
            }
        }
        else
        {
            Console.WriteLine("invalid command");
        }
    }
    else if (args.Length > 1 && args[1] == "volatility")
    {
        if (args.Length == 3)
        {
            Coin? c = find_coin(args[2]);
            if (c != null)
            {
                var stdev = calculate_volatility(c.Value);
                Console.WriteLine($"Daily: {stdev}; Weekly: {stdev * Math.Sqrt(7)}; Monthly: {stdev * Math.Sqrt(30)}");
            }
            else
            {
                Console.WriteLine("coin not found");
            }
        }
        else
        {
            Console.WriteLine("invalid command");
        }
    }
    else if (args.Length > 1 && args[1] == "rating")
    {
        if (args.Length == 3 && args[2] == "market_cap")
        {
            List<Tuple<DateOnly, string, Decimal>> items = new List<Tuple<DateOnly, string, Decimal>>();
            foreach (var c in coins)
            {
                using (var cmd = db.CreateCommand($"select marketcapusd, dateid from dailydata where coinid = {c.coin_id} order by dateid desc limit 1"))
                {
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        Decimal d = reader.GetDecimal(0);
                        DateOnly date = DateOnly.ParseExact(reader.GetInt32(1).ToString(), "yyyyMMdd");
                        items.Add(new Tuple<DateOnly, string, Decimal>(date, c.name, d));
                    }
                }
            }
            items.Sort((pair1, pair2) => pair2.Item3.CompareTo(pair1.Item3));
            Console.WriteLine($"Market Cap Ranking:");
            foreach (var item in items)
            {
                Console.WriteLine($"Name: {item.Item2, -3} \t Market Cap: ${item.Item3, -15:0.00} \t ({item.Item1, 10})");
            }
        }
        else if (args.Length == 3 && args[2] == "daily_volume")
        {
            List<Tuple<DateOnly, string, Decimal>> items = new List<Tuple<DateOnly, string, Decimal>>();
            foreach (var c in coins)
            {
                using (var cmd = db.CreateCommand($"select tradedvolumeusd, dateid from dailydata where coinid = {c.coin_id} order by dateid desc limit 1"))
                {
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        Decimal d = reader.GetDecimal(0);
                        DateOnly date = DateOnly.ParseExact(reader.GetInt32(1).ToString(), "yyyyMMdd");
                        items.Add(new Tuple<DateOnly, string, Decimal>(date, c.name, d));
                    }
                }
            }
            items.Sort((pair1, pair2) => pair2.Item3.CompareTo(pair1.Item3));
            Console.WriteLine($"24h Traded Volume Ranking:");
            foreach (var item in items)
            {
                Console.WriteLine($"Name: {item.Item2, -3} \t Traded Volume: ${item.Item3, -15:0.00} \t ({item.Item1, 10})");
            }
        }
        else
        {
            Console.WriteLine("invalid command");
        }
    }
    else if (args.Length > 1 && args[1] == "price_anomalies")
    {
        if (args.Length == 3)
        {
            Coin? c = find_coin(args[2]);
            if (c != null)
            {
                List<Tuple<DateOnly, Decimal>> price_diffs = new List<Tuple<DateOnly, Decimal>>();
                using (var cmd = db.CreateCommand($"select return, dateid from dailydiff where coinid = {c.Value.coin_id} order by dateid"))
                {
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        Decimal d = reader.GetDecimal(0);
                        DateOnly date = DateOnly.ParseExact(reader.GetInt32(1).ToString(), "yyyyMMdd");
                        price_diffs.Add(new Tuple<DateOnly, Decimal>(date, d));
                    }
                }

                DateOnly from, to;
                from = price_diffs[0].Item1;
                to = price_diffs.Last().Item1;
            
                Decimal mean = 0;
                int count = price_diffs.Count;
                for (int i = 0; i < price_diffs.Count; i++)
                {
                    mean += price_diffs[i].Item2;
                }
                mean /= count;
            
                Decimal variance = 0;
                for (int i = 0; i < price_diffs.Count; i++)
                {
                    variance += (price_diffs[i].Item2 - mean) * (price_diffs[i].Item2 - mean);
                }
                variance /= count;
                double stdev = Math.Sqrt((double)(variance));
                Console.WriteLine($"Coin: {c.Value.name}");
                if (stdev > 0.1)
                {
                    Console.WriteLine($"Period: {from} - {to}");
                    Console.WriteLine($"Mean: {mean:0.00} USD");
                    for (int i = 0; i < price_diffs.Count; i++)
                    {
                        double delta = (double)(price_diffs[i].Item2 - mean) / stdev;
                        if (Math.Abs(delta) >= 3.0)
                        {
                            Console.WriteLine($"Date: {price_diffs[i].Item1, -8} \t Diff: {price_diffs[i].Item2 + " USD", -10} \t ({delta, -5:0.00} st. devs)");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"st. dev. is too low, the coin may be bound and thus very stable.");
                }
            }
            else
            {
                Console.WriteLine("coin not found");
            }
        }
        else
        {
            Console.WriteLine("invalid command");
        }
    }
    else if (args.Length > 1 && args[1] == "correlation")
    {
        if (args.Length == 4)
        {
            Coin? c1 = find_coin(args[2]);
            Coin? c2 = find_coin(args[3]);
            if (c1 != null && c2 != null)
            {
                var coeff = calculate_correlation(c1.Value, c2.Value);
                Console.WriteLine($"Pearson's correlation coefficient: {coeff:0.0000}");
            }
        }
        else
        {
            Console.WriteLine("invalid command");
        }
    }
    else
    {
        Console.WriteLine("invalid command");
    }
}
else if (args.Length > 0 && args[0] == "predict")
{
    if (args.Length > 1 && args[1] == "price")
    {
        if (args.Length == 3)
        {
            Coin? c = find_coin(args[2]);
            if (c != null)
            {
                List<Tuple<DateOnly, Decimal>> prices = new List<Tuple<DateOnly, Decimal>>();
                using (var cmd = db.CreateCommand($"select priceusd, dateid from dailydata where coinid = {c.Value.coin_id} order by dateid desc"))
                {
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        Decimal d = reader.GetDecimal(0);
                        DateOnly date = DateOnly.ParseExact(reader.GetInt32(1).ToString(), "yyyyMMdd");
                        prices.Add(new Tuple<DateOnly, Decimal>(date, d));
                    }
                }

                DateOnly from, to;
                from = prices.Last().Item1;
                to = prices[0].Item1;
                Console.WriteLine($"Period: {from} - {to}");
                double denom = 1;
                double weight_sum = 0;
                double sum = 0;
                for (int i = 0; i < prices.Count; i++)
                {
                    double weight = 1 / denom;
                    weight_sum += weight;
                    sum += weight * (double)prices[i].Item2;
                    denom += i + 2;
                }
                sum /= weight_sum;
                Console.WriteLine($"Price prediction: ${sum:0.00}");
            }
            else
            {
                Console.WriteLine("coin not found");
            }
        }
        else
        {
            Console.WriteLine("invalid command");
        }
    } 
    else if (args.Length > 1 && args[1] == "market_cap")
    {
        if (args.Length == 3)
        {
            Coin? c = find_coin(args[2]);
            if (c != null)
            {
                List<Tuple<DateOnly, Decimal>> market_caps = new List<Tuple<DateOnly, Decimal>>();
                using (var cmd = db.CreateCommand($"select marketcapusd, dateid from dailydata where coinid = {c.Value.coin_id} order by dateid desc"))
                {
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        Decimal d = reader.GetDecimal(0);
                        DateOnly date = DateOnly.ParseExact(reader.GetInt32(1).ToString(), "yyyyMMdd");
                        market_caps.Add(new Tuple<DateOnly, Decimal>(date, d));
                    }
                }

                DateOnly from, to;
                from = market_caps.Last().Item1;
                to = market_caps[0].Item1;
                Console.WriteLine($"Period: {from} - {to}");
                double denom = 1;
                double weight_sum = 0;
                double sum = 0;
                for (int i = 0; i < market_caps.Count; i++)
                {
                    double weight = 1 / denom;
                    weight_sum += weight;
                    sum += weight * (double)market_caps[i].Item2;
                    denom += i + 2;
                }
                sum /= weight_sum;
                Console.WriteLine($"Market cap prediction: ${sum:0.00}");
            }
            else
            {
                Console.WriteLine("coin not found");
            }
        }
        else
        {
            Console.WriteLine("invalid command");
        }
    }
    else
    {
        Console.WriteLine("invalid command");
    }
}
else if (args.Length > 0 && args[0] == "pdf")
{
    if (args.Length > 1 && args[1] == "summary")
    {
        if (args.Length == 3)
        {
            Coin? c = find_coin(args[2]);
            if (c != null)
            {
                List<Tuple<DateOnly, Decimal>> market_caps = new List<Tuple<DateOnly, Decimal>>();
                List<Tuple<DateOnly, Decimal>> prices = new List<Tuple<DateOnly, Decimal>>();
                List<Tuple<DateOnly, Decimal>> daily_volumes = new List<Tuple<DateOnly, Decimal>>();
                using (var cmd = db.CreateCommand($"select marketcapusd, tradedvolumeusd, priceusd, dateid from dailydata where coinid = {c.Value.coin_id} order by dateid desc limit 366"))
                {
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        Decimal market_cap = reader.GetDecimal(0);
                        Decimal traded_volume = reader.GetDecimal(1);
                        Decimal price = reader.GetDecimal(2);
                        DateOnly date = DateOnly.ParseExact(reader.GetInt32(3).ToString(), "yyyyMMdd");
                        market_caps.Add(new Tuple<DateOnly, Decimal>(date, market_cap));
                        daily_volumes.Add(new Tuple<DateOnly, Decimal>(date, traded_volume));
                        prices.Add(new Tuple<DateOnly, Decimal>(date, price));
                    }
                }
                market_caps.Reverse();
                prices.Reverse();
                
                var document = new Document();
                document.LastSection.AddParagraph($"{c.Value.name} summary", StyleNames.Heading1);
                DateOnly from, to;
                from = prices[0].Item1;
                to = prices.Last().Item1;
                document.LastSection.AddParagraph("Price Chart (in USD)", StyleNames.Heading2);
                document.LastSection.AddParagraph($"Period: {from} - {to}", StyleNames.Heading3);

                var price_chart = new Chart();
                price_chart.Left = 0;
                price_chart.Width = Unit.FromCentimeter(16);
                price_chart.Height = Unit.FromCentimeter(10);

                var price_series = price_chart.SeriesCollection.AddSeries();
                price_series.ChartType = ChartType.Line;
                price_series.MarkerStyle = MarkerStyle.None;
                price_series.LineFormat.Color = Colors.Red;
                foreach (var price in prices)
                {
                    price_series.Add((double)price.Item2);
                }

                var price_xSeries = price_chart.XValues.AddXSeries();
                price_xSeries.Add((prices.Count - 1).ToString());
                for (int i = prices.Count - 2; i >= 0; i--)
                {
                    if (i % (prices.Count / 5) == 0)
                    {
                        price_xSeries.Add(i.ToString());
                    }
                    else
                    {
                        price_xSeries.Add(string.Empty);
                    }
                }

                price_chart.XAxis.Title.Caption = "Records ago";
                price_chart.XAxis.MajorTickMark = TickMarkType.Cross;

                price_chart.YAxis.MajorTickMark = TickMarkType.Outside;
                price_chart.YAxis.HasMajorGridlines = true;

                price_chart.PlotArea.LineFormat.Color = Colors.DarkGray;
                price_chart.PlotArea.LineFormat.Width = 1;

                document.LastSection.Add(price_chart);
                
                document.LastSection.AddParagraph("Market Cap Chart (in billions USD)", StyleNames.Heading2);
                document.LastSection.AddParagraph($"Period: {from} - {to}", StyleNames.Heading3);

                var market_cap_chart = new Chart();
                market_cap_chart.Left = 0;
                market_cap_chart.Width = Unit.FromCentimeter(16);
                market_cap_chart.Height = Unit.FromCentimeter(10);

                var market_cap_series = market_cap_chart.SeriesCollection.AddSeries();
                market_cap_series.ChartType = ChartType.Line;
                market_cap_series.MarkerStyle = MarkerStyle.None;
                market_cap_series.LineFormat.Color = Colors.Green;
                foreach (var market_cap in market_caps)
                {
                    market_cap_series.Add((double)(market_cap.Item2 / 1_000_000_000));
                }

                var market_cap_xSeries = market_cap_chart.XValues.AddXSeries();
                market_cap_xSeries.Add(market_caps[0].Item1.ToString());
                for (int i = market_caps.Count - 2; i >= 0; i--)
                {
                    if (i % (market_caps.Count / 5) == 0)
                    {
                        market_cap_xSeries.Add(market_caps[market_caps.Count - i - 1].Item1.ToString());
                    }
                    else
                    {
                        market_cap_xSeries.Add(string.Empty);
                    }
                }

                market_cap_chart.XAxis.Title.Caption = "Records ago";

                market_cap_chart.YAxis.MajorTickMark = TickMarkType.Outside;
                market_cap_chart.YAxis.HasMajorGridlines = true;

                market_cap_chart.PlotArea.LineFormat.Color = Colors.DarkGray;
                market_cap_chart.PlotArea.LineFormat.Width = 1;

                document.LastSection.Add(market_cap_chart);
                document.LastSection.AddParagraph("Traded Volume Info (in billions USD)", StyleNames.Heading2);
                document.LastSection.AddParagraph($"Period: {from} - {to}", StyleNames.Heading3);
                Decimal total_volume = 0;
                int count = daily_volumes.Count;
                foreach (var daily_volume in daily_volumes)
                {
                    total_volume += daily_volume.Item2 / 1_000_000_000;
                }
                Paragraph paragraph = new Paragraph();
                paragraph.Format.Font.Color = Colors.Black;
                paragraph.AddText($"Total Volume Traded: {total_volume:0.00}\n");
                if (count == 0)
                {
                    paragraph.AddText($"Average Daily Volume Traded: NaN\n");
                }
                else
                {
                    paragraph.AddText($"Average Daily Volume Traded: {(total_volume / count):0.00}\n");
                }
                paragraph.AddText("\n");
                paragraph.AddText("\n");
                paragraph.AddText("\n");
                document.LastSection.Add(paragraph);
                
                document.LastSection.AddParagraph("Daily Traded Volume Chart (in billions USD)", StyleNames.Heading2);
                document.LastSection.AddParagraph($"Period: {from} - {to}", StyleNames.Heading3);

                var daily_volume_chart = new Chart();
                daily_volume_chart.Left = 0;
                daily_volume_chart.Width = Unit.FromCentimeter(16);
                daily_volume_chart.Height = Unit.FromCentimeter(10);

                var daily_volume_series = daily_volume_chart.SeriesCollection.AddSeries();
                daily_volume_series.ChartType = ChartType.Line;
                daily_volume_series.MarkerStyle = MarkerStyle.None;
                daily_volume_series.LineFormat.Color = Colors.Green;
                foreach (var daily_volume in daily_volumes)
                {
                    daily_volume_series.Add((double)(daily_volume.Item2 / 1_000_000_000));
                }

                var daily_volume_xSeries = daily_volume_chart.XValues.AddXSeries();
                daily_volume_xSeries.Add(daily_volumes[0].Item1.ToString());
                for (int i = daily_volumes.Count - 2; i >= 0; i--)
                {
                    if (i % (daily_volumes.Count / 5) == 0)
                    {
                        daily_volume_xSeries.Add(daily_volumes[daily_volumes.Count - i - 1].Item1.ToString());
                    }
                    else
                    {
                        daily_volume_xSeries.Add(string.Empty);
                    }
                }

                daily_volume_chart.XAxis.Title.Caption = "Records ago";

                daily_volume_chart.YAxis.MajorTickMark = TickMarkType.Outside;
                daily_volume_chart.YAxis.HasMajorGridlines = true;

                daily_volume_chart.PlotArea.LineFormat.Color = Colors.DarkGray;
                daily_volume_chart.PlotArea.LineFormat.Width = 1;

                document.LastSection.Add(daily_volume_chart);
                
                List<Tuple<DateOnly, Decimal>> ret_percentages = new List<Tuple<DateOnly, Decimal>>();
                Decimal mean_ret_percent = 0;
                count = 0;
                using (var cmd = db.CreateCommand($"select returnpercent, dateid from dailydiff where coinid = 1 order by dateid desc limit 366"))
                {
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        Decimal ret_percent = reader.GetDecimal(0);
                        DateOnly date = DateOnly.ParseExact(reader.GetInt32(1).ToString(), "yyyyMMdd");
                        mean_ret_percent += ret_percent;
                        ret_percentages.Add(new Tuple<DateOnly, Decimal>(date, ret_percent));
                        count++;
                    }
                }
                from = ret_percentages.Last().Item1;
                to = ret_percentages[0].Item1;
                document.LastSection.AddParagraph("Volatility Info (based on log return)", StyleNames.Heading2);
                document.LastSection.AddParagraph($"Period: {from} - {to}", StyleNames.Heading3);
                if (count == 0)
                {
                    paragraph.AddText($"Volatility info unavailable");
                }
                else
                {
                    double stdev = calculate_volatility(c.Value);
                    paragraph = new Paragraph();
                    paragraph.Format.Font.Color = Colors.Black;
                    paragraph.AddText($"Daily: {(stdev):0.0000}\n");
                    paragraph.AddText($"Weekly: {(stdev * Math.Sqrt(7)):0.0000}\n");
                    paragraph.AddText($"Monthly: {(stdev * Math.Sqrt(30)):0.0000}\n");
                    paragraph.AddText($"Period: {(stdev * Math.Sqrt(ret_percentages.Count)):0.0000}\n");
                    paragraph.AddText($"\n");
                    document.LastSection.Add(paragraph);
                }
                
                document.LastSection.AddParagraph("Return Info (in %)", StyleNames.Heading2);
                document.LastSection.AddParagraph($"Period: {from} - {to}", StyleNames.Heading3);
                if (count == 0)
                {
                    paragraph.AddText($"Return info unavailable");
                }
                else
                {
                    mean_ret_percent /= count;
                    var period_ret = (prices.Last().Item2 - prices[0].Item2) / prices[0].Item2;
                    paragraph = new Paragraph();
                    paragraph.Format.Font.Color = Colors.Black;
                    paragraph.AddText($"Average Daily: {(mean_ret_percent):0.000}%\n");
                    paragraph.AddText($"Total Period: {(period_ret):0.000}%\n");
                    document.LastSection.Add(paragraph);
                }
                
                MigraDoc.DocumentObjectModel.IO.DdlWriter.WriteToFile(document, "MigraDoc.mdddl");
                
                var pdfRenderer = new PdfDocumentRenderer
                {
                    Document = document,
                    PdfDocument =
                    {
                        PageLayout = PdfPageLayout.SinglePage,
                        ViewerPreferences =
                        {
                            FitWindow = true
                        }
                    }
                };
                pdfRenderer.PdfDocument.Options.CompressContentStreams = true;
                pdfRenderer.RenderDocument();
                var filename = PdfFileUtility.GetTempPdfFullFileName("samples-MigraDoc/HelloMigraDoc.pdf");
                pdfRenderer.PdfDocument.Save(filename);
                PdfFileUtility.ShowDocument(filename);
            }
            else
            {
                Console.WriteLine("coin not found");
            }
        }
        else
        {
            Console.WriteLine("invalid command");
        }
    }
    else if (args.Length > 1 && args[1] == "current")
    {
        pdf_current_data(coins);
    }
    else if (args.Length > 1 && args[1] == "correlation")
    {
        int i = 2;
        List<Coin> coins_to_display = new List<Coin>();
        while (i < args.Length)
        {
            Coin? c = find_coin(args[i]);
            if (c != null)
            {
                coins_to_display.Add(c.Value);
                i++;
            }
            else
            {
                Console.WriteLine($"coin {args[i]} not found");
                return;
            }
        }
        var count = coins_to_display.Count;
        var document = new Document();
        var table = document.LastSection.AddTable();
        table.Borders.Visible = true;
        List<Row> rows = new List<Row>();
        
        for (int _ = 0; _ < count + 1; _++)
        {
            var col = table.AddColumn(Unit.FromCentimeter(16.0 / (count + 1)));
        }
        
        for (int _ = 0; _ < count + 1; _++)
        {
            var row = table.AddRow();
            row.Height = Unit.FromCentimeter(16.0 / (count + 1));
            rows.Add(row);
        }

        rows[0][0].AddParagraph("Correlation\n Matrix").Format.Alignment = ParagraphAlignment.Center;
        
        for (int _ = 0; _ < count; _++)
        {
            rows[0][_ + 1].AddParagraph(coins_to_display[_].name).Format.Alignment = ParagraphAlignment.Center;
            rows[_ + 1][0].AddParagraph(coins_to_display[_].name).Format.Alignment = ParagraphAlignment.Center;
        }

        for (int _1 = 0; _1 < count + 1; _1++)
        {
            for (int _2 = 0; _2 < count + 1; _2++)
            {
                rows[_1][_2].VerticalAlignment = VerticalAlignment.Center;
            }
        }

        for (int _1 = 0; _1 < count; _1++)
        {
            for (int _2 = 0; _2 < count; _2++)
            {
                double coeff = calculate_correlation(coins_to_display[_1], coins_to_display[_2]);
                rows[_1 + 1][_2 + 1].AddParagraph($"{coeff:0.0000}").Format.Alignment = ParagraphAlignment.Center;
                if (coeff <= 0)
                {
                    rows[_1 + 1][_2 + 1].Shading.Color = Color.FromRgb((byte)Math.Ceiling(255 * (1 + coeff)), (byte)Math.Ceiling(255 * (1 + coeff)), 255);
                }
                else
                {
                    rows[_1 + 1][_2 + 1].Shading.Color = Color.FromRgb(255, (byte)Math.Ceiling(255 * (1 - coeff)), (byte)Math.Ceiling(255 * (1 - coeff)));
                }
            }
        }
        
        MigraDoc.DocumentObjectModel.IO.DdlWriter.WriteToFile(document, "MigraDoc.mdddl");
        
        var pdfRenderer = new PdfDocumentRenderer
        {
            Document = document,
            PdfDocument =
            {
                PageLayout = PdfPageLayout.SinglePage,
                ViewerPreferences =
                {
                    FitWindow = true
                }
            }
        };
        pdfRenderer.PdfDocument.Options.CompressContentStreams = true;
        pdfRenderer.RenderDocument();
        var filename = PdfFileUtility.GetTempPdfFullFileName("samples-MigraDoc/HelloMigraDoc.pdf");
        pdfRenderer.PdfDocument.Save(filename);
        PdfFileUtility.ShowDocument(filename);
    }
    else if (args.Length > 1 && args[1] == "anomalies")
    {
        var document = new Document();
        document.LastSection.AddParagraph($"Daily Price Change Anomalies", StyleNames.Heading1);
        foreach (var coin in coins) 
        {
            List<Tuple<DateOnly, Decimal>> price_diffs = new List<Tuple<DateOnly, Decimal>>();
            using (var cmd = db.CreateCommand($"select return, dateid from dailydiff where coinid = {coin.coin_id} order by dateid"))
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Decimal d = reader.GetDecimal(0);
                    DateOnly date = DateOnly.ParseExact(reader.GetInt32(1).ToString(), "yyyyMMdd");
                    price_diffs.Add(new Tuple<DateOnly, Decimal>(date, d));
                }
            }

            DateOnly from, to;
            from = price_diffs[0].Item1;
            to = price_diffs.Last().Item1;
            
            Decimal mean = 0;
            int count = price_diffs.Count;
            for (int i = 0; i < price_diffs.Count; i++)
            {
                mean += price_diffs[i].Item2;
            }
            mean /= count;
            
            Decimal variance = 0;
            for (int i = 0; i < price_diffs.Count; i++)
            {
                variance += (price_diffs[i].Item2 - mean) * (price_diffs[i].Item2 - mean);
            }
            variance /= count;
            double stdev = Math.Sqrt((double)(variance));
            document.LastSection.AddParagraph($"Coin: {coin.name}\n", StyleNames.Heading2);
            if (stdev > 0.1)
            {
                document.LastSection.AddParagraph($"Period: {from} - {to}\n");
                document.LastSection.AddParagraph($"Mean: {mean:0.00} USD\n");
                document.LastSection.AddParagraph("----------Begin---------\n");
                for (int i = 0; i < price_diffs.Count; i++)
                {
                    double delta = (double)(price_diffs[i].Item2 - mean) / stdev;
                    if (Math.Abs(delta) >= 3.0)
                    {
                        document.LastSection.AddParagraph($"Date: {price_diffs[i].Item1, -8}    Diff: {price_diffs[i].Item2 + " USD", -10}    ({delta, -5:0.00} st. devs)\n");
                    }
                }
                document.LastSection.AddParagraph("-----------End----------\n");
            }
            else
            {
                document.LastSection.AddParagraph($"st. dev. is too low, the coin may be bound and thus very stable.");
            }
            document.LastSection.AddParagraph("\n");
        }
        
        MigraDoc.DocumentObjectModel.IO.DdlWriter.WriteToFile(document, "MigraDoc.mdddl");
        
        var pdfRenderer = new PdfDocumentRenderer
        {
            Document = document,
            PdfDocument =
            {
                PageLayout = PdfPageLayout.SinglePage,
                ViewerPreferences =
                {
                    FitWindow = true
                }
            }
        };
        pdfRenderer.PdfDocument.Options.CompressContentStreams = true;
        pdfRenderer.RenderDocument();
        var filename = PdfFileUtility.GetTempPdfFullFileName("samples-MigraDoc/HelloMigraDoc.pdf");
        pdfRenderer.PdfDocument.Save(filename);
        PdfFileUtility.ShowDocument(filename);
    }
    else
    {
        Console.WriteLine("invalid command");
    }
}
else if (args.Length == 1 && args[0] == "fetch_current")
{
    int i = 1;
    List<Coin> coins_to_fetch = new List<Coin>();
    while (i < args.Length)
    {
        Coin? c = find_coin(args[i]);
        if (c != null)
        {
            coins_to_fetch.Add(c.Value);
            i++;
        }
        else
        {
            Console.WriteLine($"coin {args[i]} not found");
            return;
        }
    }
    print_current_data(coins_to_fetch);
}
else if (args.Length == 1 && args[0] == "refresh_data")
{
    foreach (var coin in coins)
    {
        add_historical_coin_data_coingecko(coin);
    }
}
else if (args.Length == 1 && args[0] == "coin_list")
{
    Console.WriteLine($"NumID \t\t CoinID \t\t Symbol \t Name");
    foreach (var coin in coins)
    {
        Console.WriteLine($"{coin.coin_id} \t\t {coin.id} \t\t {coin.symbol} \t\t {coin.name}");
    }
}
else if (args.Length == 1 && args[0] == "help")
{
    Console.WriteLine("help - displays this message");
    Console.WriteLine("coin_list - displays available coin info");
    Console.WriteLine("refresh_data - fetches data from the CoinGecko API and put it into the database");
    Console.WriteLine("fetch_current [coin1 coin2 ...] - fetches current data about coins from the CoinGecko API");
    Console.WriteLine();
    Console.WriteLine("analyze basic [coin] - displays basic statistical information about a coin");
    Console.WriteLine("analyze volatility [coin] - displays volatility information about a coin");
    Console.WriteLine("analyze rating (market_cap | daily_volume) - displays a metric rating information");
    Console.WriteLine("analyze price_anomalies [coin] - displays sudden price changes of a coin");
    Console.WriteLine("analyze correlation [coin1 coin2] - displays correlation information of two coins");
    Console.WriteLine();
    Console.WriteLine("predict (market_cap | daily_volume) [coin] - displays a prediction of a metric of a coin");
    Console.WriteLine();
    Console.WriteLine("pdf summary [coin] - generates and opens a .pdf file with basic statistical information about a coin");
    Console.WriteLine("pdf current - fetches current data about coins from the CoinGecko API, then generates and opens a .pdf file with that data");
    Console.WriteLine("pdf anomalies - generates and opens a .pdf file with sudden price changes of all coins");
    Console.WriteLine("pdf correlation [coin1 coin2 ...] - generates and opens a .pdf file with a correlation matrix of coins");
}
else
{
    Console.WriteLine("invalid command");
}

struct Coin
{
    public int coin_id {get;}
    public string id {get;}
    public string symbol {get;}
    public string name {get;}

    public Coin(int coin_id, string id, string symbol, string name)
    {
        this.coin_id = coin_id;
        this.id = id;
        this.symbol = symbol;
        this.name = name;
    }
}

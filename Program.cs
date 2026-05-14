using HtmlAgilityPack;

namespace MySchemaApp
{
    internal class Program
    {
        static readonly SchemaManager schemaManager = new();
        static readonly SettingsManager settingsManager = new();
        static async Task Main(string[] args)
        {
            List<Schema> schemas = schemaManager.Schemas;
            List<Schema> favoriteSchemas = schemas.Where(s => s.IsFavorite).ToList();
            if (favoriteSchemas.Count > 0)
            {
                foreach (var schema in favoriteSchemas)
                {
                    if (settingsManager.Settings.StartDateTodayStartupOnly 
                        || settingsManager.Settings.StartDateTodayOnAll)
                    {
                        // Settings wants this schema to use startdate=today.
                        schema.Url = Printer.StartDateToday(schema.Url);
                    }

                    if (settingsManager.Settings.CustomizedTable) await PrintCustomizedSchema(schema);
                    else await PrintDefaultSchema(schema);
                }
                Console.WriteLine("\nTryck enter för att återgå till startmenyn.");
                Console.ReadKey();
            }

            await NormalOptions();
        }
        static async Task PrintCustomizedSchema(Schema schema)
        {
            FullClearConsole();
            Console.WriteLine("Schema: " + schema.Title);

            using var http = new HttpClient();

            var html = await http.GetStringAsync(schema.Url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var schemaTable = doc.DocumentNode.SelectSingleNode("//table[@class='schemaTabell']");
            if (schemaTable == null)
            {
                // If this is null the user managed to input a faulty url.
                Console.WriteLine("Kunde inte hitta ett schema på sidan.");
                Console.WriteLine("Är länken korrekt?");
                await Task.Delay(1500);
                return;
            }

            var schemaTableRows = schemaTable.SelectNodes("./tr");

            Printer.CustomPrintTable(schemaTableRows);
        }

        static async Task PrintDefaultSchema(Schema schema)
        {
            FullClearConsole();
            Console.WriteLine("Schema: " + schema.Title);

            using var http = new HttpClient();

            var html = await http.GetStringAsync(schema.Url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var schemaTable = doc.DocumentNode.SelectSingleNode("//table[@class='schemaTabell']");
            if (schemaTable == null)
            {
                // If this is null the user managed to input a faulty url.
                Console.WriteLine("Kunde inte hitta ett schema på sidan.");
                Console.WriteLine("Är länken korrekt?");
                await Task.Delay(1500);
                return;
            }

            var schemaTableRows = schemaTable.SelectNodes("./tr");

            Printer.DefaultPrintTable(schemaTableRows);
        }

        static void FullClearConsole()
        {
            // Removes scrollback because Console.Clear() only deletes
            // what's currently in view not including what's seen when scrolling up.
            Console.Write("\x1b[3J");
            Console.Clear();
        }
        static async Task NormalOptions()
        {
            bool running = true;
            while (running)
            {
                FullClearConsole();
                Console.WriteLine("Välj ett alternativ:");
                Console.WriteLine("0. Stäng av applikationen.");
                Console.WriteLine("1. Lägg till schema.");
                Console.WriteLine("2. Ta bort schema.");
                Console.WriteLine("3. Visa alla scheman.");
                Console.WriteLine("4. Hantera favoritscheman");
                Console.WriteLine("5. Hantera inställningar");

                string choice = Console.ReadLine().Trim();
                switch (choice)
                {
                    case "0":
                        // Exits the app.
                        Environment.Exit(0);
                        running = false;
                        break;

                    case "1":
                        // Add schema
                        await AddSchema();
                        break;

                    case "2":
                        // Remove schema
                        await RemoveSchema();
                        break;

                    case "3":
                        // Show all schemas
                        await ViewSchemas();
                        break;

                    case "4":
                        // Handling FavoriteSchemas (startup schemas)
                        await FavoriteSchema();
                        break;

                    case "5":
                        // Handling settings
                        ChangeSettings();
                        break;

                    default:
                        break;
                }
            }
        }
        static async Task AddSchema()
        {
            bool schemaAdded = false;
            List<Schema> schemas = schemaManager.Schemas;

            while (!schemaAdded)
            {
                FullClearConsole();
                Console.WriteLine("0. Tillbaka till startmenyn");
                Console.WriteLine("Skriv länken för schemat. " +
                    "\n\nLänken kan exempelvis se ut såhär: " +
                    "\nhttps://schema.oru.se/setup/jsp/Schema.jsp?startDatum=2026-01-19&intervallTyp=m&intervallAntal=6&sokMedAND=false&sprak=SV&resurser=k.IK205G-V2062V26-%2C");
                Console.Write("\nDin länk: ");

                string url = Console.ReadLine().Trim();
                if (url == "0") return; //Back to main menu.

                if (url.Contains("startDatum=idag"))
                {
                   url = await Printer.AutoSetUrlStartDate(url);
                }

                if (schemas.Any(s => s.Url == url)) //Controls for duplicates.
                {
                    Console.WriteLine("Det finns redan ett schema med den URL:en, försök igen.");
                    await Task.Delay(1000);
                    continue;
                }

                if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                && uri.Host == "schema.oru.se")
                {

                    bool titleChosen = false;
                    while (!titleChosen)
                    {
                        FullClearConsole();
                        Console.WriteLine("0. Tillbaka till startmenyn");
                        Console.Write("Skriv en titel för schemat: ");

                        string title = Console.ReadLine().Trim();
                        if (title == "0") return;

                        if (!string.IsNullOrEmpty(title)) //Saves the Schema
                        {
                            schemas.Add(new Schema { Title = title, Url = url });
                            schemaManager.Save(schemas);

                            FullClearConsole();
                            Console.Write("Schemat har lagts till! Skickar dig till startmenyn");
                            for (int i = 0; i < 3; i++)
                            {
                                await Task.Delay(1250);
                                Console.Write(".");
                            }
                            titleChosen = true;
                            schemaAdded = true;
                        }
                        else
                        {
                            Console.WriteLine("Titeln kan inte vara tom, försök igen.");
                            await Task.Delay(1000);
                        }
                    }

                }
                else
                {
                    Console.WriteLine("Kunde inte hitta det schemat, försök igen.");
                    await Task.Delay(1000);
                }
            }
        }

        static async Task RemoveSchema()
        {
            FullClearConsole();
            List<Schema> schemas = schemaManager.Schemas;

            // If there are no schemas, return to main menu.
            if (schemas.Count == 0)
            {
                Console.Write("Inga scheman hittades, skickar dig till startmenyn.");
                for (int i = 0; i < 2; i++)
                {
                    await Task.Delay(1250); Console.Write(".");
                }
                return;
            }

            Console.WriteLine("0. Tillbaka till startmenyn");
            // Prints the saved schemas' titles.
            ShowAllSchemaTitles(schemas, "Välj ett schema att ta bort:");

            string choice = Console.ReadLine();
            if (int.TryParse(choice, out int result)
                && result < schemas.Count + 1
                && result >= 0)
            {
                if (result == 0) return; //Back to main menu.

                Console.Write("Raderar schemat \"" + schemas[result - 1].Title + "\".");
                schemas.RemoveAt(result - 1);
                for (int i = 0; i < 2; i++)
                {
                    await Task.Delay(500); Console.Write(".");
                }
                schemaManager.Save(schemas);
                return;
            }
        }

        static async Task ViewSchemas()
        {
            FullClearConsole();
            List<Schema> schemas = schemaManager.Schemas;
            Console.WriteLine("0. Tillbaka till startmenyn");

            // Displays all schemas with numbers, and asks the user to choose one to remove.
            if (schemas.Count == 0)
            {
                Console.Write("Inga scheman hittades, skickar dig till startmenyn.");
                for (int i = 0; i < 2; i++)
                {
                    await Task.Delay(500); 
                    Console.Write(".");
                }
                return;
            }

            // Prints the saved schemas' titles.
            ShowAllSchemaTitles(schemas, "Välj ett schema att visa:");

            string choice = Console.ReadLine();
            if (int.TryParse(choice, out int result)
                && result < schemas.Count + 1
                && result >= 0)
            {
                if (result == 0) return;

                Schema schema = schemas[result - 1];

                // Settings wants this schema to use startdate=today.
                if (settingsManager.Settings.StartDateTodayOnAll) schema.Url = Printer.StartDateToday(schema.Url);

                if (settingsManager.Settings.CustomizedTable) await PrintCustomizedSchema(schemas[result - 1]);
                else await PrintDefaultSchema(schemas[result - 1]);

                Console.WriteLine("\nValfri knapp: Återgår till huvudmeny.");
                Console.ReadKey();
                return;
            }
        }

        static async Task FavoriteSchema()
        {
            FullClearConsole();
            List<Schema> schemas = schemaManager.Schemas;
            List<Schema> normalSchemas = schemas.Where(s => !s.IsFavorite).ToList();
            List<Schema> favoriteSchemas = schemas.Where(s => s.IsFavorite).ToList();

            Console.WriteLine("Här kan du välja att \"Favorita\" ett schema.");
            Console.WriteLine("När du favoritar ett schema kommer det att visas så fort du startar applikationen.");

            Console.WriteLine("\n0. Tillbaka till startmenyn.\n");

            if (normalSchemas.Count == 0)
            {
                Console.WriteLine("Alla scheman är redan favoriter! Om du vill ta bort ett schema från favoriter, välj det under de redan favoritmarkerade scheman nedan.");
            }
            else
            {
                Console.WriteLine("Scheman som inte är favoriter ännu:");
                for (int i = 0; i < normalSchemas.Count; i++)
                {
                    Console.WriteLine(i + 1 + ". " + normalSchemas[i].Title);
                }
            }

            if (favoriteSchemas.Count > 0)
            {
                Console.WriteLine("\nOm du vill \"De-Favorita\" ett schema väljer du en av dessa scheman.");
                Console.WriteLine("Scheman som redan är favoriter:");
                for (int i = 0; i < favoriteSchemas.Count; i++)
                {
                    Console.WriteLine(i + 1 + normalSchemas.Count + ". " + favoriteSchemas[i].Title);
                }
            }

            string choice = Console.ReadLine();
            if (int.TryParse(choice, out int result)
                && result < schemas.Count + 1
                && result >= 0)
            {
                if (result == 0) return;

                if (result <= normalSchemas.Count)
                {
                    // Favorite a normal schema
                    normalSchemas[result - 1].IsFavorite = true;
                    Console.WriteLine("Favoritar schemat \"" + normalSchemas[result - 1].Title + ".");
                }

                else
                {
                    // De-Favorite a favorite schema
                    favoriteSchemas[result - 1 - normalSchemas.Count].IsFavorite = false;
                    Console.WriteLine("De-Favoritar schemat \"" + favoriteSchemas[result - 1 - normalSchemas.Count].Title + ".");
                }

                schemaManager.Save(schemas);

                // Buffer
                Console.Write("Skickar dig till startmenyn.");
                for (int i = 0; i < 2; i++)
                {
                    await Task.Delay(500); Console.Write(".");
                }
                return;
            }
            Console.ReadKey();
        }

        static void ChangeSettings()
        {
            var settings = settingsManager.Settings;
            bool running = true;
            while (running)
            {
                FullClearConsole();
                Console.WriteLine("Här kan du ändra inställningar!");

                Console.WriteLine("0. Tillbaka till huvudmenyn.");

                Console.WriteLine("1. Jag vill ha scheman som endast visar det viktiga: " + 
                    (settings.CustomizedTable ? "Ja." : "Nej."));

                Console.WriteLine("2. Jag vill att ALLA mina scheman ska starta från dagens datum: " +
                    (settings.StartDateTodayOnAll ? "Ja." : "Nej."));

                Console.WriteLine("3. Jag vill att mina Favorit-scheman startar från dagens datum vid uppstart: " +
                    (settings.StartDateTodayOnAll ? "Ja (pga. ovanstående Ja)." :
                    (settings.StartDateTodayStartupOnly ? "Ja." : "Nej.")));

                Console.WriteLine("4. Mer information om inställningar."); //Senare

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "0":
                        running = false;
                        break;

                    case "1":
                        settings.CustomizedTable = !settings.CustomizedTable;
                        break;

                    case "2":
                        settings.StartDateTodayOnAll = !settings.StartDateTodayOnAll;
                        break;

                    case "3":
                        settings.StartDateTodayStartupOnly = !settings.StartDateTodayStartupOnly;
                        break;

                    case "4":
                        SettingsInfo();
                        Console.WriteLine("Tryck enter för att återgå till inställningar.");
                        Console.ReadKey();
                        break;
                    
                    default:
                        break;
                }
            }
            settingsManager.Save(settings);
        }

        static void SettingsInfo()
        {
            FullClearConsole();
            var settings = settingsManager.Settings;
            Console.WriteLine("Jag vill ha scheman som endast visar det viktiga: " +
                    (settings.CustomizedTable ? "Ja." : "Nej."));
            Console.WriteLine("Detta justerar så att schemat som skrivs ut endast visar viktig information när den är satt som \"Ja\"." +
                " Detta innebär att celler såsom vilken lärare man har och diverse tomma celler inte kommar att följa med när schemat skrivs ut. " +
                "Om man istället vill ha schemat printat på samma sätt som det ser ut på Kronox kan man istället sätta denna till \"Nej\".");
            Console.WriteLine("Startvärde: Ja\n");

            Console.WriteLine("Jag vill att ALLA mina scheman ska starta från dagens datum: " +
                (settings.StartDateTodayOnAll ? "Ja." : "Nej."));
            Console.WriteLine("Denna inställningen när den är satt som \"Ja\" gör att scheman som skrivs ut endast kommer att skriva ut rader från schemat vars datum inte skett ännu. " +
                "Om det exempelvis är 2:a januari idag och schemat har en lektion den 1:a januari kommer den lektionen att ignoreras när schemat ska skrivas ut." +
                "\nOBS: endast möjligt om länken du angett för schemat ursprungligen innehållt det hela schemat.");
            Console.WriteLine("Startvärde: Nej\n");

            Console.WriteLine("Jag vill att mina Favorit-scheman startar från dagens datum vid uppstart: " +
                (settings.StartDateTodayOnAll ? "Ja (pga. ovanstående Ja)." :
                (settings.StartDateTodayStartupOnly ? "Ja." : "Nej.")));
            Console.WriteLine("Samma som ovanstående inställning men påverkar endast scheman du har satt som \"Favorit\". Denna inställningen är automatiskt " +
                "satt som \"Ja\" ifall ovanstående inställning är satt som det då den inställningen gäller alla scheman.");
            Console.WriteLine("Startvärde: Ja\n");
        }

        static void ShowAllSchemaTitles(List<Schema> schemas, string headermsg)
        {
            // Displays all schemas with numbers, and asks the user to choose one to remove.
            // This only prints the schemas, the logic and choices are handled in whatever calls this method.
            Console.WriteLine(headermsg);
            for (int i = 0; i < schemas.Count; i++)
            {
                Console.WriteLine(i + 1 + ". " + schemas[i].Title);
            }
        }

        //for testing
        //var url = "https://schema.oru.se/setup/jsp/Schema.jsp?startDatum=2026-01-19&intervallTyp=m&intervallAntal=6&sokMedAND=false&sprak=SV&resurser=k.IK205G-V2062V26-%2C";
        //var urlKrim = "https://schema.oru.se/setup/jsp/Schema.jsp?startDatum=idag&intervallTyp=m&intervallAntal=6&sokMedAND=false&sprak=SV&resurser=k.KR400G-V3060V26-%2C";
    }
}

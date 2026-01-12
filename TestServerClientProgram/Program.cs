// CLIENT
// Test Data Server Program

using System.Net.Http.Json;
using System.Net;

// Same model as the server
public class DataItem
{
    public string Key { get; set; } = "";
    public int Value { get; set; }
}

class Program
{	
	const string NGROK_URL = "https://sulkiest-lucina-dandyish.ngrok-free.dev";
    
    static async Task Main()
    {
        // SETUP HTTP CLIENT
        using HttpClient client = new()
        {
            BaseAddress = new Uri(NGROK_URL),
			Timeout = TimeSpan.FromSeconds(10) // timeout for diagnostics
        };
        
		// GEMINI: bypasses the NGROK browser warning for automated requests
		client.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
		
		// RUN DIAGNOSTICS
		Console.WriteLine("--- RUNNING DIAGNOSTICS ---");
		if(!await RunDiagnostics(client))
		{
			Console.WriteLine("\n[ERROR] Fatal setup issue. Resolve the items above.");
			Console.ReadLine();
			return;
		}		
		Console.WriteLine("--- DIAGNOSTICS PASSED ---");
		
		bool exit = false;
		const string exitLabel = "[ESC].";
		string[] menuOptions = [
            "Upload new key-value pair",
            "Delete an existing key",
            "List all stored data",
            "Exit"
        ];
        while (!exit) // Main menu loop
        {
            await ResetUI();
            Console.WriteLine("\n--- Test Data Server Menu ---");
            for (int i = 0; i < menuOptions.Length; i++)
            {
                if (i < menuOptions.Length - 1) Console.Write($"{i + 1}.");
                else Console.Write(exitLabel);
                Console.SetCursorPosition(exitLabel.Length + 1, Console.CursorTop);
                Console.WriteLine($"{menuOptions[i]}");
            }            
            Console.Write("\nSelect an option: ");

            // Use ReadKey for immediate response
            var choice = Console.ReadKey(true);
            switch (choice.Key)
            {
                case ConsoleKey.D1: case ConsoleKey.NumPad1:
                    await HandleUpload(client);
                    break;
                case ConsoleKey.D2: case ConsoleKey.NumPad2:
                    await HandleDelete(client);
                    break;
                case ConsoleKey.D3: case ConsoleKey.NumPad3:
                    await RetrieveData(client);
                    break;
                case ConsoleKey.Escape: case ConsoleKey.D4:
                    exit = true;
                    break;
            }
        }        
        await ResetUI();
    }	
	
	// DATA upload
	static async Task HandleUpload(HttpClient client)
    {
        Console.Clear();
		while(true)
		{
            const string uploadHeader = "\n[UPLOAD MODE] Press ESC at any time to cancel and return to menu.";
            const string enterKeyText = "Enter Key: ";
            Console.WriteLine(uploadHeader);
		    
			// 1. Prompt for Key with Escape support
			Console.Write(enterKeyText);
			string? rawkey = ReadLineOrEscape();
			if (rawkey == null) return; // User pressed ESC, exit to menu
			
			string key = rawkey.Trim();
			if (string.IsNullOrWhiteSpace(key) || key.Length > 13)
            {
                if(string.IsNullOrWhiteSpace(key) || string.IsNullOrEmpty(key)) Console.WriteLine("ERROR: Invalid key. Cannot be empty.");
                if (key.Length > 13) Console.WriteLine($"ERROR: Invalid key. Maximum length is 13 characters. (key={key})");
                await ResetUI();
                continue;
            }

			// 2. Prompt for Value
			const string enterValueText = "Enter Integer Value: "; 
			bool itemUploaded = false;
			while(!itemUploaded) // Loop until valid integer or ESC
            {
                Console.Write(enterValueText);
                string? valueStr = ReadLineOrEscape();
                if (valueStr == null) return; // User pressed ESC, exit to menu

                if (!int.TryParse(valueStr, out int value))
                {
                    Console.WriteLine("Invalid value. Must be an integer.");
                    
                    // Re-display header, entered key and enter value prompt
                    await ResetUI();
                    Console.WriteLine(uploadHeader);
                    Console.Write(enterKeyText + key + "\n"); // Re-display entered key
                    continue;
                }
                
                // 3. Attempt to upload
                var response = await client.PostAsJsonAsync("api/data", new DataItem { Key = key, Value = value });
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Success: Data uploaded. ({key}, {value})");
                    await ResetUI();
                    itemUploaded = true;
                    continue;
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    Console.WriteLine($"Error: Key is already in use. (key={key}");
                    await ResetUI();
                }
                else
                {                
                    Console.WriteLine($"Error: {response.StatusCode}");
                    await ResetUI();
                }
                await Task.Delay(100); // Small delay before retrying
            }
        await Task.Delay(100); // Small delay before next upload
		}
    }
		
    // DATA deletion
	static async Task HandleDelete(HttpClient client)
    {
        while (true)
        {
            Console.WriteLine("\n[DELETE MODE] Press ESC to return to main menu.");
            Console.Write("Enter Key to delete: ");
            
            // Use the custom helper to listen for ESC
            string? key = ReadLineOrEscape(); 
            
            // If user pressed ESC, return to main menu
            if (key == null) break; 
            
            if (string.IsNullOrWhiteSpace(key)) continue;

            try
            {
                // 1. Fetch data to display for confirmation
                HttpResponseMessage getResponse = await client.GetAsync($"api/data/{key}");
                
                if (!getResponse.IsSuccessStatusCode)
                {
                    if (getResponse.StatusCode == HttpStatusCode.NotFound)
                        {
                        Console.WriteLine(">> Error: Key not found.");
                        await ResetUI();
                        }
                    else
                    {
                        Console.WriteLine($">> Error retrieving record: {getResponse.StatusCode}");
                        await ResetUI();
                    }
                    continue;
                }

                DataItem? item = await getResponse.Content.ReadFromJsonAsync<DataItem>();
                if (item == null) continue;

                // 2. Request confirmation
                Console.WriteLine($"\n>> Found: [Key: {item.Key}, Value: {item.Value}]");
                Console.Write(">> Delete this record? (y/n): ");
                
                ConsoleKeyInfo confirm = Console.ReadKey(intercept: true);
                Console.WriteLine(); 

                if (confirm.Key != ConsoleKey.Y)
                {
                    Console.WriteLine(">> Deletion cancelled.");
                    await ResetUI();
                    continue;
                }

                // 3. Perform Deletion
                HttpResponseMessage deleteResponse = await client.DeleteAsync($"api/data/{key}");

                if (deleteResponse.IsSuccessStatusCode)
                    Console.WriteLine($">> Success: Key '{key}' deleted.");
                else
                    Console.WriteLine($">> Error during deletion: {deleteResponse.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($">> Unexpected error: {ex.Message}");                
            }
            await ResetUI();
        }
    }

    // DATA retrieval
    static async Task RetrieveData(HttpClient client)
    {
        try
        {
            // Use relative path because BaseAddress is already set to NGROK_URL
            var allData = await client.GetFromJsonAsync<List<DataItem>>("api/data");

            if (allData != null && allData.Count > 0)
            {
                Console.WriteLine("\n--- Current Database State ---");
                int i = 1;
                foreach (var item in allData)
                {
                    // Numbered formatting for better readability
                    Console.WriteLine($"{i}. Key: {item.Key,-15} Value: {item.Value}");
                    i++;
                }
            }
        }
        // Specifically catch the 409 Conflict your server throws for empty tables
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            Console.WriteLine("\n[INFO] Database is currently empty.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[ERROR] Could not retrieve data: {ex.Message}");
        }
    }

    // DIAGNOSTICS
    static async Task<bool> RunDiagnostics(HttpClient client)
    {
        // 1. VERIFY TUNNEL: Check if the Ngrok URL is active
        try
        {
            var response = await client.GetAsync("/health");
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("[PASS] Tunnel: Connection established to RHEL/WSL server.");
            }
            else
            {
                Console.WriteLine($"[FAIL] Tunnel: Reached ngrok, but server returned {response.StatusCode}.");
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[FAIL] Tunnel: Could not reach the ngrok endpoint.");
            Console.WriteLine($"       Check: Is ngrok running in your RHEL 10 terminal?");
            Console.WriteLine($"       Details: {ex.Message}");
            return false;
        }

        // 2. VERIFY DATABASE: Check if SQLite can be queried (Write test)
        try
        {
            // Testing the 'GetAll' endpoint to see if DB is locked or file missing
            var dbResponse = await client.GetAsync("api/data");
            
            // Your server returns 409 Conflict if DB is empty, which is a "Success" for connectivity
            if (dbResponse.StatusCode == HttpStatusCode.OK || dbResponse.StatusCode == HttpStatusCode.Conflict)
            {
                Console.WriteLine("[PASS] Database: SQLite is online and accessible.");
            }
            else
            {
                Console.WriteLine($"[FAIL] Database: Server responded with error {dbResponse.StatusCode}.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FAIL] Database: Unexpected server error during data test.");
            Console.WriteLine($"       Details: {ex.Message}");
            return false;
        }

        return true;
    }
    
    /// Helper to read a line of input while monitoring for the Escape key.
	/// Returns null if Escape is pressed, otherwise returns the string entered.	
	static string? ReadLineOrEscape()
	{
		string input = "";
		while (true)
		{
			ConsoleKeyInfo keyInfo = Console.ReadKey(true);

			// Check for Escape key
			if (keyInfo.Key == ConsoleKey.Escape)
			{
				Console.WriteLine(" [Cancelled]");
				return null;
			}

			// Check for Enter key to finish input
			if (keyInfo.Key == ConsoleKey.Enter)
			{
				Console.WriteLine();
				return input;
			}

			// Handle Backspace
			if (keyInfo.Key == ConsoleKey.Backspace)
			{
				if (input.Length > 0)
				{
					input = input[..^1];
					Console.Write("\b \b"); // Move back, write space, move back again
				}
			}
			// Handle standard character input
			else if (!char.IsControl(keyInfo.KeyChar))
			{
				input += keyInfo.KeyChar;
				Console.Write(keyInfo.KeyChar);
			}
		}
	}

    // Helper to pause and clear console
    static async Task ResetUI()
    {
                Console.Write("\nPress any key to conitunue...");
                Console.ReadKey();
                Console.Clear();
    }
    
}

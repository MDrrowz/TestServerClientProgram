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
    
    static readonly string[] keys = ["score", "place", "color"];
    static readonly int[] values = [123, 430, 69];
	
	const string NGROK_URL = "https://sulkiest-lucina-dandyish.ngrok-free.dev";
    
    static async Task Main()
    {
        using HttpClient client = new()
        {
            BaseAddress = new Uri(NGROK_URL),
			Timeout = TimeSpan.FromSeconds(10) // timeout for diagnostics
        };
        
		// GEMINI: bypasses the NGROK browser warning for automated requests
		Client.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
		
		Console-WriteLine("--- SYSTEM DIAGNOSTICS ---");
		if(!await RunDiagnostics(client))
		{
			Console.WriteLine("\n[ERROR] Fatal setup issue. Resolve the items above.")
			Console.ReadLine();
			return;
		}
		
		Console.WriteLine("\n--- DIAGNOSTICS PASSES ---");
		
		bool exit = false;
        while (!exit)
        {
            Console.WriteLine("\n--- Test Data Server Menu ---");
            Console.WriteLine("1. Upload new key-value pair");
            Console.WriteLine("2. Delete an existing key");
            Console.WriteLine("3. List all stored data");
            Console.WriteLine("4. Exit");
            Console.Write("\nSelect an option: ");

            switch (Console.ReadLine())
            {
                case "1":
                    await HandleUpload(client);
                    break;
                case "2":
                    await HandleDelete(client);
                    break;
                case "3":
                    await RetrieveData(client);
                    break;
                case "4":
                    exit = true;
                    break;
                default:
                    Console.WriteLine("Invalid selection. Try again.");
                    break;
            }
        }
				
        // foreach (string key in keys)
        // {
            // int index = Array.IndexOf(keys, key);
            
            // // Console.WriteLine($"index={index}, key={key}");
            
            // var item = new DataItem
            // {
            // Key = key,
            // Value = values[index]
            // };
            
            // await UploadData(client, item);
            
            // await RequestValue(client, key);
        // }
        
        // await RetrieveData(client);
        
        var a = Console.ReadLine();
    }
	
	// DATA upload
	static async Task HandleUpload(HttpClient client)
    {
		Console.WriteLine("\n[UPLOAD MODE] Press ESC at any time to cancel and return to menu.");
		
		while(!!)
		{
			// 1. Prompt for Key with Escape support
			Console.Write("Enter Key: ");
			string? key = ReadLineOrEscape();
			if (key == null) return; // User pressed ESC, exit to menu

			// 2. Prompt for Value
			Console.Write("Enter Integer Value: ");
			string? valueStr = ReadLineOrEscape();
			if (valueStr == null) return; // User pressed ESC, exit to menu

			if (!int.TryParse(valueStr, out int value))
			{
				Console.WriteLine("Invalid value. Must be an integer.");
				return;
			}

			var item = new DataItem { Key = key, Value = value };
			var response = await client.PostAsJsonAsync("api/data", item);

			if (response.IsSuccessStatusCode)
				Console.WriteLine($"Success: Data uploaded. ({key}, {value}");
			else if (response.StatusCode == HttpStatusCode.Conflict)
				Console.WriteLine($"Error: Key is already in use. (key={key}");
			else
				Console.WriteLine($"Error: {response.StatusCode}");
		}
    }
	
	/// Helper to read a line of input while monitoring for the Escape key.
	/// Returns null if Escape is pressed, otherwise returns the string entered.
	static async Task<string?> GetInputOrEscape()
	{
		string input = "";
		while (true)
		{
			if (Console.KeyAvailable)
			{
				var key = Console.ReadKey(true);
				if (key.Key == ConsoleKey.Escape) return null;
				if (key.Key == ConsoleKey.Enter) 
				{
					Console.WriteLine();
					return input;
				}
				if (key.Key == ConsoleKey.Backspace && input.Length > 0)
				{
					input = input[..^1];
					Console.Write("\b \b"); // Erase character from console
				}
				else if (!char.IsControl(key.KeyChar))
				{
					input += key.KeyChar;
					Console.Write(key.KeyChar);
				}
			}
			await Task.Delay(10); // Prevent CPU spiking
		}
	}
	
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


	static async Task HandleDelete(HttpClient client)
	{
		Console.Write("\nEnter Key to delete: ");
		string key = Console.ReadLine() ?? "";

		if (string.IsNullOrWhiteSpace(key)) return;

		try
		{
			// 1. Fetch current data to display to user
			HttpResponseMessage getResponse = await client.GetAsync($"api/data/{key}");
			
			if (!getResponse.IsSuccessStatusCode)
			{
				if (getResponse.StatusCode == HttpStatusCode.NotFound)
					Console.WriteLine("Error: Key not found.");
				else
					Console.WriteLine($"Error retrieving record: {getResponse.StatusCode}");
				return;
			}

			DataItem? item = await getResponse.Content.ReadFromJsonAsync<DataItem>();
			if (item == null) return;

			// 2. Request confirmation
			Console.WriteLine($"\nRecord Found: [Key: {item.Key}, Value: {item.Value}]");
			Console.Write("Are you sure you want to delete this record? (y/n): ");
			
			ConsoleKeyInfo confirm = Console.ReadKey(intercept: true);
			Console.WriteLine(); // New line after key press

			if (confirm.Key != ConsoleKey.Y)
			{
				Console.WriteLine("Deletion cancelled.");
				return;
			}

			// 3. Proceed with deletion
			HttpResponseMessage deleteResponse = await client.DeleteAsync($"api/data/{key}");

			if (deleteResponse.IsSuccessStatusCode)
				Console.WriteLine($"Success: Key '{key}' has been permanently deleted.");
			else
				Console.WriteLine($"Error during deletion: {deleteResponse.StatusCode}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"An unexpected error occurred: {ex.Message}");
		}
	}


    static async Task RetrieveData(HttpClient client)
    {
        try
        {
            var allData = await client.GetFromJsonAsync<List<DataItem>>("api/data");
            if (allData != null)
            {
                Console.WriteLine("\nStored Data:");
                allData.ForEach(d => Console.WriteLine($" - {d.Key}: {d.Value}"));
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            Console.WriteLine("Database is currently empty.");
        }
    }

    static async Task<bool> RunDiagnostics(HttpClient client)
    {
        try {
            var response = await client.GetAsync("/health");
            if (response.IsSuccessStatusCode) return true;
        } catch { }
        Console.WriteLine("Critical Error: Server unreachable via ngrok.");
        return false;
    }
}
	
	
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
            return false;
        }

        return true;
    }
	    
    static async Task RetrieveData(HttpClient client)
    {
        try
        {
            List<DataItem>? allData =
                await client.GetFromJsonAsync<List<DataItem>>($"{NGROK_URL}/api/data");
            
            if (allData != null)
            {
            Console.WriteLine("Retrieved Data:");
            int i = 1;
                foreach (DataItem item in allData)
                {
                    Console.WriteLine($"{i} - Key={item.Key}, Value={item.Value}");
                    i++;
                }
            }                            
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine("Network or server error:");
            Console.WriteLine(ex.Message);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Request timed out.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Unexpected error:");
            Console.WriteLine(ex);
        }    
    }
    
    static async Task UploadData(HttpClient client, DataItem item)
    {
        // ---- UPLOAD DATA ----        
        try
        {
            HttpResponseMessage postResponse =
                await client.PostAsJsonAsync("api/data", item);
                
            if (postResponse.StatusCode == HttpStatusCode.Conflict)
            {
                string error = await postResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Upload failed (duplicate key): {error}");
            }
            else
            {
                try                
                {
                    postResponse.EnsureSuccessStatusCode();
                    Console.WriteLine("Data uploaded.");
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Unexpected error:");
                    Console.WriteLine(ex);                    
                }
            }       
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine("Network or server error:");
            Console.WriteLine(ex.Message);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Request timed out.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Unexpected error:");
            Console.WriteLine(ex);
        }
    }
    
    static async Task RequestValue(HttpClient client, string key)
    {
        // ---- RETRIEVE VALUE ----            
        try
        {
            HttpResponseMessage getResponse =
                await client.GetAsync($"api/data/{key}");

            if (getResponse.IsSuccessStatusCode)
            {
                DataItem? result =
                await getResponse.Content.ReadFromJsonAsync<DataItem>();

            // Console.WriteLine($"Retrieved value: {result?.Value} (key={key})");
            }                    
            else
            {
                Console.WriteLine("Data not found.");
            }  
        }                
        catch (HttpRequestException ex)
        {
            Console.WriteLine("Network or server error:");
            Console.WriteLine(ex.Message);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Request timed out.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Unexpected error:");
            Console.WriteLine(ex);
        }       
    }
}

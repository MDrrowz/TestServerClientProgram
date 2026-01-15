// CLIENT
// Test Data Server Program

using System.Net.Http.Json;
using System.Net;
using System.IdentityModel.Tokens.Jwt;

// Same model as the server
public class DataItem
{
    public string Key { get; set; } = "";
    public int Value { get; set; }
}

public class AdminLoginRequest
{
    public string Password { get; set; } = "";
}
public class AdminLoginResponse
{
    public string Token { get; set; } = "";
}

class Program
{	
	const string NGROK_URL = "https://sulkiest-lucina-dandyish.ngrok-free.dev/";
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
			await ResetUI();
			return;
		}		
		Console.WriteLine("--- DIAGNOSTICS PASSED ---");
		await ResetUI();
		
		// AUTHENTICATE ADMIN
        await AuthenticateAdmin(client);
        
        await IsUserAuthenticated(client); // Test if auth header is set correctly
		
		bool exit = false;
		const string exitLabel = "[ESC].";		
		string[] menuOptions = [
            "Upload new key-value pair",
            "Delete an existing key",
            "List all stored data",
            "Attempt Admin Login",
            "Exit"
        ];
        
        while (!exit) // Main menu loop
        {
            Console.Clear();
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
                case ConsoleKey.D4: case ConsoleKey.NumPad4:
                    await AuthenticateAdmin(client);
                    break;
                case ConsoleKey.Escape:
                    exit = true;
                    break;
            }
        }
        Console.WriteLine("\nExiting program. Goodbye!");
        Task.Delay(3000).Wait(); // Pause before exit
    }	
    
    // Auth Token/header check
    static async Task<bool> IsUserAuthenticated(HttpClient client)
    {
        Console.Clear();
        Console.WriteLine("--- Checking Admin Authentication Status ---");
        
        Console.WriteLine($"Client Authorization header: {client.DefaultRequestHeaders.Authorization}");

        try
        {
            Console.WriteLine("Sending request to verify authentication...");
            await Task.Delay(500); // Small delay for clarity    
                    
            // Call lightweight endpoint to verify admin status
            var req = new HttpRequestMessage(HttpMethod.Get, "api/auth/check-admin");
            req.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
            
            var response = await client.SendAsync(req);

            Console.WriteLine($"Auth check response: {response.StatusCode}");
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine(">> User is verified as Admin.");
            }
            else
            {
                Console.WriteLine(">> User is NOT verified as Admin.");
            }
        await ResetUI();
        return response.IsSuccessStatusCode;
        }
        catch (Exception e)
        {
            Console.WriteLine(">> Error: Could not connect to server for auth check.");
            Console.WriteLine($"Error: {e.Message}");
            await ResetUI();
            return false;
        }
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
			string? rawkey = await ReadLineOrEscape();
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
                string? valueStr = await ReadLineOrEscape();
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
                var response = await client.PostAsJsonAsync(
                "api/data", 
                new DataItem { Key = key, Value = value }
                );
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Success: Data uploaded. ({key}, {value})");
                    await ResetUI();
                    itemUploaded = true;
                    continue;
                }
                else if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    Console.WriteLine($"Error: Key is already in use. (key = {key})");
                    await ResetUI();
                    break; // Exit to main upload prompt
                }
                else
                {                
                    Console.WriteLine($"Error: {response.StatusCode}");
                    await ResetUI();
                    break; // Exit to main upload prompt
                }
            }
        await Task.Delay(100); // Small delay before next upload
		}
    }
		
    // DATA deletion
	static async Task HandleDelete(HttpClient client)
    {
        Console.Clear();
        try 
        {
            // 1. Server-side authorization check
            // We call a lightweight endpoint that returns 200 OK for admins or 403 for others
            Console.WriteLine("--- Verifying Administrator Privileges ---");
            
            var authReq = new HttpRequestMessage(HttpMethod.Get, "api/auth/check-admin");
            authReq.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
            HttpResponseMessage authCheck = await client.SendAsync(authReq);

            
            Console.WriteLine($"Authorization check response: {authCheck.StatusCode}");

            if (!authCheck.IsSuccessStatusCode)
            {
                if (authCheck.StatusCode == HttpStatusCode.Forbidden || authCheck.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine(">> Access Denied: Administrator privileges required.");
                }
                else
                {
                    Console.WriteLine($">> Authorization check failed: {authCheck.StatusCode}");
                }
                await ResetUI();
                return;
            }
            else
            {
            Console.WriteLine("--- Privileges Verified ---");
            await ResetUI();                
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($">> Unexpected error during auth check: {ex.Message}");
            await ResetUI();
            return;
        }
            
        while (true)
        {
            Console.Clear();
            Console.WriteLine("\n[DELETE MODE] Press ESC to return to main menu.");
            Console.Write("Enter Key to delete: ");
            
            // Use the custom helper to listen for ESC
            string? key = await ReadLineOrEscape(); 
            
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
                var request = new HttpRequestMessage(HttpMethod.Delete, $"api/data/{key}");
                request.Headers.Authorization = client.DefaultRequestHeaders.Authorization;

                HttpResponseMessage deleteResponse = await client.SendAsync(request);


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
                Console.Clear();
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
        await ResetUI();
    }

    // DIAGNOSTICS
    static async Task<bool> RunDiagnostics(HttpClient client)
    {
        // 1. VERIFY TUNNEL: Check if the Ngrok URL is active
        try
        {
            var response = await client.GetAsync("api/health");
            
            Console.WriteLine($"Ngrok health check response:\n{response}"); 
            
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
        
		// 3. VERIFY SERVER VERSION
		try
		{
			var meta = await client.GetFromJsonAsync<Dictionary<string, object>>("api/meta");

			if (meta == null)
			{
				Console.WriteLine("[FAIL] Server metadata missing.");
				return false;
			}

			Console.WriteLine("[PASS] Server metadata:");
			foreach (var kv in meta)
				Console.WriteLine($"       {kv.Key}: {kv.Value}");
		}
		catch (Exception ex)
		{
			Console.WriteLine("[FAIL] Could not retrieve server metadata.");
			Console.WriteLine($"       {ex.Message}");
			return false;
		}
		
        return true;
    }
    
    // Helper to pause and clear console
    static async Task ResetUI()
    {
        await Task.Delay(50);
        Console.Write("\nPress any key to continue...");
        Console.ReadKey();
        Console.Clear();
    }
    
    // ADMIN AUTHENTICATION
    static async Task AuthenticateAdmin(HttpClient client)
    {        
        // Loop until successful login or guest exit
        while(true)
        {
            Console.Clear();
            Console.WriteLine("\n--- Authorize Admin Priviledges ---");
            Console.WriteLine(">> [ESC] to continue as guest.\n");
            Console.Write("Enter Admin Password: ");
            string? inputPassword = ReadPasswordMasked();
            Console.WriteLine(Environment.NewLine + inputPassword);

            if (inputPassword == null) 
            {
                Console.WriteLine("\nContinuing as guest...");
                await ResetUI();
                return;
            }
            
            try
            {
                Console.WriteLine($"Attempting to fetch token...");
                await Task.Delay(500); // Small delay for clarity
                
                var response = await client.PostAsJsonAsync("api/auth/login", new AdminLoginRequest { Password = inputPassword });
                
                Console.WriteLine($"Login response status: {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {                                                            
                    var result = await response.Content.ReadFromJsonAsync<AdminLoginResponse>();            
                    
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    var jwt = handler.ReadJwtToken(result?.Token);

                    Console.WriteLine("JWT claims:");
                    foreach (var c in jwt.Claims)
                        Console.WriteLine($"  {c.Type} = {c.Value}");
                    
                    if (!string.IsNullOrEmpty(result?.Token))
                    {                        
                        // Set the Authorization header for future requests
                        client.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.Token);
                            
                        Console.WriteLine($"Authorization header set for future requests.");
                        Console.WriteLine($">> Header: {client.DefaultRequestHeaders.Authorization.Scheme} {client.DefaultRequestHeaders.Authorization.Parameter}"); // Debug output
                        
                        Console.WriteLine("\n--- Admin login successful ---");
                        await ResetUI();
                        return; // EXIT loop and function on success
                    }
                }                
                // If we reach here, the response was not successful
                Console.WriteLine("\n[ERROR] Invalid Password.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERROR] Connection failed: {ex.Message}");
            }            
            await ResetUI();
        }
    }
    
    // Helper to read password input with masking
    static string? ReadPasswordMasked()
    {
        string? password = null;
        ConsoleKeyInfo key;

        do {
            key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Escape)
            {
                return null;
            }
            else if (key.Key == ConsoleKey.Backspace&& password != null && password.Length > 0)
            {
                password = password[..^1];
                Console.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password += key.KeyChar;
                Console.Write("*");
            }
        } while (key.Key != ConsoleKey.Enter);

        return password;
    }

    // Helper to read a line of input while monitoring for the Escape key.
	static async Task<string?> ReadLineOrEscape()
	{
		string input = "";
		await Task.Delay(100);
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

}
using System.Collections.Specialized;
using System.Net.Http;
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
    static async Task Main()
    {
        using HttpClient client = new()
        {
            BaseAddress = new Uri("http://localhost:5000/")
        };
        
        var item = new DataItem
        {
            Key = "score",
            Value = 123
        };
        
        
        
        var a = Console.ReadLine();
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
                postResponse.EnsureSuccessStatusCode();
                Console.WriteLine("Data uploaded.");
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
    
    static async Task RequestData(HttpClient client, string key)
    {
        // ---- RETRIEVE DATA ----            
        try
        {
            HttpResponseMessage getResponse =
                await client.GetAsync("api/data/score");

            if (getResponse.IsSuccessStatusCode)
            {
                DataItem? result =
                await getResponse.Content.ReadFromJsonAsync<DataItem>();

            Console.WriteLine($"Retrieved value: {result?.Value}");
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

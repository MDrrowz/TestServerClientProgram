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
    
    static async Task Main()
    {
        using HttpClient client = new()
        {
            BaseAddress = new Uri("http://localhost:5000/")
        };
        
        foreach (string key in keys)
        {
            int index = Array.IndexOf(keys, key);
            
            // Console.WriteLine($"index={index}, key={key}");
            
            var item = new DataItem
            {
            Key = key,
            Value = values[index]
            };
            
            await UploadData(client, item);
            
            await RequestValue(client, key);
        }
        
        await RetrieveData(client);
        
        var a = Console.ReadLine();
    }
    
    static async Task RetrieveData(HttpClient client)
    {
        try
        {
            List<DataItem>? allData =
                await client.GetFromJsonAsync<List<DataItem>>("http://localhost:5000/api/data");
            
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
